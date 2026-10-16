using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SignLink.Api
{
    internal sealed class Request
    {
        public string Method, Path;
        public Dictionary<string, string> Query;
        public byte[] Body;
        public bool Authenticated;
        public string BodyText => Encoding.UTF8.GetString(Body);
    }

    /// <summary>A minimal HTTP/1.1 server (one request per connection, JSON out). Security model:
    /// - every request but GET / (the import page), GET /ping and GET /favicon.ico must carry the master password
    ///   (header X-SignLink-Password, compared in constant time); five wrong ones within a minute lock that address out
    ///   for a minute (a request without the header is refused but does not count, so a web page cannot lock you out);
    ///   this computer is never locked out (Lockout: every local client is 127.0.0.1, and it can read the config anyway);
    /// - only this computer by default (127.0.0.1); AllowNetwork listens on all interfaces but answers only this computer
    ///   and local networks (HttpRules.LocalNetwork: private, link-local, CGNAT/Tailscale, unique local addresses); any
    ///   other peer is closed unanswered;
    /// - no CORS: other web sites cannot send the password header (their preflight gets no permission), and the import
    ///   page is served from the API itself, so it needs none; the page may not be framed;
    /// - the Host header must name this computer (localhost; in network mode also its address or name), which stops
    ///   DNS-rebinding pages;
    /// - a request's first byte must arrive within 2 s, its head within 10 s and its body within 30 s (or 2 MB a second),
    ///   so a silent or slow connection cannot hold the API; four requests are served at once, the rest wait their turn
    ///   (game work stays serialised by MainThread.Run);
    /// - the API itself can only touch signs (Routes): a leaked password changes signs, nothing else;
    /// - a request body is read only after the Host, lock-out and password checks passed, only where the exact route
    ///   reads one, capped there (HttpRules.BodyCap; a body elsewhere is refused, 413), and only with its Content-Length
    ///   (411 without: read as empty, a close-delimited POST /sign would erase the sign);
    /// - GET / serves the import page (Web/index.html, no secrets in it) under a Content Security Policy (its own
    ///   inline script and style, connections to this API only): the page asks for the password and sends it with
    ///   every call like any other client.
    /// A raw TcpListener instead of HttpListener: no URL ACL or admin rights needed.</summary>
    internal sealed class LocalHttpServer
    {
        const int HeadMs = 10000, PeerHeadMs = 3000, FirstByteMs = 2000, ReadMs = 5000, PerPeer = 3;
        /// <summary>Requests now open per network peer (not this computer): at most PerPeer (0.20.151, review: one peer
        /// could hold all four slots with slow heads and keep everyone else waiting).</summary>
        readonly Dictionary<IPAddress, int> open = new Dictionary<IPAddress, int>();
        static readonly HashSet<string> LocalHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "localhost", "127.0.0.1", "[::1]" };

        readonly int port;
        readonly bool network;
        readonly Func<Request, string> handler;
        readonly Lockout lockout = new Lockout();
        readonly Throttle refusedLog = new Throttle(TimeSpan.FromMinutes(1));
        TcpListener listener;
        volatile bool running;

        public bool Running => running;

        public LocalHttpServer(int port, bool network, Func<Request, string> handler)
        {
            this.port = port;
            this.network = network;
            this.handler = handler;
        }

        public void Start()
        {
            try
            {
                listener = new TcpListener(network ? IPAddress.Any : IPAddress.Loopback, port);
                listener.Start();
                running = true;
                new Thread(Serve) { IsBackground = true, Name = "SignLink HTTP" }.Start();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"cannot listen on port {port}: {e.Message}");
            }
        }

        public void Stop()
        {
            running = false;
            try { listener?.Stop(); } catch { }
        }

        /// <summary>At most this many requests are served at once; the rest wait their turn for half a minute, then
        /// are dropped.</summary>
        readonly SemaphoreSlim gate = new SemaphoreSlim(4, 4);

        void Serve()
        {
            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch
                {
                    if (!running) return;
                    Thread.Sleep(100);                                      // a listener that keeps failing does not spin (0.20.132, review)
                    continue;
                }
                TcpClient c = client;
                if (!Allowed(c))                                            // not this computer nor a local network: no answer
                {
                    c.Close();
                    continue;
                }
                // a turn is waited for here, not on a pool thread: the game's own workers (thumbnails, GIFs) share the pool
                // (0.20.130, review: a burst of connections held them for half a minute); a network peer waits for none and
                // holds at most PerPeer at once (0.20.151, review)
                IPAddress peer = ((IPEndPoint)c.Client.RemoteEndPoint).Address;
                bool local = IPAddress.IsLoopback(peer);
                if (!local)
                    lock (open)
                    {
                        open.TryGetValue(peer, out int n);
                        if (n >= PerPeer) { c.Close(); continue; }
                        open[peer] = n + 1;
                    }
                if (!gate.Wait(local ? 30000 : 0))
                {
                    Done(peer, local);
                    c.Close();
                    continue;
                }
                if (!running)                                               // stopped meanwhile: nothing more is served
                {
                    gate.Release();
                    Done(peer, local);
                    c.Close();
                    return;
                }
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { if (running) Handle(c); }
                    catch (Exception e) { Plugin.Log.LogDebug($"request failed: {e.Message}"); }   // an idle socket that timed out is no warning
                    finally
                    {
                        gate.Release();
                        Done(peer, local);
                        c.Close();
                    }
                });
            }
        }

        void Done(IPAddress peer, bool local)
        {
            if (local) return;
            lock (open)
                if (open.TryGetValue(peer, out int n)) { if (n <= 1) open.Remove(peer); else open[peer] = n - 1; }
        }

        /// <summary>Is the peer this computer or on a local network (HttpRules.LocalNetwork)? Any other is closed unanswered,
        /// with a log line per address at most once a minute (0.20.132, review: the network mode listens on every
        /// interface, and a peer from anywhere that reached the port got answers). In local mode every peer is loopback.</summary>
        bool Allowed(TcpClient c)
        {
            IPAddress peer;
            try { peer = (c.Client.RemoteEndPoint as IPEndPoint)?.Address; }
            catch (Exception) { return false; }                             // closed already
            if (HttpRules.LocalNetwork(peer)) return true;
            string who = peer?.ToString() ?? "an unknown peer";
            if (refusedLog.Allow(who, DateTime.UtcNow))
                Plugin.Log.LogWarning($"API: refused {who}: neither this computer nor a local network (logged once a minute)");
            return false;
        }

        void Handle(TcpClient client)
        {
            client.ReceiveTimeout = FirstByteMs;                            // the first byte within 2 s, the rest 5 s apart
            client.SendTimeout = 60000;
            IPAddress remote = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            NetworkStream net = client.GetStream();
            // a peer on the network has 3 s for its head, this computer 10 (0.20.151, review: a slow head held a slot)
            string head = ReadHead(client, net, IPAddress.IsLoopback(remote) ? HeadMs : PeerHeadMs);
            if (head == null) return;
            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            if (first.Length < 2) return;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int c = lines[i].IndexOf(':');
                if (c > 0) headers[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim();
            }
            string method = first[0].ToUpperInvariant();
            if (method == "OPTIONS")                                        // a preflight: no CORS permission given
            {
                Write(net, 204, "");
                return;
            }
            bool hasLength = headers.TryGetValue("Content-Length", out string cl);
            long length = 0;
            void Reject(int code, string message)
            {
                Write(net, code, Error(message));
                Drain(client, net, length);
            }
            if (headers.ContainsKey("Transfer-Encoding")) { Write(net, 411, Error("send the body with a Content-Length")); return; }
            if (hasLength && (!long.TryParse(cl, out length) || length < 0)) { Write(net, 400, Error("bad Content-Length")); return; }
            string target = first[1], path = target, query = "";
            int q = target.IndexOf('?');
            if (q >= 0) { path = target.Substring(0, q); query = target.Substring(q + 1); }
            string route = path.TrimEnd('/');
            // the body's cap by the exact route (HttpRules.BodyCap: none where a route reads none, 0.20.132, review), and
            // its Content-Length where one is read (a body read as empty would erase a sign)
            long cap = HttpRules.BodyCap(method, route);
            if (length > cap)
            {
                Reject(413, cap == 0 ? $"{method} {route} takes no body" : $"body too large for {route} ({cap >> 20} MB at most)");
                return;
            }
            if (!hasLength && HttpRules.NeedsLength(method, route))
            {
                Write(net, 411, Error("send the body with a Content-Length (Content-Length: 0 for an empty one)"));
                return;
            }
            var req = new Request { Method = method, Path = route, Query = ParseQuery(query) };

            if (!KnownHost(HostName(headers)))
            {
                Reject(403, network ? "unknown Host: use this computer's address or name" : "unknown Host: this API answers only to localhost");
                return;
            }
            if (lockout.Locked(remote, DateTime.UtcNow))
            {
                Reject(429, "too many wrong passwords: try again in a minute");
                return;
            }
            if (method == "GET" && (req.Path == "" || req.Path == "/index.html"))
            {
                Write(net, 200, Page(), "text/html; charset=utf-8");
                return;
            }
            if (method == "GET" && req.Path == "/favicon.ico")
            {
                Write(net, 204, "");
                return;
            }
            bool sent = headers.TryGetValue("X-SignLink-Password", out string pw) && pw.Length > 0;
            req.Authenticated = sent && PasswordMatches(pw);
            bool openPing = method == "GET" && req.Path == "/ping" && length == 0 && !sent;
            if (!req.Authenticated && !openPing)
            {
                if (sent && lockout.Strike(remote, DateTime.UtcNow))        // only a wrong password counts, never from here
                    Plugin.Log.LogWarning($"API: five wrong passwords from {HttpRules.Plain(remote)}, locked for a minute");
                Reject(401, "missing or wrong X-SignLink-Password (SignLink window or config)");
                return;
            }
            if (length > 0 && headers.TryGetValue("Expect", out string expect) &&
                expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
            {
                byte[] go = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                net.Write(go, 0, go.Length);
            }
            req.Body = ReadBody(client, net, (int)length);
            int status = 200;
            string json;
            try { json = handler(req); }
            catch (ArgumentException e) when (e.Message.StartsWith("unknown endpoint", StringComparison.Ordinal))
            {
                status = 404;
                json = Error(e.Message);
            }
            catch (Exception e) { status = 400; json = Error(e.Message); }
            Write(net, status, json);
        }

        /// <summary>The Host header names this computer: localhost in local mode; in network mode also an IP literal,
        /// this computer's name and name.local (a DNS-rebinding page names someone else's host).</summary>
        bool KnownHost(string host)
        {
            if (LocalHosts.Contains(host) || DockerHost(host)) return true;
            if (!network) return false;
            if (IPAddress.TryParse(host.Trim('[', ']'), out _)) return true;
            string me = MyName;
            return me.Length > 0 && (host.Equals(me, StringComparison.OrdinalIgnoreCase) || host.Equals(me + ".local", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>host.docker.internal (Docker tools on this computer reach the game by it: sign.sh, signart/live.py) counts
        /// as this computer only when its hosts file names it, as Docker Desktop writes it there; any DNS could otherwise
        /// give that name to a web page and rebind it here (0.20.130, review).</summary>
        static bool DockerHost(string host)
        {
            if (!host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase)) return false;
            if (dockerHost == null)
            {
                bool listed = false;
                try
                {
                    string hosts = Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers"), "etc"), "hosts");
                    if (!File.Exists(hosts)) hosts = "/etc/hosts";
                    foreach (string line in File.ReadAllLines(hosts))
                    {
                        int hash = line.IndexOf('#');
                        string l = hash >= 0 ? line.Substring(0, hash) : line;
                        foreach (string part in l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                            if (part.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase)) listed = true;
                    }
                }
                catch (Exception) { }
                dockerHost = listed;
            }
            return dockerHost.Value;
        }

        static bool? dockerHost;

        static string myName;

        static string MyName
        {
            get
            {
                if (myName == null)
                {
                    try { myName = Dns.GetHostName() ?? ""; }
                    catch (Exception) { myName = ""; }
                }
                return myName;
            }
        }

        /// <summary>After a rejection: stop sending and read away (up to 32 MB, 3 s) what the client is still uploading.
        /// Closing a socket with unread data sends a reset, and browsers then show "failed to fetch" instead of the
        /// answer.</summary>
        static void Drain(TcpClient client, NetworkStream net, long length)
        {
            if (length <= 0) return;
            try
            {
                client.Client.Shutdown(SocketShutdown.Send);
                client.ReceiveTimeout = 1000;
                var buf = new byte[65536];
                long left = Math.Min(length, 32L << 20);
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (left > 0 && deadline.ElapsedMilliseconds < 3000)
                {
                    int r = net.Read(buf, 0, (int)Math.Min(buf.Length, left));
                    if (r <= 0) break;
                    left -= r;
                }
            }
            catch (Exception) { }
        }

        static string HostName(Dictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Host", out string host)) return "";
            if (host.StartsWith("[", StringComparison.Ordinal)) return host.Substring(0, host.IndexOf(']') + 1);
            int c = host.LastIndexOf(':');
            return c > 0 ? host.Substring(0, c) : host;
        }

        static bool PasswordMatches(string got)
        {
            string want = PluginConfig.ApiPassword.Value ?? "";
            int diff = want.Length ^ got.Length;
            for (int i = 0; i < Math.Min(want.Length, got.Length); i++) diff |= want[i] ^ got[i];
            return want.Length > 0 && diff == 0;
        }

        static string Error(string message) => "{\"ok\":false,\"error\":" + Json.Str(message) + "}";

        static string page;

        /// <summary>The import page, embedded in the DLL (Web/index.html).</summary>
        static string Page()
        {
            if (page != null) return page;
            using (Stream s = typeof(LocalHttpServer).Assembly.GetManifestResourceStream("SignLink.Web.index.html"))
            {
                if (s == null) return page = "<!doctype html><title>SignLink</title><p>The import page is missing from this build.";
                using (var r = new StreamReader(s, Encoding.UTF8)) return page = r.ReadToEnd();
            }
        }

        static void Write(NetworkStream net, int status, string json, string type = "application/json; charset=utf-8")
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            string reason = status == 200 ? "OK" : status == 204 ? "No Content" : status == 401 ? "Unauthorized" :
                            status == 403 ? "Forbidden" : status == 404 ? "Not Found" : status == 411 ? "Length Required" :
                            status == 413 ? "Payload Too Large" : status == 429 ? "Too Many Requests" : "Bad Request";
            var sb = new StringBuilder($"HTTP/1.1 {status} {reason}\r\n");
            sb.Append("Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\n");
            sb.Append(type.StartsWith("text/html", StringComparison.Ordinal)                 // the page: its own inline script and
                ? "Content-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; " +   // style, this API only
                  "img-src blob: data:; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'\r\n"
                : "Content-Security-Policy: frame-ancestors 'none'\r\n");
            if (status != 204) sb.Append("Content-Type: ").Append(type).Append("\r\n");
            sb.Append("Content-Length: ").Append(status == 204 ? 0 : payload.Length).Append("\r\nConnection: close\r\n\r\n");
            byte[] header = Encoding.ASCII.GetBytes(sb.ToString());
            net.Write(header, 0, header.Length);
            if (status != 204) net.Write(payload, 0, payload.Length);
            net.Flush();
        }

        /// <summary>The request head, or null when the client closed the connection; throws past ms milliseconds or
        /// 64 KB. Each read waits 2 s for the first byte, 5 s after, and never past what is left of ms (HttpRules.ReadWait),
        /// so the limit holds while no byte comes too (0.20.132, review: it was checked only when one came, so a head
        /// could take 15 s).</summary>
        static string ReadHead(TcpClient client, Stream s, int ms)
        {
            var buf = new List<byte>(512);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int timeout = client.ReceiveTimeout;
            while (true)
            {
                int wait = HttpRules.ReadWait(clock.ElapsedMilliseconds, ms, buf.Count == 0 ? FirstByteMs : ReadMs);
                if (wait <= 0) throw new IOException("request head too slow");
                if (wait != timeout) client.ReceiveTimeout = timeout = wait;
                int b = s.ReadByte();
                if (b < 0) return null;
                buf.Add((byte)b);
                int n = buf.Count;
                if (n >= 4 && buf[n - 4] == 13 && buf[n - 3] == 10 && buf[n - 2] == 13 && buf[n - 1] == 10)
                    return Encoding.ASCII.GetString(buf.ToArray(), 0, n - 4);
                if (n > 65536) throw new InvalidDataException("request header too large");
            }
        }

        /// <summary>The body, all length bytes of it: each read waits at most 5 s, the whole at most HttpRules.BodyMs (30 s,
        /// or 2 MB a second), so a sender trickling a byte every few seconds cannot hold one of the four turns (0.20.132,
        /// review).</summary>
        static byte[] ReadBody(TcpClient client, Stream s, int length)
        {
            var body = new byte[length];
            int got = 0, total = HttpRules.BodyMs(length), timeout = client.ReceiveTimeout;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (got < length)
            {
                int wait = HttpRules.ReadWait(clock.ElapsedMilliseconds, total, ReadMs);
                if (wait <= 0) throw new IOException("request body too slow");
                if (wait != timeout) client.ReceiveTimeout = timeout = wait;
                int r = s.Read(body, got, length - got);
                if (r <= 0) throw new EndOfStreamException("body shorter than Content-Length");
                got += r;
            }
            return body;
        }

        static Dictionary<string, string> ParseQuery(string q)
        {
            var d = new Dictionary<string, string>();
            foreach (string part in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int e = part.IndexOf('=');
                string k = Uri.UnescapeDataString(e < 0 ? part : part.Substring(0, e));
                d[k] = e < 0 ? "" : Uri.UnescapeDataString(part.Substring(e + 1).Replace('+', ' '));
            }
            return d;
        }
    }
}

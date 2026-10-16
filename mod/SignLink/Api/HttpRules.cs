using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace SignLink.Api
{
    /// <summary>The local API's rules apart from sockets (pure: mod/SignLink.Tests checks them; 0.20.132, security
    /// review): which routes read a body and how much, how long a head or a body may take, which peers are this computer
    /// and which a local network.</summary>
    internal static class HttpRules
    {
        /// <summary>The most frames POST /anim takes (refused before the per-frame arrays are made).</summary>
        public const int MaxAnimFrames = 20000;

        /// <summary>A body may take this long whatever its size, a larger one a second for every 2 MB (BodyMs).</summary>
        const int MinBodyMs = 30000;
        const long BodyBytesPerSecond = 2L << 20;

        /// <summary>The most bytes a request's body may hold, by its exact route (path without a trailing slash): frames
        /// 256 MB, a picture file 64 MB, texts 16 MB; every other route (GETs, the stop endpoints, /signs/delete, /pose,
        /// unknown paths) reads none, so a body there is refused (413) instead of read. Exact routes, no prefixes: a path
        /// that only starts like one gets nothing.</summary>
        public static long BodyCap(string method, string path)
        {
            if (method != "POST") return 0;
            switch (path)
            {
                case "/screen": return 256L << 20;
                case "/image": return 64L << 20;
                case "/sign":
                case "/anim":
                case "/signs/clone": return 16L << 20;
                default: return 0;
            }
        }

        /// <summary>A route that reads a body wants its Content-Length (411 without): read as empty, a POST /sign whose
        /// body ends at the connection's close would erase the sign. Body-less POSTs (the stop endpoints) need none:
        /// curl -X POST without data sends no Content-Length.</summary>
        public static bool NeedsLength(string method, string path) => BodyCap(method, path) > 0;

        /// <summary>The whole body within this many ms: 30 s, or 2 MB a second for a larger one (a slow sender cannot
        /// hold one of the four turns for long; each read also waits at most ReadWait).</summary>
        public static int BodyMs(long length) => (int)Math.Min(int.MaxValue, Math.Max(MinBodyMs, length * 1000 / BodyBytesPerSecond));

        /// <summary>How long the next read may wait (ms): perReadMs, or what is left of totalMs when less; 0 when the whole
        /// is over (a socket's ReceiveTimeout of 0 would wait for ever, so 0 means: stop).</summary>
        public static int ReadWait(long elapsedMs, int totalMs, int perReadMs)
        {
            long left = totalMs - elapsedMs;
            return left <= 0 ? 0 : (int)Math.Min(perReadMs, left);
        }

        /// <summary>How many parts s.Split(sep) gives (one more than the separators), counted without making them.</summary>
        public static int Parts(string s, char sep)
        {
            int n = 1;
            if (s != null) foreach (char c in s) if (c == sep) n++;
            return n;
        }

        /// <summary>This computer: 127.0.0.0/8, ::1, and their IPv4-mapped forms.</summary>
        public static bool Loopback(IPAddress a)
        {
            a = Plain(a);
            if (a == null) return false;
            byte[] b = a.GetAddressBytes();
            return a.AddressFamily == AddressFamily.InterNetwork ? b[0] == 127 : V6Loopback(b);
        }

        /// <summary>A peer on this computer or a local network: loopback, private IPv4 (10/8, 172.16/12, 192.168/16),
        /// link-local (169.254/16), CGNAT and Tailscale (100.64/10), IPv6 loopback, unique local (fc00::/7) and
        /// link-local (fe80::/10), IPv4-mapped forms too. The network mode answers no one else.</summary>
        public static bool LocalNetwork(IPAddress a)
        {
            a = Plain(a);
            if (a == null) return false;
            byte[] b = a.GetAddressBytes();
            if (a.AddressFamily == AddressFamily.InterNetwork)
                return b[0] == 127 || b[0] == 10 || (b[0] == 172 && (b[1] & 0xF0) == 16) || (b[0] == 192 && b[1] == 168) ||
                       (b[0] == 169 && b[1] == 254) || (b[0] == 100 && (b[1] & 0xC0) == 64);
            if (a.AddressFamily == AddressFamily.InterNetworkV6)
                return V6Loopback(b) || (b[0] & 0xFE) == 0xFC || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80);
            return false;
        }

        /// <summary>An IPv4-mapped IPv6 address as the IPv4 address it carries; any other as it is.</summary>
        public static IPAddress Plain(IPAddress a) =>
            a != null && a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a;

        static bool V6Loopback(byte[] b)
        {
            if (b.Length != 16 || b[15] != 1) return false;
            for (int i = 0; i < 15; i++) if (b[i] != 0) return false;
            return true;
        }
    }

    /// <summary>Wrong passwords by address: five within a minute lock that address out for a minute. This computer is never
    /// counted (0.20.132, review): every local client (the browser, live.py, Docker through its proxy) is 127.0.0.1, so
    /// any program here could lock the owner out, and it can read the password from the config anyway. Each strike
    /// forgets entries over a minute old that are not locked, so the table does not grow with every address. Thread-safe.</summary>
    internal sealed class Lockout
    {
        const int Most = 5;
        static readonly TimeSpan Span = TimeSpan.FromMinutes(1);

        sealed class Entry
        {
            public int Count;
            public DateTime First, LockedUntil;
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        /// <summary>Addresses in the table.</summary>
        public int Count
        {
            get { lock (entries) return entries.Count; }
        }

        public bool Locked(IPAddress who, DateTime now)
        {
            lock (entries) return entries.TryGetValue(Key(who), out Entry e) && e.LockedUntil > now;
        }

        /// <summary>A wrong password from who; true when this one locked it out (to say so once in the log).</summary>
        public bool Strike(IPAddress who, DateTime now)
        {
            if (who == null || HttpRules.Loopback(who)) return false;
            lock (entries)
            {
                Prune(now);
                string key = Key(who);
                if (!entries.TryGetValue(key, out Entry e) || now - e.First > Span) entries[key] = e = new Entry { First = now };
                if (++e.Count < Most) return false;
                e.LockedUntil = now + Span;
                return true;
            }
        }

        void Prune(DateTime now)
        {
            List<string> old = null;
            foreach (var kv in entries)
                if (now - kv.Value.First > Span && kv.Value.LockedUntil <= now) (old ?? (old = new List<string>())).Add(kv.Key);
            if (old != null) foreach (string k in old) entries.Remove(k);
        }

        static string Key(IPAddress who) => HttpRules.Plain(who)?.ToString() ?? "";
    }

    /// <summary>At most once per `every` for each key (a refused peer's log line); a full table drops its old keys and
    /// past MostKeys new ones are not logged, so a flood of addresses neither fills the log nor the memory. Thread-safe.</summary>
    internal sealed class Throttle
    {
        const int MostKeys = 1000;
        readonly TimeSpan every;
        readonly Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();

        public Throttle(TimeSpan every) => this.every = every;

        public bool Allow(string key, DateTime now)
        {
            lock (last)
            {
                if (last.TryGetValue(key, out DateTime t) && now - t < every) return false;
                if (!last.ContainsKey(key) && last.Count >= MostKeys)
                {
                    var old = new List<string>();
                    foreach (var kv in last) if (now - kv.Value >= every) old.Add(kv.Key);
                    foreach (string k in old) last.Remove(k);
                    if (last.Count >= MostKeys) return false;
                }
                last[key] = now;
                return true;
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SignLink.Api
{
    /// <summary>Runs work queued from other threads on Unity's main thread (drained in Plugin.Update).</summary>
    internal static class MainThread
    {
        static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        /// <summary>Where a job's exception goes (the plugin logs it; the test program prints it).</summary>
        public static Action<Exception> Report = e => System.Console.Error.WriteLine(e);   // System.: the game has a Console class of its own

        public static void Drain()
        {
            while (Queue.TryDequeue(out Action job))
            {
                try { job(); }
                catch (Exception e) { Report(e); }
            }
        }

        /// <summary>Queues work for the main thread without waiting (worker threads hand back results this way).</summary>
        public static void Post(Action job) => Queue.Enqueue(job);

        /// <summary>Runs f on the main thread and waits for its result (game state is only safe there).</summary>
        public static T Run<T>(Func<T> f, int timeoutMs = 15000)
        {
            T result = default;
            Exception error = null;
            int state = 0;                                   // 0 queued, 1 running, 2 given up
            var done = new ManualResetEventSlim(false);      // not disposed: a late job may still Set() it
            Queue.Enqueue(() =>
            {
                if (Interlocked.CompareExchange(ref state, 1, 0) != 0) return;     // the caller gave up: do not run late
                try { result = f(); }
                catch (Exception e) { error = e; }
                finally { done.Set(); }
            });
            if (!done.Wait(timeoutMs))
            {
                if (Interlocked.CompareExchange(ref state, 2, 0) == 0)
                    throw new TimeoutException($"the game did not run the request within {timeoutMs / 1000} s (a loading screen?); nothing was done");
                done.Wait();                                 // it started just now: let it finish
            }
            if (error != null) throw new InvalidOperationException(error.Message);
            return result;
        }
    }
}

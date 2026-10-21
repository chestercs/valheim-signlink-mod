using System;
using System.Threading;

namespace SignLink.UI
{
    /// <summary>Keyed background work for the window (quick views, costs, thumbnails): the key says what is wanted
    /// (the file and the grid, the piece, the stack and its revision). Want is called every Layout event with the
    /// current key; the latest key runs once it has stood still for `settle` seconds (sliders move), on the thread
    /// pool, and its result is delivered on the main thread only if that key is still the wanted one. A key that
    /// changed meanwhile runs again on the next Want. One such class instead of a flag set per view. Work started with
    /// WantStoppable is handed a staleness test, so a long one stops when nobody waits for it any more (0.20.132).</summary>
    internal sealed class Job<T>
    {
        string wanted, done;                                            // wanted: read by the running work (Stale)
        float wantedAt;
        bool busy;
        int run;                                                        // counts starts: a result names the run it came from

        /// <summary>Work is running.</summary>
        public bool Busy => busy;

        /// <summary>The key whose result was delivered (or failed) last; null before the first.</summary>
        public string Done => done;

        /// <summary>Delivers again even for the same key (what it was made from changed under the same name).</summary>
        public void Forget() => done = null;

        /// <summary>Drops the running work's result when it comes (another sign was selected: its view was cleared at
        /// once) and lets the next Want start without waiting for it, even for the same key; work started with
        /// WantStoppable sees it is stale.</summary>
        public void Abandon()
        {
            Interlocked.Increment(ref run);
            busy = false;
            done = null;
        }

        /// <summary>begin runs on the main thread when the work starts (read game state, copy inputs) and returns the
        /// work for the pool; deliver and fail run on the main thread. A null key wants nothing.</summary>
        public void Want(string key, float now, float settle, Func<Func<T>> begin, Action<T> deliver, Action<Exception> fail)
        {
            if (!Due(key, now, settle)) return;                         // nothing made unless work starts (0.20.132, review)
            Func<T> work;
            try { work = begin(); }
            catch (Exception e)
            {
                Failed(key, e, fail);
                return;
            }
            Start(key, _ => work(), deliver, fail);
        }

        /// <summary>The same for work that takes a staleness test (any thread): true once Abandon ran, a newer run started,
        /// or the wanted key moved away from this run's. The work asks it between its steps and throws an
        /// OperationCanceledException then, which is dropped like a stale result: no fail, and the key is not done, so
        /// it runs again if it is wanted again (0.20.132, review: a decode or a count nobody waited for ran to its end,
        /// a photo's main-thread decode included).</summary>
        public void WantStoppable(string key, float now, float settle, Func<Func<Func<bool>, T>> begin, Action<T> deliver,
                                  Action<Exception> fail)
        {
            if (!Due(key, now, settle)) return;
            Func<Func<bool>, T> work;
            try { work = begin(); }
            catch (Exception e)
            {
                Failed(key, e, fail);
                return;
            }
            Start(key, work, deliver, fail);
        }

        /// <summary>Notes the key as Want does and says whether Want would start its work now: asked first, the delegates a
        /// caller hands Want are made only when work starts (0.20.132, review: four or five sets every Layout event).</summary>
        public bool Wants(string key, float now, float settle) => Due(key, now, settle);

        /// <summary>Notes the key and says whether its work starts now.</summary>
        bool Due(string key, float now, float settle)
        {
            if (key != wanted)
            {
                Volatile.Write(ref wanted, key);
                wantedAt = now;
            }
            return key != null && !busy && key != done && now - wantedAt >= settle;
        }

        void Failed(string key, Exception e, Action<Exception> fail)
        {
            done = key;                                                 // not tried again until the key changes
            fail?.Invoke(e);
        }

        void Start(string key, Func<Func<bool>, T> work, Action<T> deliver, Action<Exception> fail)
        {
            busy = true;
            int me = Interlocked.Increment(ref run);                    // this run: a later run (or Abandon) owns the flags
            string mine = key;
            Func<bool> stale = () => Volatile.Read(ref run) != me || Volatile.Read(ref wanted) != mine;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                T result = default;
                Exception error = null;
                try { result = work(stale); }
                catch (Exception e) { error = e; }
                MainThread.Post(() =>
                {
                    if (me != run) return;                              // abandoned, or a newer run took over
                    busy = false;
                    if (mine != wanted) return;                         // moved on: the next Want starts the new key
                    if (error is OperationCanceledException) return;    // stopped as stale: dropped, runs again if wanted
                    if (error is TimeoutException) return;              // the game did not answer (a loading screen): tried again,
                                                                        // not "cannot read" for good (1.1.0, review)
                    done = mine;
                    if (error != null) fail?.Invoke(error);
                    else deliver(result);
                });
            });
        }
    }
}

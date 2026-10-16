using System;
using System.Collections.Generic;
using System.Diagnostics;
using SignLink.Animation;

namespace SignLink.Signs
{
    /// <summary>Re-reads changed sign texts every RefreshSeconds (0.1-2 s; at 2 s, the vanilla pace of Sign.cs:39,
    /// nothing runs), so animations written by another player's tool arrive smoothly. Signs this client animates itself are left alone. A sign whose
    /// refresh took over SlowMs (a big frame rewritten often) is left to vanilla's own 2 s refresh for a while, so
    /// another player's writes cannot cost SignLink players twenty times what vanilla pays; one sign's failing
    /// refresh (the game throws for author "host") does not stop the rest. A sign's mesh is built again only when the
    /// text it shows changed (0.20.132, review: a new revision with the same text, wear or another player's Move
    /// Apply on a 300 KB stack, rebuilt every copy, some 340 ms in one frame; the game's own refresh sets the text,
    /// which TMP skips when it is the same), and a step works at most StepMs, the signs left over going on in the next
    /// frame.</summary>
    internal sealed class FastRefresh
    {
        const double SlowMs = 20, StepMs = 20;
        readonly TextAnimator text;
        Sign[] known = new Sign[0];
        float nextScan, nextCheck, lastWarning = -100f;
        readonly Dictionary<Sign, float> leaveUntil = new Dictionary<Sign, float>();
        int goneSeen;
        /// <summary>Where the last step stopped on its time budget (0: it finished): the next frame goes on from there.</summary>
        int resume;

        public FastRefresh(TextAnimator text) => this.text = text;

        public void Step(float now)
        {
            float every = PluginConfig.RefreshSeconds.Value;
            if (every >= 1.95f)                                             // 2 s: the game's own pace, nothing to add
            {
                resume = 0;
                return;
            }
            if (resume == 0)
            {
                if (now < nextCheck) return;
                nextCheck = now + Math.Max(0.05f, every);
                if (now >= nextScan)
                {
                    nextScan = now + 2f;
                    known = SignFinder.Loaded();
                    SignFinder.Prune(leaveUntil, ref goneSeen);             // destroyed signs go (0.20.132, review)
                    if (leaveUntil.Count > 500) leaveUntil.Clear();
                }
            }
            long stepStarted = Stopwatch.GetTimestamp(), budget = (long)(StepMs * Stopwatch.Frequency / 1000.0);
            int from = Math.Min(resume, known.Length), done = 0;
            resume = 0;
            for (int i = from; i < known.Length; i++)
            {
                Sign s = known[i];
                if (!s || text.Drives(s) || Plugin.Stored.Drives(s)) continue;
                ZDO zdo = SignFinder.Zdo(s);
                if (zdo == null || s.m_lastRevision == zdo.DataRevision) continue;
                if (leaveUntil.TryGetValue(s, out float until) && now < until) continue;
                if (done > 0 && Stopwatch.GetTimestamp() - stepStarted > budget)
                {
                    resume = i;                                             // the rest in the next frame
                    return;
                }
                done++;
                long started = Stopwatch.GetTimestamp();
                try
                {
                    string shown = s.m_textWidget.text;
                    s.UpdateText();
                    // timed here: a slow sign is left to the game (0.20.130); only a text that changed is built again
                    if (!string.Equals(shown, s.m_textWidget.text, StringComparison.Ordinal)) s.m_textWidget.ForceMeshUpdate();
                }
                catch (Exception e)
                {
                    leaveUntil[s] = now + 2f;
                    if (now - lastWarning > 10f)
                    {
                        lastWarning = now;
                        Plugin.Log.LogWarning($"fast refresh of {SignFinder.Id(s)} failed (the game's own refresh keeps it): {e.Message}");
                    }
                    continue;
                }
                if ((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency > SlowMs) leaveUntil[s] = now + 2f;
            }
        }
    }
}

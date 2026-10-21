using System.Collections.Generic;
using SignLink.Pictures;
using SignLink.Presets;
using SignLink.Signs;
using SignLink.UI.Sources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignLink.UI
{
    /// <summary>The frame of a stack where it stands (Show all, the hover), measured where its glyphs draw.</summary>
    internal partial class Ghost
    {
        /// <summary>The board's outline alone, at the sign's own pose: the faint grey of every sign a click could select
        /// (AllBoards, 0.20.172). Built again only when the sign moved.</summary>
        public void ShowBoard(Sign target)
        {
            if (!target || !target.m_textWidget)
            {
                Hide();
                return;
            }
            Transform t = target.transform;
            if (root && ReferenceEquals(boardFor, target) && boardAt == t.position && boardTurn == t.rotation)
            {
                if (!root.activeSelf) root.SetActive(true);
                return;
            }
            if (!Ensure(target, target))
            {
                Hide();
                return;
            }
            boardFor = target;
            boardAt = t.position;
            boardTurn = t.rotation;
            shownKey = null;                                                // not a frame ShowStack or Show built
            var rt = (RectTransform)root.transform;
            rt.SetPositionAndRotation(t.position, t.rotation);
            Vector3 ls = rt.parent.lossyScale;
            rt.localScale = new Vector3(Safe(ls.x), Safe(ls.y), Safe(ls.z));
            PlaceFill(Vector3.zero, Vector3.up, 0f, 0f, false, true);
            for (int i = 0; i < 4; i++) Place(edge[i], Vector3.zero, Vector3.up, 0f, 0f, false);
            Place(mark, Vector3.zero, Vector3.up, 0f, 0f, false);
            PlaceBoard(target);
            root.SetActive(true);
        }

        /// <summary>The frame around a stack as it is: the union of its signs' texts (TMP's bounds, what is drawn) in
        /// the first sign's plane, in front of the frontmost band, with the top mark, and the board's outline, at the
        /// sign's own pose. Built like the selection's frame (0.20.21 dropped the board from this one and the author lost
        /// track of which rectangle was which): the content green and red in both, the board neon lime when selected and
        /// cool white under the cursor (Select sign, a row of Signs nearby), so a click's target shows before the click.
        /// An empty sign gets its board alone.</summary>
        public void ShowStack(Sign target, Sign host, List<Sign> stack)
        {
            if (!target || !target.m_textWidget || !host || !host.m_textWidget)
            {
                Hide();
                return;
            }
            // the first sign's data revision too (0.20.90, review): a stack put on again or refit by another player keeps its
            // place, and Show all kept the frame of what it held before
            Transform screen = Plugin.Screens.ScreenOver(stack);            // a moving GIF's screen (0.20.116)
            // the two switches as for the selection (1.1.0, the author): the board's outline Highlight signs', the content's
            // frame Highlight sign contents'; Show all and the hover alike
            bool boardOn = PluginConfig.Highlight.Value, contentOn = PluginConfig.HighlightContent.Value;
            ZDO tz = SignFinder.Zdo(target);
            var inputs = new Inputs
            {
                Stack = true, Count = stack.Count, Position = target.transform.position, Rotation = target.transform.rotation,
                HasRevision = tz != null, Revision = tz != null ? tz.DataRevision : 0u, Content = contentOn, Screen = screen != null, Board = boardOn,
            };
            inputs.Target = Uid(target, out inputs.HasTarget);
            inputs.Host = Uid(host, out inputs.HasHost);
            if (shownKey != null && root && ReferenceEquals(sign, target) && inputs.Same(shownInputs))
            {
                if (!root.activeSelf) root.SetActive(true);                 // nothing changed: no key built (0.20.132)
                Side();
                return;
            }
            string key = $"stack|{SignFinder.Id(target)}|{SignFinder.Id(host)}|{stack.Count}|{target.transform.position}|{target.transform.rotation}|" +
                         $"{SignFinder.Zdo(target)?.DataRevision}|{contentOn}|{screen != null}|{boardOn}";
            shownInputs = inputs;                                           // what the key below is made of
            if (key == shownKey && root && ReferenceEquals(sign, target))
            {
                if (!root.activeSelf) root.SetActive(true);
                Side();
                return;
            }
            if (!Ensure(target, host))
            {
                Hide();
                return;
            }
            shownKey = key;
            var rt = (RectTransform)root.transform;
            rt.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            Vector3 ls = rt.parent.lossyScale;
            rt.localScale = new Vector3(Safe(ls.x), Safe(ls.y), Safe(ls.z));

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, front = float.MinValue;
            bool texts = false, stale = false;
            foreach (Sign s in stack)
            {
                TMP_Text t = s ? s.m_textWidget : null;
                if (!t || string.IsNullOrEmpty(t.text)) continue;
                texts = true;
                if (TextBehind(s)) stale = true;
                if (!Ink.Rect(t, out Vector2 lo, out Vector2 hi))             // where its glyphs draw (0.20.155: not TMP's textBounds)
                {
                    stale = true;
                    continue;
                }
                Matrix4x4 m = target.transform.worldToLocalMatrix * t.transform.localToWorldMatrix;   // text-local -> sign-local (metres)
                for (int i = 0; i < 4; i++)
                {
                    Vector3 c = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, 0f));
                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y);
                    maxY = Mathf.Max(maxY, c.y);
                    front = Mathf.Max(front, c.z);
                }
            }
            // a moving GIF's screen counts as it shows (0.20.116, the author: I cleared the sign, then put the GIF in again
            // ... Show all ... shows only the sign's border): it hides the text, which a GIF put on anew has never laid out,
            // so there was nothing to measure; and it is a pixel larger on every side than the still
            if (screen is RectTransform sr)
            {
                sr.GetWorldCorners(screenCorners);
                Matrix4x4 w2s = target.transform.worldToLocalMatrix;
                foreach (Vector3 corner in screenCorners)
                {
                    Vector3 c = w2s.MultiplyPoint3x4(corner);
                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y);
                    maxY = Mathf.Max(maxY, c.y);
                    front = Mathf.Max(front, c.z);
                }
                texts = true;
            }
            // a built-in piece or a picture SignLink made: framed as the selection frames it, from what made it (a piece's size
            // and middle as made, NAME.fit; a picture's grid of whole pixels), with the fit the sign keeps, so picking a sign
            // from Show all or the hover does not move its frame (0.20.154, 0.20.155: the author saw a picture's frame jump)
            if (screen == null && ModelRect(target, maxX - minX, maxY - minY, out Vector3 pc, out float pw, out float ph))
            {
                minX = pc.x - pw / 2;
                maxX = pc.x + pw / 2;
                minY = pc.y - ph / 2;
                maxY = pc.y + ph / 2;
                if (front == float.MinValue) front = pc.z;                 // no text measured: the first sign's text plane
                texts = true;
            }
            bool has = maxX > minX && maxY > minY && contentOn;              // the content's frame: a setting of its own
            // text the game has not laid out yet (a sign that just loaded): measured again at the next look, not kept as
            // a frame without its content (0.20.90, review)
            if (!has && texts && contentOn) shownKey = null;
            if (stale) shownKey = null;                                     // a sign's text not laid out as its data says yet: measured again
            Vector3 centre = has ? new Vector3((minX + maxX) / 2, (minY + maxY) / 2, front + OnText) : Vector3.zero;
            float w = has ? maxX - minX : 0f, h = has ? maxY - minY : 0f;
            plane = centre;
            PlaceFill(centre, Vector3.up, w, h, has, stack.Count <= 1);
            Place(edge[0], centre + Vector3.up * (h / 2), Vector3.up, w + Thick, Thick, has);
            Place(edge[1], centre - Vector3.up * (h / 2), Vector3.up, w + Thick, Thick, has);
            Place(edge[2], centre - Vector3.right * (w / 2), Vector3.up, Thick, h, has);
            Place(edge[3], centre + Vector3.right * (w / 2), Vector3.up, Thick, h, has);
            Place(mark, centre + Vector3.up * (h / 2 + 0.03f), Vector3.up, 0.12f, 0.02f, has);   // the same frame as the selection's
            PlaceBoard(target, boardOn);                                     // the board's outline too, as the selection's: one language
            root.SetActive(true);
            Side();
        }

        // the held stack's ink, measured once a stack and data revision (HeldInk)
        Sign inkSign;
        uint inkRevision;
        int inkCount = -1;
        Fit inkFit;
        bool inkHas;
        Vector2 inkM;
        double inkX, inkY;

        /// <summary>What the selected stack holds, measured where its glyphs draw (Ink.Rect over its signs, in the first sign's
        /// text-local frame): its size as made (metres, the held fit's scale taken out) and its middle where it shows now
        /// (wu, text-local), which Show moves as Refit moves the texts (Fit.Between). Once a data revision; false while
        /// nothing is laid out yet.</summary>
        bool HeldInk(Session s, Sign target, out Vector2 m, out double px, out double py)
        {
            m = Vector2.zero;
            px = py = 0;
            List<Sign> stack = s.Stack.Count > 0 ? s.Stack : new List<Sign> { target };
            ZDO z = SignFinder.Zdo(target);
            uint revision = z != null ? z.DataRevision : 0u;
            if (!ReferenceEquals(inkSign, target) || inkRevision != revision || inkCount != stack.Count || !inkFit.Same(s.Held.Fit))
            {                                                               // no key string a Layout: compared field by field
                inkHas = false;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                bool complete = true;
                Matrix4x4 toFirst = target.m_textWidget.transform.worldToLocalMatrix;
                foreach (Sign sign in stack)
                {
                    TMP_Text t = sign ? sign.m_textWidget : null;
                    if (!t) continue;
                    if (TextBehind(sign)) complete = false;
                    if (!Ink.Rect(t, out Vector2 lo, out Vector2 hi))
                    {
                        if (!string.IsNullOrEmpty(t.text)) complete = false;
                        continue;
                    }
                    Matrix4x4 mm = toFirst * t.transform.localToWorldMatrix;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 c = mm.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, 0f));
                        x0 = Mathf.Min(x0, c.x);
                        x1 = Mathf.Max(x1, c.x);
                        y0 = Mathf.Min(y0, c.y);
                        y1 = Mathf.Max(y1, c.y);
                    }
                }
                if (!(x1 > x0 && y1 > y0)) return false;                      // not laid out yet: measured at a later look
                inkSign = complete ? target : null;                             // all laid out as their data says: kept, else measured again
                inkRevision = revision;
                inkCount = stack.Count;
                inkFit = s.Held.Fit;
                Fit held = s.Held.Fit;
                double k = held.Scale > 0 ? held.Scale : 1;
                inkM = new Vector2((float)((x1 - x0) * 0.05 / k), (float)((y1 - y0) * 0.05 / k));   // 1 local unit = 2 wu = 0.05 m
                inkX = x0 + x1;                                                 // the middle in wu: (x0 + x1) / 2 * 2
                inkY = y0 + y1;
                inkHas = true;
            }
            if (!inkHas) return false;
            m = inkM;
            px = inkX;
            py = inkY;
            return true;
        }

        /// <summary>The sign's text widget still shows an older data revision than its ZDO (another player's write reaches
        /// the widget up to 2 s later, at the game's UpdateText): what it measures is not what the data says (0.20.157,
        /// review: such a measure was kept until the next revision).</summary>
        static bool TextBehind(Sign s)
        {
            ZDO z = s ? SignFinder.Zdo(s) : null;
            return z != null && s.m_lastRevision != z.DataRevision;
        }

        /// <summary>A stack whose fit the sign keeps (SignFit.Read) and that holds a built-in piece with a known size (NAME.fit)
        /// or a picture with its pixel (the recipe's Pitch): its content's middle in the sign's own frame (metres, sign-local)
        /// and its width and height, as Show frames the selection (Fit.Centre; ArtSource.Size; a picture's plan, W x H whole
        /// pixels: the ink measured, inkW x inkH metres, a fraction of a pixel over it where seamless pixels overlap, gives
        /// the count); false for anything else (the ink then).</summary>
        static bool ModelRect(Sign first, float inkW, float inkH, out Vector3 centre, out float w, out float h)
        {
            centre = Vector3.zero;
            w = h = 0f;
            SignRecipe r = SignRecipe.Parse(SignMarks.Source(first));
            double wWu, hWu;
            Fit fit = SignFit.Read(first, out double c0x, out double c0y, out bool known);
            if (!known) return false;
            if (r.Kind == SignRecipe.Kinds.Art)
            {
                List<Preset> items = ArtLibrary.Items;
                int i = Preset.Find(items, r.Path);
                if (i < 0 || !items[i].Size(out wWu, out hWu)) return false;
            }
            else if (r.Kind == SignRecipe.Kinds.Picture && r.Pitch > 0 && inkW > 0f && inkH > 0f)
            {
                double pitch = r.Pitch / fit.Scale;                          // made at its own size: wWu * Scale below is the plan's
                double cell = r.Pitch * 0.025;                               // one pixel, metres
                wWu = System.Math.Max(1, System.Math.Round(inkW / cell)) * pitch;
                hWu = System.Math.Max(1, System.Math.Round(inkH / cell)) * pitch;
            }
            else return false;
            fit.Centre(c0x, c0y, out double cx, out double cy);
            Matrix4x4 toSign = first.transform.worldToLocalMatrix * first.m_textWidget.transform.localToWorldMatrix;
            centre = toSign.MultiplyPoint3x4(new Vector3((float)(cx / 2), (float)(cy / 2), 0f));   // 1 local unit = 2 wu
            w = (float)(wWu * fit.Scale * 0.025);
            h = (float)(hWu * fit.Scale * 0.025);
            return w > 0f && h > 0f;
        }
    }
}

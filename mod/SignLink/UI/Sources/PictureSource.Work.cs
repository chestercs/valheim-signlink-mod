using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>The jobs behind the rows: the list's words, the plan, the decode, the quick view's grid and the count, each once its inputs stand still.</summary>
    internal partial class PictureSource
    {
        /// <summary>Each file's row: its words (name, size or why it cannot be read, moving), its thumbnail's key, the words
        /// of its empty box (a photo too large to decode for its row alone) and whether it can be picked.</summary>
        void RowWords()
        {
            if (ReferenceEquals(rowFiles, files) && rowHeaders == headersChanged) return;
            rowFiles = files;
            rowHeaders = headersChanged;
            int n = files.Count;
            rowWords = new string[n];
            rowKey = new string[n];
            rowBox = new string[n];
            rowBad = new bool[n];
            for (int i = 0; i < n; i++)
            {
                string f = files[i];
                Header h = headers.TryGetValue(f, out Header hh) ? hh : null;
                bool bad = h != null && h.Error != null;
                string size = h == null ? "" : bad ? "   " + h.Error : $"   {h.W} x {h.H}";
                bool gif = f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
                long px = h != null && !bad ? (long)h.W * h.H : 0;
                rowWords[i] = $"{Path.GetFileName(f)}{size}{(gif && !bad ? "   moving" : "")}";
                rowKey[i] = h != null && !bad ? f + "|" + h.Stamp : null;
                rowBox[i] = px > Thumbs.MaxDecodePixels ? $"{px / 1000000} MP" : "";
                rowBad[i] = bad;
            }
        }

        // PlanFor's inputs when plan, fineShift and fineLimited were made last (0.20.132, review: the plan's pitch search,
        // with its string work, and three keys with their numbers written out, every Layout on every tab)
        int planSrcW = -1, planSrcH, planDetail, planFineCap, planPixels;
        float planWidth, planPitch;
        bool planFine;

        /// <summary>The same float, to the sign of a zero (a key writes -0 and 0 apart); NaN is NaN.</summary>
        static bool Same(float a, float b) => a == b ? a != 0f || 1f / a == 1f / b : float.IsNaN(a) && float.IsNaN(b);

        /// <summary>The plan for the header and the options (PictureOptions.PlanFor: a function of exactly these inputs)
        /// and what Fine Width made of the Resolution, made again only when one of them changed.</summary>
        void Plan()
        {
            if (!known)
            {
                plan = default;
                fineShift = 0;
                fineLimited = false;
                planSrcW = -1;                                              // made again when it is known again
                return;
            }
            if (planSrcW == header.W && planSrcH == header.H && planDetail == o.Detail && Same(planWidth, o.WidthM) && planFine == o.Fine &&
                planFineCap == o.FineCap && Same(planPitch, o.Pitch) && planPixels == o.Pixels) return;
            planSrcW = header.W;
            planSrcH = header.H;
            planDetail = o.Detail;
            planWidth = o.WidthM;
            planFine = o.Fine;
            planFineCap = o.FineCap;
            planPitch = o.Pitch;
            planPixels = o.Pixels;
            plan = o.PlanFor(header.W, header.H);
            // a new size with the same pixels makes no new grid: its caption follows here (0.20.191, test: it kept the old
            // metres after a Width change with Fine Width)
            if (thumb.Has && thumb.Texture.width == plan.W && thumb.Texture.height == plan.H) thumb.Info = Caption(plan);
            int most = MostDetail(header), chosen = Math.Min(o.Detail, most);   // Fine Width: the pixels the size took
            fineShift = o.Fine && !plan.Coarsened && !plan.Capped ? plan.W - chosen : 0;
            // the cap held it, not the size's own fewer pixels (0.20.189, review)
            int fineWant = PictureOptions.FineDetail(o.WidthM, chosen, most, out _);
            fineLimited = fineShift != 0 && o.FineCap > 0 && fineWant > plan.W && fineWant > Math.Max(o.FineCap, chosen) &&
                          fineWant - chosen <= Math.Max(2, chosen * PictureOptions.FineSpread);
        }

        static string Caption(PicturePlan p) => $"{p.W} x {p.H} px, {p.WidthM:0.##} x {p.HeightM:0.##} m";

        // the keys' inputs when they were made last
        string keyFile, keyStamp, keyDecode, keyWant;
        int keyW, keyH;
        bool keyTransparent, keyGlow;
        float keyPitch, keyCover, keyRight, keyUp, keyOut, keyMaxKb, keyTilt, keyTurn;

        /// <summary>decodeKey, wantKey and optionsKey for the file, the plan and the options, each written out again only
        /// when what it is made of changed (the very strings otherwise).</summary>
        void Keys()
        {
            if (!known) decodeKey = null;
            else if (decodeKey == null || file != keyFile || header.Stamp != keyStamp)
            {
                keyFile = file;
                keyStamp = header.Stamp;
                decodeKey = file + "|" + header.Stamp;
            }
            if (!decoded) wantKey = null;
            else if (wantKey == null || decodeKey != keyDecode || plan.W != keyW || plan.H != keyH || o.Transparent != keyTransparent)
            {
                keyDecode = decodeKey;
                keyW = plan.W;
                keyH = plan.H;
                keyTransparent = o.Transparent;
                wantKey = $"{decodeKey}|{plan.W}x{plan.H}|{o.Transparent}";
            }
            if (wantKey == null) optionsKey = null;
            else if (optionsKey == null || wantKey != keyWant || !Same(plan.Pitch, keyPitch) || !Same(o.Cover, keyCover) || !Same(o.RightM, keyRight) ||
                     !Same(o.UpM, keyUp) || !Same(o.OutM, keyOut) || !Same(o.MaxKb, keyMaxKb) || o.Glow != keyGlow ||
                     !Same(o.TiltDeg, keyTilt) || !Same(o.TurnDeg, keyTurn))
            {
                keyWant = wantKey;
                keyPitch = plan.Pitch;
                keyCover = o.Cover;
                keyRight = o.RightM;
                keyUp = o.UpM;
                keyOut = o.OutM;
                keyMaxKb = o.MaxKb;
                keyGlow = o.Glow;
                keyTilt = o.TiltDeg;
                keyTurn = o.TurnDeg;
                optionsKey = $"{wantKey}|{plan.Pitch}|{o.Cover}|{o.RightM}|{o.UpM}|{o.OutM}|{o.MaxKb}|{o.Glow}|{o.TiltDeg}|{o.TurnDeg}";
            }
        }

        static Dictionary<string, Header> ReadHeaders(string[] list)
        {
            var got = new Dictionary<string, Header>();
            foreach (string f in list)
            {
                var h = new Header { Stamp = Stamp(f) ?? "" };
                try
                {
                    if (!PictureImporter.IsReadable(f) || !ImageHeader.ImageSize(f, out h.W, out h.H)) h.Error = Unreadable;
                }
                catch (Exception) { h.Error = Unreadable; }
                got[f] = h;
            }
            return got;
        }

        /// <summary>Decodes the picked file once it has been the pick for a moment: the bytes are read on a worker, the
        /// game decodes them (one copy of the texture's bytes, the only main-thread part), a worker makes the RGBA rows,
        /// halved as Grid would first (a 50 MP photo is held as about 50 MB), turned as the EXIF orientation says.</summary>
        void Decode(float now)
        {
            if (!decodeJob.Wants(decodeKey, now, 0.3f)) return;             // the delegates only when work starts (0.20.132)
            decodeJob.WantStoppable(decodeKey, now, 0.3f,
                () =>
                {
                    string f = file, stamp = header.Stamp;
                    thumb.Info = (long)header.W * header.H > LargePhoto ? "reading a large photo ..." : $"reading {name} ...";
                    readErrorKey = null;
                    return stale => DecodeFile(f, stamp, stale);
                },
                d =>
                {
                    if (fromSign) return;                                   // the sign's own picture is the pick now
                    source = d;
                    if (headers.TryGetValue(d.File, out Header h) && (h.W != d.SrcW || h.H != d.SrcH))
                    {
                        h.W = d.SrcW;                                       // a header that lies: one plan everywhere
                        h.H = d.SrcH;
                        headersChanged++;                                   // its row says the size it has
                    }
                },
                e =>
                {
                    readError = "cannot read: " + e.Message;
                    readErrorKey = decodeJob.Done;
                    thumb.Info = "no quick view: " + e.Message;
                });
        }

        /// <summary>Worker: the file to a Decoded (the game decodes PNG and JPG on the main thread, GIFs decode here).
        /// stale (the pick, or the window, moved on): asked before the main thread's decode is queued and again when it
        /// would run (0.20.132, review: a 24-50 MP photo froze the game for a second after the window had closed).</summary>
        static Decoded DecodeFile(string file, string stamp, Func<bool> stale)
        {
            byte[] bytes = File.ReadAllBytes(file);
            var d = new Decoded { File = file, Stamp = stamp };
            if (GifDecoder.IsGif(bytes))
            {
                GifImage g = GifDecoder.Decode(bytes, maxFrames: 1, cancelled: stale);   // its first frame
                // what moving does, the frames' memory left to the row: it follows the Resolution (the screen is sized to the sign)
                d.Motion = g.SourceFrames < 2 ? "One frame: it does not move." : null;   // a long one's stride: RoomForGif
                d.MW = g.Width;
                d.MH = g.Height;
                d.Count = g.SourceFrames;
                d.Rgba = PictureImporter.GifFrameRgba(g.Frames[0]);
                d.W = d.SrcW = g.Width;
                d.H = d.SrcH = g.Height;
                for (int n = RawPixels.Halvings(d.W, d.H, 4 * PictureOptions.MaxPixels); n > 0; n--)
                    d.Rgba = RawPixels.Halve(d.Rgba, ref d.W, ref d.H);
                return d;
            }
            int orientation = PictureImporter.Orientation(bytes);
            if (stale()) throw new OperationCanceledException("no longer wanted");
            PictureImporter.RawTexture raw = MainThread.Run(() => stale() ? null : PictureImporter.ReadRaw(bytes));
            if (raw == null) throw new OperationCanceledException("no longer wanted");
            bytes = null;
            d.Rgba = RawPixels.ToRgba(raw.Bytes, raw.Format, raw.W, raw.H, RawPixels.Halvings(raw.W, raw.H, 4 * PictureOptions.MaxPixels),
                                      out d.W, out d.H);
            d.SrcW = raw.W;
            d.SrcH = raw.H;
            raw = null;
            d.Rgba = ImageHeader.Orient(d.Rgba, d.W, d.H, orientation, out d.W, out d.H);
            if (orientation >= 5)
            {
                int t = d.SrcW;
                d.SrcW = d.SrcH;
                d.SrcH = t;
            }
            return d;
        }

        /// <summary>The quick view's grid for the decoded picture and the plan, once they have stood still for a moment.</summary>
        void MakeGrid(float now)
        {
            if (!gridJob.Wants(wantKey, now, 0.3f)) return;
            gridJob.WantStoppable(wantKey, now, 0.3f,
                () =>
                {
                    Decoded d = source;
                    PictureOptions opts = o.Clone();
                    string key = wantKey;
                    thumb.Info = "drawing the quick view ...";
                    return stale =>                                         // a grid nobody waits for stops (0.20.132)
                    {
                        PicturePlan p = opts.PlanFor(d.SrcW, d.SrcH);
                        int[] px = PictureImporter.Grid(d.Rgba, d.W, d.H, p, !opts.Transparent, stale);   // the very grid Place encodes
                        return new Gridded { Px = px, Rows = Thumb.Rows(px, p.W, p.H), Plan = p, Key = key };
                    };
                },
                g =>
                {
                    thumb.Set(g.Plan.W, g.Plan.H, new[] { g.Rows }, 0);
                    gridPx = g.Px;
                    gridKey = g.Key;
                    thumb.Info = Caption(g.Plan);
                },
                e =>
                {
                    thumb.Info = "no quick view: " + e.Message;
                    gridError = e.Message;                                  // these settings: said by Problem (0.20.130, review:
                    gridErrorKey = gridJob.Done;                            // "Counting ..." for ever)
                });
        }

        /// <summary>The sign texts for the grid and the options, once they have stood still for a moment: their world-save
        /// size for the cost line, and the texts themselves for Place.</summary>
        void Count(float now)
        {
            // half a second still: a few Shift + wheel notches or a drag make one count, not one a pause (0.20.125)
            if (!costJob.Wants(costKey, now, 0.5f)) return;
            costJob.WantStoppable(costKey, now, 0.5f,
                () =>
                {
                    int[] px = gridPx;
                    PictureOptions opts = o.Clone();
                    PicturePlan p = plan;
                    string nm = name, stamp = header.Stamp, f = file;
                    int sw = header.W, sh = header.H;
                    if (px == null || px.Length != (long)p.W * p.H) throw new InvalidOperationException("the quick view is not this grid");
                    return stale =>                                         // a count nobody waits for stops (0.20.132)
                    {
                        EncodedPicture pic = PictureImporter.EncodeGrid(px, p, opts, stale);
                        pic.Name = nm;
                        pic.SourceW = sw;
                        pic.SourceH = sh;
                        int glyphs = 0;
                        foreach (int v in px) if (PixelText.Drawn(v)) glyphs++;
                        return new Costed { Pic = pic, Bytes = pic.SaveBytes, Largest = pic.MaxBytes, Glyphs = glyphs, Stamp = stamp,
                                            File = f, Pixels = p.Pixels };
                    };
                },
                c =>
                {
                    cost = c;
                    costFor = costJob.Done;
                    costError = null;
                },
                e =>
                {
                    cost = null;
                    costFor = costJob.Done;
                    costError = e.Message;
                });
        }

        bool Have => counted && costError == null && cost != null;

        /// <summary>The quick view's grid failed for these very settings (out of memory on a huge photo, say).</summary>
        string gridError, gridErrorKey;
        bool GridFailed => wantKey != null && gridErrorKey == wantKey;

        /// <summary>The size or the Resolution moved since this very picture was counted, and the new count is still to
        /// come: the last count scaled to the pixels now (Est) stands in for it, so the button and the rows stay as they
        /// are instead of going grey and lighting up again after every step (0.20.125, the author: the count runs 3-4 times
        /// or keeps toggling the button below, which flashes yellow ... let us make a smooth solution).</summary>
        bool Estimated => !counted && known && cost != null && costError == null && cost.Pixels > 0 &&
                          cost.File == file && cost.Stamp == header.Stamp && !GridFailed;

        /// <summary>A number of the last count (bytes, glyphs) for the pixels the plan has now (Estimated).</summary>
        long Est(long n) => SaveCost.Scaled(n, cost.Pixels, plan.Pixels);

        /// <summary>The bytes it stores: counted, else estimated (Estimated), else -1.</summary>
        long SaveBytes() => Have ? cost.Bytes : Estimated ? Est(cost.Bytes) : -1;

        /// <summary>Seconds every player's game spends building it: its counted glyphs (see-through pixels are none),
        /// estimated while a new count comes, else every pixel of the plan.</summary>
        double Load() => Have ? SaveCost.LoadSeconds(cost.Glyphs) : Estimated ? SaveCost.LoadSeconds(Est(cost.Glyphs)) : plan.LoadSeconds;

        /// <summary>The button pressed while the numbers were estimated: it goes on once they are counted (the texts Place
        /// writes, the limits checked on the counted bytes), the button saying "Working ..." meanwhile (Busy).</summary>
        Action pendingAct;
        SignLinkWindow pendingIn;

        void Pending()
        {
            if (pendingAct == null || Estimated) return;
            Action act = pendingAct;
            pendingAct = null;
            pendingIn = null;
            act();
        }

        /// <summary>The picture or the sign changed under a pending press: nothing goes on, the window stays open.</summary>
        void DropPending()
        {
            pendingIn?.KeepOpen();
            pendingAct = null;
            pendingIn = null;
        }
    }
}

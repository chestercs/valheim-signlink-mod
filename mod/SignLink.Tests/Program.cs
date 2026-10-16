using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SignLink.Pictures;

// Compares the plugin's picture code with the Python pipeline (vectors/vectors.json from signart/encoder_vectors.py).
// Sign texts must match byte for byte; resampled colours may differ by one level where float32 pow rounds
// differently (numpy's powf against Math.Pow), in a small share of pixels.
static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        if (ok) return;
        failures++;
        if (failures <= 30) Console.WriteLine("FAIL " + what);
    }

    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "vectors/vectors.json";
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement v = doc.RootElement;

        var samples = new List<(string name, string[] texts)>();   // resized and moved below, checked by transform_check.py
        int n = 0;
        foreach (JsonElement c in v.GetProperty("num").EnumerateArray())
        {
            double x = c.GetProperty("v").GetDouble();
            int nd = c.GetProperty("nd").GetInt32();
            Check(PyMath.Round(x, nd) == c.GetProperty("round").GetDouble(), $"round({x:R}, {nd}) = {PyMath.Round(x, nd):R}");
            Check(PyMath.Fixed(x, nd) == c.GetProperty("fixed").GetString(), $"fixed({x:R}, {nd}) = {PyMath.Fixed(x, nd)}");
            Check(PyMath.Num(x, nd) == c.GetProperty("num").GetString(), $"num({x:R}, {nd}) = {PyMath.Num(x, nd)}");
            n++;
        }
        Console.WriteLine($"numbers: {n} cases");

        foreach (JsonElement c in v.GetProperty("line_height").EnumerateArray())
        {
            double step = c.GetProperty("step").GetDouble(), want = c.GetProperty("lh").GetDouble();
            double got = PixelText.ExactLineHeight(step);
            Check(got == want, $"line height {step}: {got} != {want}");
        }
        foreach (JsonElement c in v.GetProperty("fit").EnumerateArray())
        {
            Resampler.Fit(c.GetProperty("w").GetInt32(), c.GetProperty("h").GetInt32(), c.GetProperty("max").GetInt64(), out int W, out int H);
            int[] want = c.GetProperty("fit").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            Check(W == want[0] && H == want[1], $"fit {c}: {W} x {H}");
        }
        Console.WriteLine("line heights and fits checked");

        foreach (JsonElement c in v.GetProperty("text").EnumerateArray())
        {
            string name = c.GetProperty("name").GetString();
            int w = c.GetProperty("w").GetInt32(), h = c.GetProperty("h").GetInt32();
            int[] px = c.GetProperty("px").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            double pitch = c.GetProperty("pitch").GetDouble(), cover = c.GetProperty("cover").GetDouble();
            double size = PixelText.GlyphSize(pitch, cover);
            Check(size == c.GetProperty("size").GetDouble(), $"{name}: glyph size {size:R}");
            List<PixelText.Band> got;
            string error = null;
            try
            {
                got = PixelText.Bands(px, w, h, pitch, c.GetProperty("cx").GetDouble(), c.GetProperty("bottom").GetDouble(),
                                      size, c.GetProperty("max_kb").GetDouble(), c.GetProperty("max_px").GetInt32(),
                                      !c.TryGetProperty("glow", out JsonElement g) || g.GetBoolean());
            }
            catch (ArgumentException e)
            {
                got = new List<PixelText.Band>();
                error = e.Message;
            }
            bool pyError = c.GetProperty("error").ValueKind != JsonValueKind.Null;
            Check(pyError == (error != null), $"{name}: python error {pyError}, C# error {error}");
            JsonElement[] want = c.GetProperty("bands").EnumerateArray().ToArray();
            Check(got.Count == want.Length, $"{name}: {got.Count} bands, python {want.Length}");
            if (want.Length > 0 && want[0].GetProperty("text").GetString().Length < 40000)
                samples.Add(("pixels " + name, new[] { want[0].GetProperty("text").GetString() }));
            long chars = 0;
            for (int i = 0; i < Math.Min(got.Count, want.Length); i++)
            {
                string t = want[i].GetProperty("text").GetString();
                chars += t.Length;
                Check(got[i].Row0 == want[i].GetProperty("r0").GetInt32() && got[i].Row1 == want[i].GetProperty("r1").GetInt32(),
                      $"{name} band {i}: rows {got[i].Row0}-{got[i].Row1}");
                if (got[i].Text != t)
                {
                    int k = 0;
                    while (k < Math.Min(t.Length, got[i].Text.Length) && t[k] == got[i].Text[k]) k++;
                    Check(false, $"{name} band {i}: text differs at {k}: python '{Excerpt(t, k)}' C# '{Excerpt(got[i].Text, k)}'");
                }
            }
            Console.WriteLine($"text: {name}: {got.Count} band(s), {chars} characters" + (error != null ? $" (error as python: {error})" : ""));

            // the quick view reads the picture back from its texts: the grid, cropped to its pixels (one pixel alone
            // tells no pixel pitch)
            if (want.Length > 0 && px.Count(PixelText.Drawn) > 1)
            {
                string[] texts = want.Select(e => e.GetProperty("text").GetString()).ToArray();
                bool ok = TextReadback.Read(texts, out int rw, out int rh, out int[] back);
                int r0 = h, r1 = -1, c0 = w, c1 = -1;
                for (int r = 0; r < h; r++)
                    for (int cc = 0; cc < w; cc++)
                        if (PixelText.Drawn(px[r * w + cc])) { r0 = Math.Min(r0, r); r1 = Math.Max(r1, r); c0 = Math.Min(c0, cc); c1 = Math.Max(c1, cc); }
                int ew = c1 - c0 + 1, eh = r1 - r0 + 1, diff = 0;
                if (ok && rw == ew && rh == eh)
                    for (int r = 0; r < eh; r++)
                        for (int cc = 0; cc < ew; cc++)
                            if (back[r * rw + cc] != px[(r + r0) * w + cc + c0]) diff++;
                Check(ok && rw == ew && rh == eh && diff == 0, $"{name}: read back {(ok ? $"{rw} x {rh}, {diff} pixels differ" : "failed")}, want {ew} x {eh}");

                // ... and so does a picture the fine-tuner moved or resized (every band of the stack on its own)
                if (ok && TextTransform.Supports(texts, out _))
                {
                    double[][] moves = { new[] { 1.0, 10, 0 }, new[] { 1.0, 0, 10 }, new[] { 1.0, -13.3, -27.1 }, new[] { 1.0, 0, -200 },
                                         new[] { 1.5, 0, 0 }, new[] { 0.5, 4, 9 }, new[] { 2.0, -30, 60 } };
                    foreach (double[] m in moves)
                    {
                        double k = TextTransform.Snap(texts, m[0]);
                        if (k <= 0) continue;
                        string[] shifted;
                        try { shifted = TextTransform.Apply(texts, k, m[1], m[2]); }
                        catch (ArgumentException) { continue; }
                        bool ok2 = TextReadback.Read(shifted, out int mw, out int mh, out int[] mback);
                        int mdiff = 0;
                        if (ok2 && mw == rw && mh == rh)
                            for (int i2 = 0; i2 < back.Length; i2++)
                                if (mback[i2] != back[i2]) mdiff++;
                        Check(ok2 && mw == rw && mh == rh && mdiff == 0,
                              $"{name}: moved x{k} by ({m[1]}, {m[2]}): read back {(ok2 ? $"{mw} x {mh}, {mdiff} pixels differ" : "failed")}, want {rw} x {rh}");
                    }
                }
            }
        }

        foreach (JsonElement c in v.GetProperty("resample").EnumerateArray())
        {
            string name = c.GetProperty("name").GetString();
            byte[] rgba = Convert.FromBase64String(c.GetProperty("rgba").GetString());
            int w = c.GetProperty("w").GetInt32(), h = c.GetProperty("h").GetInt32();
            int W = c.GetProperty("W").GetInt32(), H = c.GetProperty("H").GetInt32();
            int[] want = c.GetProperty("px").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            int[] got = Resampler.Grid(rgba, w, h, W, H, c.GetProperty("opaque").GetBoolean());
            int exact = 0, maxDiff = 0, emptyMismatch = 0;
            for (int i = 0; i < want.Length; i++)
            {
                if (got[i] == want[i]) { exact++; continue; }
                if (!PixelText.Drawn(got[i]) || !PixelText.Drawn(want[i])) { emptyMismatch++; continue; }
                for (int s = 0; s < 32; s += 8)
                    maxDiff = Math.Max(maxDiff, Math.Abs(((got[i] >> s) & 255) - ((want[i] >> s) & 255)));
            }
            double share = (double)exact / want.Length;
            Console.WriteLine($"resample: {name}: {w}x{h} -> {W}x{H}, {share:P3} exact, max difference {maxDiff}, empty mismatches {emptyMismatch}");
            Check(maxDiff <= 1 && emptyMismatch == 0 && share >= 0.995, $"{name}: resample off");
        }

        // recipes (SignUtil.SourceKey): every kind round-trips, 0.19's strings still read, odd names survive, numbers from
        // another player's sign are checked; Saved is gone (0.20.64): its recipes are none
        {
            var made = new PictureOptions { WidthM = 3.25f, Pitch = 0.25f, Cover = 0f, Transparent = false, Glow = false, Animate = true };
            foreach (string name in new[] { "my|odd name.png", "quote\"s.jpg", "ékezet és szóköz.gif", "a" })
            {
                string src = SignLink.Signs.SignRecipe.ForPicture(made, name).ToString();
                SignLink.Signs.SignRecipe back = SignLink.Signs.SignRecipe.Parse(src);
                var o = new PictureOptions();
                back.Apply(o);
                Check(back.Kind == SignLink.Signs.SignRecipe.Kinds.Picture && back.File == name && o.WidthM == 3.25f && o.Pitch == 0.25f && o.Cover == 0f &&
                      !o.Transparent && !o.Glow && o.Animate && o.Place == Placement.Custom && back.ToString() == src, $"recipe: {src}");
            }
            Check(SignLink.Signs.SignRecipe.Parse("pic|2|0.125|2|1|1|0|starry.jpg").SameItem(SignLink.Signs.SignRecipe.Parse("pic|3|0.5|0|0|0|0|STARRY.jpg")), "recipe: the same file, other settings");
            Check(!SignLink.Signs.SignRecipe.Parse("pic|2|0.125|2|1|1|0|a.jpg").SameSettings(SignLink.Signs.SignRecipe.Parse("pic|3|0.125|2|1|1|0|a.jpg")), "recipe: another width");
            var withRes = SignLink.Signs.SignRecipe.Parse("pic|4.5|0.625|2|1|1|0|320|Impression, Sunrise (Monet).jpg");
            Check(withRes.Kind == SignLink.Signs.SignRecipe.Kinds.Picture && withRes.Detail == 320 && withRes.File == "Impression, Sunrise (Monet).jpg" &&
                  withRes.ToString() == "pic|4.5|0.625|2|1|1|0|320|Impression, Sunrise (Monet).jpg", "recipe: a picture with its Resolution");
            Check(SignLink.Signs.SignRecipe.Parse("pic|4.5|0.625|2|1|1|0|a.jpg").Detail == 0 &&
                  SignLink.Signs.SignRecipe.Parse("pic|4.5|0.625|2|1|1|0|a.jpg").ToString() == "pic|4.5|0.625|2|1|1|0|a.jpg", "recipe: an older picture keeps its form");
            Check(withRes.SameSettings(SignLink.Signs.SignRecipe.Parse("pic|4.5|0.625|2|1|1|0|288|Impression, Sunrise (Monet).jpg")),
                  "recipe: the Resolution is not a setting of the grid");
            Check(SignLink.Signs.SignRecipe.Parse("pic|4.5|0.625|2|1|1|0|x|a.jpg").File == "x|a.jpg", "recipe: no number there is an older file name with a bar");
            SignLink.Signs.SignRecipe art = SignLink.Signs.SignRecipe.Parse("art|Glowing|SignLink.Art.glyph/Eikthyr.stack.txt");
            Check(art.Kind == SignLink.Signs.SignRecipe.Kinds.Art && art.Light == GlowMode.Glowing && art.Path == "SignLink.Art.glyph/Eikthyr.stack.txt" &&
                  art.ToString() == "art|Glowing|SignLink.Art.glyph/Eikthyr.stack.txt", "recipe: art");
            SignLink.Signs.SignRecipe still = SignLink.Signs.SignRecipe.Parse("art|Lit|still|SignLink.Art.pixel/Puppy (moving, bouncing).anim.txt");
            Check(still.Kind == SignLink.Signs.SignRecipe.Kinds.Art && still.Still && still.Light == GlowMode.Lit &&
                  still.Path == "SignLink.Art.pixel/Puppy (moving, bouncing).anim.txt" &&
                  still.ToString() == "art|Lit|still|SignLink.Art.pixel/Puppy (moving, bouncing).anim.txt" && !art.Still, "recipe: an animated piece as a still");
            Check(still.SameItem(SignLink.Signs.SignRecipe.ForPiece(SignLink.Signs.SignRecipe.Kinds.Art, GlowMode.Lit, still.Path)) &&
                  !still.SameSettings(SignLink.Signs.SignRecipe.ForPiece(SignLink.Signs.SignRecipe.Kinds.Art, GlowMode.Lit, still.Path)),
                  "recipe: still or moving is the same piece with other settings");
            Check(SignLink.Signs.SignRecipe.Parse("art|Lit|still|").Kind == SignLink.Signs.SignRecipe.Kinds.None, "recipe: a still with no piece is none");
            Check(SignLink.Signs.SignRecipe.Parse("saved|Lit|My sign.stack.txt").Kind == SignLink.Signs.SignRecipe.Kinds.None, "recipe: Saved is gone, its recipe is none");
            foreach (string bad in new[] { "pic|NaN|0.125|2|1|1|0|a.png", "pic|Infinity|0.125|2|1|1|0|a.png", "pic|2|0.3|2|1|1|0|a.png", "pic|2|0.125|7|1|1|0|a.png", "pic|0.0005|0.125|2|1|1|0|a.png", "pic|301|0.125|2|1|1|0|a.png" })
                Check(SignLink.Signs.SignRecipe.Parse(bad).Kind == SignLink.Signs.SignRecipe.Kinds.None, $"recipe: '{bad}' is out of range");
            Check(SignLink.Signs.SignRecipe.Clean("<b>evil</b>\u0001name\n" + new string('x', 200)).Length == 80 && !SignLink.Signs.SignRecipe.Clean("<b>x").Contains("<"), "recipe: names cleaned");
            Check(SignLink.Signs.SignRecipe.Parse("art|Nonsense|x").Light == GlowMode.AsMade, "recipe: an unknown light reads as made");
            Check(SignLink.Signs.SignRecipe.Parse("text").Kind == SignLink.Signs.SignRecipe.Kinds.Text && SignLink.Signs.SignRecipe.ForText().ToString() == "text", "recipe: text");
            foreach (string bad in new[] { "", "pic|x|y", "pic|1|0.5|2|1|1|1|", "art|Glowing", "nonsense", "pic" })
                Check(SignLink.Signs.SignRecipe.Parse(bad).Kind == SignLink.Signs.SignRecipe.Kinds.None, $"recipe: '{bad}' is none");
            Check(SignLink.Signs.SignRecipe.None.ToString() == "" && !SignLink.Signs.SignRecipe.None.SameItem(SignLink.Signs.SignRecipe.None), "recipe: none");
            // a piece by path, by a name only one piece has, and not by a shared name (two Dragon ships)
            var items = SignLink.Presets.ArtLibrary.Items;
            int ship = items.FindIndex(p => p.Name == "Dragon ship");
            Check(ship >= 0 && items.FindLastIndex(p => p.Name == "Dragon ship") != ship, "art: two Dragon ships expected (the test's premise)");
            Check(SignLink.Presets.Preset.Find(items, "Dragon ship") == -1, "find: a shared name finds nothing");
            Check(SignLink.Presets.Preset.Find(items, items[ship].Path) == ship, "find: by path");
            int eik = items.FindIndex(p => p.Name == "Eikthyr");
            Check(eik >= 0 && SignLink.Presets.Preset.Find(items, "Eikthyr") == eik, "find: by a unique name");
            Check(SignLink.Presets.Preset.Find(items, "") == -1 && SignLink.Presets.Preset.Find(items, null) == -1, "find: nothing");
            Console.WriteLine("recipes checked");
        }

        // EXIF orientations: the pixels turned as Pillow's exif_transpose turns them
        foreach (JsonElement c in v.GetProperty("orient").EnumerateArray())
        {
            int o = c.GetProperty("o").GetInt32();
            byte[] got = ImageHeader.Orient(Convert.FromBase64String(c.GetProperty("rgba").GetString()), c.GetProperty("w").GetInt32(),
                                            c.GetProperty("h").GetInt32(), o, out int ow, out int oh);
            Check(ow == c.GetProperty("W").GetInt32() && oh == c.GetProperty("H").GetInt32() &&
                  got.SequenceEqual(Convert.FromBase64String(c.GetProperty("want").GetString())), $"orientation {o}: turned wrong");
        }
        Console.WriteLine("EXIF orientations checked");

        // half-transparent pixels under overlapping glyphs (cover 2: four over every point) show their own alpha
        {
            int worst = 0;
            for (int a = 1; a < 255; a++)
            {
                int put = PixelText.ForCover(new[] { PixelText.Pack(0x336699, a) }, 2)[0];
                worst = Math.Max(worst, Math.Abs(PixelText.Alpha(PixelText.AsSeen(put, 4)) - a));
                Check((put & 0xFFFFFF) == 0x336699 && PixelText.Alpha(put) <= a, $"cover alpha {a}: put {PixelText.Alpha(put)}");
            }
            Check(worst <= 4, $"cover alpha: seen up to {worst} off");
            Check(PixelText.ForCover(new[] { 0x123456, -1 }, 2)[0] == 0x123456, "cover: opaque stays");
            Check(PixelText.ShortHex(PixelText.Pack(0x112233, 0x44)) == "#1234" && PixelText.ShortHex(PixelText.Pack(0x102030, 0x80)) == "#10203080" &&
                  PixelText.ShortHex(0x112233) == "#123", "short hex with alpha");
            Console.WriteLine($"half transparency checked (four layers seen within {worst}/255)");
        }

        // picture sizes from headers: every image next to the vectors (encoder_vectors.py copies them as sizes.json)
        string sizes = Path.Combine(Path.GetDirectoryName(path), "sizes.json");
        if (File.Exists(sizes))
            foreach (JsonElement c in JsonDocument.Parse(File.ReadAllText(sizes)).RootElement.EnumerateArray())
            {
                string file = Path.Combine(Path.GetDirectoryName(path), c.GetProperty("file").GetString());
                bool ok = ImageHeader.ImageSize(file, out int w, out int h);
                Check(ok && w == c.GetProperty("w").GetInt32() && h == c.GetProperty("h").GetInt32(),
                      $"header size of {c.GetProperty("file").GetString()}: {w} x {h}");
                Console.WriteLine($"header: {c.GetProperty("file").GetString()}: {w} x {h}");
            }

        // GIFs: every composited frame against Pillow's
        string gifs = Path.Combine(Path.GetDirectoryName(path), "gifs.json");
        if (File.Exists(gifs))
            foreach (JsonElement c in JsonDocument.Parse(File.ReadAllText(gifs)).RootElement.EnumerateArray())
            {
                string name = c.GetProperty("file").GetString();
                GifImage g = GifDecoder.Decode(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), name)));
                string[] want = c.GetProperty("sha1").EnumerateArray().Select(e => e.GetString()).ToArray();
                int[] durs = c.GetProperty("durations").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                Check(g.Width == c.GetProperty("w").GetInt32() && g.Height == c.GetProperty("h").GetInt32(), $"{name}: size {g.Width} x {g.Height}");
                Check(g.Frames.Count == want.Length, $"{name}: {g.Frames.Count} frames, pillow {want.Length}");
                int same = 0;
                using (var sha = System.Security.Cryptography.SHA1.Create())
                    for (int i = 0; i < Math.Min(want.Length, g.Frames.Count); i++)
                    {
                        string got = BitConverter.ToString(sha.ComputeHash(g.Frames[i])).Replace("-", "").ToLowerInvariant();
                        if (got == want[i]) same++;
                        else Check(false, $"{name}: frame {i} differs");
                        Check(g.DelaysMs[i] == durs[i], $"{name}: frame {i} delay {g.DelaysMs[i]} ms, pillow {durs[i]}");
                    }
                Console.WriteLine($"gif: {name}: {g.Width} x {g.Height}, {g.Frames.Count} frames, {same} identical to pillow");
            }

        // glowing and lit (SignGlow): a lit picture is the glowing one without its font tag; glowing tags of every
        // spelling go, other fonts stay
        {
            int[] gpx = { 0xff0000, 0x00ff00, -1, 0x123456 };
            double gs = PixelText.GlyphSize(1.0, 2);
            string glowText = PixelText.ColumnText(gpx, 2, 2, 0, 2, 1.0, 0, 40, gs);
            string litText = PixelText.ColumnText(gpx, 2, 2, 0, 2, 1.0, 0, 40, gs, glow: false);
            Check(glowText.Contains(SignGlow.Tag) && !litText.Contains("<font"), "glow: the picture's tag");
            Check(SignGlow.Strip(glowText) == litText, "glow: stripping a picture's tag gives the lit picture");
            Check(SignGlow.Apply(litText, GlowMode.Glowing) == SignGlow.Tag + litText, "glow: glowing adds one tag");
            Check(SignGlow.Apply(glowText, GlowMode.Glowing) == SignGlow.Tag + litText, "glow: no second tag");
            Check(SignGlow.Apply(glowText, GlowMode.Lit) == litText, "glow: lit");
            Check(SignGlow.Apply(glowText, GlowMode.AsMade) == glowText, "glow: as made");
            Check(SignGlow.Apply("", GlowMode.Glowing) == "", "glow: an empty sign stays empty");
            string rune = "<font=valheim_fonts/valheim-rune>";
            // the Text source's Light row reads the text itself: a glowing tag anywhere makes it glowing, none makes it lit
            Check(SignGlow.IsGlowing(SignGlow.Tag + "x") && SignGlow.IsGlowing("a <font=\"Valheim-Norse\">b</FONT> c") &&
                  !SignGlow.IsGlowing("x") && !SignGlow.IsGlowing("") && !SignGlow.IsGlowing(null) && !SignGlow.IsGlowing(rune + "A</font>"),
                  "glow: a text is glowing when it holds a glowing tag, lit when it holds none");
            string[][] strip =
            {
                new[] { "<font=亨杓麩壀辨>A</font>B", "AB" },        // the 5-character alias
                new[] { "<font=BTR1GBJ>A</font>B", "AB" },
                new[] { "<font=\"Valheim-Norse\">A</FONT>B", "AB" },
                new[] { rune + "A</font>B", rune + "A</font>B" },
                new[] { rune + "A" + SignGlow.Tag + "B</font>C</font>D", rune + "ABC</font>D" },
                new[] { "3 < 4 " + SignGlow.Tag + "x", "3 < 4 x" },
                new[] { "<b>bold</b> <font>", "<b>bold</b> <font>" },
            };
            foreach (string[] c in strip)
            {
                string a = c[0], want = c[1];
                Check(SignGlow.Strip(a) == want, $"glow: strip '{a}' = '{SignGlow.Strip(a)}', want '{want}'");
            }
            Check(SaveCost.Bytes(new[] { "", "é" }) == 2 * SaveCost.PerSign + 2, "save cost");
            Check(TextReadback.Words(new[] { "<size=20><#f00>Hello \n  world</size>", "", "<b>x</b>" }, 100) == "Hello world / x",
                  $"words: '{TextReadback.Words(new[] { "<size=20><#f00>Hello \n  world</size>", "", "<b>x</b>" }, 100)}'");
            Check(TextReadback.Words(new[] { "abcdefghij" }, 4) == "abcd...", "words: cut");
            Check(SaveCost.Glyphs(new[] { "<size=2><#f00>a b\nc", "3 < 4 <b>x</b>" }) == 7, $"glyphs: {SaveCost.Glyphs(new[] { "<size=2><#f00>a b\nc", "3 < 4 <b>x</b>" })}");
            Check(SaveCost.Glyphs(new[] { glowText }) == 3, "glyphs of a picture: one per pixel");

            // the wait of a player who arrives on a server: the download (150 KB/s at best) and then the build. A 664 KB
            // picture that builds in 0.7 s showed "0.7 s" in green; it costs about 5 s (author, 2026-10-11)
            double wait = SaveCost.WaitSeconds(0.7, 664 * 1024);
            Check(Math.Abs(wait - (664 / 150.0 + 0.7)) < 1e-9, $"wait = download + build: {wait}");
            Check(SaveCost.WaitSeconds(0.7, -1) == 0.7, "wait before the bytes are counted: the build only");
            Check(SaveCost.Level(2.9) == 0 && SaveCost.Level(3.1) == 1 && SaveCost.Level(10) == 1 && SaveCost.Level(10.1) == 2,
                  "wait levels: green to 3 s, yellow to 10 s, red above");
            Check(!SaveCost.Heavy(0.7, 664 * 1024, 100 * 1024), "a 664 KB picture is not red");
            Check(SaveCost.Heavy(0.7, 2 * 1048576, 100 * 1024), "a 2 MB picture is red (about 14 s)");
            Check(SaveCost.Heavy(10.5, -1, 0) && !SaveCost.Heavy(6, -1, 0), "not counted yet: the build alone decides");
            Check(SaveCost.Heavy(0, 1000, 401 * 1024), "a sign over 400 KB is red");
            string line = SaveCost.WaitLine(0.7, 664 * 1024);
            Check(line.Contains($"{wait:0.#} s") && line.Contains($"{664 / 150.0:0.#} s") && line.Contains($"{0.7:0.#} s"),
                  $"wait line names the total, the download and the build: '{line}'");

            // the hard limits in a shared world (author, 2026-10-11): 300 KB on one sign stack, 600 KB in its area
            Check(SaveCost.SignLimit == 300 * 1024 && SaveCost.AreaLimit == 600 * 1024 && SaveCost.AreaRadiusM == 100,
                  $"limits: {SaveCost.SignLimit} / {SaveCost.AreaLimit} within {SaveCost.AreaRadiusM} m");
            Check(SaveCost.Fits(300 * 1024, true) && !SaveCost.Fits(300 * 1024 + 1, true), "300 KB fits a sign, a byte more does not");
            Check(SaveCost.Fits(50L * 1048576, false), "single player: no limit");
            Check(!SaveCost.Fits(-1, true), "not counted yet: does not fit in a shared world");
            string over = SaveCost.OverLimit(400 * 1024);
            Check(over.Contains("400 KB") && over.Contains("300 KB"), $"over-limit words name both sizes: '{over}'");
            const long K = 1024;
            Check(SaveCost.Allowed(5000 * K, 0, 5000 * K, false, out string why) && why == null, "single player: anything");
            Check(SaveCost.Allowed(200 * K, 0, 300 * K, true, out why) && why == null, "200 KB into an area holding 300 KB: fits");
            Check(SaveCost.Allowed(200 * K, 0, 400 * K, true, out why), "exactly 600 KB in the area: fits");
            Check(!SaveCost.Allowed(200 * K, 0, 500 * K, true, out why) && why.Contains("600 KB") && why.Contains("500 KB"),
                  $"over the area: refused, naming the area's bytes and the limit: '{why}'");
            Check(why.Contains("SignLink signs"), $"over the area: it says only SignLink signs count (0.20.67): '{why}'");
            Check(SaveCost.Allowed(100 * K, 250 * K, 700 * K, true, out why), "smaller than what the sign holds: allowed even in a full area");
            Check(!SaveCost.Allowed(350 * K, 0, 0, true, out why) && why.Contains("300 KB"), $"over the sign limit: '{why}'");
            Check(!SaveCost.Allowed(-1, 0, 0, true, out why), "not counted yet: refused");
            string meter = SaveCost.AreaLine(250 * K, 370 * K);
            Check(meter.Contains("250 KB / 600 KB") && meter.Contains("with this: 370 KB"), $"area line: '{meter}'");
            Check(!SaveCost.AreaLine(250 * K, 250 * K).Contains("with this"), "area line without a change: no 'with this'");
            Console.WriteLine("glow and lit checked");
        }

        // where the encoder puts a picture on its sign, turned with the sign (SignPose) or not, and that a lying sign's
        // boards stay under the floor for as many signs as a picture can need
        {
            var stand = new PictureOptions { Place = Placement.Custom, RightM = 1.5f, UpM = -2f, Pitch = 0.5f };
            var plan = new PicturePlan { W = 80, H = 60, Pitch = 0.5f };
            stand.Origin(plan, out double sx, out double sb);
            Check(sx == 60 && sb == -80 - 15, $"origin standing: {sx}, {sb}");
            PictureOptions turned = stand.Clone();
            turned.TurnDeg = 90f;                     // clockwise: the sign's x points down, its y right
            turned.Origin(plan, out double tx, out double tb);
            Check(Math.Abs(tx - 80) < 1e-9 && Math.Abs(tb - (60 - 15)) < 1e-9, $"origin turned 90°: ({tx}, {tb})");
            turned.TurnDeg = -180f;
            turned.Origin(plan, out tx, out tb);
            Check(Math.Abs(tx + 60) < 1e-9 && Math.Abs(tb - (80 - 15)) < 1e-9, $"origin turned -180°: ({tx}, {tb})");
            int most = (int)Math.Ceiling((double)PictureOptions.MaxPixels / PixelText.MaxPixelsPerSign) + 4;   // band rounding
            Check(SignLink.Signs.FloorPlan.Lift > 0, "floor: the picture over the floor");
            Check(SignLink.Signs.FloorPlan.BoardTop(most - 1) < 0, $"floor: the board of sign {most} sticks out ({SignLink.Signs.FloorPlan.BoardTop(most - 1):0.####} m)");
            Check(SignLink.Signs.FloorPlan.ColliderTop(2 * most) < 0, "floor: a collider sticks out");
            Console.WriteLine($"origin and lying checked (a lying stack of {most} signs stays under the floor)");

            // tipping over the bottom edge (FloorPlan.Tip): the edge stays, face up the text lies Lift over the floor the
            // sign stood on, face down Lift under that line, upright nothing moves
            {
                const double foot = 0.275, textZ = 0.07, lift = SignLink.Signs.FloorPlan.Lift;
                SignLink.Signs.FloorPlan.Tip(0, foot, textZ, out double f0, out double u0);
                Check(Math.Abs(f0) < 1e-12 && Math.Abs(u0) < 1e-12, $"tip 0°: moved ({f0}, {u0})");
                for (int i = 0; i <= 36; i++)
                {
                    double t = -90 + 5 * i, a = t * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                    SignLink.Signs.FloorPlan.Tip(t, foot, textZ, out double fw, out double up);
                    // the edge (textZ - Lift, -foot) in the sign's frame, tipped, then moved: where it was
                    double ez = textZ - lift, ey = -foot;
                    double gz = fw + ez * c - ey * s, gy = up + ez * s + ey * c;
                    Check(Math.Abs(gz - ez) < 1e-12 && Math.Abs(gy - ey) < 1e-12, $"tip {t}°: the edge moved to ({gz}, {gy})");
                }
                SignLink.Signs.FloorPlan.Tip(90, foot, textZ, out double fu, out double uu);
                Check(Math.Abs(uu + textZ - (-foot + lift)) < 1e-12, $"face up: text {uu + textZ + foot:0.####} m over the floor");
                Check(fu < 0, $"face up: the picture lies behind where the sign stood ({fu:0.###} m)");
                SignLink.Signs.FloorPlan.Tip(-90, foot, textZ, out double fd, out double ud);
                Check(Math.Abs(ud - textZ - (-foot - lift)) < 1e-12, $"face down: text {ud - textZ + foot:0.####} m from the line");
                Check(fd > 0, $"face down: the picture lies in front ({fd:0.###} m)");
                Check(Fit.TiltWords(90) == "lying face up" && Fit.TiltWords(-90) == "lying face down" && Fit.TiltWords(0) == "" &&
                      Fit.TiltWords(30) == "tilted back 30°", "tilt words");
                Console.WriteLine($"tipping checked: face up the picture's middle lies {textZ - fu:0.###} m behind where the text stood, face down {fd - textZ:0.###} m in front");
            }

            // a text already carrying its fit (the sign's own, in the editor): where its middle was as made (0.20.40)
            {
                var arnd = new Random(13);
                for (int t = 0; t < 100; t++)
                {
                    double c0x = arnd.NextDouble() * 200 - 100, c0y = arnd.NextDouble() * 200 - 100;
                    Fit f = Rand(arnd);
                    f.Centre(c0x, c0y, out double shx, out double shy);         // where it shows with the fit
                    f.MadeCentre(shx, shy, out double bx, out double by);           // ... and back
                    Check(Math.Abs(bx - c0x) < 1e-6 && Math.Abs(by - c0y) < 1e-6, $"as made {t}: ({bx}, {by}) for ({c0x}, {c0y})");
                }
            }

            // a piece put centred (0.20.38, the author: at 0-0-0 the glyph art pieces are not centred by default):
            // Right and Up count from the sign's centre, whatever centre c0 the piece was made at
            {
                var f0 = new Fit { Scale = 1 };
                f0.OffsetCentred(0, 60, out double ax, out double ay);
                Check(Math.Abs(ax) < 1e-9 && Math.Abs(ay + 60) < 1e-9, $"centred: 0-0-0 brings a piece made 60 wu up to the middle ({ax}, {ay})");
                var crnd = new Random(11);
                for (int t = 0; t < 100; t++)
                {
                    double c0x = crnd.NextDouble() * 200 - 100, c0y = crnd.NextDouble() * 200 - 100;
                    Fit f = Rand(crnd);
                    f.OffsetCentred(c0x, c0y, out double cdx, out double cdy);
                    f.Offset(0, 0, out double zx, out double zy);            // where a piece made at the middle goes
                    Check(Math.Abs(f.Scale * c0x + cdx - zx) < 1e-6 && Math.Abs(f.Scale * c0y + cdy - zy) < 1e-6,
                          $"centred {t}: the piece's middle lands where one made at the middle would");
                }
            }

            // Fit: texts as made at c0, scaled, moved and turned with their sign; Between gets from one fit to another
            var rnd = new Random(7);
            for (int t = 0; t < 200; t++)
            {
                double c0x = rnd.NextDouble() * 200 - 100, c0y = rnd.NextDouble() * 200 - 100;
                Fit f1 = Rand(rnd), f2 = Rand(rnd);
                f1.Offset(c0x, c0y, out double o1x, out double o1y);
                f2.Offset(c0x, c0y, out double o2x, out double o2y);
                // where the centre shows once the sign turns: where Right and Up put it on the sign
                f2.OnSign(out double r2, out double u2, out double n2);
                Fit.TurnPoint(f2.Scale * c0x + o2x, f2.Scale * c0y + o2y, f2.Turn, out double wx, out double wy);
                Check(Math.Abs(wx - (f2.Scale * c0x + r2)) < 1e-6 && Math.Abs(wy - (f2.Scale * c0y + u2)) < 1e-6,
                      $"fit {t}: the centre lands off ({wx}, {wy})");
                // ... which, with the sign moved along its normal, is Right, Up, Out as built: the tilt turns only the sign
                double ta = f2.Tilt * Math.PI / 180;
                double gy = u2 * Math.Cos(ta) + n2 * Math.Sin(ta), gz = -u2 * Math.Sin(ta) + n2 * Math.Cos(ta);
                Check(Math.Abs(gy - f2.Up) < 1e-6 && Math.Abs(gz - f2.Out) < 1e-6, $"fit {t}: tilted, the place moved to ({gy}, {gz})");
                // a point of the piece: as f1 put it, moved on by Between, is where f2 puts it
                double px = rnd.NextDouble() * 400 - 200, py = rnd.NextDouble() * 400 - 200;
                Fit.Between(f1, f2, c0x, c0y, out double k, out double dx, out double dy);
                double ax = k * (f1.Scale * px + o1x) + dx, ay = k * (f1.Scale * py + o1y) + dy;
                Check(Math.Abs(ax - (f2.Scale * px + o2x)) < 1e-6 && Math.Abs(ay - (f2.Scale * py + o2y)) < 1e-6, $"fit {t}: between");
                // the piece turns as a whole: a point keeps its distance from the centre, turned by the sign
                Fit.TurnPoint(f2.Scale * px + o2x, f2.Scale * py + o2y, f2.Turn, out double qx, out double qy);
                double rx = f2.Scale * (px - c0x), ry = f2.Scale * (py - c0y);
                Fit.TurnPoint(rx, ry, f2.Turn, out double tx2, out double ty2);
                Check(Math.Abs(qx - (wx + tx2)) < 1e-6 && Math.Abs(qy - (wy + ty2)) < 1e-6, $"fit {t}: turns around its centre");
            }
            Check(Fit.AsMade.Same(new Fit { Scale = 1, Turn = 360 }), "fit: a full turn is no turn");
            Fit flat = new Fit { Scale = 1, Right = 80, Out = 200, Tilt = -90 };
            flat.OnSign(out double fr, out double fu2, out double fn);
            Check(Math.Abs(fr - 80) < 1e-9 && Math.Abs(fu2 - 200) < 1e-9 && Math.Abs(fn) < 1e-9, "face down: towards you is up its face, no move");
            flat.Tilt = 90;
            flat.OnSign(out _, out fu2, out fn);
            Check(Math.Abs(fu2 + 200) < 1e-9 && Math.Abs(fn) < 1e-9, "face up: towards you is down its face");
            flat.Tilt = 0;
            flat.OnSign(out _, out fu2, out fn);
            Check(Math.Abs(fu2) < 1e-9 && Math.Abs(fn - 200) < 1e-9, "standing: towards you moves the sign out");
            // the content's middle on the turned sign (Fit.Centre), where the ghost frame draws: a picture made centred,
            // 1 m right, shows at (40, 0) wu as built; with the sign turned 90° clockwise that point is "up" in its own frame
            var right = new Fit { Scale = 1, Right = 40 };
            right.Centre(0, 0, out double gcx, out double gcy);
            Check(Math.Abs(gcx - 40) < 1e-9 && Math.Abs(gcy) < 1e-9, $"centre: ({gcx}, {gcy})");
            right.Turn = 90;
            right.Centre(0, 0, out gcx, out gcy);
            Check(Math.Abs(gcx) < 1e-9 && Math.Abs(gcy - 40) < 1e-9, $"centre turned 90°: ({gcx}, {gcy})");
            var piece = new Fit { Scale = 2, Up = 10, Tilt = 90 };                 // a piece made at (5, 60), doubled, 0.25 m "up" face up: along its face towards you
            piece.Centre(5, 60, out gcx, out gcy);
            Check(Math.Abs(gcx - 10) < 1e-9 && Math.Abs(gcy - 120) < 1e-9, $"centre lying: ({gcx}, {gcy}) (Up does nothing lying)");
            piece.Out = -10;                                                        // away: up along a face-up sign's face
            piece.Centre(5, 60, out gcx, out gcy);
            Check(Math.Abs(gcx - 10) < 1e-9 && Math.Abs(gcy - 130) < 1e-9, $"centre lying, away: ({gcx}, {gcy})");
            Console.WriteLine("fit checked (200 random fits: centre, between, turning around the centre, the place kept when tilted)");
        }

        // the built-in art (ArtLibrary, the same embedded files as the plugin): every piece loads; pixel pieces read
        // back (animations frame by frame), glyph pieces carry their render
        foreach (SignLink.Presets.Preset p in SignLink.Presets.ArtLibrary.Items)
        {
            SignLink.Presets.Preset.Content c = p.Load();
            var input = new List<IList<string>>();
            if (p.Kind == SignLink.Presets.Preset.Kinds.Anim) foreach (string f in c.Texts) input.Add(new[] { f });
            else input.Add(c.Texts);
            bool pixel = TextReadback.ReadFrames(input, out int aw, out int ah, out List<int[]> grids);
            bool picture = p.ReadPicture != null && p.ReadPicture().Length > 0;
            Console.WriteLine($"art: {p.Category} / {p.Name}: {p.Kind}, {c.Texts.Length} text(s), " + (pixel ? $"{aw} x {ah} pixels" : picture ? "render" : "NO QUICK VIEW"));
            Check(p.Pixel ? pixel : picture, $"art {p.Name}: no quick view");
            IList<string> lit = SignGlow.Apply(c.Texts, GlowMode.Lit), glowing = SignGlow.Apply(c.Texts, GlowMode.Glowing);
            Check(lit.All(t => !t.Contains("<font=" + PixelText.Font)), $"art {p.Name}: lit keeps a glowing tag");
            Check(glowing.All(t => t.Length == 0 || t.StartsWith(SignGlow.Tag)), $"art {p.Name}: glowing lacks the tag");
            samples.Add(("art " + p.Name, p.Kind == SignLink.Presets.Preset.Kinds.Anim ? new[] { c.Texts[0] } : c.Texts));
            Check(p.Centre(out double acx, out double acy) && acy > 40, $"art {p.Name}: no middle as made (NAME.fit)");
            if (pixel)
            {
                var litInput = new List<IList<string>>();
                if (p.Kind == SignLink.Presets.Preset.Kinds.Anim) foreach (string f in lit) litInput.Add(new[] { f });
                else litInput.Add(lit);
                Check(TextReadback.ReadFrames(litInput, out int lw, out int lh, out _) && lw == aw && lh == ah, $"art {p.Name}: lit reads back");
            }
        }

        // resizing and moving placed glyphs (TextTransform): the texts go to transform_check.py, which lays both out
        // with the Python engine; here only what C# alone can tell
        var moved = new List<Dictionary<string, object>>();
        foreach ((string name, string[] texts) in samples)
        {
            if (!TextTransform.Supports(texts, out string why))
            {
                Console.WriteLine($"transform: {name}: not resizable ({why})");
                continue;
            }
            foreach ((double k, double dx, double dy) in new[] { (1.5, 40.0, -20.0), (0.5, -10.0, 100.0), (1.0, 0.0, -200.0), (2.0, 0.0, 0.0), (1.0, -30.0, 60.0) })
            {
                double kk = TextTransform.Snap(texts, k);
                if (kk == 0) continue;
                string[] outs;
                try { outs = TextTransform.Apply(texts, kk, dx, dy); }
                catch (ArgumentException e)
                {
                    Console.WriteLine($"transform: {name} x{kk} ({dx}, {dy}): refused: {e.Message}");
                    continue;
                }
                for (int i = 0; i < texts.Length; i++)
                    moved.Add(new Dictionary<string, object> { ["name"] = $"{name} [{i}] x{kk} ({dx}, {dy})", ["text"] = texts[i],
                                                                ["out"] = outs[i], ["k"] = kk, ["dx"] = dx, ["dy"] = dy });
            }
        }
        Check(TextTransform.Apply("<line-height=0><size=3>A\n<voffset=5>B", 1, 0, 0) == "<line-height=0><size=3>A\n<voffset=5>B", "transform: identity");
        Check(TextTransform.Snap(new[] { "<line-height=.0625>x" }, 0.5) == 1, $"transform: the finest pixels only grow whole ({TextTransform.Snap(new[] { "<line-height=.0625>x" }, 0.5)})");
        Check(TextTransform.Snap(new[] { "<line-height=.25>x" }, 1.3) == 1.25, "transform: snapped to an exact line height");
        Check(!TextTransform.Supports(new[] { "<space=3>x" }, out _), "transform: <space> refused");
        Check(!TextTransform.Supports(new[] { "Hello" }, out _), "transform: plain text (the game sizes it) refused");
        Check(!TextTransform.Supports(new[] { "<size=+2>x" }, out _), "transform: a relative size refused");
        Check(TextTransform.Supports(new[] { "<size=3><voffset=-5>x", "" }, out _), "transform: a sized text and an empty one");
        Check(!TextTransform.Supports(new[] { "a\nb" }, out _), "transform: lines without a line height refused");
        // escapes, spaces and hostile texts (0.20.0): TMP reads \n as a line feed; a leading space is no glyph in Check
        // and in Apply alike; a text of tens of KB of '<' is answered in bounded time (the per-position tag match is anchored)
        Check(!TextTransform.Supports(new[] { "<size=3>a\\nb" }, out string whyEscape) && whyEscape.Contains("escape"), "transform: a backslash escape refused");
        Check(!TextTransform.Supports(new[] { "<size=3>a\\u0041" }, out _), "transform: a \\u escape refused");
        Check(TextTransform.Supports(new[] { "<size=3>a b" }, out _), "transform: spaces are fine");
        Check(TextTransform.Apply(" <size=3><voffset=5>x", 1, 0, 10) == " " + TextTransform.Apply("<size=3><voffset=5>x", 1, 0, 10),
              "transform: a leading space is no glyph in Apply either");
        var hostileClock = System.Diagnostics.Stopwatch.StartNew();
        TextTransform.Supports(new[] { "<size=1>" + new string('<', 40000) }, out _);
        Check(hostileClock.ElapsedMilliseconds < 1000, $"transform: 40 KB of '<' answered in {hostileClock.ElapsedMilliseconds} ms");
        string movedPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "transformed.json");
        File.WriteAllText(movedPath, JsonSerializer.Serialize(moved));
        Console.WriteLine($"transform: {moved.Count} texts resized and moved -> {movedPath} (transform_check.py lays them out)");

        // a stack is a chain of neighbours along the sign's normal (StackMath.Chain): copies 0.3-0.5 mm apart hold
        // together, a hand-placed sign (at least 4.45 cm off) never joins, and from any copy the chain reaches the first sign
        {
            int[] Chain(float[] along, int me) => SignLink.Signs.StackMath.Chain(along, me, 0.002f);
            string Show(int[] a) => string.Join(",", a);
            Check(Show(Chain(new[] { 0f, 0.0005f, 0.001f, 0.05f, 0.0505f }, 1)) == "0,1,2", "chain: a copy reaches its first sign and the copies behind it");
            Check(Show(Chain(new[] { 0f, 0.0005f, 0.001f, 0.05f, 0.0505f }, 3)) == "3,4", "chain: a sign set against the stack is its own chain");
            Check(Show(Chain(new[] { 0f, 0.01f }, 0)) == "0", "chain: a lone sign");
            var tight = new float[25];
            for (int i = 0; i < 25; i++) tight[i] = i * 0.0003f;
            Check(Show(Chain(tight, 24)) == string.Join(",", Enumerable.Range(0, 25)), "chain: 25 copies 0.3 mm apart, from the front copy");
            Check(Show(Chain(new[] { 0f, 0.0005f, 0.0015f, 0.002f }, 0)) == "0,1,2,3", "chain: one copy removed (a 1 mm gap) is still one stack");
            Check(Show(Chain(new[] { 0f, 0.002f }, 0)) == "0,1" && Show(Chain(new[] { 0f, 0.0021f }, 0)) == "0", "chain: a gap of exactly 2 mm joins, 2.1 mm does not");
            Console.WriteLine("chain checked");
        }

        // saved-animation blobs (AnimBlob): a packed animation unpacks to the same frames and rate; a hostile blob (too many
        // frames, a length past the end, a bad rate, one that inflates past 16 MB) is refused without an exception
        {
            string[] frames = { "<size=3>a", "<size=3>b", "" };
            byte[] blob = SignLink.Animation.AnimBlob.Pack(frames, 4f);
            Check(SignLink.Animation.AnimBlob.TryUnpack(blob, out string[] back, out float fps) && fps == 4f && back.SequenceEqual(frames), "blob: round trip");
            Check(!SignLink.Animation.AnimBlob.TryUnpack(null, out _, out _) && !SignLink.Animation.AnimBlob.TryUnpack(new byte[3], out _, out _), "blob: nothing");
            byte[] Patched(byte[] b, int at, int value)
            {
                var c = (byte[])b.Clone();
                BitConverter.GetBytes(value).CopyTo(c, at);
                return c;
            }
            Check(!SignLink.Animation.AnimBlob.TryUnpack(Patched(blob, 8, 6000), out _, out _), "blob: too many frames refused");
            Check(!SignLink.Animation.AnimBlob.TryUnpack(Patched(blob, 12, 1 << 20), out _, out _), "blob: a length past the end refused");
            var badFps = (byte[])blob.Clone();
            BitConverter.GetBytes(61f).CopyTo(badFps, 4);
            Check(!SignLink.Animation.AnimBlob.TryUnpack(badFps, out _, out _), "blob: a rate over 60 refused");
            byte[] bomb = SignLink.Animation.AnimBlob.Pack(new[] { new string('a', 20 << 20) }, 1f);
            Check(bomb.Length < 100000 && !SignLink.Animation.AnimBlob.TryUnpack(bomb, out _, out _), "blob: one that inflates past 16 MB is refused, no exception");
            Console.WriteLine("blobs checked");
        }

        // jobs: the latest key runs once it settles, a stale result is dropped, a failure does not block the next key
        {
            var job = new SignLink.UI.Job<string>();
            var got = new List<string>();
            var fails = new List<string>();
            void Drain()
            {
                for (int i = 0; i < 400 && job.Busy; i++) { System.Threading.Thread.Sleep(5); SignLink.Api.MainThread.Drain(); }
                SignLink.Api.MainThread.Drain();
            }
            Func<Func<string>> A = () => () => "A";
            job.Want("a", 0f, 0.3f, A, got.Add, e => fails.Add(e.Message));
            Check(!job.Busy && got.Count == 0, "jobs: nothing runs before the key settles");
            job.Want("a", 0.5f, 0.3f, A, got.Add, e => fails.Add(e.Message));
            Drain();
            Check(got.Count == 1 && got[0] == "A" && job.Done == "a", "jobs: delivered once settled");
            job.Want("a", 1f, 0.3f, A, got.Add, e => fails.Add(e.Message));
            Drain();
            Check(got.Count == 1, "jobs: the same key does not run again");
            var gate = new System.Threading.ManualResetEventSlim(false);
            job.Want("b", 2f, 0f, () => () => { gate.Wait(); return "B"; }, got.Add, e => fails.Add(e.Message));
            Check(job.Busy, "jobs: b runs");
            job.Want("c", 2.1f, 0f, () => () => "C", got.Add, e => fails.Add(e.Message));
            gate.Set();
            Drain();
            Check(got.Count == 1, "jobs: b's result is dropped, the key moved on");
            job.Want("c", 2.5f, 0f, () => () => "C", got.Add, e => fails.Add(e.Message));
            Drain();
            Check(got.Count == 2 && got[1] == "C" && job.Done == "c", "jobs: c delivered");
            job.Want("d", 3f, 0f, () => () => throw new InvalidOperationException("boom"), got.Add, e => fails.Add(e.Message));
            Drain();
            Check(fails.Count == 1 && fails[0] == "boom" && job.Done == "d" && !job.Busy, "jobs: a failure is reported once");
            job.Want("e", 4f, 0f, () => throw new InvalidOperationException("no start"), got.Add, e => fails.Add(e.Message));
            Check(fails.Count == 2 && !job.Busy && job.Done == "e", "jobs: a failing begin is reported at once");
            job.Forget();
            job.Want("e", 5f, 0f, () => () => "E", got.Add, e => fails.Add(e.Message));
            Drain();
            Check(got.Count == 3 && got[2] == "E", "jobs: Forget lets the same key run again");
            // clear while busy: Abandon drops the running result and the next key runs at once, even the same key
            var gate2 = new System.Threading.ManualResetEventSlim(false);
            job.Want("f", 6f, 0f, () => () => { gate2.Wait(); return "F-old"; }, got.Add, e => fails.Add(e.Message));
            Check(job.Busy, "jobs: f runs");
            job.Abandon();
            Check(!job.Busy, "jobs: abandoned at once");
            job.Want("f", 6.1f, 0f, () => () => "F-new", got.Add, e => fails.Add(e.Message));
            Check(job.Busy, "jobs: f runs again without waiting");
            gate2.Set();
            Drain();
            Check(got.Count == 4 && got[3] == "F-new" && job.Done == "f", $"jobs: the abandoned result is dropped, the new one delivered ({string.Join(",", got)})");
            Console.WriteLine("jobs checked");
        }

        // the footer verb (Change.Decide): what the sign holds against what the tabs chose
        {
            var noSign = new SignLink.UI.Held();
            SignLink.Signs.SignRecipe pic = SignLink.Signs.SignRecipe.ForPicture(new PictureOptions { WidthM = 2f, Pitch = 0.125f, Glow = true }, "a.png");
            var c = new SignLink.UI.Choice { Recipe = pic.Copy(), Light = GlowMode.Glowing };
            Check(SignLink.UI.Change.Decide(noSign, c) == SignLink.UI.Verb.None, "verb: no sign");
            var empty = new SignLink.UI.Held { HasSign = true };
            Check(SignLink.UI.Change.Decide(empty, c) == SignLink.UI.Verb.Place, "verb: a picture on an empty sign is placed");
            Check(SignLink.UI.Change.Decide(empty, new SignLink.UI.Choice()) == SignLink.UI.Verb.None, "verb: nothing chosen, nothing held");
            var holds = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Recipe = pic.Copy(), Bytes = 1000 };
            Check(SignLink.UI.Change.Decide(holds, c) == SignLink.UI.Verb.None, "verb: the same picture as it is");
            c.Fit = new Fit { Scale = 1, Right = 40 };
            Check(SignLink.UI.Change.Decide(holds, c) == SignLink.UI.Verb.Refit && SignLink.UI.Change.Live(holds, c, SignLink.UI.Verb.Refit), "verb: moved 1 m: refit, live");
            c.Fit = new Fit { Scale = 1, Tilt = 90 };
            Check(SignLink.UI.Change.Decide(holds, c) == SignLink.UI.Verb.Refit && !SignLink.UI.Change.Live(holds, c, SignLink.UI.Verb.Refit), "verb: laid face up: refit, but never live (the stack is made again)");
            c.Fit = new Fit { Scale = 1, Turn = 10 };
            Check(!SignLink.UI.Change.Live(holds, c, SignLink.UI.Verb.Refit), "verb: turned: not live");
            c.Fit = Fit.AsMade;
            c.Recipe.WidthM = 3f;
            Check(SignLink.UI.Change.Decide(holds, c) == SignLink.UI.Verb.Update && !SignLink.UI.Change.Live(holds, c, SignLink.UI.Verb.Update), "verb: another width: update, not live");
            c.Recipe.WidthM = 2f;
            c.Light = GlowMode.Lit;
            c.Recipe.Glow = false;
            Check(SignLink.UI.Change.Decide(holds, c) == SignLink.UI.Verb.Update, "verb: a picture relit: update (its glow is in its settings)");
            var art = new SignLink.UI.Choice { Recipe = SignLink.Signs.SignRecipe.ForPiece(SignLink.Signs.SignRecipe.Kinds.Art, GlowMode.Lit, "p"), Light = GlowMode.Lit };
            Check(SignLink.UI.Change.Decide(holds, art) == SignLink.UI.Verb.Put, "verb: a piece over a picture is put");
            var holdsArt = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Recipe = art.Recipe.Copy(), Bytes = 1000 };
            Check(SignLink.UI.Change.Decide(holdsArt, art) == SignLink.UI.Verb.None, "verb: the same piece as it is");
            art.Light = GlowMode.Glowing;
            Check(SignLink.UI.Change.Decide(holdsArt, art) == SignLink.UI.Verb.Refit && SignLink.UI.Change.Live(holdsArt, art, SignLink.UI.Verb.Refit), "verb: a piece relit: refit, live on a small stack");
            var unknown = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Bytes = 500 };
            Check(SignLink.UI.Change.Decide(unknown, new SignLink.UI.Choice()) == SignLink.UI.Verb.None, "verb: unknown content as it is");
            Check(SignLink.UI.Change.Decide(unknown, new SignLink.UI.Choice { Fit = new Fit { Scale = 1.5 } }) == SignLink.UI.Verb.Apply, "verb: unknown content resized: apply");
            Check(SignLink.UI.Change.Live(unknown, new SignLink.UI.Choice { Fit = new Fit { Scale = 1.5 } }, SignLink.UI.Verb.Apply), "verb: ... live on a small stack");
            Check(SignLink.UI.Change.Decide(unknown, new SignLink.UI.Choice { Light = GlowMode.Glowing }) == SignLink.UI.Verb.Apply, "verb: unknown content relit: apply");
            var big = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Bytes = 3 << 20 };
            Check(!SignLink.UI.Change.Live(big, new SignLink.UI.Choice { Fit = new Fit { Scale = 1.5 } }, SignLink.UI.Verb.Apply), "verb: a big stack waits for the button");
            Check(SignLink.UI.Change.SamePose(new Fit { Turn = 180 }, new Fit { Turn = -180 }) && !SignLink.UI.Change.SamePose(new Fit(), new Fit { Tilt = 1 }), "verb: same pose");
            // the tilt's pivot (over the bottom edge, or about the middle) is part of the pose only while the sign is tilted
            Check(!SignLink.UI.Change.SamePose(new Fit { Tilt = 30, Edge = true }, new Fit { Tilt = 30 }) && SignLink.UI.Change.SamePose(new Fit { Edge = true }, new Fit()),
                  "verb: same pose: the pivot counts only when tilted");
            Check(!new Fit { Scale = 1, Tilt = 90, Edge = true }.Same(new Fit { Scale = 1, Tilt = 90 }) && new Fit { Scale = 1, Edge = true }.Same(new Fit { Scale = 1 }),
                  "fit: the pivot counts only when tilted");
            Check(new Fit { Scale = 1, Tilt = 90, Edge = true }.Describe().Contains("over its edge") && !new Fit { Scale = 1, Tilt = 90 }.Describe().Contains("edge"),
                  "fit: Describe names the edge pivot");
            var text = new SignLink.UI.Held { HasSign = true, Empty = false, Recipe = SignLink.Signs.SignRecipe.ForText(), Text = "hello", Made = "hello", Bytes = 5 };
            Check(SignLink.UI.Change.Decide(text, new SignLink.UI.Choice { Recipe = SignLink.Signs.SignRecipe.ForText(), Text = "hello" }) == SignLink.UI.Verb.None &&
                  SignLink.UI.Change.Decide(text, new SignLink.UI.Choice { Recipe = SignLink.Signs.SignRecipe.ForText(), Text = "bye" }) == SignLink.UI.Verb.Update &&
                  SignLink.UI.Change.Decide(empty, new SignLink.UI.Choice { Recipe = SignLink.Signs.SignRecipe.ForText(), Text = "x" }) == SignLink.UI.Verb.Place, "verb: text");
            Check(SignLink.UI.Change.LightOf(holds) == GlowMode.Glowing && SignLink.UI.Change.LightOf(unknown) == GlowMode.AsMade, "verb: the held light");
            // a moving GIF refits like a still: rewriting or remaking the stack parks its screen's frames, and the screen
            // comes back from memory on the new stack (ScreenRestore), so a fit or pose change moves the texts (Refit)
            // instead of decoding and encoding the picture again (0.20.7-0.20.13 made it again)
            SignLink.Signs.SignRecipe gif = SignLink.Signs.SignRecipe.ForPicture(new PictureOptions { WidthM = 2f, Pitch = 0.125f, Animate = true }, "Clip.GIF");
            var holdsGif = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Recipe = gif.Copy(), Bytes = 1000 };
            var laid = new SignLink.UI.Choice { Recipe = gif.Copy(), Light = GlowMode.Glowing, Fit = new Fit { Scale = 1, Tilt = 90 } };
            Check(SignLink.UI.Change.Decide(holdsGif, laid) == SignLink.UI.Verb.Refit, "verb: a moving GIF laid face up refits (its screen comes back from memory)");
            Check(SignLink.UI.Change.Decide(holdsGif, new SignLink.UI.Choice { Recipe = gif.Copy(), Light = GlowMode.Glowing }) == SignLink.UI.Verb.None, "verb: the same moving GIF as it is");
            laid.Recipe.Animate = false;
            holdsGif.Recipe.Animate = false;
            Check(SignLink.UI.Change.Decide(holdsGif, laid) == SignLink.UI.Verb.Refit, "verb: a GIF placed as a still refits");
            // an empty sign: its pose can still change (stood up again after its content went), never live (made again);
            // size and place mean nothing on it
            var emptyTilted = new SignLink.UI.Held { HasSign = true, Empty = true, Fit = new Fit { Scale = 1, Tilt = 30 } };
            Check(SignLink.UI.Change.Decide(emptyTilted, new SignLink.UI.Choice { Fit = new Fit { Scale = 1 } }) == SignLink.UI.Verb.Apply &&
                  !SignLink.UI.Change.Live(emptyTilted, new SignLink.UI.Choice { Fit = new Fit { Scale = 1 } }, SignLink.UI.Verb.Apply),
                  "verb: an empty sign stood up again: apply, on the button");
            Check(SignLink.UI.Change.Decide(emptyTilted, new SignLink.UI.Choice { Fit = new Fit { Scale = 1, Tilt = 30 } }) == SignLink.UI.Verb.None, "verb: an empty sign in its pose: nothing");
            Check(SignLink.UI.Change.Decide(emptyTilted, new SignLink.UI.Choice { Fit = new Fit { Scale = 2, Right = 40, Tilt = 30 } }) == SignLink.UI.Verb.None, "verb: an empty sign has no size or place to change");
            Console.WriteLine("verbs checked");
        }

        // quick views of hostile texts (TextReadback caps): a two-glyph text that reads as a 20001-pixel-wide picture is
        // refused (no texture that size), and millions of lines are refused before anything is allocated per line
        {
            Check(!TextReadback.Read(new[] { "<line-height=1><size=1>■\n<indent=40000>■" }, out _, out _, out _), "preview: a 20001-px-wide picture refused");
            Check(TextReadback.Read(new[] { "<line-height=1><size=1>■\n<indent=4>■" }, out int okW, out int okH, out _) && okW == 3 && okH == 2, $"preview: a small one still reads ({okW} x {okH})");
            var lines = new System.Text.StringBuilder("<line-height=1><size=1>");
            for (int i = 0; i < 3000000; i++) lines.Append("■\n");
            string huge = lines.ToString();
            var previewClock = System.Diagnostics.Stopwatch.StartNew();
            bool read = TextReadback.Read(new[] { huge }, out _, out _, out _);
            Check(!read && previewClock.ElapsedMilliseconds < 1000, $"preview: 3 M lines refused in {previewClock.ElapsedMilliseconds} ms");
            Console.WriteLine("preview caps checked");
        }

        // raw texture bytes (Unity's rows bottom-up, by format) to RGBA rows top-down, halved on the way with Grid's own
        // 2 x 2 box (RawPixels): the Picture source's decode in one pass, without a second full-size copy of a photo
        {
            byte[] rgb = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };                     // 2 x 2 RGB24, the bottom row stored first
            byte[] top = SignLink.Pictures.RawPixels.ToRgba(rgb, SignLink.Pictures.RawPixels.Format.Rgb24, 2, 2, 0, out int tw, out int th);
            Check(tw == 2 && th == 2 && top.Length == 16 && top[0] == 7 && top[1] == 8 && top[2] == 9 && top[3] == 255 && top[8] == 1 && top[11] == 255,
                  "raw: RGB24 rows flipped and expanded with alpha 255");
            byte[] fromArgb = SignLink.Pictures.RawPixels.ToRgba(new byte[] { 9, 1, 2, 3 }, SignLink.Pictures.RawPixels.Format.Argb32, 1, 1, 0, out _, out _);
            Check(fromArgb[0] == 1 && fromArgb[1] == 2 && fromArgb[2] == 3 && fromArgb[3] == 9, "raw: ARGB32 reordered");
            byte[] fromBgra = SignLink.Pictures.RawPixels.ToRgba(new byte[] { 3, 2, 1, 9 }, SignLink.Pictures.RawPixels.Format.Bgra32, 1, 1, 0, out _, out _);
            Check(fromBgra[0] == 1 && fromBgra[1] == 2 && fromBgra[2] == 3 && fromBgra[3] == 9, "raw: BGRA32 reordered");
            Check(SignLink.Pictures.RawPixels.Halvings(10000, 5000, 4000000) == 1 && SignLink.Pictures.RawPixels.Halvings(4000, 3000, 4000000) == 0 &&
                  SignLink.Pictures.RawPixels.Halvings(16000, 12000, 4000000) == 2, "raw: halvings while the halved one still has 4 M pixels");
            var rawRnd = new Random(7);
            byte[] big = new byte[8 * 6 * 4];
            rawRnd.NextBytes(big);
            byte[] plain = SignLink.Pictures.RawPixels.ToRgba(big, SignLink.Pictures.RawPixels.Format.Rgba32, 8, 6, 0, out int pw, out int ph);
            byte[] halvedTwice = SignLink.Pictures.RawPixels.Halve(SignLink.Pictures.RawPixels.Halve(plain, ref pw, ref ph), ref pw, ref ph);
            byte[] fused = SignLink.Pictures.RawPixels.ToRgba(big, SignLink.Pictures.RawPixels.Format.Rgba32, 8, 6, 2, out int fw, out int fh);
            Check(fw == 2 && fh == 1 && pw == 2 && ph == 1 && fused.SequenceEqual(halvedTwice), "raw: two halvings fused equal Halve twice");
            byte[] oddRaw = new byte[5 * 3 * 3];
            rawRnd.NextBytes(oddRaw);
            byte[] oddPlain = SignLink.Pictures.RawPixels.ToRgba(oddRaw, SignLink.Pictures.RawPixels.Format.Rgb24, 5, 3, 0, out int ow, out int oh);
            byte[] oddHalved = SignLink.Pictures.RawPixels.Halve(oddPlain, ref ow, ref oh);
            byte[] oddFused = SignLink.Pictures.RawPixels.ToRgba(oddRaw, SignLink.Pictures.RawPixels.Format.Rgb24, 5, 3, 1, out int ofw, out int ofh);
            Check(ofw == 2 && ofh == 1 && ow == 2 && oh == 1 && oddFused.SequenceEqual(oddHalved), "raw: odd edges dropped the same way as Halve");
            bool shortRefused = false;
            try { SignLink.Pictures.RawPixels.ToRgba(new byte[10], SignLink.Pictures.RawPixels.Format.Rgba32, 2, 2, 0, out _, out _); }
            catch (ArgumentException) { shortRefused = true; }
            Check(shortRefused, "raw: short buffers refused");
            Console.WriteLine("raw pixels checked");
        }

        // pieces are files: caps that keep a hostile piece from creating thousands of signs or exhausting memory
        // (Preset.Load, ParseFit, Size)
        {
            Check(!SignLink.Presets.Preset.ParseFit("centre=1e400,0", out _, out _) && !SignLink.Presets.Preset.ParseFit("centre=40000,0", out _, out _) &&
                  !SignLink.Presets.Preset.ParseFit("centre=NaN,1", out _, out _) &&
                  SignLink.Presets.Preset.ParseFit("centre=-32767,12.5", out double fx, out double fy) && fx == -32767 && fy == 12.5,
                  "fit: finite centres within 32767 wu only");
            var sizeHuge = new SignLink.Presets.Preset { Name = "p", ReadFit = () => "size=1e9,1" };
            var sizeOk = new SignLink.Presets.Preset { Name = "p", ReadFit = () => "size=80,40" };
            Check(!sizeHuge.Size(out _, out _) && sizeOk.Size(out double sw, out double sh) && sw == 80 && sh == 40, "fit: sizes within 32767 wu only");
            var tooBig = new SignLink.Presets.Preset { Name = "big", Kind = SignLink.Presets.Preset.Kinds.Text, Bytes = 40L << 20,
                                                       ReadText = () => throw new InvalidOperationException("read") };
            string refused = null;
            try { tooBig.Load(); }
            catch (ArgumentException e) { refused = e.Message; }
            Check(refused != null && refused.Contains("too large"), "preset: a 40 MB file is refused before it is read");
            var manySigns = new SignLink.Presets.Preset { Name = "stack", Kind = SignLink.Presets.Preset.Kinds.Stack, Bytes = 100,
                                                          ReadText = () => string.Join("\u001d", new string[65]) };
            refused = null;
            try { manySigns.Load(); }
            catch (ArgumentException e) { refused = e.Message; }
            Check(refused != null && refused.Contains("too large"), "preset: a stack of 65 signs is refused");
            var okStack = new SignLink.Presets.Preset { Name = "stack", Kind = SignLink.Presets.Preset.Kinds.Stack, Bytes = 100,
                                                        ReadText = () => string.Join("\u001d", new string[64]) };
            Check(okStack.Load().Texts.Length == 64, "preset: 64 signs load");
            var manyFrames = new SignLink.Presets.Preset { Name = "anim", Kind = SignLink.Presets.Preset.Kinds.Anim, Bytes = 100,
                                                           ReadText = () => "#signlink anim fps=4\n" + string.Join("\u001e", new string[5001]) };
            refused = null;
            try { manyFrames.Load(); }
            catch (ArgumentException e) { refused = e.Message; }
            Check(refused != null && refused.Contains("too large"), "preset: an animation of 5001 frames is refused");
            Console.WriteLine("preset caps checked");
        }

        // thumbnails: a box-filtered small copy (Resampler.Box), a JPEG's own EXIF preview (ImageHeader.Thumbnail), and
        // the PNG header's chunk name checked (ImageHeader.Size)
        {
            byte[] grad = new byte[10 * 6 * 4];
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 10; x++)
                    for (int c = 0; c < 4; c++) grad[(y * 10 + x) * 4 + c] = (byte)(x * 20 + c);
            byte[] small = Resampler.Box(grad, 10, 6, 5, out int bw, out int bh);
            Check(bw == 5 && bh == 3 && small.Length == 60 && small[0] == 10 && small[1] == 11 && small[(2 * 5 + 4) * 4] == 170,
                  "box: 10 x 6 to 5 x 3, each pixel its 2 x 2 average");
            byte[] same = Resampler.Box(grad, 10, 6, 20, out int sw2, out int sh2);
            Check(sw2 == 10 && sh2 == 6 && same.SequenceEqual(grad), "box: a small picture is copied as it is");

            // a JPEG with an EXIF APP1: IFD0 (orientation 1) linking to IFD1 with a 6-byte JPEG thumbnail, then a SOF0
            byte[] Jpeg(bool withThumb, bool truncate)
            {
                var tiff = new List<byte> { (byte)'I', (byte)'I', 0x2A, 0, 8, 0, 0, 0 };    // little-endian, IFD0 at 8
                tiff.AddRange(new byte[] { 1, 0, 0x12, 0x01, 3, 0, 1, 0, 0, 0, 1, 0, 0, 0 });   // one entry: orientation = 1
                tiff.AddRange(new byte[] { 26, 0, 0, 0 });                                   // next IFD at 26
                tiff.AddRange(new byte[] { 2, 0, 0x01, 0x02, 4, 0, 1, 0, 0, 0, 56, 0, 0, 0,  // IFD1: JPEGInterchangeFormat = 56
                                           0x02, 0x02, 4, 0, 1, 0, 0, 0, 6, 0, 0, 0,         // JPEGInterchangeFormatLength = 6
                                           0, 0, 0, 0 });
                if (!truncate) tiff.AddRange(new byte[] { 0xFF, 0xD8, 0, 0, 0xFF, 0xD9 });   // the thumbnail at 56
                var payload = new List<byte> { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 };
                payload.AddRange(tiff);
                var file = new List<byte> { 0xFF, 0xD8 };
                if (withThumb)
                {
                    int len = payload.Count + 2 + (truncate ? 6 : 0);                        // the length claims the full segment
                    file.AddRange(new byte[] { 0xFF, 0xE1, (byte)(len >> 8), (byte)len });
                    file.AddRange(payload);
                }
                if (!truncate) file.AddRange(new byte[] { 0xFF, 0xC0, 0, 11, 8, 0, 30, 0, 40, 1, 1, 0x11, 0 });   // SOF0: 40 x 30
                file.AddRange(new byte[] { 0xFF, 0xD9 });
                return file.ToArray();
            }
            byte[] withPreview = Jpeg(true, false);
            Check(ImageHeader.Thumbnail(withPreview, out byte[] preview) && preview.Length == 6 && preview[0] == 0xFF && preview[1] == 0xD8 && preview[5] == 0xD9,
                  "thumbnail: the EXIF preview's bytes");
            Check(!ImageHeader.Thumbnail(Jpeg(false, false), out _), "thumbnail: none without APP1");
            Check(!ImageHeader.Thumbnail(Jpeg(true, true), out _), "thumbnail: none from a truncated file");
            Check(ImageHeader.Size(withPreview, out int jw, out int jh, out int jo) && jw == 40 && jh == 30 && jo == 1, $"header: the JPEG's size and orientation still read ({jw} x {jh}, {jo})");
            byte[] png = { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 7, 0, 0, 0, 5 };
            Check(ImageHeader.Size(png, out int pw2, out int ph2, out _) && pw2 == 7 && ph2 == 5, "header: a PNG's IHDR");
            png[15] = (byte)'X';
            Check(!ImageHeader.Size(png, out _, out _, out _), "header: a PNG whose first chunk is not IHDR is refused");
            Console.WriteLine("thumbnails checked");
        }

        // the pure protections: the API password rules, picture plans, hostile GIFs, the main-thread timeout, headers
        // that cannot be read, a golden grid (a refactor of Grid or Halve must keep it), and names on disk
        {
            Check(SignLink.Api.Password.Problem("") != null && SignLink.Api.Password.Problem("short") != null &&
                  SignLink.Api.Password.Problem("has space 123456") != null && SignLink.Api.Password.Problem("ékezet12345678") != null &&
                  SignLink.Api.Password.Problem("abcdefghijkl") == null, "password: the rules");
            string p1 = SignLink.Api.Password.New(), p2 = SignLink.Api.Password.New();
            Check(SignLink.Api.Password.Problem(p1) == null && p1 != p2, "password: a new one passes and differs");

            var opt = new PictureOptions { WidthM = 2f, Pitch = 0.125f, Place = Placement.Custom };
            PicturePlan plan1 = opt.PlanFor(4000, 3000);
            Check(plan1.W == 640 && !plan1.Coarsened && !plan1.Capped, $"plan: 2 m at pitch 0.125 on 4000 px is 640 wide ({plan1.W})");
            PicturePlan plan2 = opt.PlanFor(100, 75);
            Check(plan2.Coarsened && plan2.W <= 100 && plan2.Pitch > 0.125f, $"plan: a 100-px source is coarsened ({plan2.W} at {plan2.Pitch})");
            var wideOpt = new PictureOptions { WidthM = 60f, Pitch = 0.125f, Place = Placement.Custom };
            PicturePlan plan3 = wideOpt.PlanFor(2000, 2000);
            Check(plan3.Capped && plan3.Pixels <= PictureOptions.MaxPixels, $"plan: 60 m at 0.125 is capped ({plan3.W} x {plan3.H})");

            // the Detail model (0.20.35, the author: only the density should affect the amount of data, not the
            // sizing): Detail (pixels across) sets the grid, Width only the pixel size
            var d2 = new PictureOptions { WidthM = 2f, Detail = 120, Place = Placement.Custom };
            var d20 = new PictureOptions { WidthM = 20f, Detail = 120, Place = Placement.Custom };
            PicturePlan small = d2.PlanFor(1000, 750), big = d20.PlanFor(1000, 750);
            Check(small.W == 120 && small.H == 90 && big.W == 120 && big.H == 90,
                  $"detail: 2 m and 20 m wide have the same pixels ({small.W}x{small.H}, {big.W}x{big.H})");
            Check(Math.Abs(big.WidthM - 20f) < 0.2f && Math.Abs(small.WidthM - 2f) < 0.2f, $"detail: the widths are kept ({small.WidthM}, {big.WidthM} m)");
            Check(PictureOptions.PitchFor(3f, 120) == 1f && PictureOptions.PitchFor(3.1f, 120) == 1f && PictureOptions.PitchFor(3.2f, 120) == 1.125f,
                  "detail: the pixel snaps to 1/8 wu (3.125 mm), the line height TMP adds up exactly");
            Check(PictureOptions.PitchFor(0.01f, 500) == PictureOptions.PitchStep && PictureOptions.PitchFor(500f, 8) == PictureOptions.MaxPitch,
                  "detail: the pixel stays within its range");
            bool allExact = true;
            for (float wm = 0.25f; wm <= 30f; wm += 0.05f)
            {
                double pp = PictureOptions.PitchFor(wm, 97);
                if (PixelText.ExactLineHeight(pp / 2) != pp / 2) { allExact = false; Console.WriteLine($"  not exact: {wm} m -> {pp}"); break; }
            }
            Check(allExact, "detail: every snapped pixel's half is a line height TMP reads exactly (1.375 is not: its half 0.6875 drifts)");
            Check(PictureOptions.PitchFor(4.125f, 120) != 1.375f, "detail: 1.375 wu is skipped for a near exact one");
            {
                // a pixel the old list never had (0.625 wu): encoded and read back to the same tgrid and pixel
                int tw = 7, th = 5;
                var tgrid = new int[tw * th];
                for (int i = 0; i < tgrid.Length; i++) tgrid[i] = PixelText.Pack(i * 2654435, 255);   // opaque
                List<PixelText.Band> gb = PixelText.Bands(tgrid, tw, th, 0.625, 0, 10, PixelText.GlyphSize(0.625, 2), 400);
                var gin = new List<IList<string>> { gb.ConvertAll(b => b.Text) };
                bool back = TextReadback.ReadFrames(gin, out int rw, out int rh, out List<int[]> rg, out double rp, false);
                Check(back && rw == tw && rh == th && Math.Abs(rp - 0.625) < 1e-6 && rg[0].SequenceEqual(tgrid),
                      $"detail: a 0.625 wu picture reads back ({back}, {rw}x{rh}, pitch {rp})");
            }
            var dMore = new PictureOptions { WidthM = 3f, Detail = 500, Place = Placement.Custom };
            PicturePlan few = dMore.PlanFor(200, 100);
            Check(few.W == 200 && few.Coarsened, $"detail: never more pixels than the picture has ({few.W}, coarsened {few.Coarsened})");

            // the Max button (0.20.41, the author: a Max button that fits the density to the 300 KB limit)
            {
                const long KB = 1024;
                Check(!PictureOptions.MaxStep(100, 100 * KB, 300 * KB, 1000, out int m1) && m1 == 171, $"max: 100 KB at 100 px grows to 171 ({m1})");
                Check(PictureOptions.MaxStep(171, 293 * KB, 300 * KB, 1000, out int m2) && m2 == 171, $"max: 293 KB at 171 px is the most ({m2})");
                Check(!PictureOptions.MaxStep(200, 320 * KB, 300 * KB, 1000, out int m3) && m3 < 200 && m3 > 180, $"max: over, it shrinks ({m3})");
                Check(!PictureOptions.MaxStep(199, 301 * KB, 300 * KB, 1000, out int m4) && m4 < 199, $"max: over by a rounding, a step down ({m4})");
                Check(!PictureOptions.MaxStep(50, 10 * KB, 300 * KB, 60, out int m5) && m5 == 60 &&
                      PictureOptions.MaxStep(60, 15 * KB, 300 * KB, 60, out int m6) && m6 == 60, "max: no more pixels than the picture has");
            }

            // code review, 0.20.40
            {
                byte[] th = Resampler.Box(new byte[1920 * 16 * 4], 1920, 16, 96, out int bw, out int bh);   // a 120:1 banner
                Check(bw == 96 && bh == 1 && th.Length == 96 * 4, $"review: a very wide picture's thumbnail ({bw} x {bh})");
                var full = new PictureOptions { WidthM = 10f, Detail = 4005 };
                PicturePlan fp = full.PlanFor(4005, 1000);
                Check(fp.Pixels <= PictureOptions.MaxPixels && fp.W == PictureOptions.MaxDetail(4005, 1000),
                      $"review: Full resolution stays within the pixel cap ({fp.W} x {fp.H})");
                Check(PictureOptions.AutoDetail(400, 5000, false) <= PictureOptions.MaxDetail(400, 5000), "review: the default resolution fits the cap");
                var api = new PictureOptions { WidthM = 50f, Pitch = 4f };      // width and pitch (the API), a 300 px source
                PicturePlan ap = api.PlanFor(300, 300);
                Check(ap.W == 300 && Math.Abs(ap.WidthM - 50f) < 1f, $"review: coarsening keeps the width past 2 wu ({ap.W} at {ap.Pitch}, {ap.WidthM} m)");
                var asked = new PictureOptions { WidthM = 2f, Detail = 200, Pitch = 0.25f };
                PicturePlan pp = asked.PlanFor(1000, 750);
                SignLink.Signs.SignRecipe rr = SignLink.Signs.SignRecipe.ForPicture(asked, pp, "a.png");
                var back = new PictureOptions();
                SignLink.Signs.SignRecipe.Parse(rr.ToString()).Apply(back);
                PicturePlan bp = back.PlanFor(1000, 750);
                Check(bp.W == pp.W && bp.H == pp.H && bp.Pitch == pp.Pitch, $"review: the recipe reads back as the very grid ({bp.W} at {bp.Pitch}, made {pp.W} at {pp.Pitch})");
                Check(SignLink.Signs.SignRecipe.Parse("pic|0.1875|0.125|2|1|1|0|a.png").Kind == SignLink.Signs.SignRecipe.Kinds.Picture, "review: a picture under 0.25 m wide has a recipe");
            }
            Check(PictureOptions.ValidPitch(1.25f) && PictureOptions.ValidPitch(0.5f) && !PictureOptions.ValidPitch(1.375f) &&
                  !PictureOptions.ValidPitch(0.3f) && !PictureOptions.ValidPitch(0f), "detail: a recipe's pixel is one the Detail model makes");
            Check(PictureOptions.AutoDetail(1000, 750, true) <= 172 && PictureOptions.AutoDetail(1000, 750, true) >= 150 &&
                  PictureOptions.AutoDetail(1000, 750, true) % 4 == 0, $"auto detail, multiplayer: about 22 000 pixels ({PictureOptions.AutoDetail(1000, 750, true)})");
            Check(PictureOptions.AutoDetail(100, 100, true) == 100, "auto detail: never more than the picture has");
            Check(PictureOptions.AutoDetail(1000, 750, false) == 320 && PictureOptions.AutoDetail(300, 200, false) == 300,
                  "auto detail, single player: 320 across as before (2 m at pixel size 2)");

            string wholeGif = Path.Combine(Path.GetDirectoryName(path), "g_restore.gif");
            if (File.Exists(wholeGif))
            {
                byte[] all = File.ReadAllBytes(wholeGif);
                var half = new byte[all.Length / 2];
                Array.Copy(all, half, half.Length);
                // a file cut short gives the frames that are whole, or an argument error: never another exception
                var gifClock = System.Diagnostics.Stopwatch.StartNew();
                string cutError = null;
                int cutFrames = 0;
                try { cutFrames = GifDecoder.Decode(half).Frames.Count; }
                catch (ArgumentException e) { cutError = e.Message; }
                catch (Exception e) { cutError = "WRONG TYPE " + e.GetType().Name; }
                Check((cutFrames > 0 || cutError != null) && (cutError == null || !cutError.StartsWith("WRONG TYPE")) && gifClock.ElapsedMilliseconds < 1000,
                      $"gif: a file cut in half gives whole frames or an argument error ({cutFrames} frames, {cutError})");
                var stub = new byte[40];
                Array.Copy(all, stub, stub.Length);                              // cut inside the colour table
                string stubError = null;
                try { GifDecoder.Decode(stub); }
                catch (ArgumentException e) { stubError = e.Message; }
                catch (Exception e) { stubError = "WRONG TYPE " + e.GetType().Name; }
                Check(stubError != null && !stubError.StartsWith("WRONG TYPE"), $"gif: a 40-byte stub is refused as an argument error ({stubError})");
                var huge = (byte[])all.Clone();
                huge[6] = 0x20; huge[7] = 0x4E; huge[8] = 0x20; huge[9] = 0x4E;            // 20000 x 20000 in the header
                string hugeError = null;
                try { GifDecoder.Decode(huge); }
                catch (ArgumentException e) { hugeError = e.Message; }
                catch (Exception e) { hugeError = "WRONG TYPE " + e.GetType().Name; }
                Check(hugeError != null && !hugeError.StartsWith("WRONG TYPE"), $"gif: a 20000 x 20000 header is refused ({hugeError})");
            }
            else Check(false, "gif: g_restore.gif missing (run ./sign.sh modtest first)");

            bool ran = false;
            string timedOut = null;
            try { SignLink.Api.MainThread.Run(() => { ran = true; return 1; }, 200); }
            catch (TimeoutException e) { timedOut = e.Message; }
            SignLink.Api.MainThread.Drain();
            Check(timedOut != null && !ran, "main thread: a job nobody runs times out and never runs later");

            var shortPng = new byte[20];
            Array.Copy(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, shortPng, 4);
            Check(!ImageHeader.Size(shortPng, out _, out _, out _), "header: a truncated PNG");
            byte[] noSof = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xD9 };
            Check(!ImageHeader.Size(noSof, out _, out _, out _), "header: a JPEG without SOF");
            byte[] JpegWithExif(byte[] tiff)
            {
                var payload = new List<byte> { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 };
                payload.AddRange(tiff);
                var file = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, (byte)((payload.Count + 2) >> 8), (byte)(payload.Count + 2) };
                file.AddRange(payload);
                file.AddRange(new byte[] { 0xFF, 0xC0, 0, 11, 8, 0, 30, 0, 40, 1, 1, 0x11, 0, 0xFF, 0xD9 });
                return file.ToArray();
            }
            byte[] farIfd = JpegWithExif(new byte[] { (byte)'I', (byte)'I', 0x2A, 0, 0xF0, 0xFF, 0, 0, 1, 0, 2, 3, 4, 5, 6, 7, 8, 9 });
            Check(ImageHeader.Size(farIfd, out int fw2, out _, out int fo2) && fw2 == 40 && fo2 == 1, "header: an IFD offset past the segment reads as orientation 1");
            byte[] longOri = JpegWithExif(new byte[] { (byte)'I', (byte)'I', 0x2A, 0, 8, 0, 0, 0, 1, 0, 0x12, 0x01, 4, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0 });
            Check(ImageHeader.Size(longOri, out _, out _, out int lo) && (lo == 1 || lo == 6), $"header: an orientation stored as LONG does not throw ({lo})");

            var big2 = new byte[2600 * 2000 * 4];
            for (int y = 0; y < 2000; y++)
                for (int x = 0; x < 2600; x++)
                {
                    int o = (y * 2600 + x) * 4;
                    big2[o] = (byte)(x * 255 / 2599);
                    big2[o + 1] = (byte)(y * 255 / 1999);
                    big2[o + 2] = (byte)((x + y) * 255 / 4599);
                    big2[o + 3] = 255;
                }
            int gw = 2600, gh = 2000;
            byte[] src2 = big2;
            while ((long)gw * gh > 4000000 && (long)(gw / 2) * (gh / 2) >= 4L * 650 * 500)   // PictureImporter.Grid's halving rule
                src2 = SignLink.Pictures.RawPixels.Halve(src2, ref gw, ref gh);
            int[] grid = Resampler.Grid(src2, gw, gh, 650, 500, true);
            var gridBytes = new byte[grid.Length * 4];
            Buffer.BlockCopy(grid, 0, gridBytes, 0, gridBytes.Length);
            string gridHash;
            using (var sha = System.Security.Cryptography.SHA1.Create())
                gridHash = BitConverter.ToString(sha.ComputeHash(gridBytes)).Replace("-", "").ToLowerInvariant();
            Check(gw == 1300 && gh == 1000 && gridHash == "dfa8836485448d6bbcc89a8c61294f55d4aa8a04",
                  $"grid: the golden hash of a 2600 x 2000 gradient halved once and resampled ({gridHash})");

            Console.WriteLine("protections checked");
        }

        // PyMath against the BigInteger reference (PyMathSlow, the test's copy of the exact implementation): the fast
        // 128-bit path must give the same doubles and strings: every Python vector, random values over ±10^6 with 0-9
        // decimals, the position grids (0.005 and 1/64 in ±4000) and the halves that decide the rounding
        {
            var slowRnd = new Random(42);
            long tried = 0;
            string firstDiff = null;
            void Same(double x, int nd)
            {
                tried++;
                if (firstDiff != null) return;
                double a = PyMath.Round(x, nd), b = PyMathSlow.Round(x, nd);
                string fa = PyMath.Fixed(x, nd), fb = PyMathSlow.Fixed(x, nd), na = PyMath.Num(x, nd), nb = PyMathSlow.Num(x, nd);
                bool ints = nd != 0 || PyMath.RoundInt(x) == PyMathSlow.RoundInt(x);
                if (!(a.Equals(b) && fa == fb && na == nb && ints)) firstDiff = $"{x:R} nd {nd}: {a:R}/{b:R} {fa}/{fb} {na}/{nb}";
            }
            foreach (JsonElement c in v.GetProperty("num").EnumerateArray()) Same(c.GetProperty("v").GetDouble(), c.GetProperty("nd").GetInt32());
            for (int i = 0; i < 2000000; i++) Same((slowRnd.NextDouble() * 2 - 1) * 1e6, slowRnd.Next(10));
            for (int k = -800000; k <= 800000; k++) Same(k * 0.005, 2);                 // the 0.005 wu grid of indents
            for (int k = -256000; k <= 256000; k++) Same(k / 64.0, 6);                  // line heights on the 1/64 grid
            for (int k = -50000; k <= 50000; k++)
            {
                Same(k + 0.5, 0);
                Same(k / 10.0 + 0.05, 1);
                Same(k / 100.0 + 0.005, 2);
            }
            Check(firstDiff == null, $"pymath: a value differs from the reference: {firstDiff}");
            Console.WriteLine($"pymath: {tried} values checked against the BigInteger reference");
        }

        // screens play at their own frame times (FrameTimes.FrameAt: the frame whose end the elapsed time has not
        // reached), and GIFs cannot run away (GifDecoder: at most 5000 frames and 1.5 G frame pixels, checked before
        // any frame is decoded; a frame off the canvas keeps its delay without a decode)
        {
            long[] ends = { 40, 100 };
            Check(SignLink.Animation.FrameTimes.FrameAt(ends, 0) == 0 && SignLink.Animation.FrameTimes.FrameAt(ends, 39) == 0 &&
                  SignLink.Animation.FrameTimes.FrameAt(ends, 40) == 1 && SignLink.Animation.FrameTimes.FrameAt(ends, 99) == 1,
                  "frame times: a 40/60 ms GIF at 0, 39, 40, 99 ms");
            Check(SignLink.Animation.FrameTimes.FrameAt(ends, 100 % 100) == 0 && SignLink.Animation.FrameTimes.FrameAt(ends, 199 % 100) == 1,
                  "frame times: the elapsed time wraps at the total");
            long[] many = new long[1000];
            for (int i = 0; i < many.Length; i++) many[i] = (i + 1) * 7L;
            Check(SignLink.Animation.FrameTimes.FrameAt(many, 6999) == 999 && SignLink.Animation.FrameTimes.FrameAt(many, 7) == 1 &&
                  SignLink.Animation.FrameTimes.FrameAt(many, 3500) == 500, "frame times: a long list");

            // a synthetic GIF: a header with a 2-colour global palette, then `frames` image blocks of fw x fh whose LZW
            // data is one cleared code and the end (the rest of the pixels stay 0)
            byte[] Synthetic(int w, int h, int frames, int fw, int fh, int left)
            {
                var g = new List<byte> { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
                                         (byte)w, (byte)(w >> 8), (byte)h, (byte)(h >> 8), 0x80, 0, 0, 0, 0, 0, 255, 255, 255 };
                for (int i = 0; i < frames; i++)
                {
                    g.AddRange(new byte[] { 0x21, 0xF9, 4, 0, 5, 0, 0, 0 });                          // graphic control: 50 ms
                    g.AddRange(new byte[] { 0x2C, (byte)left, (byte)(left >> 8), 0, 0, (byte)fw, (byte)(fw >> 8), (byte)fh, (byte)(fh >> 8), 0 });
                    g.AddRange(new byte[] { 2, 2, 0x44, 0x01, 0 });                                   // LZW: clear, 0, end
                }
                g.Add(0x3B);
                return g.ToArray();
            }
            GifImage small = GifDecoder.Decode(Synthetic(4, 4, 3, 4, 4, 0));
            Check(small.Frames.Count == 3 && small.DelaysMs[1] == 50, $"gif: the synthetic file decodes ({small.Frames.Count} frames)");
            string tooMany = null;
            var manyClock = System.Diagnostics.Stopwatch.StartNew();
            try { GifDecoder.Decode(Synthetic(4, 4, 5001, 4, 4, 0)); }
            catch (ArgumentException e) { tooMany = e.Message; }
            Check(tooMany != null && tooMany.Contains("too complex") && manyClock.ElapsedMilliseconds < 2000, $"gif: 5001 frames are refused before decoding ({tooMany})");
            string tooBig = null;
            var bigClock = System.Diagnostics.Stopwatch.StartNew();
            try { GifDecoder.Decode(Synthetic(4000, 4000, 100, 4000, 4000, 0)); }
            catch (ArgumentException e) { tooBig = e.Message; }
            Check(tooBig != null && tooBig.Contains("too complex") && bigClock.ElapsedMilliseconds < 2000, $"gif: 1.6 G frame pixels are refused before decoding ({tooBig})");
            GifImage offCanvas = GifDecoder.Decode(Synthetic(4, 4, 2, 4, 4, 10));                    // every frame right of the canvas
            Check(offCanvas.Frames.Count == 2 && offCanvas.DelaysMs[0] == 50 && offCanvas.DelaysMs[1] == 50, "gif: frames off the canvas keep their delays");

            // disposal 3 and transparency as the GIF spec has them (Pillow's own reader restores to the previous frame and
            // colours transparent pixels, so it is no oracle here): a 3 x 3 canvas, colour 0 red, colour 1 white; a frame
            // whose LZW data is one pixel of index 1 (white) with the rest index 0
            byte[] Built(int w, int h, (int left, int top, int fw, int fh, int disposal, bool transparent0, bool white)[] fr)
            {
                var g = new List<byte> { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', (byte)w, 0, (byte)h, 0, 0x80, 0, 0, 255, 0, 0, 255, 255, 255 };
                foreach (var f in fr)
                {
                    g.AddRange(new byte[] { 0x21, 0xF9, 4, (byte)((f.disposal << 2) | (f.transparent0 ? 1 : 0)), 5, 0, 0, 0 });
                    g.AddRange(new byte[] { 0x2C, (byte)f.left, 0, (byte)f.top, 0, (byte)f.fw, 0, (byte)f.fh, 0, 0 });
                    g.AddRange(f.white ? new byte[] { 2, 2, 0x4C, 0x01, 0 } : new byte[] { 2, 2, 0x44, 0x01, 0 });
                }
                g.Add(0x3B);
                return g.ToArray();
            }
            int Px(byte[] frame, int x, int y) => frame[(y * 3 + x) * 3];                           // the red channel: 255 for white and red, 0 for black
            GifImage a3 = GifDecoder.Decode(Built(3, 3, new[] { (0, 0, 3, 3, 3, false, true), (2, 2, 1, 1, 0, false, true) }));
            Check(a3.Frames.Count == 2 && Px(a3.Frames[0], 0, 0) == 255 && a3.Frames[0][1] == 255 && Px(a3.Frames[1], 2, 2) == 255 && a3.Frames[1][1] == 0 && a3.Frames[1][0] == 0,
                  "gif: disposal 3 on the first frame restores the empty canvas before the next");
            GifImage b3 = GifDecoder.Decode(Built(3, 3, new[] { (0, 0, 3, 3, 0, false, true), (1, 1, 1, 1, 3, false, true), (2, 2, 1, 1, 0, false, true) }));
            Check(b3.Frames.Count == 3 && b3.Frames[1][1] == 255 && Px(b3.Frames[1], 1, 1) == 255 && b3.Frames[1][(1 * 3 + 1) * 3 + 1] == 255 &&
                  b3.Frames[2][1] == 255 && Px(b3.Frames[2], 2, 2) == 255 && Px(b3.Frames[2], 1, 1) == 255 && b3.Frames[2][(1 * 3 + 1) * 3 + 1] == 0,
                  "gif: disposal 3 restores only the frame's rectangle (the pixel under it goes back to red, the rest stays)");
            GifImage tr = GifDecoder.Decode(Built(3, 3, new[] { (0, 0, 3, 3, 0, true, true), (0, 0, 1, 1, 0, true, false) }));
            Check(tr.Frames.Count == 2 && tr.Frames[0][1] == 255 && Px(tr.Frames[0], 1, 1) == 0 && tr.Frames[0][(1 * 3 + 1) * 3 + 2] == 0 &&
                  tr.Frames[1][0] == 255 && tr.Frames[1][1] == 255, "gif: a transparent pixel draws nothing on the first frame and keeps the pixel under it later");
            Console.WriteLine("frame times and GIF bounds checked");
        }

        // a piece's recipe read back from its sign names the same item as the one the source makes, so an unchanged sign is
        // "as it is" and a resized one a refit, never a new Put
        {
            const string eikthyr = "SignLink.Art.glyph/Eikthyr.stack.txt";
            var old = SignLink.Signs.SignRecipe.Parse("art|AsMade|" + eikthyr);
            var now = SignLink.Signs.SignRecipe.ForPiece(SignLink.Signs.SignRecipe.Kinds.Art, GlowMode.AsMade, eikthyr);
            Check(old.Kind == SignLink.Signs.SignRecipe.Kinds.Art && old.SameItem(now) && now.SameItem(old) && old.SameSettings(now),
                  "recipe: a piece read back is the piece");
            var other = SignLink.Signs.SignRecipe.ForPiece(SignLink.Signs.SignRecipe.Kinds.Art, GlowMode.AsMade, "SignLink.Art.glyph/Other.stack.txt");
            Check(!old.SameItem(other), "recipe: another piece is another item");
            var heldOld = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Recipe = old, Fit = Fit.AsMade, Bytes = 1000 };
            Check(SignLink.UI.Change.Decide(heldOld, new SignLink.UI.Choice { Recipe = now, Light = GlowMode.AsMade }) == SignLink.UI.Verb.None,
                  "verb: an unchanged piece is as it is");
            Check(SignLink.UI.Change.Decide(heldOld, new SignLink.UI.Choice { Recipe = now, Light = GlowMode.AsMade, Fit = new Fit { Scale = 1.5 } }) == SignLink.UI.Verb.Refit,
                  "verb: a resized piece is a refit");
            Console.WriteLine("piece recipes checked");
        }

        // the thumbnail folder on disk stays under a cap: the oldest files go first, until the rest fit; other files stay
        {
            string dir = Path.Combine(Path.GetTempPath(), "signlink-thumbs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            for (int i = 0; i < 6; i++)
            {
                string f = Path.Combine(dir, i + ".rgba");
                File.WriteAllBytes(f, new byte[1000]);
                File.SetLastWriteTimeUtc(f, new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc));   // 0 is the oldest
            }
            File.WriteAllBytes(Path.Combine(dir, "other.txt"), new byte[1000]);                       // not a thumbnail
            int gone = SignLink.UI.ThumbCache.Prune(dir, 3500);
            string[] left = Directory.GetFiles(dir, "*.rgba");
            Array.Sort(left, StringComparer.Ordinal);
            Check(gone == 3 && left.Length == 3 && Path.GetFileName(left[0]) == "3.rgba" && File.Exists(Path.Combine(dir, "other.txt")),
                  "thumb cache: over the cap the oldest files go until the rest fit");
            Check(SignLink.UI.ThumbCache.Prune(dir, 3500) == 0, "thumb cache: under the cap nothing goes");
            Check(SignLink.UI.ThumbCache.Prune(Path.Combine(dir, "missing"), 3500) == 0, "thumb cache: a missing folder is nothing to prune");
            Directory.Delete(dir, true);
            Console.WriteLine("thumb cache checked");
        }

        // 0.19's config sections were renamed: the entries a 0.19 config holds (orphans after the rename) find their 0.20 homes
        {
            var old = new Dictionary<string, string>
            {
                ["1 - API|Password"] = "secret-of-twelve", ["1 - API|Enabled"] = "true", ["3 - Viewer|FastRefresh"] = "false",
                ["3 - Viewer|PictureSigns"] = "false", ["4 - Menu|Scale"] = "1.3", ["5 - Building|AllowOnServers"] = "true",
                ["2 - Playback|Distance"] = "32", ["1 - API|Unknown"] = "x", ["9 - Developer API|Port"] = "8500",
                ["3 - Viewer|HoverSprites"] = "true", ["3 - Signs in the world|HoverSprites"] = "false",   // dropped in 0.20.4
                ["Viewer|HoverSprites"] = "false",                                                           // a 0.1x section
                ["4 - Playback|FastRefresh"] = "true",                                                       // dropped in 0.20.17 (RefreshSeconds)
                ["5 - Pictures|MultiplayerSafe"] = "false",                                                  // dropped in 0.20.39 (always on)
                ["2 - Building|AllowOnServers"] = "true",                                                    // dropped in 0.20.44 (always allowed)
            };
            List<SignLink.ConfigMoves.Move> plan = SignLink.ConfigMoves.Plan(old);
            string Got(string k) { foreach (SignLink.ConfigMoves.Move e in plan) if (e.To == k) return e.Value; return null; }
            Check(plan.Count > 0 && plan[0].From == "1 - API|Enabled" && plan[0].To == "9 - Developer API|Enabled", "config moves: a move knows where it came from");
            Check(plan.Count == 5 && Got("9 - Developer API|Password") == "secret-of-twelve" && Got("9 - Developer API|Enabled") == "true" &&
                  Got("3 - Sign options|PreventEditor") == "false" &&
                  Got("1 - Menu|Scale") == "1.3" && Got("4 - Playback|Distance") == "32",
                  "config moves: every 0.19 entry goes to its 0.20 section; unknown and 0.20 ones stay");
            Check(Got("3 - Signs in the world|HoverSprites") == null && Got("4 - Playback|FastRefresh") == null &&
                  Got("2 - Building|AllowOnServers") == null, "config moves: a dropped setting is not moved");
            List<string> stale = SignLink.ConfigMoves.Stale(old);
            Check(stale.Count == 8 && stale.Contains("3 - Viewer|HoverSprites") && stale.Contains("3 - Signs in the world|HoverSprites") &&
                  stale.Contains("Viewer|HoverSprites") && stale.Contains("3 - Viewer|FastRefresh") && stale.Contains("4 - Playback|FastRefresh") &&
                  stale.Contains("5 - Pictures|MultiplayerSafe") &&
                  stale.Contains("5 - Building|AllowOnServers") && stale.Contains("2 - Building|AllowOnServers"),
                  "config moves: a dropped setting leaves the file from every section it had (FastRefresh since 0.20.17, MultiplayerSafe since 0.20.39, AllowOnServers since 0.20.44)");
            Check(SignLink.ConfigMoves.Stale(new Dictionary<string, string> { ["1 - Menu|Scale"] = "1" }).Count == 0, "config moves: nothing stale is nothing");
            // 0.20.123: Show all is no setting any more, it starts off every game (the author: forget Show all signs
            // after a game restart, a logout and the like)
            Check(SignLink.ConfigMoves.Stale(new Dictionary<string, string> { ["3 - Sign options|ShowAll"] = "true" }).Contains("3 - Sign options|ShowAll"),
                  "config moves: Show all leaves the file (it starts off every game since 0.20.123)");
            // 0.20.124: fine Width steps on by default (the author: fine tune enabled by default): a new entry, as every
            // file holds the old one's default off
            Check(SignLink.ConfigMoves.Stale(new Dictionary<string, string> { ["5 - Pictures|FineWidthSteps"] = "false" }).Contains("5 - Pictures|FineWidthSteps"),
                  "config moves: the 0.20.122 fine Width entry leaves the file (FineWidth, on, since 0.20.124)");
            Check(SignLink.ConfigMoves.Plan(new Dictionary<string, string>()).Count == 0, "config moves: nothing to move is nothing");
            Console.WriteLine("config moves checked");
        }

        // 0.20.45: the sign settings under "3 - Sign options" with clearer names (the author: rename Signs in
        // the world to Sign options ... call it just Prevent ...); the newest value of a setting wins
        {
            var old = new Dictionary<string, string>
            {
                ["1 - Menu|ShowGhost"] = "false", ["4 - Playback|RefreshSeconds"] = "0.5",
                ["3 - Signs in the world|PictureSigns"] = "true", ["3 - Viewer|PictureSigns"] = "false",
            };
            List<SignLink.ConfigMoves.Move> plan = SignLink.ConfigMoves.Plan(old);
            List<SignLink.ConfigMoves.Move> To(string k) => plan.FindAll(e => e.To == k);
            Check(To("3 - Sign options|Highlight").Count == 1 && To("3 - Sign options|Highlight")[0].Value == "false" &&
                  To("3 - Sign options|RefreshSeconds").Count == 1 && To("3 - Sign options|RefreshSeconds")[0].Value == "0.5",
                  "config moves: the frame switch is Highlight, the re-read pace a sign option");
            List<SignLink.ConfigMoves.Move> prevent = To("3 - Sign options|PreventEditor");
            Check(prevent.Count == 2 && prevent[0].From == "3 - Signs in the world|PictureSigns" && !prevent[0].Superseded &&
                  prevent[0].Value == "true" && prevent[1].From == "3 - Viewer|PictureSigns" && prevent[1].Superseded,
                  "config moves: two old homes of one setting: the newer one's value goes, the older one only leaves the file");
            Console.WriteLine("sign options moves checked");
        }

        // 0.20.46: Width and Height apart (the author: my problem with the window size setting is that it does not
        // set the height ... even at Very large the mod does not reach the top and bottom of the monitor)
        {
            SignLink.UI.WindowSizing.Size(3840, 2160, 0.45f, 1f, 2f, out float w1, out float h1, out float s1);
            Check(h1 == 2160f && Math.Abs(w1 - 1728f) < 0.5f && s1 == 2f, $"window: 100 % height fills the screen from top to bottom ({w1} x {h1})");
            SignLink.UI.WindowSizing.Size(3840, 2160, 1f, 0.86f, 2f, out float w2, out float h2, out _);
            Check(w2 == 3840f && Math.Abs(h2 - 1857.6f) < 0.5f, $"window: the width goes up to the whole screen (0.20.50, the author: up to 100%; {w2} x {h2})");
            SignLink.UI.WindowSizing.Size(3840, 2160, 0.2f, 0.4f, 2f, out float w3, out float h3, out _);
            Check(w3 == 1240f && h3 == 1120f, $"window: never narrower or lower than the rows need at the text size ({w3} x {h3})");
            SignLink.UI.WindowSizing.Size(800, 600, 0.45f, 0.86f, 1.5f, out float w4, out _, out float s4);
            Check(w4 == 800f && Math.Abs(s4 - 800f / 620f) < 0.001f, $"window: a screen too narrow for the rows takes smaller text ({w4}, {s4})");
            SignLink.UI.WindowSizing.Size(1920, 1080, 9f, -1f, 1f, out float w5, out float h5, out _);
            Check(w5 == 1920f && h5 == 560f, $"window: shares out of range are clamped ({w5} x {h5})");
            Check(SignLink.ConfigMoves.OldSize(new Dictionary<string, string> { ["1 - Menu|WindowSize"] = "VeryLarge", ["4 - Menu|WindowSize"] = "Small" },
                                               out float ow, out float oh) && ow == 0.5f && oh == 0.92f,
                  "window: the old Very large keeps its size as Width and Height (the newer entry wins)");
            Check(SignLink.ConfigMoves.OldSize(new Dictionary<string, string> { ["4 - Menu|WindowSize"] = "Small" }, out ow, out oh) &&
                  ow == 0.33f && oh == 0.70f, "window: a 0.19 Small too");
            Check(!SignLink.ConfigMoves.OldSize(new Dictionary<string, string> { ["1 - Menu|WindowSize"] = "Huge" }, out _, out _) &&
                  !SignLink.ConfigMoves.OldSize(new Dictionary<string, string>(), out _, out _), "window: no old size, or one unknown, is no size");
            Check(SignLink.ConfigMoves.Stale(new Dictionary<string, string> { ["1 - Menu|WindowSize"] = "Large", ["4 - Menu|WindowSize"] = "Large" }).Count == 2 &&
                  SignLink.ConfigMoves.Plan(new Dictionary<string, string> { ["4 - Menu|WindowSize"] = "Large" }).Count == 0,
                  "window: the old WindowSize leaves the file, moved nowhere");
            Console.WriteLine("window size checked");
        }

        // 0.20.47: every screen finds its size (the author: will users find the setting that suits them this way, on
        // small and large resolutions alike?): Width 0 is automatic, as wide as 80 % of the screen's height on any
        // screen shape, and the rows' ranges are what the screen can show at the text size (no stretch of a row that
        // moves nothing)
        {
            SignLink.UI.WindowSizing.Size(3440, 1440, 0f, 0.86f, 1440f / 1080f, out float wu, out _, out _);
            SignLink.UI.WindowSizing.Size(5120, 1440, 0f, 0.86f, 1440f / 1080f, out float ws, out _, out _);
            SignLink.UI.WindowSizing.Size(1920, 1080, 0f, 0.86f, 1f, out float wf, out _, out _);
            Check(Math.Abs(wu - 1152f) < 0.5f && Math.Abs(ws - 1152f) < 0.5f && Math.Abs(wf - 864f) < 0.5f,
                  $"window: automatic width is 80 % of the screen's height on 21:9, 32:9 and 16:9 alike ({wu}, {ws}, {wf})");
            SignLink.UI.WindowSizing.Size(1024, 768, 0f, 0.86f, 768f / 1080f, out float w43, out _, out _);
            Check(Math.Abs(w43 - 614.4f) < 0.5f, $"window: on 4:3 the automatic width is 80 % of the height too ({w43})");
            Check(SignLink.ConfigMoves.OldSize(new Dictionary<string, string> { ["1 - Menu|WindowSize"] = "Large" }, out float lw, out float lh) &&
                  lw == 0f && lh == 0.86f, "window: the old Large is the automatic size");
            Console.WriteLine("window size on every screen checked");
        }

        // 0.20.50: the window resizes from every edge and corner, never past the screen nor below what its rows need
        // (the author: let the window be resized by grabbing the GUI's edge, at the corners and the edges too, but
        // still so that it cannot be pulled off the screen)
        {
            const int L = SignLink.UI.WindowSizing.Left, R = SignLink.UI.WindowSizing.Right, T = SignLink.UI.WindowSizing.Top,
                      B = SignLink.UI.WindowSizing.Bottom;
            void Drag(int edges, float dx, float dy, out float x, out float y, out float w, out float h) =>
                SignLink.UI.WindowSizing.Drag(100, 100, 800, 600, dx, dy, edges, 620, 560, 1920, 1080, out x, out y, out w, out h);
            Drag(R, 50, 0, out float x1, out float y1, out float w1, out float h1);
            Check(x1 == 100 && y1 == 100 && w1 == 850 && h1 == 600, "window drag: the right edge widens it");
            Drag(L, -40, 0, out float x2, out _, out float w2, out _);
            Check(x2 == 60 && w2 == 840, "window drag: the left edge moves out, the right edge stays");
            Drag(L, -500, 0, out float x3, out _, out float w3, out _);
            Check(x3 == 0 && w3 == 900, "window drag: not past the screen's left edge");
            Drag(L, 400, 0, out float x4, out _, out float w4, out _);
            Check(w4 == 620 && x4 == 280, "window drag: not narrower than the rows need (the right edge stays)");
            Drag(R | B, 5000, 5000, out float x5, out float y5, out float w5, out float h5);
            Check(x5 == 100 && y5 == 100 && w5 == 1820 && h5 == 980, "window drag: a corner, not past the screen's right and bottom");
            Drag(T | L, -300, -300, out float x6, out float y6, out float w6, out float h6);
            Check(x6 == 0 && y6 == 0 && w6 == 900 && h6 == 700, "window drag: the top-left corner, not past the top and the left");
            Drag(T, 200, 100, out _, out float y7, out float w7, out float h7);
            Check(y7 == 140 && h7 == 560 && w7 == 800, "window drag: the top edge down to what the rows need; across it nothing moves");
            SignLink.UI.WindowSizing.Drag(0, 0, 1920, 1080, 10, 10, R | B, 2000, 1200, 1920, 1080, out float x8, out float y8, out float w8, out float h8);
            Check(x8 == 0 && y8 == 0 && w8 == 1920 && h8 == 1080, "window drag: rows wider than the screen: the whole screen");
            Console.WriteLine("window drag checked");
        }

        // 0.20.62: the Text editor's Undo and Redo (the author: the text editor has no undo, no Ctrl+Z and Ctrl+Y,
        // and no buttons for them)
        {
            var h = new SignLink.UI.TextHistory();
            h.Seen("", 0);
            h.Seen("H", 1.0); h.Seen("He", 1.2); h.Seen("Hel", 1.5); h.Seen("Hello", 1.9);   // typing: one step
            Check(h.CanUndo && !h.CanRedo && h.Undo() == "" && !h.CanUndo && h.CanRedo, "history: a burst of typing is one step");
            Check(h.Redo() == "Hello" && !h.CanRedo, "history: redo brings it back");
            h.Seen("Hello w", 4.0);                                         // a pause: a new step
            h.Seen("Hello world", 4.5);
            h.Seen("<b>Hello world", 4.6, discrete: true);                  // a template, the light, Clear: a step of its own
            Check(h.Undo() == "Hello world" && h.Undo() == "Hello" && h.Undo() == "" && !h.CanUndo,
                  "history: a pause and a discrete change start new steps");
            Check(h.Redo() == "Hello" && h.Redo() == "Hello world", "history: redo walks forward");
            h.Seen("Hello there", 9.0);
            Check(!h.CanRedo && h.Undo() == "Hello world", "history: a new change drops what redo had");
            h.Reset("other sign");
            Check(!h.CanUndo && !h.CanRedo && h.Current == "other sign", "history: another sign starts over");
            var big = new SignLink.UI.TextHistory();
            big.Seen("", 0);
            for (int i = 1; i <= 150; i++) big.Seen("x" + i, i * 10.0, discrete: true);
            int steps = 0;
            while (big.CanUndo) { big.Undo(); steps++; }
            Check(steps == SignLink.UI.TextHistory.Most, $"history: at most {SignLink.UI.TextHistory.Most} steps back ({steps})");
            Console.WriteLine("text history checked");
        }

        // 0.20.49: a picture read back from its sign, for editing it without its file (the author: I expect a proper
        // solution, nail this one carefully because it breeds bugs): the pixels, where they sit, the margins the sign
        // does not hold
        {
            // 6 x 4 with a see-through left column and bottom row (the box is 5 x 3, half a pixel right of and above the
            // picture's middle) and a half-transparent pixel; one band per row, so three signs (the empty row has none)
            const int W = 6, H = 4;
            var px = new int[W * H];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    px[r * W + c] = c == 0 || r == H - 1 ? -1 : PixelText.Pack(0x203040 + c * 0x100 + r * 0x10, 255);
            px[1 * W + 2] = PixelText.Pack(0x804020, 120);
            const double pitch = 1.0, cx = 3.25, bottom = -2.5, cy = bottom + H * pitch / 2;
            foreach (double cover in new[] { 0.0, 2.0 })
                foreach (bool glow in new[] { false, true })
                {
                    var texts = PixelText.Bands(PixelText.ForCover(px, cover), W, H, pitch, cx, bottom, PixelText.GlyphSize(pitch, cover), 0,
                                                maxPixels: W, glow: glow).ConvertAll(b => b.Text);
                    string tag = $"cover {cover}, glow {glow}";
                    Check(texts.Count == 3, $"sign picture: three signs ({tag}: {texts.Count})");
                    bool read = TextReadback.ReadBox(texts, out int bw, out int bh, out int[] box, out double p, out double mx, out double my, asSeen: true);
                    Check(read && bw == 5 && bh == 3 && Math.Abs(p - pitch) < 1e-9, $"sign picture: the drawn box ({tag}: {bw} x {bh}, pitch {p})");
                    // as near as the sign's rounding lets: indents to 0.1 wu here (a pitch of 1: PositionDecimals), heights twice that
                    Check(read && Math.Abs(mx - (cx + 0.5 * pitch)) <= 0.05 + 1e-9 && Math.Abs(my - (cy + 0.5 * pitch)) <= 0.1 + 1e-9,
                          $"sign picture: the box's middle where the encoder put it ({tag}: {mx}, {my}; want {cx + 0.5}, {cy + 0.5})");
                    bool same = read;
                    for (int r = 0; r < 3 && same; r++)
                        for (int c = 0; c < 5 && same; c++)
                        {
                            int want = px[r * W + c + 1], got = box[r * 5 + c];
                            same = (want & 0xFFFFFF) == (got & 0xFFFFFF) && Math.Abs(PixelText.Alpha(want) - PixelText.Alpha(got)) <= (cover > 0 ? 1 : 0);
                        }
                    Check(same, $"sign picture: every pixel as it looks ({tag})");
                    // the fit puts the picture's middle at (cx, cy): the canvas gets its margin back, the middle stays
                    bool ok = SignPicture.Read(texts, cx, cy, out SignPicture sp, out string why);
                    Check(ok && sp.W == W && sp.H == H && sp.PadLeft == 1 && sp.PadBottom == 1 && sp.PadRight == 0 && sp.PadTop == 0 &&
                          sp.ShiftX == 0 && sp.ShiftY == 0 && Math.Abs(sp.Pitch - pitch) < 1e-9,
                          $"sign picture: the canvas is the picture as made ({tag}: {why} {sp?.W} x {sp?.H}, pads {sp?.PadLeft} {sp?.PadRight} {sp?.PadTop} {sp?.PadBottom}, shift {sp?.ShiftX} {sp?.ShiftY})");
                    if (ok && cover == 0)
                    {
                        int[] again = Resampler.Grid(sp.Rgba, sp.W, sp.H, sp.W, sp.H, opaque: false);
                        bool all = true;
                        for (int i = 0; i < px.Length && all; i++) all = again[i] == px[i];
                        Check(all, "sign picture: its canvas makes the very same grid again (Resolution and Width as they are)");
                    }
                }
            // the margins on one side only, whole pixels; past four times the box (content far from where its fit says,
            // a fit someone else's tool made stale) no padding: the box shifts instead
            SignPicture.Canvas(5, 3, 0.5, 0.5, out int l1, out int r1, out int t1, out int b1, out double sx1, out double sy1);
            Check(l1 == 1 && r1 == 0 && t1 == 0 && b1 == 1 && sx1 == 0 && sy1 == 0, "sign picture: margins back on the far side");
            SignPicture.Canvas(10, 10, -3, -2, out int l2, out int r2, out int t2, out int b2, out _, out _);
            Check(l2 == 0 && r2 == 6 && t2 == 4 && b2 == 0, "sign picture: a box left of and below the middle gets them right and on top");
            SignPicture.Canvas(10, 10, 0.3, 0.2, out int l3, out int r3, out int t3, out int b3, out double sx3, out double sy3);
            Check(l3 == 1 && r3 == 0 && t3 == 0 && b3 == 0 && sx3 == 0 && sy3 == 0,
                  "sign picture: what whole pixels miss is the sign's rounding, no shift");
            // a sprite in a corner of a big see-through picture: its canvas is the picture as made again (review, 0.20.49:
            // a four-times cap shifted it instead, and the shift did not survive an Update)
            SignPicture.Canvas(64, 64, 224, -224, out int l5, out int r5, out int t5, out int b5, out double sx5, out double sy5);
            Check(l5 == 448 && r5 == 0 && t5 == 448 && b5 == 0 && sx5 == 0 && sy5 == 0, "sign picture: a sprite in a corner gets its whole picture back");
            // past MaxPixels no picture was ever made that big: a fit another tool made stale; the box shifts instead
            SignPicture.Canvas(800, 800, 900, 0, out int l4, out int r4, out _, out _, out double sx4, out _);
            Check(l4 == 0 && r4 == 0 && sx4 == 900, "sign picture: a box farther than any picture is not padded but shifted");
            Check(SignPicture.FileColumns(40, 80, 100) == 50 && SignPicture.FileColumns(40, 80, 0) == 40 && SignPicture.FileColumns(40, 80, 60) == 40,
                  "sign picture: the recipe's columns count the file's full width (none known, or fewer than the canvas: the canvas's)");
            var padded = new SignPicture { W = 80, H = 40 };
            var shifted = new SignPicture { W = 80, H = 40, ShiftX = 2.5 };
            Check(padded.RecipeColumns(40, 0.5, 100) == 50 && shifted.RecipeColumns(40, 0.5, 100) == 40,
                  "sign picture: a shifted box has no file middle to count from: its own columns");
            Check(padded.RecipeColumns(40, 30, 1000) == 40 && 500 * 30 / 40f > SignLink.Signs.SignRecipe.MaxWidthM,
                  "sign picture: columns a recipe could not hold (wider than MaxWidthM) stay the canvas's");
            Check(SignPicture.TextsHash(new[] { "ab", "c" }) == SignPicture.TextsHash(new List<string> { "ab", "c" }) &&
                  SignPicture.TextsHash(new[] { "ab", "c" }) != SignPicture.TextsHash(new[] { "a", "bc" }) &&
                  SignPicture.TextsHash(new[] { "ab", "c" }) != SignPicture.TextsHash(new[] { "ab", "d" }),
                  "sign picture: the texts' hash tells a changed picture from a moved sign");
            Check(!SignPicture.Read(new[] { "<size=2>Hello" }, 0, 0, out _, out string notPic) && notPic != null, "sign picture: a text is no picture");
            // 0.20.52: what every player's game builds for a sign (SaveCost.LoadSeconds counts glyphs; 0.20.62: one count): the visible
            // characters, tags and spaces aside (1 Sign's Multiplayer row says when it arrives)
            Check(SaveCost.Glyphs(new[] { "<size=2>■ ■\n<#fff>a", "", "<line-height=1>bc" }) == 5 &&
                  SaveCost.Glyphs(new string[0]) == 0,
                  "glyphs: the visible characters of a stack's texts (one count: SaveCost.Glyphs)");
            Console.WriteLine("sign picture checked");
        }

        // 0.20.49: Copy carries what made the sign, Paste puts it back (the author: if I copy a sign and paste it ...
        // that is the same situation, isn't it?)
        {
            var c = new SignLink.Signs.SignClip { Recipe = "pic|2.5|0.5|2|1|1|0|my|odd pic.png", Name = "Kép <b>ő</b>", HasFit = true,
                                                  Fit = new Fit { Scale = 1.25, Right = -12.5, Up = 3, Out = 0.75, Turn = 30, Tilt = -45, Edge = true },
                                                  C0x = 0.5, C0y = -1, C0Known = true };
            c.Texts.AddRange(new[] { "<size=1>■\n■", "", "<#ff0000>■" });
            string packed = c.Pack();
            SignLink.Signs.SignClip u = SignLink.Signs.SignClip.Unpack(packed);
            Check(u.Recipe == c.Recipe && u.Name == c.Name && u.HasFit && u.C0Known && u.C0x == 0.5 && u.C0y == -1 &&
                  u.Fit.Scale == 1.25 && u.Fit.Right == -12.5 && u.Fit.Up == 3 && u.Fit.Out == 0.75 && u.Fit.Turn == 30 && u.Fit.Tilt == -45 && u.Fit.Edge &&
                  u.Texts.Count == 3 && u.Texts[0] == c.Texts[0] && u.Texts[1] == "" && u.Texts[2] == c.Texts[2],
                  "clip: recipe, name, fit and every sign's text come back");
            SignLink.Signs.SignClip old = SignLink.Signs.SignClip.Unpack("<size=1>■\u001d<size=1>■");
            Check(old.Recipe == "" && !old.HasFit && old.Texts.Count == 2, "clip: a copy made before 0.20.49 pastes as its texts");
            var plain = new SignLink.Signs.SignClip();
            plain.Texts.Add("Hello there");
            Check(plain.Pack() == "Hello there", "clip: a plain text copies as itself (no header to paste into a chat)");
            SignLink.Signs.SignClip bad = SignLink.Signs.SignClip.Unpack("\u0002SignLink clip 1\nfit=x,y\nrecipe=%ZZ\n\u0003abc");
            Check(bad.Texts.Count == 1 && bad.Texts[0] == "abc" && !bad.HasFit, "clip: a broken header is ignored, never thrown");
            // 0.20.178: a moving piece's frames go along
            var moving = new SignLink.Signs.SignClip { Anim = new byte[] { 0, 1, 2, 250, 255 } };
            moving.Texts.Add("frame one");
            SignLink.Signs.SignClip back = SignLink.Signs.SignClip.Unpack(moving.Pack());
            Check(back.Anim != null && back.Anim.Length == 5 && back.Anim[3] == 250 && back.Texts.Count == 1 && back.Texts[0] == "frame one",
                  "clip: a moving piece's saved frames come back with it");
            Check(SignLink.Signs.SignClip.Unpack("\u0002SignLink clip 1\nanim=***\n\u0003abc").Anim == null, "clip: frames it cannot read are dropped");
            Console.WriteLine("clip checked");
        }

        // 0.20.49: a fit moved to another sign's pose shows its content where it was (Paste onto a turned or tilted sign)
        {
            var f = new Fit { Scale = 1.3, Right = 12, Up = 5, Out = 2, Turn = 30, Tilt = 20, Edge = false };
            Fit g = Fit.Repose(f, -45, 70, true, 3, -1);
            f.Centre(3, -1, out double fx, out double fy);
            g.Centre(3, -1, out double gx, out double gy);
            f.OnSign(out _, out _, out double fn);
            g.OnSign(out _, out _, out double gn);
            Check(Math.Abs(fx - gx) < 1e-9 && Math.Abs(fy - gy) < 1e-9 && g.Turn == -45 && g.Tilt == 70 && g.Edge && g.Scale == 1.3 &&
                  Math.Abs(fn - gn) < 1e-9, $"fit: reposed, the content's middle shows where it did ({fx},{fy} / {gx},{gy})");
            Fit same = Fit.Repose(f, f.Turn, f.Tilt, f.Edge, 3, -1);
            Check(same.Same(f), "fit: the same pose is the same fit");
            Console.WriteLine("repose checked");
        }

        // 0.20.66: Back to where it was built stands a tilted or rotated sign as built; its fit in the new pose keeps the
        // content where the rows show it on the face (Right, Up along the face, the part off it), as the Tilt row does
        {
            var f = new Fit { Scale = 1.3, Right = 12, Up = 5, Out = 2, Turn = 30, Tilt = 70, Edge = true };
            Fit g = f.Posed(0, 0, false);
            f.OnSign(out double fr, out double fu, out double fn);
            g.OnSign(out double gr, out double gu, out double gn);
            Check(Math.Abs(fr - gr) < 1e-9 && Math.Abs(fu - gu) < 1e-9 && Math.Abs(fn - gn) < 1e-9 && g.Turn == 0 && g.Tilt == 0 &&
                  !g.Edge && g.Scale == 1.3, $"fit: posed again, the content stays where it sits on the face ({fu},{fn} / {gu},{gn})");
            Check(Math.Abs(g.Up - fu) < 1e-9 && Math.Abs(g.Out - fn) < 1e-9, "fit: standing, Up runs along the face and Out off it");
            Fit lying = f.Posed(-20, 90, false);
            lying.OnSign(out _, out double lu, out double ln);
            Check(Math.Abs(lu - fu) < 1e-9 && Math.Abs(ln - fn) < 1e-9 && lying.Turn == -20 && lying.Tilt == 90, "fit: posed lying, the same place on the face");
            Check(f.Posed(f.Turn, f.Tilt, f.Edge).Same(f), "fit: posed as it is, the same fit");
            Console.WriteLine("posed checked");
        }

        // 0.20.107: a pose button that turns the face round (face up <-> face down) keeps the content where it is in the
        // world (the author: set to Face up, with the Up slider at minus 2 metres the picture is towards me ...
        // pressing Face down on this setup makes the picture jump over and land just as far from me ... I expect the
        // same for Up; chose: the Up row changes sign); one that turns it a quarter (standing <-> lying) keeps it on
        // the face, as Posed
        {
            var up = new Fit { Scale = 1, Right = 20, Up = 0, Out = 130, Turn = 15, Tilt = 90 };   // face up, 3.25 m towards you
            up.OnSign(out _, out double onUp, out _);
            Fit down = up.Laid(-90, false);
            down.OnSign(out _, out double onDown, out _);
            Check(Math.Abs(down.Up - up.Up) < 1e-9 && Math.Abs(down.Out - up.Out) < 1e-9 && Math.Abs(down.Right - up.Right) < 1e-9 &&
                  down.Tilt == -90 && down.Turn == 15 && !down.Edge,
                  "laid: face up to face down, the content stays where it is (Right, Up, Out as built)");
            Check(Math.Abs(onUp + 130) < 1e-9 && Math.Abs(onDown - 130) < 1e-9, $"laid: so the Up row changes sign ({onUp} / {onDown})");
            var standing = new Fit { Scale = 1, Right = 0, Up = 40, Out = 0, Tilt = 0 };
            Check(standing.Laid(90, false).Same(standing.Posed(0, 90, false)) && up.Laid(0, false).Same(up.Posed(15, 0, false)),
                  "laid: a quarter turn keeps the content on the face, as Posed");
            Check(up.Laid(90, false).Same(up), "laid: the same pose, the same fit");
            Console.WriteLine("laid checked");
        }

        // the pose alone: what content SignLink cannot resize or move goes on with (as made, posed), and the pose the rows
        // say now over texts worked out for an earlier one (the live preview while the rows move)
        {
            var posed = new Fit { Scale = 2.5, Right = 12, Up = -8, Out = 3, Turn = 30, Tilt = 45, Edge = true };
            Fit only = posed.PoseOnly();
            Check(only.Scale == 1 && only.Right == 0 && only.Up == 0 && only.Out == 0 && only.Turn == 30 && only.Tilt == 45 && only.Edge,
                  "pose only: as made, in the fit's pose");
            var earlier = new Fit { Scale = 0.5, Right = -4, Up = 6, Out = 1, Turn = -10, Tilt = -90, Edge = false };
            Fit now = earlier.WithPose(posed);
            Check(now.Scale == 0.5 && now.Right == -4 && now.Up == 6 && now.Out == 1 && now.Turn == 30 && now.Tilt == 45 && now.Edge,
                  "with pose: its size and place, the other's pose");
            Check(earlier.Turn == -10 && earlier.Tilt == -90 && !earlier.Edge, "with pose: a copy, the fit itself as it was");
            Console.WriteLine("pose only and with pose checked");
        }

        // 0.20.68: the Text editor holds a text as made; the sign shows it fitted (FitTexts.ForPut: what a Text put
        // writes), and a sign's text loaded into the editor has its fit taken off again (Unfit), so the Size rows measure
        // the same text whatever its size on the sign (the author: a chessboard put at its smallest read back as the largest)
        {
            // a Text put's sign text (made at the sign's centre, 0, 0) and the size it really takes
            static string Fitted(string t, Fit f, out double scale)
            {
                FitTexts.Result r = FitTexts.ForPut(new[] { t }, 0, 0, false, f, false);
                scale = r.Fit.Scale;
                return r.Texts[0];
            }
            string made = Snippets.Compose(Snippets.Pattern("█", 4, 3, 0.25, 0x222222, 255, 0xDDDDDD, 255, alternate: true));
            var fit = new Fit { Scale = 0.25, Right = 12, Up = -8, Turn = 30 };
            string onSign = Fitted(made, fit, out double k);
            Check(k == 0.25 && onSign != made, "fitted: a text put at a quarter of its size is rewritten");
            fit.Offset(0, 0, out double fdx, out double fdy);
            Check(onSign == TextTransform.Apply(made, 0.25, fdx, fdy), "fitted: what a Text put writes (SignFit.Put, made at 0, 0)");
            string back = TextTransform.Unfit(onSign, fit);
            Check(SameNumbers(back, made, 1e-3), $"unfit: the sign's text back as made, every tag number within 0.001 ({NumberDiff(back, made, 1e-3)})");
            Check(SameNumbers(Fitted(back, fit, out _), onSign, 1e-3), "unfit: fitted again, the same sign text");
            Check(TextTransform.Unfit(made, Fit.AsMade) == made && Fitted(made, Fit.AsMade, out double k1) == made && k1 == 1,
                  "as made: nothing to put on or take off");
            Check(TextTransform.Unfit("Hello", fit) == "Hello" && Fitted("Hello", fit, out double kp) == "Hello" && kp == 1,
                  "a text the game sizes itself: as it is both ways");
            var big = new Fit { Scale = 3, Right = -40, Up = 20 };
            Check(SameNumbers(TextTransform.Unfit(Fitted(made, big, out _), big), made, 1e-3), $"unfit: three times as large and moved, back as made ({NumberDiff(TextTransform.Unfit(Fitted(made, big, out _), big), made, 1e-3)})");
            string typed = "<size=3><line-height=2.5>Skål\n<voffset=1>ok";
            var small = new Fit { Scale = 0.5, Up = 6 };
            string typedOn = Fitted(typed, small, out double ks);
            string typedBack = TextTransform.Unfit(typedOn, new Fit { Scale = ks, Up = 6 });
            Check(ks == 0.5 && SameNumbers(typedBack, typed, 1e-3),
                  $"unfit: a typed text with lines back as made (the voffset the move put in front goes again): '{typedBack}'");
            Check(SameNumbers(Fitted(typedBack, small, out _), typedOn, 1e-3), "unfit: the typed text fitted again, the same");

            // the verb compares the editor's text as made with the sign's text as made, not with the fitted one
            var textRecipe = SignLink.Signs.SignRecipe.ForText();
            var heldText = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Recipe = textRecipe, Fit = fit, Text = onSign, Made = back, Bytes = 2000 };
            Check(SignLink.UI.Change.Decide(heldText, new SignLink.UI.Choice { Recipe = textRecipe, Fit = fit, Text = back }) == SignLink.UI.Verb.None,
                  "verb: a text at a quarter of its size, loaded as made, is as it is");
            var bigger = fit;
            bigger.Scale = 0.5;
            Check(SignLink.UI.Change.Decide(heldText, new SignLink.UI.Choice { Recipe = textRecipe, Fit = bigger, Text = back }) == SignLink.UI.Verb.Refit,
                  "verb: the same text made larger is a refit");
            Check(SignLink.UI.Change.Decide(heldText, new SignLink.UI.Choice { Recipe = textRecipe, Fit = fit, Text = back + "x" }) == SignLink.UI.Verb.Update,
                  "verb: another text is an update");
            Console.WriteLine("text fit checked");
        }

        // 0.20.76: the content highlight is hatched (Hatch.Stripes): diagonal stripes, clipped to the content's rectangle,
        // in the frame quad's unit square, so the picture shows between them (the author: a full amber veil hid it)
        {
            List<double[]> bands = SignLink.UI.Hatch.Stripes(2.0, 1.0, 0.2, 0.07);
            double area = 0;
            bool inside = true, convex = true;
            foreach (double[] p in bands)
            {
                double a = 0;
                for (int i = 0; i < p.Length; i += 2)
                {
                    double x0 = p[i], y0 = p[i + 1], x1 = p[(i + 2) % p.Length], y1 = p[(i + 3) % p.Length];
                    a += x0 * y1 - x1 * y0;
                    if (Math.Abs(x0) > 0.5 + 1e-9 || Math.Abs(y0) > 0.5 + 1e-9) inside = false;
                }
                if (a < -1e-12) convex = false;                             // every polygon counterclockwise
                area += a / 2;
            }
            Check(bands.Count > 10 && inside && convex, $"hatch: {bands.Count} stripes, all in the unit square, counterclockwise");
            Check(Math.Abs(area - 0.35) < 0.03, $"hatch: the stripes cover their share of the rectangle ({area:0.###} of 0.35)");
            List<double[]> one = SignLink.UI.Hatch.Stripes(1.0, 1.0, 0.2, 0.07);
            Check(one.TrueForAll(p => p.Length >= 6 && p.Length % 2 == 0), "hatch: polygons of three corners or more");
            Check(SignLink.UI.Hatch.Stripes(0, 1, 0.2, 0.07).Count == 0 && SignLink.UI.Hatch.Stripes(1, 1, 0, 0.07).Count == 0,
                  "hatch: nothing for no size or no pitch");
            Check(SignLink.UI.Hatch.Stripes(300, 300, 0.01, 0.004).Count <= SignLink.UI.Hatch.MostStripes, "hatch: a cap on stripes");
            Console.WriteLine("hatch checked");
        }

        // 0.20.82: the hotkey as people write it: the modifiers first, in Ctrl, Alt, Shift order, by their short names
        // (the author: BepInEx wrote "PageUp + LeftShift")
        {
            string[] Keys(string main, params string[] mods) => SignLink.UI.HotkeyText.Parts(main, mods);
            Check(string.Join("|", Keys("PageUp", "LeftShift")) == "Shift|Page Up", $"hotkey: Shift first ({string.Join("|", Keys("PageUp", "LeftShift"))})");
            Check(SignLink.UI.HotkeyText.Combo("PageUp", new[] { "LeftShift" }) == "Shift + Page Up", "hotkey: joined with +");
            Check(string.Join("|", Keys("F5", "RightShift", "LeftAlt", "LeftControl")) == "Ctrl|Alt|Shift|F5", "hotkey: Ctrl, Alt, Shift, then the key");
            Check(string.Join("|", Keys("Alpha7", "LeftShift", "RightShift")) == "Shift|7", "hotkey: a modifier once, digits plain");
            Check(SignLink.UI.HotkeyText.Name("Keypad5") == "Num 5" && SignLink.UI.HotkeyText.Name("Return") == "Enter" &&
                  SignLink.UI.HotkeyText.Name("Escape") == "Esc" && SignLink.UI.HotkeyText.Name("ScrollLock") == "Scroll Lock" &&
                  SignLink.UI.HotkeyText.Name("UpArrow") == "Up" && SignLink.UI.HotkeyText.Name("Insert") == "Insert",
                  "hotkey: key names");
            Check(SignLink.UI.HotkeyText.Combo("None", new string[0]) == "" && Keys("None").Length == 0, "hotkey: none");
            Console.WriteLine("hotkey text checked");
        }

        // 0.20.90 (review): 2 Content's strip follows the choice slots (SourceFollow): the player's own source comes back
        // with their own slot, also after a sign whose content no source loads (Session.Blank, 0.20.89), which loads into
        // no source: the strip went back to the source of an older load (Text at first) instead of the player's own
        {
            const int Text = 0, Picture = 2;
            object own = new object(), blank = new object(), blank2 = new object(), picture = new object();
            var f = new SignLink.UI.SourceFollow();
            int at = f.Follow(own, false, Text);
            Check(at == Text, "strip: the own slot at first, as it is");
            at = Picture;                                                   // the player switches to Picture on their own slot
            at = f.Follow(own, false, at);
            Check(at == Picture && f.Follow(blank, true, at) == Picture, "strip: a blank slot leaves the strip where it is");
            Check(f.Follow(own, false, Picture) == Picture, "strip: back to the own slot after a blank one, its own source (Picture)");
            at = f.Follow(blank2, true, Picture);
            at = Text;                                                      // Text on the blank slot, to type something there
            at = f.Follow(blank2, true, at);
            Check(at == Text && f.Follow(own, false, at) == Picture, "strip: a source picked on a blank slot does not become the own one");
            f.Leaving(false, Picture);                                      // a picture sign loads (ContentTab.LoadInto) ...
            at = f.Follow(picture, true, Picture);                          // ... into the Picture source
            Check(at == Picture, "strip: a loaded slot keeps the source it loaded into");
            at = f.Follow(own, false, Text);                                // the strip on Text, the own slot back
            Check(at == Picture, "strip: the own source back after a loaded slot");
            Console.WriteLine("source strip checked");
        }

        // 0.20.91 (review): the Picture source lights only a picture it holds (Change.PictureLight). With nothing picked it
        // turned the slot's light to Glowing, so a sign holding something no source loads got the verb Apply, live: 3 Place
        // relit and rewrote it half a second after it opened, with no click
        {
            var vanilla = new SignLink.UI.Held { HasSign = true, Empty = false, Movable = true, Recipe = SignLink.Signs.SignRecipe.None, Text = "Bob's", Made = "Bob's", Bytes = 40 };
            GlowMode light = SignLink.UI.Change.PictureLight(GlowMode.AsMade, false, true);
            Check(light == GlowMode.AsMade, "picture light: nothing picked, the slot's light stays as it is");
            var blank = new SignLink.UI.Choice { Recipe = SignLink.Signs.SignRecipe.None, Light = light };
            Check(SignLink.UI.Change.Decide(vanilla, blank) == SignLink.UI.Verb.None, "picture light: nothing picked on a held sign, nothing to do");
            Check(SignLink.UI.Change.PictureLight(GlowMode.AsMade, true, true) == GlowMode.Glowing &&
                  SignLink.UI.Change.PictureLight(GlowMode.AsMade, true, false) == GlowMode.Lit &&
                  SignLink.UI.Change.PictureLight(GlowMode.Lit, true, true) == GlowMode.Lit &&
                  SignLink.UI.Change.PictureLight(GlowMode.Glowing, false, false) == GlowMode.Glowing,
                  "picture light: a picked picture glows or is lit as its options say; a light chosen stays");
            // the area line counts the pick only when a put would write it (an empty Text editor is not "the sign emptied")
            Check(SignLink.UI.Change.AreaBytes(SignLink.UI.Verb.None, 100) == -1 && SignLink.UI.Change.AreaBytes(SignLink.UI.Verb.Refit, 100) == -1 &&
                  SignLink.UI.Change.AreaBytes(SignLink.UI.Verb.Apply, 100) == -1 && SignLink.UI.Change.AreaBytes(SignLink.UI.Verb.Place, 100) == 100 &&
                  SignLink.UI.Change.AreaBytes(SignLink.UI.Verb.Put, 7) == 7 && SignLink.UI.Change.AreaBytes(SignLink.UI.Verb.Update, 9) == 9,
                  "area: the pick's bytes only for Place, Put and Update");
            Console.WriteLine("picture light checked");
        }

        // 0.20.93: Shift + mouse wheel over a slider's row turns it a fine step a notch (the author: when I hover
        // the mouse over a slider in the GUI, Shift + wheel should turn the slider finely): a fifth of the
        // slider's snap, never finer than the row shows; a value off that grid goes to the next grid point first
        {
            Func<float, float, bool> near = (a, b) => Math.Abs(a - b) < 1e-4f;
            Check(near(SignLink.UI.FineStep.Of(5f, "0.#"), 1f) && near(SignLink.UI.FineStep.Of(0.25f, "0.##"), 0.05f) &&
                  near(SignLink.UI.FineStep.Of(0.05f, "0.##"), 0.01f) && near(SignLink.UI.FineStep.Of(15f, "0"), 3f),
                  "fine step: a fifth of the slider's step (tilt 1°, right 5 cm, width 1 cm, a template's turn 3°)");
            Check(near(SignLink.UI.FineStep.Of(4f, "0"), 1f) && near(SignLink.UI.FineStep.Of(1f, "0"), 1f) &&
                  near(SignLink.UI.FineStep.Of(0.1f, "0.0"), 0.1f),
                  "fine step: no finer than the row shows (a pixel, a column, a tenth of a second)");
            Check(near(SignLink.UI.FineStep.Nudge(10f, -3f, 5f, "0.#", -90f, 90f), 11f) &&
                  near(SignLink.UI.FineStep.Nudge(10f, 3f, 5f, "0.#", -90f, 90f), 9f),
                  "fine step: the wheel up turns it up, down turns it down, one step a notch");
            Check(near(SignLink.UI.FineStep.Nudge(90f, -3f, 5f, "0.#", -90f, 90f), 90f) &&
                  near(SignLink.UI.FineStep.Nudge(-90f, 3f, 5f, "0.#", -90f, 90f), -90f),
                  "fine step: not past the ends");
            Check(near(SignLink.UI.FineStep.Nudge(1.23f, -1f, 0.25f, "0.##", -25f, 25f), 1.25f) &&
                  near(SignLink.UI.FineStep.Nudge(1.23f, 1f, 0.25f, "0.##", -25f, 25f), 1.20f) &&
                  near(SignLink.UI.FineStep.Nudge(1.25f, -1f, 0.25f, "0.##", -25f, 25f), 1.30f) &&
                  near(SignLink.UI.FineStep.Nudge(-0.05f, -1f, 0.25f, "0.##", -25f, 25f), 0f),
                  "fine step: a typed value goes to the next grid point, not past it");
            Check(near(SignLink.UI.FineStep.Nudge(7f, 0f, 5f, "0.#", -90f, 90f), 7f), "fine step: no wheel, no change");
            // 0.20.98: a row whose value snaps (a picture's width to pixels TMP adds up exactly, a piece's size to exact
            // line heights) goes a notch to the next size it really takes (the author: Shift + wheel does not work
            // on the content size's width and height: a centimetre snapped back to the same width, so nothing moved)
            Func<float, float> pixels = w => (float)(Math.Round(w / 0.625) * 0.625);   // 200 px wide: 62.5 cm a pitch step
            Check(near(SignLink.UI.FineStep.Notch(2.5f, -1f, 0.05f, "0.##", 0.25f, 30f, pixels), 3.125f) &&
                  near(SignLink.UI.FineStep.Notch(2.5f, 1f, 0.05f, "0.##", 0.25f, 30f, pixels), 1.875f),
                  "notch: a snapping row goes to the next size it takes, up or down");
            Check(near(SignLink.UI.FineStep.Notch(2.6f, -1f, 0.05f, "0.##", 0.25f, 30f, pixels), 3.125f),
                  "notch: from the size it shows (2.6 shows 2.5), not from the row's own value");
            Check(near(SignLink.UI.FineStep.Notch(1.2f, -1f, 0.05f, "0.##", 0.25f, 30f, w => w), 1.21f),
                  "notch: a row that does not snap moves a fine step");
            Check(near(SignLink.UI.FineStep.Notch(29.9f, -1f, 0.05f, "0.##", 0.25f, 30f, pixels), 30f) &&
                  near(SignLink.UI.FineStep.Notch(30f, -1f, 0.05f, "0.##", 0.25f, 30f, pixels), 30f),
                  "notch: no size past the end, the size it shows stays");
            Check(near(SignLink.UI.FineStep.Notch(1f, -1f, 0.05f, "0.##", 0.25f, 30f, w => w < 1.5f ? 1f : w < 1.6f ? 0f : 2f), 2f),
                  "notch: a value that takes no size (0) is passed over");
            Console.WriteLine("fine step checked");
        }

        // 0.20.95: a new picture or piece picked over a sign that holds something hides what it holds, on this computer
        // (the author: do not show the existing picture when editing once I pick new content): the old picture showed
        // under the new pick's frame. The held item again (other settings), a text (the Text source shows its own
        // preview), no pick and an empty sign show the sign as it is
        {
            var a = SignLink.Signs.SignRecipe.ForPicture(new PictureOptions { WidthM = 2f, Pitch = 0.125f }, "a.png");
            var a2 = SignLink.Signs.SignRecipe.ForPicture(new PictureOptions { WidthM = 3f, Pitch = 0.25f }, "A.PNG");
            var b = SignLink.Signs.SignRecipe.ForPicture(new PictureOptions { WidthM = 2f, Pitch = 0.125f }, "b.gif");
            var p = SignLink.Signs.SignRecipe.ForPiece(SignLink.Signs.SignRecipe.Kinds.Art, GlowMode.Lit, "p");
            var held = new SignLink.UI.Held { HasSign = true, Empty = false, Recipe = a, Text = "x", Made = "x", Bytes = 9 };
            var vanilla = new SignLink.UI.Held { HasSign = true, Empty = false, Recipe = SignLink.Signs.SignRecipe.None, Text = "Bob's", Made = "Bob's", Bytes = 5 };
            var empty = new SignLink.UI.Held { HasSign = true, Empty = true };
            Func<SignLink.Signs.SignRecipe, SignLink.UI.Choice> pick = r => new SignLink.UI.Choice { Recipe = r };
            Check(SignLink.UI.Change.HidesHeld(held, pick(b)) && SignLink.UI.Change.HidesHeld(held, pick(p)) &&
                  SignLink.UI.Change.HidesHeld(vanilla, pick(b)),
                  "veil: another picture or a piece over what a sign holds hides it");
            Check(!SignLink.UI.Change.HidesHeld(held, pick(a2)) && !SignLink.UI.Change.HidesHeld(held, pick(SignLink.Signs.SignRecipe.None)) &&
                  !SignLink.UI.Change.HidesHeld(held, new SignLink.UI.Choice { Recipe = SignLink.Signs.SignRecipe.ForText(), Text = "new" }) &&
                  !SignLink.UI.Change.HidesHeld(empty, pick(b)) && !SignLink.UI.Change.HidesHeld(new SignLink.UI.Held(), pick(b)),
                  "veil: the held picture again, no pick, a text, an empty sign or no sign hide nothing");
            Console.WriteLine("veil checked");
        }

        // 0.20.106: Move the sign weighs the support as the game does (SupportMath, WearNTear.UpdateSupport): it asked only
        // whether anything was there, and a sign moved far from what held it broke (the author: I kept pressing
        // until the signpost broke at random). Wood: at most 100, at least 10, losing 0.2 a metre sideways, 0.125
        // downwards
        {
            Func<double, double, bool> near = (a, b) => Math.Abs(a - b) < 1e-6;
            Func<double, double, double, double, double, SignLink.Signs.SupportMath.Supporter> by = (sup, d, x, y, z) =>
                new SignLink.Signs.SupportMath.Supporter { Support = sup, Distance = d, Px = x, Py = y, Pz = z };
            Func<SignLink.Signs.SupportMath.Supporter[], double> wood = s => SignLink.Signs.SupportMath.Support(s, 100, 0.2, 0.125);
            Check(near(wood(new[] { by(50, 0.6, 0.5, 0, 0) }), 44), "support: beside it, the sideways loss over the distance");
            Check(near(wood(new[] { by(50, 0.4, 0, -0.3, 0) }), 47.5), "support: under it, the downward loss");
            Check(wood(new[] { by(50, 2.1, 0.5, 0, 0) }) < wood(new[] { by(50, 0.6, 0.5, 0, 0) }), "support: farther from what holds it, less");
            Check(near(wood(new[] { by(50, 0.6, 0.5, 0, 0), by(50, 0.6, -0.5, 0, 0) }), 46.25) &&
                  near(wood(new[] { by(50, 0.6, 0.5, 0, 0), by(50, 0.6, 0.5, 0, 0.1) }), 44),
                  "support: held from both sides, the downward loss; from one side, the sideways");
            Check(near(wood(new[] { by(1000, 0.2, 0, -0.1, 0) }), 100) && near(wood(new SignLink.Signs.SupportMath.Supporter[0]), 0) &&
                  near(wood(new[] { by(50, 0.6, 0, 0.3, 0) }), 44),
                  "support: never more than its material holds; nothing there, none; over it, sideways only");
            Check(SignLink.Signs.SupportMath.Refuse(30, 9, 10) && !SignLink.Signs.SupportMath.Refuse(8, 9, 10) &&
                  !SignLink.Signs.SupportMath.Refuse(30, 20, 10) && SignLink.Signs.SupportMath.Refuse(30, 10.2, 10),
                  "support: a step is refused when it leaves too little (with a margin), unless it holds better than before");
            Console.WriteLine("support checked");
        }

        // 0.20.111: the body scrolled on and on into empty room (the author: in 3 Place I can scroll down
        // forever and empty content waits there): its least height followed the scroll (0.20.84) and the scroll's own
        // view is a little shorter than its rect, so every notch made room for the next. It holds at most the height it
        // had, so content that got shorter while scrolled to its end still does not pull the scroll back
        {
            float natural = 900f, rect = 600f, view = 596f, scrollY = 0f, last = 0f, content = 0f;
            for (int i = 0; i < 200; i++)
            {
                float least = SignLink.UI.WindowSizing.BodyLeast(scrollY, rect, last, false);
                content = Math.Max(natural, least);
                scrollY = Math.Min(scrollY + 30f, Math.Max(0f, content - view));
                last = content;
            }
            Check(Math.Abs(content - natural) < 0.01f, $"body: scrolling down makes no room of its own ({content} for {natural})");
            natural = 700f;                                                 // shorter, scrolled to its end
            content = Math.Max(natural, SignLink.UI.WindowSizing.BodyLeast(scrollY, rect, last, false));
            Check(Math.Abs(content - 900f) < 0.01f, "body: shorter content scrolled to its end keeps its height (no pull back)");
            Check(Math.Abs(SignLink.UI.WindowSizing.BodyLeast(0f, rect, 900f, false)) < 0.01f &&
                  Math.Abs(SignLink.UI.WindowSizing.BodyLeast(0f, rect, 900f, true) - 900f) < 0.01f,
                  "body: scrolled to the top the room goes; while steady (held, typed) it stays");
            Console.WriteLine("body scroll checked");
        }

        // 0.20.122: a Resolution one pixel more at the width that many pixels take at the same pixel size (Fine Width
        // off): the plan keeps that pixel size and that many pixels, a pixel size TMP reads exactly staying one (a spike:
        // even a 1/32 grid left 20-60 cm steps)
        {
            var fo = new PictureOptions { Detail = 320, DetailAuto = false, WidthM = 3.5f };
            PicturePlan a = fo.PlanFor(1000, 750);
            fo.Detail = a.W + 1;
            fo.WidthM = (a.W + 1) * a.Pitch / 40f;
            PicturePlan b = fo.PlanFor(1000, 750);
            Check(b.W == a.W + 1 && Math.Abs(b.Pitch - a.Pitch) < 1e-6 && Math.Abs(b.WidthM - a.WidthM - a.Pitch / 40f) < 1e-5,
                  $"fine width: a pixel more at the same pixel size ({a.W} x {a.Pitch} -> {b.W} x {b.Pitch})");
            Console.WriteLine("fine width checked");
        }

        // 0.20.125: while a picture's new size is being counted, what it costs is the last count scaled by its pixels
        // (the author: the calculation runs 3-4 times or keeps clicking the button below, which flashes yellow): the
        // button and the cost line stay steady instead of waiting for every count
        {
            Check(SignLink.Pictures.SaveCost.Scaled(1000, 100 * 50, 101 * 51) == 1030 && SignLink.Pictures.SaveCost.Scaled(1000, 100 * 50, 100 * 50) == 1000 &&
                  SignLink.Pictures.SaveCost.Scaled(1000, 100 * 50, 50 * 25) == 250,
                  "scaled: the last count by the pixels now over the pixels then");
            Check(SignLink.Pictures.SaveCost.Scaled(1000, 0, 500) == -1 && SignLink.Pictures.SaveCost.Scaled(-1, 100, 500) == -1,
                  "scaled: nothing counted, nothing to scale");
            Console.WriteLine("scaled cost checked");
        }

        // 0.20.126: Fine Width on, a drag or a typed size is fine too (the author: dragged with the mouse it should
        // not click in steps but be fine-tuned too): the exact pixel nearest the size over the Resolution the drag
        // started with, then the
        // pixels that size takes at it, so the width follows within half a pixel and the Resolution stays near
        {
            Check(PictureOptions.FineDetail(3f, 120, 500, out float p0) == 120 && p0 == 1f, "fine size: an exact size keeps the Resolution");
            bool close = true, near = true;
            for (int cm = 300; cm <= 600; cm++)
            {
                float t = cm / 100f;
                int w = PictureOptions.FineDetail(t, 120, 500, out float p);
                double got = w * p / 40.0;
                if (Math.Abs(got - t) > p / 80.0 + 1e-4) close = false;     // within half a pixel
                if (Math.Abs(w - 120) > 15) near = false;
            }
            Check(close, "fine size: within half a pixel of what was asked");
            Check(near, "fine size: the Resolution stays near the one the drag started with");
            Check(PictureOptions.FineDetail(3.06f, 120, 500, out float p1) * p1 / 40.0 > 3.04 &&
                  PictureOptions.PitchFor(3.06f, 120) * 120 / 40.0 == 3.0, "fine size: finer than the steps of the same data");
            int capped = PictureOptions.FineDetail(30f, 120, 130, out float p2);
            Check(capped <= 130 && capped >= 8 && PictureOptions.ValidPitch(p2), "fine size: never over the picture's own pixels");
            Check(PictureOptions.FineDetail(0.3f, 120, 500, out float p3) >= 8 && PictureOptions.ValidPitch(p3), "fine size: a tiny one");
            Console.WriteLine("fine size checked");
        }

        // 0.20.128: the Resolution is the player's (the author: when I change the size the KB grow and I cannot save
        // over it ... when I edit and lower the size, its density gets very bad; chose: the Resolution is yours): a size
        // never rewrites it, the plan takes the exact size with the pixels nearest it, and a cap keeps sizing from going over
        {
            var fo = new PictureOptions { Detail = 120, DetailAuto = false, WidthM = 3.06f, Fine = true };
            PicturePlan fp = fo.PlanFor(1200, 900);
            Check(Math.Abs(fp.W * fp.Pitch / 40.0 - 3.06) <= fp.Pitch / 80.0 + 1e-4 && fp.W != 120 && Math.Abs(fp.W - 120) <= 15 && fo.Detail == 120,
                  "your resolution: the exact size with the pixels nearest it, the Resolution kept");
            fo.Fine = false;
            Check(fo.PlanFor(1200, 900).W == 120, "your resolution: Fine Width off, the pixels are the Resolution");
            fo.Fine = true;
            fo.FineCap = 120;
            PicturePlan cp = fo.PlanFor(1200, 900);
            Check(cp.W <= 120 && Math.Abs(cp.W * cp.Pitch / 40.0 - 3.06) <= cp.Pitch / 80.0 + 1e-4,
                  "your resolution: never over the cap, the size still exact");
            int cw = PictureOptions.FineDetail(3.06f, 120, 120, out float cpitch);
            Check(cw <= 120 && Math.Abs(cw * cpitch / 40.0 - 3.06) <= cpitch / 80.0 + 1e-4, "fine size: capped, a bigger pixel keeps the size");
            Func<int, long> est = w => 200000L * w * (int)Math.Round(w * 0.75) / (120 * 90);
            int most = PictureOptions.MostWithin(200000, 120 * 90, 300000, 0.75);
            Check(most > 120 && est(most) <= 300000 && est(most + 1) > 300000, "the cap: the most pixels across within the bytes");
            Check(PictureOptions.MostWithin(0, 100, 300000, 0.75) == 0 && PictureOptions.MostWithin(1000, 0, 300000, 0.75) == 0,
                  "the cap: nothing counted, no cap");
            // a size whose exact pixels are far apart (320 across at 1.2 m: 384 or 192): fewer pixels make it, never more
            // (0.20.183; it kept the Resolution and stepped to 1.0 m before)
            var tiny = new PictureOptions { Detail = 320, DetailAuto = false, WidthM = 1.2f, Fine = true };
            PicturePlan tp = tiny.PlanFor(3200, 2400);
            Check(tp.W == 192 && Math.Abs(tp.W * tp.Pitch / 40.0 - 1.2) < 0.01 && PictureOptions.ValidPitch(tp.Pitch),
                  "your resolution: a size it cannot make takes fewer pixels, exactly");
            var cappedFar = new PictureOptions { Detail = 200, DetailAuto = false, WidthM = 1.3f, Fine = true, FineCap = 200 };
            PicturePlan cf = cappedFar.PlanFor(3200, 2400);
            Check(cf.W <= 200 && Math.Abs(cf.W * cf.Pitch / 40.0 - 1.3) <= 0.025, "your resolution: under the cap too, never more pixels");
            var near = new PictureOptions { Detail = 200, DetailAuto = false, WidthM = 1.26f, Fine = true, FineCap = 200 };
            Check(near.PlanFor(3200, 2400).W == 200, "your resolution: a step 2.5 cm or less from the size keeps every pixel");
            // the author's case: 640 across took 2.2 m, then 3.6 m; now every size between, within 2.5 cm, pixels never up
            {
                double last = -1, gap = 0;
                bool within = true, never = true;
                for (int cm = 220; cm <= 360; cm++)
                {
                    var big = new PictureOptions { Detail = 640, DetailAuto = false, WidthM = cm / 100f, Fine = true };
                    PicturePlan bp = big.PlanFor(1000, 790);
                    double m = bp.W * bp.Pitch / 40.0;
                    if (Math.Abs(m - cm / 100.0) > 0.025 + 1e-6) within = false;
                    if (bp.W > 640 * 1.1) never = false;                // Fine's own 10 % (FineSpread)
                    if (last >= 0) gap = Math.Max(gap, m - last);
                    last = m;
                }
                Check(within && never && gap < 0.05, $"your resolution: 640 across, 2.2-3.6 m in 1 cm steps, every size made, never past Fine's 10 % (largest step {gap:0.###} m)");
            }
            Console.WriteLine("your resolution checked");
        }

        // 0.20.190: one rule for the content's own changes (3 Place's Changes block and the footer's words)
        {
            var held = new SignLink.UI.Held { HasSign = true, Empty = false, Fit = new Fit { Scale = 1.5, Right = 2, Up = -3, Out = 0 },
                                              Recipe = SignLink.Signs.SignRecipe.Parse("art|Lit|SignLink.Art.pixel/Bat (moving, hanging and flapping).anim.txt") };
            var same = new SignLink.UI.Choice { Fit = held.Fit, Recipe = held.Recipe.Copy(), Light = SignLink.UI.Change.LightOf(held) };
            Check(!SignLink.UI.Change.SizeChanged(held, same) && !SignLink.UI.Change.PlaceChanged(held, same) && !SignLink.UI.Change.LightChanged(held, same),
                  "content changes: the held item as it is changes nothing");
            var shifted = same.Copy();
            shifted.Fit = new Fit { Scale = 1.5, Right = 2.01, Up = -3, Out = 0, Tilt = 30 };
            Check(!SignLink.UI.Change.SizeChanged(held, shifted) && SignLink.UI.Change.PlaceChanged(held, shifted),
                  "content changes: a place along the face is the content's; a tilt is not (Change.SamePose)");
            var bigger = same.Copy();
            bigger.Fit = new Fit { Scale = 1.6, Right = 2, Up = -3 };
            Check(SignLink.UI.Change.SizeChanged(held, bigger) && !SignLink.UI.Change.PlaceChanged(held, bigger), "content changes: a size");
            var lit = same.Copy();
            lit.Light = SignLink.Pictures.GlowMode.Glowing;
            Check(SignLink.UI.Change.LightChanged(held, lit), "content changes: a light");
            var none = new SignLink.UI.Choice { Fit = held.Fit };
            Check(!SignLink.UI.Change.LightChanged(held, none), "content changes: nothing picked, as made: no light change");
            none.Light = SignLink.Pictures.GlowMode.Lit;
            Check(SignLink.UI.Change.LightChanged(held, none), "content changes: nothing picked, a light asked: a change");
            Console.WriteLine("content changes checked");
        }

        // 0.20.133: the Resolution moves only where the density does (the author: the slider should skip the
        // dead ranges where the density does not even change, and round a number typed into the field): the row snaps a
        // Resolution to the pixels it really makes at this size (PictureOptions.Achievable), which make themselves again
        {
            bool fixedOk = true, planOk = true, monoOk = true, skips = false;
            string firstBad = null;
            foreach (float wm in new[] { 0.5f, 1.2f, 2f, 2.5f, 3.06f, 6f, 12f })
                foreach (int cap in new[] { 0, 150 })
                {
                    var ro = new PictureOptions { WidthM = wm, Fine = true, FineCap = cap, DetailAuto = false };
                    int top = Math.Min(PictureOptions.MaxDetail(1200, 900), 600), prev = 0;
                    for (int d = 8; d <= top; d++)
                    {
                        int a = ro.Achievable(d, 1200, 900);
                        bool bad = false;
                        if (ro.Achievable(a, 1200, 900) != a) { fixedOk = false; bad = true; }
                        ro.Detail = a;
                        if (ro.PlanFor(1200, 900).W != a) { planOk = false; bad = true; }
                        if (a < prev) { monoOk = false; bad = true; }
                        if (bad && firstBad == null) firstBad = $"{wm} m, cap {cap}: {d} -> {a} (before {prev})";
                        if (a != d) skips = true;
                        prev = a;
                    }
                }
            Check(fixedOk, "resolution snaps: a snapped Resolution makes itself again " + firstBad);
            Check(planOk, "resolution snaps: the plan takes exactly the snapped Resolution (no shift left to say) " + firstBad);
            Check(monoOk, "resolution snaps: a bigger Resolution never makes fewer pixels " + firstBad);
            Check(skips, "resolution snaps: there are dead stretches to skip");
            var off = new PictureOptions { WidthM = 2f, Fine = false, DetailAuto = false };
            Check(off.Achievable(101, 1200, 900) == 101 && off.Achievable(5000, 1200, 900) == PictureOptions.MaxDetail(1200, 900),
                  "resolution snaps: Fine Width off, every Resolution is its own");
            // Shift + wheel on the row: a notch goes to the next Resolution whose density differs, none skipped
            var wo = new PictureOptions { WidthM = 2f, Fine = true, DetailAuto = false };
            Func<float, float> snapRes = v => wo.Achievable((int)Math.Round(v), 1200, 900);
            int at = wo.Achievable(100, 1200, 900);
            int up = (int)SignLink.UI.FineStep.Notch(at, -1f, 4f, "0", 8f, 1000f, snapRes), down = (int)SignLink.UI.FineStep.Notch(at, 1f, 4f, "0", 8f, 1000f, snapRes);
            bool between = true;
            for (int d = down + 1; d < up; d++)
            {
                int a = wo.Achievable(d, 1200, 900);
                if (a != at && a != up && a != down) between = false;
            }
            Check(down < at && at < up && wo.Achievable(up, 1200, 900) == up && wo.Achievable(down, 1200, 900) == down && between,
                  $"resolution snaps: Shift + wheel goes to the next density each way ({down} < {at} < {up})");
            Console.WriteLine("resolution snaps checked");
        }

        // 0.20.134: Fine Width's cap settles (the author: the bug is back, I cannot save over it
        // because the button is grey, it notices neither the tiny width change nor my density change): made fresh from
        // every count, the cap and the plan
        // fed each other and went 109, 110, 109, ... for good, so a press waiting for the last count never went on. Now a
        // count only lowers it (PictureOptions.NextCap). A photo-like picture, really encoded, at every size 1-8 m.
        {
            int cw0 = 1200, ch0 = 900;
            var cr = new Random(7);
            var crgba = new byte[cw0 * ch0 * 4];
            for (int y = 0; y < ch0; y++)
                for (int x = 0; x < cw0; x++)
                {
                    int i = (y * cw0 + x) * 4;
                    double cv = 0.5 + 0.25 * Math.Sin(x / 37.0) * Math.Cos(y / 23.0) + 0.15 * Math.Sin((x + y) / 11.0);
                    crgba[i] = (byte)Math.Max(0, Math.Min(255, cv * 255 + cr.Next(-20, 21)));
                    crgba[i + 1] = (byte)Math.Max(0, Math.Min(255, (1 - cv) * 200 + cr.Next(-20, 21)));
                    crgba[i + 2] = (byte)Math.Max(0, Math.Min(255, x * 255.0 / cw0));
                    crgba[i + 3] = 255;
                }
            var counts = new Dictionary<string, long>();
            long CountOf(PictureOptions co, PicturePlan p)
            {
                string k = $"{p.W}x{p.H}|{p.Pitch}";
                if (counts.TryGetValue(k, out long got)) return got;
                int[] px = PixelText.ForCover(Resampler.Grid(crgba, cw0, ch0, p.W, p.H, true), co.Cover);
                co.Origin(p, out double ccx, out double cbottom);
                return counts[k] = PixelText.SaveBytes(PixelText.Bands(px, p.W, p.H, p.Pitch, ccx, cbottom, PixelText.GlyphSize(p.Pitch, co.Cover), co.MaxKb));
            }
            long aim = (long)(120 * 1024 * 0.98);
            double caspect = ch0 / (double)cw0;
            int capSizes = 0, freshStuck = 0, nextStuck = 0;
            string stuckAt = null;
            for (int t = 0; t <= 350; t++)
            {
                float wm = 1f + t * 0.02f;
                capSizes++;
                foreach (bool fresh in new[] { true, false })
                {
                    var co = new PictureOptions { Fine = true, WidthM = wm, Detail = 109, DetailAuto = false, Cover = 2f };
                    int own = Math.Min(co.Detail, PictureOptions.MaxDetail(cw0, ch0)), cap = 0;
                    PicturePlan last = co.PlanFor(cw0, ch0);
                    bool settled = false;
                    for (int it = 0; it < 12 && !settled; it++)
                    {
                        long bytes = CountOf(co, last);
                        if (fresh)                                          // as before: from every count afresh
                        {
                            int most = PictureOptions.MostWithin(bytes, last.Pixels, aim, caspect);
                            cap = most <= 0 ? 0 : Math.Max(own, most);
                        }
                        else cap = PictureOptions.NextCap(cap, own, bytes, last.Pixels, aim, caspect);
                        co.FineCap = cap;
                        PicturePlan p = co.PlanFor(cw0, ch0);
                        settled = p.W == last.W && p.H == last.H && p.Pitch == last.Pitch;
                        last = p;
                    }
                    if (!settled && fresh) freshStuck++;
                    if (!settled && !fresh)
                    {
                        nextStuck++;
                        if (stuckAt == null) stuckAt = $"{wm:0.00} m";
                    }
                }
            }
            Check(freshStuck > 0, $"the cap: made fresh from every count, some sizes never settle ({freshStuck} of {capSizes}: the case this guards)");
            Check(nextStuck == 0, $"the cap: lowered only, every size settles within a few counts ({nextStuck} do not, first {stuckAt})");
            Check(PictureOptions.NextCap(110, 100, 1000, 100, 1000000, 0.75) == 110, "the cap: a count never raises it");
            Check(PictureOptions.NextCap(0, 100, 1000000, 10000, 100000, 0.75) == 100, "the cap: never under the Resolution");
            Check(PictureOptions.NextCap(110, 100, 0, 0, 100000, 0.75) == 110, "the cap: nothing counted keeps it");
            int first = PictureOptions.NextCap(0, 100, 100000, 10000, 200000, 0.75);
            Check(first == Math.Max(100, PictureOptions.MostWithin(100000, 10000, 200000, 0.75)), "the cap: the first count sets it");
            Console.WriteLine("fine cap checked");
        }

        // 0.20.129: the live preview shows the very texts the put writes (the author: show the end result instead of the
        // half-done old picture): SignFit.Put and SignFit.Refit take theirs from FitTexts, and so does the preview
        {
            var texts = new[] { "<line-height=0><size=3>A\n<voffset=5>B" };
            var fit = new Fit { Scale = 2, Right = 40, Up = 10 };
            FitTexts.Result put = FitTexts.ForPut(texts, 0, 0, true, fit, false);
            Fit sized = fit;
            sized.Scale = TextTransform.Snap(texts, 2);
            sized.Offset(0, 0, out double pdx, out double pdy);
            Check(put.Texts[0] == TextTransform.Apply(texts, sized.Scale, pdx, pdy)[0] && put.Fit.Scale == sized.Scale && put.Known,
                  "put texts: sized and moved as Put writes them");
            var plain = new[] { "Hello" };
            FitTexts.Result asMade = FitTexts.ForPut(plain, 3, 4, true, new Fit { Scale = 2, Right = 40, Tilt = 30, Turn = 10 }, false);
            Check(ReferenceEquals(asMade.Texts, plain) && asMade.Fit.Scale == 1 && asMade.Fit.Right == 0 && asMade.Fit.Tilt == 30 &&
                  asMade.Fit.Turn == 10 && !asMade.Known, "put texts: one SignLink cannot resize goes on as made, posed");
            Check(FitTexts.ForRefit(texts, put.Fit, 0, 0, true, put.Fit, GlowMode.AsMade).Texts == null,
                  "refit texts: the same fit and light change nothing");
            FitTexts.Result lit = FitTexts.ForRefit(texts, put.Fit, 0, 0, true, put.Fit, GlowMode.Glowing);
            Check(lit.Texts != null && lit.Texts[0] == SignGlow.Apply(texts, GlowMode.Glowing)[0], "refit texts: a new light only relights");
            Fit later = put.Fit;
            later.Right += 20;
            FitTexts.Result mv = FitTexts.ForRefit(texts, put.Fit, 0, 0, true, later, GlowMode.AsMade);
            Fit.Between(put.Fit, later, 0, 0, out double k2, out double dx2, out double dy2);
            Check(mv.Texts != null && mv.Texts[0] == TextTransform.Apply(texts, k2, dx2, dy2)[0] && mv.Fit.Right == later.Right,
                  "refit texts: moved as Refit writes them");
            FitTexts.Result held = FitTexts.ForRefit(plain, put.Fit, 1, 2, true, later, GlowMode.AsMade);
            Check(held.Texts == null && held.Fit.Right == put.Fit.Right && !held.Known, "refit texts: one SignLink cannot move stays where it is");
            Console.WriteLine("fit texts checked");
        }

        // 0.20.83: a tilt about the middle turns the sign about the middle of its text's plane (FloorPlan.FaceTip), so the
        // picture stays where it is whatever the tilt (the author: face up and face down put the sign at other heights:
        // about the board's middle the text, 7 cm in front of it, went 7 cm over it and 7 cm under it)
        {
            const double z = SignLink.Signs.FloorPlan.TextZ;
            bool still = true;
            foreach (double t in new[] { -90.0, -45, -10, 0, 10, 45, 90 })
            {
                SignLink.Signs.FloorPlan.FaceTip(t, z, out double fw, out double up);
                double a = t * Math.PI / 180;
                // the text's middle (0, 0, z) tipped by t about the board's middle, plus the move: where it was standing
                double ty = z * Math.Sin(a) + up, tz = z * Math.Cos(a) + fw;
                if (Math.Abs(ty) > 1e-12 || Math.Abs(tz - z) > 1e-12) still = false;
            }
            SignLink.Signs.FloorPlan.FaceTip(0, z, out double f0, out double u0);
            SignLink.Signs.FloorPlan.FaceTip(90, z, out double fu, out double uu);
            SignLink.Signs.FloorPlan.FaceTip(-90, z, out double fd, out double ud);
            Check(still, "face tip: the text's middle stays where it stood, at every tilt");
            Check(Math.Abs(f0) < 1e-12 && Math.Abs(u0) < 1e-12, "face tip: standing, no move");
            Check(Math.Abs(uu + z) < 1e-12 && Math.Abs(ud - z) < 1e-12 && Math.Abs(fu - z) < 1e-12 && Math.Abs(fd - z) < 1e-12,
                  "face tip: face up the board goes 7 cm down and back, face down 7 cm up and back: the text stays");
            Console.WriteLine("face tip checked");
        }

        // 0.20.132 (security review): the local API and the numbers read from the world. A clone's spacing and count are
        // checked before anything runs on the main thread (a step of 1e-9 m let millions of copies through); a body only
        // where a route reads one (411 there without a Content-Length); a head and a body each within a total deadline;
        // wrong passwords from this computer are refused but never lock it out, and the table forgets old entries; the
        // network mode answers local networks only; a clipboard holding the API password is not pasted onto a sign; a
        // fit, a pose or a base read from a sign (any player's game can write them) counts only when its numbers are sane
        {
            System.Net.IPAddress Ip(string s) => System.Net.IPAddress.Parse(s);
            float gap = 0.002f, depth = 0.25f;
            Check(SignLink.Signs.StackMath.CloneProblem(0f, 1, gap, depth) == null && SignLink.Signs.StackMath.CloneProblem(0.0005f, 499, gap, depth) == null &&
                  SignLink.Signs.StackMath.CloneProblem(0.0003f, 833, gap, depth) == null && SignLink.Signs.StackMath.CloneProblem(0.002f, 124, gap, depth) == null &&
                  SignLink.Signs.StackMath.CloneProblem(0f, 833, gap, depth) == null,
                  "clone: the default spacing and 0.3-2 mm pass while the copies fit one stack");
            Check(SignLink.Signs.StackMath.CloneProblem(1e-9f, 1, gap, depth) != null && SignLink.Signs.StackMath.CloneProblem(0.0002f, 1, gap, depth) != null &&
                  SignLink.Signs.StackMath.CloneProblem(0.0021f, 1, gap, depth) != null && SignLink.Signs.StackMath.CloneProblem(-0.0005f, 1, gap, depth) != null,
                  "clone: a spacing under 0.3 mm, over 2 mm or below 0 is refused");
            Check(SignLink.Signs.StackMath.CloneProblem(0f, 834, gap, depth) != null && SignLink.Signs.StackMath.CloneProblem(0.0005f, 500, gap, depth) != null &&
                  SignLink.Signs.StackMath.CloneProblem(0.0003f, 4000000, gap, depth) != null && SignLink.Signs.StackMath.CloneProblem(0.002f, 125, gap, depth) != null,
                  "clone: more copies than one stack holds at that spacing (the default one at its finest, 0.3 mm) are refused");
            Check(SignLink.Api.HttpRules.Parts("", '\u001d') == 1 && SignLink.Api.HttpRules.Parts("a\u001db\u001d", '\u001d') == 3 &&
                  SignLink.Api.HttpRules.Parts(new string('\u001d', 9), '\u001d') == 10, "clone: the texts counted as Split makes them, before splitting");

            Check(SignLink.Api.HttpRules.BodyCap("POST", "/screen") == 256L << 20 && SignLink.Api.HttpRules.BodyCap("POST", "/image") == 64L << 20 &&
                  SignLink.Api.HttpRules.BodyCap("POST", "/sign") == 16L << 20 && SignLink.Api.HttpRules.BodyCap("POST", "/anim") == 16L << 20 &&
                  SignLink.Api.HttpRules.BodyCap("POST", "/signs/clone") == 16L << 20, "body: the routes that read one, each its cap");
            Check(SignLink.Api.HttpRules.BodyCap("GET", "/sign") == 0 && SignLink.Api.HttpRules.BodyCap("POST", "/anim/stop") == 0 &&
                  SignLink.Api.HttpRules.BodyCap("POST", "/anim/unstore") == 0 && SignLink.Api.HttpRules.BodyCap("POST", "/screen/stop") == 0 &&
                  SignLink.Api.HttpRules.BodyCap("POST", "/signs/delete") == 0 && SignLink.Api.HttpRules.BodyCap("POST", "/pose") == 0 &&
                  SignLink.Api.HttpRules.BodyCap("POST", "/screens") == 0 && SignLink.Api.HttpRules.BodyCap("POST", "/nothing") == 0 &&
                  SignLink.Api.HttpRules.BodyCap("GET", "") == 0 && SignLink.Api.HttpRules.BodyCap("PUT", "/sign") == 0,
                  "body: none anywhere else (GETs, the stop endpoints, unknown paths): exact routes, no prefixes");
            Check(SignLink.Api.HttpRules.NeedsLength("POST", "/sign") && SignLink.Api.HttpRules.NeedsLength("POST", "/signs/clone") &&
                  !SignLink.Api.HttpRules.NeedsLength("POST", "/anim/stop") && !SignLink.Api.HttpRules.NeedsLength("GET", "/ping"),
                  "body: 411 without a Content-Length only where a body is read (a body-less POST to a stop endpoint stays fine)");
            Check(SignLink.Api.HttpRules.BodyMs(0) == 30000 && SignLink.Api.HttpRules.BodyMs(16L << 20) == 30000 && SignLink.Api.HttpRules.BodyMs(256L << 20) == 128000,
                  "body: a total deadline of 30 s, or 2 MB a second for a larger one");
            Check(SignLink.Api.HttpRules.ReadWait(0, 10000, 2000) == 2000 && SignLink.Api.HttpRules.ReadWait(7000, 10000, 5000) == 3000 &&
                  SignLink.Api.HttpRules.ReadWait(9999, 10000, 5000) == 1 && SignLink.Api.HttpRules.ReadWait(10000, 10000, 5000) == 0 &&
                  SignLink.Api.HttpRules.ReadWait(15000, 10000, 5000) == 0, "head and body: a read waits at most what is left of the whole");

            Check(SignLink.Api.HttpRules.Loopback(Ip("127.0.0.1")) && SignLink.Api.HttpRules.Loopback(Ip("127.5.6.7")) &&
                  SignLink.Api.HttpRules.Loopback(Ip("::1")) && SignLink.Api.HttpRules.Loopback(Ip("::ffff:127.0.0.1")) &&
                  !SignLink.Api.HttpRules.Loopback(Ip("192.168.1.2")) && !SignLink.Api.HttpRules.Loopback(Ip("::ffff:10.0.0.1")) &&
                  !SignLink.Api.HttpRules.Loopback(null), "loopback: IPv4, IPv6 and IPv4-mapped");
            var lockout = new SignLink.Api.Lockout();
            var t0 = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
            bool any = false;
            for (int i = 0; i < 10; i++)
                any |= lockout.Strike(Ip("127.0.0.1"), t0) | lockout.Strike(Ip("::ffff:127.0.0.1"), t0) | lockout.Strike(Ip("::1"), t0);
            Check(!any && !lockout.Locked(Ip("127.0.0.1"), t0) && !lockout.Locked(Ip("::ffff:127.0.0.1"), t0) && lockout.Count == 0,
                  "lock-out: wrong passwords from this computer are refused but never counted");
            System.Net.IPAddress lan = Ip("192.168.1.20");
            for (int i = 0; i < 4; i++) any |= lockout.Strike(lan, t0.AddSeconds(i));
            Check(!any && !lockout.Locked(lan, t0.AddSeconds(4)), "lock-out: four wrong ones do not lock");
            Check(lockout.Strike(lan, t0.AddSeconds(4)) && lockout.Locked(lan, t0.AddSeconds(5)) && lockout.Locked(Ip("::ffff:192.168.1.20"), t0.AddSeconds(5)) &&
                  !lockout.Locked(lan, t0.AddSeconds(65)), "lock-out: the fifth within a minute locks that address for a minute");
            for (int i = 0; i < 5; i++) lockout.Strike(Ip("10.0.0.9"), t0.AddSeconds(100 + i));   // locked until t0 + 164 s
            lockout.Strike(Ip("10.0.0.7"), t0.AddSeconds(100));
            lockout.Strike(Ip("10.0.0.10"), t0.AddSeconds(162));
            Check(lockout.Count == 2 && lockout.Locked(Ip("10.0.0.9"), t0.AddSeconds(162)),
                  $"lock-out: a strike prunes entries over a minute old that are not locked ({lockout.Count} left)");

            string[] near = { "127.0.0.1", "10.1.2.3", "172.16.0.1", "172.31.255.254", "192.168.0.10", "169.254.1.1", "100.64.0.1",
                              "100.127.255.254", "::1", "fc00::1", "fd12:3456::1", "fe80::1", "febf::1", "::ffff:192.168.1.5", "::ffff:100.100.1.1" };
            string[] far = { "8.8.8.8", "172.15.255.255", "172.32.0.1", "192.169.0.1", "169.255.0.1", "100.63.255.255", "100.128.0.1", "11.0.0.1",
                             "2001:db8::1", "fec0::1", "::ffff:8.8.8.8", "::", "0.0.0.0", "224.0.0.1" };
            string wrongNear = string.Join(" ", near.Where(a => !SignLink.Api.HttpRules.LocalNetwork(Ip(a))));
            string wrongFar = string.Join(" ", far.Where(a => SignLink.Api.HttpRules.LocalNetwork(Ip(a))));
            Check(wrongNear.Length == 0 && wrongFar.Length == 0 && !SignLink.Api.HttpRules.LocalNetwork(null),
                  $"network mode: loopback, private, link-local, CGNAT and ULA peers only (refused: {wrongNear}; let in: {wrongFar})");
            var throttle = new SignLink.Api.Throttle(TimeSpan.FromMinutes(1));
            Check(throttle.Allow("8.8.8.8", t0) && !throttle.Allow("8.8.8.8", t0.AddSeconds(30)) && throttle.Allow("1.1.1.1", t0.AddSeconds(30)) &&
                  throttle.Allow("8.8.8.8", t0.AddSeconds(61)), "network mode: a refused peer is logged once a minute at most");

            Check(SignLink.Api.Password.In("abc SECRETpassword1 def", "SECRETpassword1") && SignLink.Api.Password.In("SECRETpassword1", "SECRETpassword1") &&
                  !SignLink.Api.Password.In("abc", "SECRETpassword1") && !SignLink.Api.Password.In("anything", "") &&
                  !SignLink.Api.Password.In("anything", null) && !SignLink.Api.Password.In(null, "SECRETpassword1"),
                  "paste: a clipboard holding the API password is refused (none set: nothing is)");

            Check(Fit.Sane(1, 0, 0, 0) && Fit.Sane(0.25, 12000, -12000, 400) && Fit.Sane(4, 40000, 40000, -40000),
                  "world numbers: the fits SignLink writes are sane (300 m offsets, as the API allows, too)");
            Check(!Fit.Sane(0, 0, 0, 0) && !Fit.Sane(-1, 0, 0, 0) && !Fit.Sane(double.PositiveInfinity, 0, 0, 0) && !Fit.Sane(double.NaN, 0, 0, 0) &&
                  !Fit.Sane(1e9, 0, 0, 0) && !Fit.Sane(1e-9, 0, 0, 0) && !Fit.Sane(1, double.NaN, 0, 0) && !Fit.Sane(1, 0, double.NegativeInfinity, 0) &&
                  !Fit.Sane(1, 1e7, 0, 0) && !Fit.Sane(1, 0, 0, 1e7), "world numbers: a fit with no size, a NaN, an infinity or a huge number is none");
            Check(Fit.SanePoint(0, 0) && Fit.SanePoint(-4000, 1200) && !Fit.SanePoint(double.NaN, 0) && !Fit.SanePoint(0, 1e12),
                  "world numbers: the content's middle as made");
            Check(SignLink.Signs.StackMath.SanePose(0, 0, 0, 6) && SignLink.Signs.StackMath.SanePose(90, 180, 0, 6) &&
                  SignLink.Signs.StackMath.SanePose(-90, -179.5f, 0.3f, 6) && !SignLink.Signs.StackMath.SanePose(float.NaN, 0, 0, 6) &&
                  !SignLink.Signs.StackMath.SanePose(120, 0, 0, 6) && !SignLink.Signs.StackMath.SanePose(0, float.PositiveInfinity, 0, 6) &&
                  !SignLink.Signs.StackMath.SanePose(0, 1e9f, 0, 6) && !SignLink.Signs.StackMath.SanePose(0, 0, 1e6f, 6) &&
                  !SignLink.Signs.StackMath.SanePose(0, 0, float.NaN, 6), "world numbers: a kept pose is finite, its tilt -90..90, its shift within reach");
            Check(SignLink.Signs.StackMath.SaneBase(0.3f, -0.2f, 0.1f, 0, 0.7071068f, 0, 0.7071068f, 6) &&
                  SignLink.Signs.StackMath.SaneBase(5.5f, 0, 0, 0, 0, 0, 1, 6), "world numbers: a kept base near the sign, with a unit rotation");
            Check(!SignLink.Signs.StackMath.SaneBase(7, 0, 0, 0, 0, 0, 1, 6) && !SignLink.Signs.StackMath.SaneBase(1e6f, 0, 0, 0, 0, 0, 1, 6) &&
                  !SignLink.Signs.StackMath.SaneBase(float.NaN, 0, 0, 0, 0, 0, 1, 6) && !SignLink.Signs.StackMath.SaneBase(0, 0, 0, 2, 0, 0, 0, 6) &&
                  !SignLink.Signs.StackMath.SaneBase(0, 0, 0, 0.1f, 0, 0, 0.1f, 6) && !SignLink.Signs.StackMath.SaneBase(0, 0, 0, float.NaN, 0, 0, 1, 6) &&
                  !SignLink.Signs.StackMath.SaneBase(0, 0, 0, 0, 0, 0, 0, 6),
                  "world numbers: a base far off, not a number, or without a unit rotation is none (as if never kept)");
            SignLink.Signs.SignClip huge = SignLink.Signs.SignClip.Unpack("\u0002SignLink clip 1\nrecipe=\nname=\nfit=1e300,0,0,0,0,0,0,0,0,1\n\u0003abc");
            SignLink.Signs.SignClip far0 = SignLink.Signs.SignClip.Unpack("\u0002SignLink clip 1\nrecipe=\nname=\nfit=1,0,0,0,0,0,0,1e300,0,1\n\u0003abc");
            Check(!huge.HasFit && !far0.HasFit && huge.Texts.Count == 1 && huge.Texts[0] == "abc", "clip: a pasted fit with a huge size or middle is no fit");
            Console.WriteLine("security review checked");
        }

        // the plugin's GUID was bakterhaz.signlink until 0.20.43 (BepInEx names the config file after it): the old file
        // takes the new name once, settings and API password with it
        {
            string dir = Path.Combine(Path.GetTempPath(), "signlink-cfgfile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                const string now = "PetyusPolisz.SignLink.cfg";
                string oldPath = Path.Combine(dir, SignLink.ConfigMoves.OldFile), newPath = Path.Combine(dir, now);
                Check(SignLink.ConfigMoves.OldFile == "bakterhaz.signlink.cfg", "config file: the old name");
                Check(!SignLink.ConfigMoves.MoveFile(dir, now) && !File.Exists(newPath), "config file: nothing old, nothing moves");
                File.WriteAllText(oldPath, "[1 - Menu]\nScale = 1.3\n");
                Check(SignLink.ConfigMoves.MoveFile(dir, now) && !File.Exists(oldPath) && File.ReadAllText(newPath) == "[1 - Menu]\nScale = 1.3\n",
                      "config file: the old one takes the new name, as it was");
                File.WriteAllText(oldPath, "[1 - Menu]\nScale = 2\n");
                Check(!SignLink.ConfigMoves.MoveFile(dir, now) && File.Exists(oldPath) && File.ReadAllText(newPath) == "[1 - Menu]\nScale = 1.3\n",
                      "config file: a new one already there stays, the old one is left alone");
                Check(!SignLink.ConfigMoves.MoveFile(dir, SignLink.ConfigMoves.OldFile) && File.Exists(oldPath), "config file: the same name is no move");
            }
            finally { Directory.Delete(dir, true); }
            Console.WriteLine("config file name checked");
        }

        {
            // text templates (Snippets): the written lines land where asked by the reference model of a multi-line
            // sign (docs/sign-rendering.md 7c, engine.layout_lines), the anchors fix K, and the output is resizable
            GlyphData.Glyph sq = GlyphData.Find("■");
            Check(sq != null && sq.Font == GlyphData.Jp && Math.Abs(sq.Y1 - 0.78) < 1e-9, "snippets: the block glyph and its metrics");
            Check(Math.Abs(Snippets.K - 0.872) < 1e-9, "snippets: K from the anchors (size 1, NotoSansJP)");
            List<Snippets.Line> block = Snippets.Block(1.0, 0.5, 0xFF0000, 255);
            Check(block.Count == 1 && Math.Abs(block[0].Size * (2 * 0.8 + 4 * 0.0203) - 20) < 1e-6, "snippets: a 0.5 m block is 20 wu tall: S from the drawn height");
            Check(Math.Abs(block[0].Scale - 2.0) < 1e-9, "snippets: a 1 x 0.5 m block is scale 2");
            string text = Snippets.Compose(block);
            Check(text.StartsWith("<line-height=0>" + Snippets.Anchor + "\n") && text.EndsWith("\n" + Snippets.Anchor), "snippets: header and anchors");
            Check(text.Contains("<#f00>"), "snippets: the colour tag, short when exact");
            Check(TextTransform.Supports(new[] { text }, out string why), "snippets: the text can be resized and moved: " + why);
            var r = new Random(7);
            int bad = 0;
            foreach (string ch in new[] { "■", "→", "★", "ᚠ", "🐺", "═" })
                for (int i = 0; i < 20; i++)
                {
                    double h = 0.1 + r.NextDouble() * 3, x = r.NextDouble() * 10 - 5, y = r.NextDouble() * 10 - 5;
                    Snippets.Line l = Snippets.Symbol(ch, h, 0x336699, 128, r.Next(2) == 0, r.NextDouble() * 360 - 180, x, y)[0];
                    Snippets.Tags(l, out double sp, out double sv);
                    Snippets.Model(l.Glyph, sp, sv, l.Size, l.Scale, out double gx, out double gy);
                    if (Math.Abs(gx - x * 40) > 0.05 || Math.Abs(gy - y * 40) > 0.05) bad++;
                    double drawn = 2 * l.Size * (l.Glyph.Y1 - l.Glyph.Y0) + 4 * l.Glyph.Font.Bold * l.Size;
                    if (Math.Abs(drawn - h * 40) > 1e-6) bad++;
                }
            Check(bad == 0, $"snippets: {bad} symbol placements or sizes off");
            List<Snippets.Line> label = Snippets.Label("Hello", 0.3, 0xFFFFFF, 255, 1.0, -0.5);
            Check(label.Count == 1 && label[0].Text == "Hello" && label[0].Size > 0, "snippets: a label is one line");
            Snippets.Tags(label[0], out double lp, out double lv);
            Check(Math.Abs(lp - 40) < 2 && Math.Abs(2 * lv + label[0].Size * (0.741 - 0.003) - Snippets.K - (-20)) < 0.05,
                  "snippets: a label's capitals centre at 1 m right, 0.5 m down");
            List<Snippets.Line> pat = Snippets.Pattern("█", 4, 3, 0.25, 0x222222, 255, 0xDDDDDD, 255, alternate: true);
            Check(pat.Count == 12 && pat[0].Rgb == 0x222222 && pat[1].Rgb == 0xDDDDDD && pat[4].Rgb == 0xDDDDDD, "snippets: a 4 x 3 pattern alternates like a chessboard");
            Check(Math.Abs(pat[0].X + 15) < 1e-9 && Math.Abs(pat[0].Y - 10) < 1e-9 && Math.Abs(pat[11].X - 15) < 1e-9 && Math.Abs(pat[11].Y + 10) < 1e-9,
                  "snippets: the pattern's cells, 25 cm apart, centred on the sign");
            string inserted = Snippets.Insert(text, label, text.Length);
            Check(inserted.EndsWith("Hello\n" + Snippets.Anchor) && inserted.StartsWith(text.Substring(0, text.Length - Snippets.Anchor.Length)),
                  "snippets: inserting goes before the last anchor");
            Check(Snippets.Insert("plain words", block, 5).StartsWith("plain words\n<pos="), "snippets: into a free text the lines are appended");
            Check(Snippets.Insert("", block, 0) == text, "snippets: into an empty text: the structure");
            Check(Snippets.Symbol("nope", 1, 0, 255, false, 0).Count == 0 && GlyphData.Find("→") != null, "snippets: an unknown glyph gives no line");
            Console.WriteLine("snippets checked");
        }

        // review 0.20.132 (runtime): saved-animation blobs are read as they unpack (AnimBlob.TryUnpack), counted in UTF-16
        // characters: the same frames as decoding the whole and splitting it (TryUnpack before), refused past MaxReadChars;
        // StoredAnims.Store packs at most MaxChars (AnimBlob.Fits)
        {
            byte[] Raw(byte[] payload, int frames)
            {
                var packed = new MemoryStream();
                using (var gz = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionMode.Compress, true)) gz.Write(payload, 0, payload.Length);
                var o = new MemoryStream();
                using (var w = new BinaryWriter(o))
                {
                    w.Write(0x31414C53);                                    // "SLA1"
                    w.Write(2f);
                    w.Write(frames);
                    w.Write((int)packed.Length);
                    w.Write(packed.ToArray());
                }
                return o.ToArray();
            }
            var utf8 = System.Text.Encoding.UTF8;
            bool Same(byte[] payload)                                       // as decoding the whole read it
            {
                string[] want = utf8.GetString(payload).Split(SignLink.Animation.AnimBlob.FrameSep);
                return SignLink.Animation.AnimBlob.TryUnpack(Raw(payload, want.Length), out string[] got, out _) && got.SequenceEqual(want);
            }
            // multi-byte characters and separators across the 64 KB reads: block characters (3 bytes), emoji (4: a pair)
            var mixed = new List<string> { new string('█', 70000), string.Concat(Enumerable.Repeat("😀", 40000)), "", "<color=#ff0000>█</color>" };
            Check(SignLink.Animation.AnimBlob.TryUnpack(SignLink.Animation.AnimBlob.Pack(mixed, 3f), out string[] mixedBack, out float mixedFps) &&
                  mixedFps == 3f && mixedBack.SequenceEqual(mixed), "blob read as it unpacks: multi-byte characters across its reads");
            for (int cut = 65530; cut <= 65540; cut++)                     // a separator, a character's bytes at a read's end
            {
                var p = new List<byte>(utf8.GetBytes(new string('a', cut)));
                p.Add(0x1E);
                p.AddRange(utf8.GetBytes("█😀b"));
                p.Add(0x1E);
                Check(Same(p.ToArray()), $"blob read as it unpacks: a separator at byte {cut}");
                var q = new List<byte>(utf8.GetBytes(new string('a', cut)));
                q.AddRange(utf8.GetBytes("█😀"));
                Check(Same(q.ToArray()), $"blob read as it unpacks: a character split at byte {cut}");
            }
            // broken UTF-8 (a stray byte, a sequence cut short, at the end too) and a byte order mark, as decoding the whole
            Check(Same(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', 0xFF, (byte)'b', 0x1E, 0xE2, 0x96, (byte)'c', 0x1E, 0xC3 }),
                  "blob read as it unpacks: broken UTF-8 as decoding the whole read it");
            var cutShort = new List<byte>(utf8.GetBytes(new string('x', 65535)));
            cutShort.AddRange(new byte[] { 0xE2, 0x96, 0x1E, 0xF0, 0x9F });
            Check(Same(cutShort.ToArray()), "blob read as it unpacks: a sequence cut short at a read's end");
            // the frame count and a frame's length as before
            byte[] three = utf8.GetBytes("a\u001eb\u001ec");
            Check(!SignLink.Animation.AnimBlob.TryUnpack(Raw(three, 2), out _, out _) && !SignLink.Animation.AnimBlob.TryUnpack(Raw(three, 4), out _, out _) &&
                  SignLink.Animation.AnimBlob.TryUnpack(Raw(three, 3), out _, out _), "blob read as it unpacks: the frame count must match");
            Check(!SignLink.Animation.AnimBlob.TryUnpack(SignLink.Animation.AnimBlob.Pack(new[] { "a", new string('b', SignLink.Animation.AnimBlob.MaxFrameChars + 1) }, 1f), out _, out _),
                  "blob read as it unpacks: a frame over MaxFrameChars is refused");
            // counted in UTF-16 characters: 34 frames of 500 000 (17 M, 17 MB of UTF-8) are past MaxReadChars, 33 are not
            // (16.5 M: what versions before 0.20.132 stored and read, 16 MB of UTF-8, still plays; final review);
            // 12 frames of 500 000 block characters (6 M, 18 MB of UTF-8) are read
            string[] Frames(int k, char c) => Enumerable.Repeat(new string(c, 500000), k).ToArray();
            Check(!SignLink.Animation.AnimBlob.TryUnpack(SignLink.Animation.AnimBlob.Pack(Frames(34, 'a'), 1f), out _, out _),
                  "blob counted in characters: 17 M characters (17 MB of UTF-8) are past MaxReadChars");
            Check(SignLink.Animation.AnimBlob.TryUnpack(SignLink.Animation.AnimBlob.Pack(Frames(33, 'a'), 1f), out string[] at33, out _) && at33.Length == 33,
                  "blob counted in characters: 16.5 M characters (an older version's 16 MB) are read");
            Check(SignLink.Animation.AnimBlob.TryUnpack(SignLink.Animation.AnimBlob.Pack(Frames(12, '█'), 1f), out string[] blocks, out _) &&
                  blocks.Length == 12 && blocks[11].Length == 500000, "blob counted in characters: 6 M block characters (18 MB of UTF-8) are read");
            // what Store packs at most
            Check(SignLink.Animation.AnimBlob.Fits(new[] { new string('a', SignLink.Animation.AnimBlob.MaxChars - 1), "b" }, out long fitChars) &&
                  fitChars == SignLink.Animation.AnimBlob.MaxChars, "store: MaxChars characters fit");
            Check(!SignLink.Animation.AnimBlob.Fits(new[] { new string('a', SignLink.Animation.AnimBlob.MaxChars), "b" }, out _), "store: one more does not");
            Console.WriteLine("blob reading checked");
        }

        // ---- the picture pipeline's review (0.20.132): the row-streamed resampler, stoppable jobs, the tag scanner, the
        // GIF decoder's frame hand-over and the list's culling, each against the code it replaced (Oracles.cs) or by its
        // own rules
        {
            // Resampler.Grid made row by row from the RGBA bytes (no full-size float planes): the very pixels of the old one
            {
                var r = new Random(132);
                int same = 0, cases = 0;
                string first = null;
                foreach (var (w, h, W, H) in new[] { (64, 48, 20, 15), (64, 48, 64, 15), (64, 48, 21, 48), (64, 48, 64, 48), (37, 53, 11, 17),
                                                     (300, 7, 30, 3), (5, 301, 2, 30), (1, 1, 1, 1), (2, 9, 1, 3), (97, 61, 96, 60), (120, 80, 13, 80),
                                                     (31, 29, 31, 9), (400, 300, 103, 77), (16, 16, 32, 32) })
                    foreach (int alphaKind in new[] { 0, 1, 2, 3, 4 })          // random, all 255, all 128, all 0, a soft disc
                        foreach (bool opaque in new[] { false, true })
                        {
                            var rgba = new byte[w * h * 4];
                            r.NextBytes(rgba);
                            for (int i = 0; i < w * h; i++)
                            {
                                int x = i % w, y = i / w;
                                double d = Math.Sqrt((x - w / 2.0) * (x - w / 2.0) + (y - h / 2.0) * (y - h / 2.0)) / Math.Max(1, Math.Min(w, h) / 2.0);
                                if (alphaKind == 1) rgba[i * 4 + 3] = 255;
                                else if (alphaKind == 2) rgba[i * 4 + 3] = 128;
                                else if (alphaKind == 3) rgba[i * 4 + 3] = 0;
                                else if (alphaKind == 4) rgba[i * 4 + 3] = (byte)Math.Max(0, Math.Min(255, (int)(255 * (1.2 - d))));
                            }
                            int[] want = OldResampler.Grid(rgba, w, h, W, H, opaque);
                            int[] got = Resampler.Grid(rgba, w, h, W, H, opaque);
                            int[] got2 = Resampler.Grid(rgba, w, h, W, H, opaque, () => false);
                            cases++;
                            if (want.SequenceEqual(got) && want.SequenceEqual(got2)) same++;
                            else first ??= $"{w}x{h} -> {W}x{H}, alpha {alphaKind}, opaque {opaque}";
                        }
                Check(same == cases, $"grid by rows: {same} of {cases} grids are the old ones ({first})");
                var big = new byte[600 * 400 * 4];
                r.NextBytes(big);
                int calls = 0;
                string stopped = null;
                try { Resampler.Grid(big, 600, 400, 150, 100, false, () => ++calls > 3); }
                catch (OperationCanceledException e) { stopped = e.Message; }
                Check(stopped != null && calls == 4, $"grid by rows: a stale grid stops at the next row ({calls} asks, {stopped})");
                Console.WriteLine($"grid by rows checked ({cases} grids)");
            }

            // stoppable jobs: the work sees when it is no longer wanted (Abandon, a newer run, the key moved away), and a
            // cancelled run is dropped like a stale result: no fail message, the key not marked done
            {
                var job = new SignLink.UI.Job<string>();
                var got = new List<string>();
                var fails = new List<string>();
                void Drain()
                {
                    for (int i = 0; i < 400 && job.Busy; i++) { System.Threading.Thread.Sleep(5); SignLink.Api.MainThread.Drain(); }
                    SignLink.Api.MainThread.Drain();
                }
                Func<bool> seen = null;
                var started = new System.Threading.ManualResetEventSlim(false);
                var gate = new System.Threading.ManualResetEventSlim(false);
                Func<Func<Func<bool>, string>> Blocking(string result) => () => stale =>
                {
                    System.Threading.Volatile.Write(ref seen, stale);
                    started.Set();
                    gate.Wait();
                    if (stale()) throw new OperationCanceledException("stale");
                    return result;
                };
                job.WantStoppable("a", 0f, 0f, Blocking("A"), got.Add, e => fails.Add(e.Message));
                started.Wait(2000);
                Func<bool> runA = System.Threading.Volatile.Read(ref seen);
                Check(job.Busy && runA != null && !runA(), "stoppable: a run that is still wanted is not stale");
                job.Want("b", 0.1f, 0f, () => () => "B", got.Add, e => fails.Add(e.Message));
                Check(runA != null && runA(), "stoppable: the key moved away: stale");
                job.Want("a", 0.2f, 0f, () => () => "A2", got.Add, e => fails.Add(e.Message));
                Check(runA != null && !runA(), "stoppable: the key came back before it looked: not stale");
                job.Want("b", 0.3f, 0f, () => () => "B", got.Add, e => fails.Add(e.Message));
                gate.Set();
                Drain();
                Check(got.Count == 0 && fails.Count == 0 && !job.Busy && job.Done == null,
                      $"stoppable: a cancelled run is dropped quietly ({string.Join(",", got)} / {string.Join(",", fails)})");
                job.Want("b", 0.4f, 0f, () => () => "B", got.Add, e => fails.Add(e.Message));
                Drain();
                Check(got.Count == 1 && got[0] == "B" && job.Done == "b", "stoppable: the next key runs after a cancelled one");

                started.Reset();
                gate.Reset();
                job.WantStoppable("c", 1f, 0f, Blocking("C"), got.Add, e => fails.Add(e.Message));
                started.Wait(2000);
                Func<bool> runC = System.Threading.Volatile.Read(ref seen);
                job.Abandon();
                Check(runC != null && runC(), "stoppable: Abandon makes the running work stale");
                started.Reset();
                job.WantStoppable("c", 1.1f, 0f, () => stale => { System.Threading.Volatile.Write(ref seen, stale); started.Set(); gate.Wait(); return "C-new"; },
                                  got.Add, e => fails.Add(e.Message));
                started.Wait(2000);
                Func<bool> runC2 = System.Threading.Volatile.Read(ref seen);
                Check(runC != null && runC() && runC2 != null && !ReferenceEquals(runC, runC2) && !runC2(),
                      "stoppable: a newer run of the same key: the old one stale, the new one not");
                gate.Set();
                Drain();
                Check(got.Count == 2 && got[1] == "C-new" && fails.Count == 0 && job.Done == "c", $"stoppable: the newer run delivers ({string.Join(",", got)})");
                job.WantStoppable("d", 2f, 0f, () => stale => throw new InvalidOperationException("boom"), got.Add, e => fails.Add(e.Message));
                Drain();
                Check(fails.Count == 1 && fails[0] == "boom" && job.Done == "d", "stoppable: another failure is still reported");
                Console.WriteLine("stoppable jobs checked");
            }

            // PixelText's band loop asks between bands; the save bytes are the bands' own counts (no second UTF-8 count)
            {
                var px = new int[40 * 30];
                var r = new Random(5);
                for (int i = 0; i < px.Length; i++) px[i] = r.Next(4) == 0 ? -1 : r.Next(0x1000000);
                List<PixelText.Band> bands = PixelText.Bands(px, 40, 30, 1.0, 0, 0, PixelText.GlyphSize(1.0, 2), 4);
                Check(bands.Count > 1, $"bands: several ({bands.Count})");
                Check(PixelText.SaveBytes(bands) == SaveCost.Bytes(bands.ConvertAll(b => b.Text)), "save bytes: the bands' own counts are the UTF-8 count");
                List<PixelText.Band> again = PixelText.Bands(px, 40, 30, 1.0, 0, 0, PixelText.GlyphSize(1.0, 2), 4, stale: () => false);
                Check(again.Count == bands.Count && again.Select(b => b.Text).SequenceEqual(bands.Select(b => b.Text)), "bands: a stale test that never fires changes nothing");
                int asked = 0;
                string stopped = null;
                try { PixelText.Bands(px, 40, 30, 1.0, 0, 0, PixelText.GlyphSize(1.0, 2), 4, stale: () => ++asked > 1); }
                catch (OperationCanceledException e) { stopped = e.Message; }
                Check(stopped != null && asked == 2, $"bands: a stale encode stops before the next band ({asked} asks)");
                Console.WriteLine("bands checked");
            }

            // the tag scanner (TextTransform without regular expressions): the very results of the regex code, on random tag
            // soup, the vector pictures and every piece of the art library; quadratic and many-height texts stay fast
            {
                string[] tokens =
                {
                    "<size=2>", "<SIZE=1.5>", "< size = 3 >", "<size=+1>", "<size=-1>", "<size=2 >", "<size= 2>", "<size=1e3>", "<size=NaN>",
                    "<size=>", "<size>", "<size=0.75>", "<pos=3.25>", "<pos=-12>", "<POS=1>", "<indent=-2>", "<indent=0.5>", "<voffset=1.5>",
                    "<voffset=-0.75>", "<voffset=0>", "<line-height=0.5>", "<line-height=0.25>", "<line-height=1.0625>", "<Line-Height=0.5>",
                    "<line-height=abc>", "<line-height=-1>", "<line-height=0>", "<line-height=0.3>", "<#f00>", "<#12345678>", "< #fff >",
                    "<font=valheim_fonts/valheim-norse>", "</font>", "<FONT=x>", "<b>", "</b>", "<color=red>", "</color>", "<sprite name=x>",
                    "<sprite=1>", "<br>", "<İndent=2>", "<ſize=2>", "<Key=1>", "<u>", "<s>", "</s>", "<alpha=#80>", "<rotate=45>", "<scale=2>",
                    "<mark=#ff0>", "<>", "<<size=2>", "<size=2", ">", "<", "<<", ">>", "■", "■", "■", "A", "é", " ", "\n", "\r", "\\n", "\\x",
                    "\\", "️", "︎", "😀", "\uDE00", "<pos=40000>", "<voffset=31999>", "<size=31999>", "<\tsize=2>",
                    "<size =2>", "<line-height=0.5 >", "<voffset=1.5.5>", "<voffset=.5>", "<voffset=5.>", "<indent=+3>", "<pos= -3 >",
                };
                var r = new Random(77);
                var texts = new List<string>();
                for (int i = 0; i < 4000; i++)
                {
                    var sb = new System.Text.StringBuilder();
                    if (r.Next(3) > 0) sb.Append(r.Next(2) == 0 ? "<line-height=0.5>" : "<size=1>");
                    int count = 1 + r.Next(30);
                    for (int k = 0; k < count; k++) sb.Append(tokens[r.Next(tokens.Length)]);
                    texts.Add(sb.ToString());
                }
                foreach (var s in samples) texts.AddRange(s.texts);
                foreach (SignLink.Presets.Preset p in SignLink.Presets.ArtLibrary.Items)
                {
                    string[] art;
                    try { art = p.Load().Texts; }
                    catch (Exception) { continue; }
                    texts.AddRange(art);
                }
                double[][] moves = { new[] { 1.0, 0, 0 }, new[] { 2.0, 0, 0 }, new[] { 1.0, 10, -7 }, new[] { 0.5, -3.3, 2.2 }, new[] { 1.25, 0, 40 },
                                     new[] { 1.0, 0, -20000 }, new[] { 3.0, 31000, 0 } };
                int differ = 0, compared = 0;
                string firstDiff = null;
                void Same(bool ok, string what)
                {
                    compared++;
                    if (ok) return;
                    differ++;
                    firstDiff ??= what;
                }
                string Result(Func<string> f)
                {
                    try { return f(); }
                    catch (ArgumentException e) { return "error: " + e.Message; }
                }
                foreach (string t in texts)
                {
                    var one = new[] { t };
                    bool a = TextTransform.Supports(one, out string wa), b = OldTextTransform.Supports(one, out string wb);
                    Same(a == b && wa == wb, $"supports '{Excerpt(t, 0)}': {a} {wa} / {b} {wb}");
                    List<double> ha = TextTransform.Heights(one), hb = OldTextTransform.Heights(one);
                    Same(hb.Count <= 64 ? ha.SequenceEqual(hb) : ha.Count == 64 && ha.SequenceEqual(hb.Take(64)), $"heights '{Excerpt(t, 0)}'");
                    if (hb.Count <= 64)
                        foreach (double k in new[] { 1.0, 0.5, 1.37, 2.0, 0.26 })
                            Same(TextTransform.Snap(one, k).Equals(OldTextTransform.Snap(one, k)), $"snap {k} '{Excerpt(t, 0)}'");
                    foreach (double[] m in t.Length > 300000 ? moves.Take(2) : moves)
                        Same(Result(() => TextTransform.Apply(t, m[0], m[1], m[2])) == Result(() => OldTextTransform.Apply(t, m[0], m[1], m[2])),
                             $"apply x{m[0]} ({m[1]}, {m[2]}) '{Excerpt(t, 0)}'");
                    var fit = new Fit { Scale = 1.5, Right = 12, Up = -8 };
                    Same(TextTransform.Unfit(t, fit) == OldTextTransform.Unfit(t, fit), $"unfit '{Excerpt(t, 0)}'");
                }
                Check(differ == 0, $"tag scanner: {differ} of {compared} results differ from the regex code (first: {firstDiff})");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                string lts = "<size=1>" + new string('<', 300000);
                bool ltOk = TextTransform.Supports(new[] { lts }, out _);
                string ltApplied = TextTransform.Apply(lts, 2, 1, 1);
                Check(clock.ElapsedMilliseconds < 2000 && ltOk && ltApplied.Length > lts.Length, $"tag scanner: 300 000 '<' in {clock.ElapsedMilliseconds} ms");
                var many = new System.Text.StringBuilder("<size=1>");
                for (int i = 1; i <= 20000; i++) many.Append("<line-height=").Append((i / 64.0).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(">■\n");
                clock.Restart();
                List<double> capped = TextTransform.Heights(new[] { many.ToString() });
                double snapMany = TextTransform.Snap(new[] { many.ToString() }, 1.3);
                Check(capped.Count == 64 && capped[0] == 1 / 64.0 && capped[63] == 1.0 && clock.ElapsedMilliseconds < 2000,
                      $"heights: at most 64 distinct, in order, fast ({capped.Count}, {clock.ElapsedMilliseconds} ms, snap {snapMany})");
                Console.WriteLine($"tag scanner checked ({texts.Count} texts, {compared} results)");
            }

            // GIF frames handed over one at a time (no full-size copy of every frame), the decode buffers reused: the same
            // frames as the copies, and a frame whose LZW data ends early still reads index 0 for the rest
            {
                // a 3 x 3 canvas, colour 0 red, colour 1 white; LZW with code size 2 written as 3-bit codes, a clear every
                // two literals so the code size never grows
                byte[] Lzw(int[] idx)
                {
                    var codes = new List<int>();
                    for (int i = 0; i < idx.Length; i++)
                    {
                        if (i % 2 == 0) codes.Add(4);
                        codes.Add(idx[i]);
                    }
                    codes.Add(5);
                    var bytes = new List<byte>();
                    int acc = 0, bits = 0;
                    foreach (int c in codes)
                    {
                        acc |= c << bits;
                        bits += 3;
                        while (bits >= 8) { bytes.Add((byte)acc); acc >>= 8; bits -= 8; }
                    }
                    if (bits > 0) bytes.Add((byte)acc);
                    return bytes.ToArray();
                }
                byte[] Gif(params int[][] frames)
                {
                    var g = new List<byte> { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 3, 0, 3, 0, 0x80, 0, 0, 255, 0, 0, 255, 255, 255 };
                    foreach (int[] f in frames)
                    {
                        g.AddRange(new byte[] { 0x21, 0xF9, 4, 0, 5, 0, 0, 0 });
                        g.AddRange(new byte[] { 0x2C, 0, 0, 0, 0, 3, 0, 3, 0, 0 });
                        byte[] data = Lzw(f);
                        g.Add(2);
                        g.Add((byte)data.Length);
                        g.AddRange(data);
                        g.Add(0);
                    }
                    g.Add(0x3B);
                    return g.ToArray();
                }
                byte[] file = Gif(new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }, new[] { 0 }, new[] { 1, 0, 1 });
                GifImage g0 = GifDecoder.Decode(file);
                Check(g0.Frames.Count == 3 && g0.Frames[0].All(b => b == 255), "gif buffers: the first frame is white");
                bool redRest = g0.Frames.Count == 3;
                for (int i = 1; i < 9 && redRest; i++) redRest &= g0.Frames[1][i * 3] == 255 && g0.Frames[1][i * 3 + 1] == 0;
                Check(redRest, "gif buffers: a frame whose data ends early reads index 0 for the rest (no pixels of the frame before)");
                Check(g0.Frames.Count == 3 && g0.Frames[2][1] == 255 && g0.Frames[2][4] == 0 && g0.Frames[2][7] == 255 && g0.Frames[2][10] == 0,
                      "gif buffers: the third frame as written");
                int handed = 0;
                GifImage g1 = GifDecoder.Decode(file, frame: (canvas, w, h) =>
                {
                    handed++;
                    return canvas.Length == w * h * 3 ? (byte[])canvas.Clone() : null;
                });
                Check(handed == 3 && g1.Frames.Count == 3 && Enumerable.Range(0, 3).All(i => g1.Frames[i].SequenceEqual(g0.Frames[i])),
                      "gif hand-over: every frame handed over as composed, kept as returned");
                GifImage g2 = GifDecoder.Decode(file, frame: (canvas, w, h) => new[] { canvas[0], canvas[4] });
                Check(g2.Frames.Count == 3 && g2.Frames[1][0] == 255 && g2.Frames[1][1] == 0, "gif hand-over: what the callback returns is kept");
                Check(GifDecoder.Measure(file, out int mw, out int mh, out int kept) && mw == 3 && mh == 3 && kept == 3, "gif measure: size and frames");
                Check(!GifDecoder.Measure(new byte[] { 1, 2, 3 }, out _, out _, out _), "gif measure: not a GIF");
                string gifList = Path.Combine(Path.GetDirectoryName(path), "gifs.json");
                if (File.Exists(gifList))
                    foreach (JsonElement c in JsonDocument.Parse(File.ReadAllText(gifList)).RootElement.EnumerateArray())
                    {
                        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), c.GetProperty("file").GetString()));
                        GifImage whole = GifDecoder.Decode(bytes);
                        GifImage viaHand = GifDecoder.Decode(bytes, frame: (canvas, w, h) => (byte[])canvas.Clone());
                        Check(GifDecoder.Measure(bytes, out _, out _, out int k) && k == whole.Frames.Count, $"gif measure: {k} frames, decoded {whole.Frames.Count}");
                        Check(viaHand.Frames.Count == whole.Frames.Count && Enumerable.Range(0, whole.Frames.Count).All(i => viaHand.Frames[i].SequenceEqual(whole.Frames[i])),
                              "gif hand-over: a vector GIF's frames as the copies");
                    }
                Console.WriteLine("gif hand-over checked");
            }

            // the lists draw only the lines in view (ListRows): a full draw measures every line, then a scroll position
            // picks the lines over the view (two spare past each edge) and the room of the others; a space that lands off
            // is corrected; another list, count, width or scale measures again
            {
                var lr = new SignLink.UI.ListRows();
                object list = new object();
                lr.Plan(100, 0f, 200f, list, 500f, 1f);
                Check(lr.First == 0 && lr.Last == 99 && lr.Above == 0f && lr.Below == 0f, "list rows: everything until measured");
                for (int i = 0; i < 100; i++) lr.Saw(i, 5 + 50 * i, 5 + 50 * i + 46);
                lr.Plan(100, 1000f, 200f, list, 500f, 1f);
                Check(lr.First == 17 && lr.Last == 25 && lr.Above == 850f && lr.Below == 3700f,
                      $"list rows: the lines in view and two more each way ({lr.First}-{lr.Last}, above {lr.Above}, below {lr.Below})");
                lr.Saw(17, 5 + 850 + 3, 5 + 850 + 49);                           // the space's margins put it 3 lower
                lr.Plan(100, 1000f, 200f, list, 500f, 1f);
                Check(lr.First == 17 && lr.Above == 847f, $"list rows: a space that lands off is corrected ({lr.Above})");
                lr.Plan(100, 0f, 200f, list, 500f, 1f);
                Check(lr.First == 0 && lr.Last == 5 && lr.Above == 0f, $"list rows: at the top ({lr.First}-{lr.Last})");
                lr.Plan(100, 1e6f, 200f, list, 500f, 1f);
                Check(lr.First == 97 && lr.Last == 99 && lr.Below == 0f, $"list rows: past the end, the last lines ({lr.First}-{lr.Last})");
                lr.Plan(100, 1000f, 200f, list, 480f, 1f);
                Check(lr.First == 0 && lr.Last == 99, "list rows: another width measures again");
                for (int i = 0; i < 100; i++) lr.Saw(i, 5 + 50 * i, 5 + 50 * i + 46);
                lr.Plan(101, 1000f, 200f, list, 480f, 1f);
                Check(lr.First == 0 && lr.Last == 100, "list rows: another count measures again");
                lr.Plan(0, 0f, 200f, new object(), 480f, 1f);
                Check(lr.Last < lr.First, "list rows: an empty list draws nothing");
                Console.WriteLine("list rows checked");
            }
        }

        Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILURES");
        return failures == 0 ? 0 : 1;
    }

    static Fit Rand(Random r) => new Fit { Scale = 0.25 + r.NextDouble() * 3, Right = r.NextDouble() * 400 - 200,
                                           Up = r.NextDouble() * 400 - 200, Out = r.Next(2) == 0 ? 0 : r.NextDouble() * 400 - 200,
                                           Turn = r.NextDouble() * 360 - 180, Tilt = r.Next(3) == 0 ? 0 : r.NextDouble() * 180 - 90 };

    static string Excerpt(string s, int k) => s.Substring(Math.Max(0, k - 30), Math.Min(60, s.Length - Math.Max(0, k - 30))).Replace("\n", "|");

    static readonly System.Text.RegularExpressions.Regex TagNumber = new System.Text.RegularExpressions.Regex(@"<([a-z\-]+)=(-?\d*\.?\d+)>");

    /// <summary>Where two texts' tag numbers first differ by more than tol (for a failing check's words).</summary>
    static string NumberDiff(string a, string b, double tol)
    {
        var ma = TagNumber.Matches(a);
        var mb = TagNumber.Matches(b);
        if (ma.Count != mb.Count) return $"{ma.Count} tags against {mb.Count}";
        for (int i = 0; i < ma.Count; i++)
        {
            double x = double.Parse(ma[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(mb[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (ma[i].Groups[1].Value != mb[i].Groups[1].Value || Math.Abs(x - y) > tol * Math.Max(1, Math.Abs(y)))
                return $"tag {i}: {ma[i].Value} against {mb[i].Value}";
        }
        return "same";
    }

    /// <summary>The same tags in the same order, their numbers within tol (rounding to the 4 decimals a tag keeps).</summary>
    static bool SameNumbers(string a, string b, double tol)
    {
        var ma = TagNumber.Matches(a);
        var mb = TagNumber.Matches(b);
        if (ma.Count != mb.Count || TagNumber.Replace(a, "<$1>") != TagNumber.Replace(b, "<$1>")) return false;
        for (int i = 0; i < ma.Count; i++)
        {
            double x = double.Parse(ma[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(mb[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(x - y) > tol * Math.Max(1, Math.Abs(y))) return false;
        }
        return true;
    }
}

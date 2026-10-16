using System;
using System.Collections.Generic;
using SignLink.Signs;

namespace SignLink.Pictures
{
    /// <summary>A picture read back from its sign, to edit it where its file is not (0.20.49: another player's picture,
    /// or one whose file was removed; the author: can we edit a picture someone else made at all?). The sign holds every
    /// drawn pixel (TextReadback.ReadBox) but not the see-through margins the picture was made with, so the box is put
    /// back on a canvas whose middle is the picture's middle as made (where the sign's fit puts a picture's middle):
    /// Resolution and Width then work as they do from the file, about the same middle, and the very same grid comes out
    /// when neither changes. Resolution goes down only: the sign has no more pixels than these. No Unity here
    /// (mod/SignLink.Tests).</summary>
    internal sealed class SignPicture
    {
        /// <summary>The canvas: the box with its margins back, RGBA rows top-down (see-through margins empty).</summary>
        public int W, H;
        public byte[] Rgba;
        /// <summary>The pixel as it shows on the sign (wu), the fit's scale in it.</summary>
        public double Pitch;
        /// <summary>The drawn pixels and the margins put back around them (pixels).</summary>
        public int BoxW, BoxH, PadLeft, PadRight, PadTop, PadBottom;
        /// <summary>Where the canvas's middle sits from the fit's picture middle, in the sign's own text frame (wu, y up):
        /// 0, or all of the box's offset when it lies too far from that middle to pad (Canvas).</summary>
        public double ShiftX, ShiftY;

        /// <summary>The sign texts of a stack (SignBuilder.Content) as a canvas; fitX, fitY: where the sign's fit puts a
        /// picture's middle in its text frame (Fit.Centre(0, 0)). False with why when it is no pixel picture.</summary>
        public static bool Read(IList<string> texts, double fitX, double fitY, out SignPicture p, out string why)
        {
            p = null;
            why = null;
            if (!TextReadback.ReadBox(texts, out int bw, out int bh, out int[] box, out double pitch, out double mx, out double my, asSeen: true) ||
                pitch <= 0)
            {
                why = "it holds no pixel picture SignLink can read back";
                return false;
            }
            Canvas(bw, bh, (mx - fitX) / pitch, (my - fitY) / pitch, out int l, out int r, out int t, out int b, out double sx, out double sy);
            p = new SignPicture { BoxW = bw, BoxH = bh, PadLeft = l, PadRight = r, PadTop = t, PadBottom = b, Pitch = pitch,
                                  ShiftX = sx * pitch, ShiftY = sy * pitch, W = bw + l + r, H = bh + t + b };
            p.Rgba = new byte[(long)p.W * p.H * 4];
            for (int row = 0; row < bh; row++)
                for (int col = 0; col < bw; col++)
                {
                    int v = box[row * bw + col];
                    int a = v == -1 ? 0 : PixelText.Alpha(v);
                    if (a <= 0) continue;
                    long o = ((long)(row + t) * p.W + col + l) * 4;
                    p.Rgba[o] = (byte)((v >> 16) & 255);
                    p.Rgba[o + 1] = (byte)((v >> 8) & 255);
                    p.Rgba[o + 2] = (byte)(v & 255);
                    p.Rgba[o + 3] = (byte)a;
                }
            return true;
        }

        /// <summary>The margins that put the box's middle, offX and offY pixels from the picture's middle (y up), back
        /// on it: whole pixels on the far side each way (the margins the picture was made with: their difference is
        /// twice the offset; a sprite in a corner of a big see-through picture gets its whole picture back). What whole
        /// pixels miss is the sign's rounding (TextReadback.ReadBox: up to a quarter of a pixel, half at the finest
        /// pixel), not a place: no shift. A canvas past MaxPixels was never a picture SignLink made (the box lies
        /// farther from its fit's middle than any picture reaches: a fit another tool made stale): no padding then,
        /// the box shifts, all of it (pixels).</summary>
        public static void Canvas(int bw, int bh, double offX, double offY, out int left, out int right, out int top, out int bottom,
                                  out double shiftX, out double shiftY)
        {
            int padX = (int)Math.Round(2 * Math.Abs(offX)), padY = (int)Math.Round(2 * Math.Abs(offY));
            bool far = (long)(bw + padX) * (bh + padY) > PictureOptions.MaxPixels;
            if (far) padX = padY = 0;
            left = offX > 0 ? padX : 0;
            right = offX < 0 ? padX : 0;
            bottom = offY > 0 ? padY : 0;
            top = offY < 0 ? padY : 0;
            shiftX = far ? offX : 0;                                        // the canvas's middle from the picture's middle
            shiftY = far ? offY : 0;
        }

        /// <summary>The columns a recipe written from the canvas names (its width over its pixel): the file's full width
        /// when the sign's recipe says how many it had (fileW, as wide as the canvas or wider: the file had margins the
        /// canvas could not restore), so whoever has the file makes the picture again at the same size; else the
        /// canvas's own.</summary>
        public static int FileColumns(int planW, int canvasW, int fileW) =>
            fileW < canvasW || canvasW <= 0 ? planW : Math.Max(1, (int)Math.Round(planW * (double)fileW / canvasW));

        /// <summary>The box lies off its fit's middle (Canvas could not pad it).</summary>
        public bool Shifted => ShiftX != 0 || ShiftY != 0;

        /// <summary>The columns Update's recipe names for a plan planW pixels wide at pitchWu: the file's (FileColumns),
        /// unless the box was shifted (the file's middle is not where the fit says: there is nothing to count from) or
        /// the recipe could not hold that width (SignRecipe.MaxWidthM): the canvas's own then.</summary>
        public int RecipeColumns(int planW, double pitchWu, int fileW)
        {
            int cols = Shifted ? planW : FileColumns(planW, W, fileW);
            return cols * pitchWu / 40 > SignRecipe.MaxWidthM ? planW : cols;
        }

        /// <summary>The texts' hash (FNV-1a over every character and each text's end): a re-read finds the same picture
        /// when only the sign moved (its revision changes, its texts do not), and the player's Resolution and Width stay.</summary>
        public static ulong TextsHash(IList<string> texts)
        {
            ulong h = 14695981039346656037UL;
            foreach (string t in texts)
            {
                foreach (char ch in t ?? "")
                {
                    h ^= ch;
                    h *= 1099511628211UL;
                }
                h ^= 0x1D;                                                  // the GS between texts: "ab","c" is not "a","bc"
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}

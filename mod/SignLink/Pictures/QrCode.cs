using System;
using System.Collections.Generic;
using System.Text;

namespace SignLink.Pictures
{
    /// <summary>A QR Code (ISO/IEC 18004) for 2 Content › Text's QR template (2026-10-16, the author: a QR code
    /// generator in the content menu, drawn with Unicode squares): the text as UTF-8 bytes in byte mode, the smallest
    /// version (1-40) that holds it at the error correction asked, the mask with the lowest penalty unless one is asked.
    /// Modules[y, x] is true for a dark module. No Unity here (mod/SignLink.Tests checks it against an independent
    /// encoder, python-qrcode, module for module, every reference read back by zbar).</summary>
    internal sealed class QrCode
    {
        /// <summary>Error correction: about 7, 15, 25 and 30 % of the code can be lost and still read.</summary>
        public enum Ecc { L, M, Q, H }

        public readonly int Version, Size, Mask;
        public readonly Ecc Level;
        public readonly bool[,] Modules;
        readonly bool[,] function;

        QrCode(int version, Ecc level, byte[] codewords, int mask)
        {
            Version = version;
            Level = level;
            Size = version * 4 + 17;
            Modules = new bool[Size, Size];
            function = new bool[Size, Size];
            DrawFunctionPatterns();
            DrawCodewords(codewords);
            if (mask < 0)                                                   // the lowest penalty, the first of equals
            {
                int best = int.MaxValue;
                for (int m = 0; m < 8; m++)
                {
                    ApplyMask(m);
                    DrawFormat(m);
                    int p = Penalty();
                    if (p < best)
                    {
                        best = p;
                        mask = m;
                    }
                    ApplyMask(m);                                           // XOR again: undone
                }
            }
            Mask = mask;
            ApplyMask(mask);
            DrawFormat(mask);
        }

        /// <summary>The code for this text, or an exception when it does not fit version 40. mask: 0-7, or -1 to choose.</summary>
        public static QrCode Encode(string text, Ecc level, int mask = -1, int minVersion = 1)
        {
            if (mask < -1 || mask > 7) throw new ArgumentException("the mask is 0-7, or -1");
            byte[] data = Encoding.UTF8.GetBytes(text ?? "");
            int version = minVersion, capacity;
            for (; ; version++)
            {
                if (version > 40) throw new ArgumentException($"{data.Length} bytes are too long for a QR code at {level} (version 40 holds at most {DataCodewords(40, level) - 3})");
                capacity = DataCodewords(version, level) * 8;
                if (4 + CountBits(version) + data.Length * 8 <= capacity) break;
            }
            var bits = new List<bool>();
            Append(bits, 0x4, 4);                                           // byte mode
            Append(bits, data.Length, CountBits(version));
            foreach (byte b in data) Append(bits, b, 8);
            Append(bits, 0, Math.Min(4, capacity - bits.Count));            // the terminator
            Append(bits, 0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) Append(bits, pad, 8);
            var codewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));
            return new QrCode(version, level, Interleave(codewords, version, level), mask);
        }

        static int CountBits(int version) => version <= 9 ? 8 : 16;

        static void Append(List<bool> bits, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }

        // ---- function patterns ----

        void Set(int x, int y, bool dark)
        {
            Modules[y, x] = dark;
            function[y, x] = true;
        }

        void DrawFunctionPatterns()
        {
            for (int i = 0; i < Size; i++)                                  // timing
            {
                Set(6, i, i % 2 == 0);
                Set(i, 6, i % 2 == 0);
            }
            Finder(3, 3);
            Finder(Size - 4, 3);
            Finder(3, Size - 4);
            int[] at = AlignmentPositions(Version);
            int n = at.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!(i == 0 && j == 0 || i == 0 && j == n - 1 || i == n - 1 && j == 0))
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                                Set(at[i] + dx, at[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            DrawFormat(0);                                                  // reserved now, written for the mask later
            if (Version >= 7)
            {
                int rem = Version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int bits = Version << 12 | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool dark = ((bits >> i) & 1) != 0;
                    int a = Size - 11 + i % 3, b = i / 3;
                    Set(a, b, dark);
                    Set(b, a, dark);
                }
            }
        }

        void Finder(int cx, int cy)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int x = cx + dx, y = cy + dy, d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (x >= 0 && x < Size && y >= 0 && y < Size) Set(x, y, d != 2 && d != 4);
                }
        }

        static int[] AlignmentPositions(int version)
        {
            if (version == 1) return new int[0];
            int n = version / 7 + 2, size = version * 4 + 17;
            int step = version == 32 ? 26 : (version * 4 + n * 2 + 1) / (n * 2 - 2) * 2;
            var at = new int[n];
            at[0] = 6;
            for (int i = n - 1, pos = size - 7; i >= 1; i--, pos -= step) at[i] = pos;
            return at;
        }

        void DrawFormat(int mask)
        {
            int data = FormatLevel(Level) << 3 | mask, rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            for (int i = 0; i <= 5; i++) Set(8, i, Bit(bits, i));
            Set(8, 7, Bit(bits, 6));
            Set(8, 8, Bit(bits, 7));
            Set(7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++) Set(14 - i, 8, Bit(bits, i));
            for (int i = 0; i < 8; i++) Set(Size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++) Set(8, Size - 15 + i, Bit(bits, i));
            Set(8, Size - 8, true);                                         // the dark module
        }

        static int FormatLevel(Ecc e) => e == Ecc.L ? 1 : e == Ecc.M ? 0 : e == Ecc.Q ? 3 : 2;

        static bool Bit(int x, int i) => ((x >> i) & 1) != 0;

        // ---- data ----

        void DrawCodewords(byte[] data)
        {
            int i = 0;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;                                  // the vertical timing column
                for (int vert = 0; vert < Size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? Size - 1 - vert : vert;
                        if (!function[y, x] && i < data.Length * 8)
                        {
                            Modules[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                            i++;
                        }
                    }
            }
        }

        void ApplyMask(int mask)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool flip;
                    switch (mask)
                    {
                        case 0: flip = (x + y) % 2 == 0; break;
                        case 1: flip = y % 2 == 0; break;
                        case 2: flip = x % 3 == 0; break;
                        case 3: flip = (x + y) % 3 == 0; break;
                        case 4: flip = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: flip = x * y % 2 + x * y % 3 == 0; break;
                        case 6: flip = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: flip = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (flip && !function[y, x]) Modules[y, x] = !Modules[y, x];
                }
        }

        /// <summary>The standard penalty: runs of five or more, 2 x 2 blocks, finder-like patterns, dark balance.</summary>
        int Penalty()
        {
            int p = 0, dark = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < Size; a++)
                {
                    int run = 0;
                    bool last = false;
                    for (int b = 0; b < Size; b++)
                    {
                        bool m = pass == 0 ? Modules[a, b] : Modules[b, a];
                        if (b > 0 && m == last) run++;
                        else
                        {
                            if (run >= 5) p += run - 2;
                            run = 1;
                            last = m;
                        }
                    }
                    if (run >= 5) p += run - 2;
                    for (int b = 0; b + 10 < Size; b++)
                        if (Finderlike(pass, a, b)) p += 40;
                }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (Modules[y, x]) dark++;
                    if (x + 1 < Size && y + 1 < Size && Modules[y, x] == Modules[y, x + 1] && Modules[y, x] == Modules[y + 1, x] &&
                        Modules[y, x] == Modules[y + 1, x + 1]) p += 3;
                }
            int total = Size * Size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            return p + Math.Max(0, k) * 10;
        }

        static readonly bool[] FinderA = { true, false, true, true, true, false, true, false, false, false, false },
                               FinderB = { false, false, false, false, true, false, true, true, true, false, true };

        bool Finderlike(int pass, int a, int b)
        {
            bool ma = true, mb = true;
            for (int i = 0; i < 11 && (ma || mb); i++)
            {
                bool m = pass == 0 ? Modules[a, b + i] : Modules[b + i, a];
                if (m != FinderA[i]) ma = false;
                if (m != FinderB[i]) mb = false;
            }
            return ma || mb;
        }

        // ---- error correction ----

        static readonly int[,] EccPerBlock =
        {
            { -1,  7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 },
            { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
        };

        static readonly int[,] Blocks =
        {
            { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4,  4,  4,  4,  4,  6,  6,  6,  6,  7,  8,  8,  9,  9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25 },
            { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5,  5,  8,  9,  9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 },
            { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8,  8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68 },
            { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81 },
        };

        static int RawModules(int version)
        {
            int r = (16 * version + 128) * version + 64;
            if (version >= 2)
            {
                int n = version / 7 + 2;
                r -= (25 * n - 10) * n - 55;
                if (version >= 7) r -= 36;
            }
            return r;
        }

        /// <summary>How many data codewords a version holds at a level.</summary>
        public static int DataCodewords(int version, Ecc level) =>
            RawModules(version) / 8 - EccPerBlock[(int)level, version] * Blocks[(int)level, version];

        static byte[] Interleave(byte[] data, int version, Ecc level)
        {
            int blocks = Blocks[(int)level, version], eccLen = EccPerBlock[(int)level, version], raw = RawModules(version) / 8;
            int shortBlocks = blocks - raw % blocks, shortLen = raw / blocks;
            byte[] divisor = Divisor(eccLen);
            var all = new byte[blocks][];
            for (int i = 0, k = 0; i < blocks; i++)
            {
                int n = shortLen - eccLen + (i < shortBlocks ? 0 : 1);
                var block = new byte[shortLen + 1];
                Array.Copy(data, k, block, 0, n);
                k += n;
                byte[] ecc = Remainder(data, k - n, n, divisor);
                Array.Copy(ecc, 0, block, shortLen + 1 - eccLen, eccLen);   // a short block's gap before its ECC is skipped below
                all[i] = block;
            }
            var result = new byte[raw];
            int at = 0;
            for (int i = 0; i <= shortLen; i++)
                for (int j = 0; j < blocks; j++)
                    if (i != shortLen - eccLen || j >= shortBlocks) result[at++] = all[j][i];
            return result;
        }

        static byte[] Divisor(int degree)
        {
            var r = new byte[degree];
            r[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    r[j] = Mul(r[j], root);
                    if (j + 1 < degree) r[j] ^= r[j + 1];
                }
                root = Mul(root, 0x02);
            }
            return r;
        }

        static byte[] Remainder(byte[] data, int from, int count, byte[] divisor)
        {
            var r = new byte[divisor.Length];
            for (int i = from; i < from + count; i++)
            {
                int factor = data[i] ^ r[0];
                Array.Copy(r, 1, r, 0, r.Length - 1);
                r[r.Length - 1] = 0;
                for (int j = 0; j < r.Length; j++) r[j] ^= Mul(divisor[j], factor);
            }
            return r;
        }

        static byte Mul(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }
    }
}

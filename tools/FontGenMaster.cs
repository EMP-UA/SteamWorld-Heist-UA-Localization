using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;

/* * SteamWorld Heist Font Generation & Debug Tool
 * Developed by EMP_UA (Yevhenii)
 * * Key Functionalities:
 * - Binary parsing of .fnt files to inject Cyrillic support.
 * - 'Slot Hijacking' to bypass engine limitations for unused ASCII characters.
 * - Dynamic generation of PNG font atlases using GDI+ with sub-pixel precision.
 * - Debug overlay generation for visual verification of character metrics.
 * * Technical Reference & Credits:
 * - Font metrics research: Special thanks to sb8gapi (Graj po Polsku community) 
 * for the initial research into the SteamWorld Heist .fnt structure.
 */

namespace SteamWorldUA_FontMaster
{
    class CharPos
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    class Program
    {
        // --- Configuration Section ---
        static string baseDir = @"C:\Path\To\Original\Fonts"; // Path to original .fnt/.png
        static string fontFilesDir = @"C:\Path\To\TTF\Fonts"; // Path to TTF (Comfortaa, Oswald, etc)
        static string outputDir = @"C:\Path\To\Output";
        static string debugOutputDir = @"C:\Path\To\Output\Debug";

        // Slot Mapping for optimized character placement
        static Dictionary<int, int> perfectDonorMap = new Dictionary<int, int> {
            { 1028, 1069 }, { 1108, 1101 }, { 1031, 1066 }, { 1111, 1098 },
            { 1168, 1067 }, { 1169, 1099 }
        };

        static string[] fonts = {
            "header_medium", "factions", "header_small", "body_large",
            "ingame", "body_medium", "indicator", "body_small",
            "ingame_small", "body_xsmall", "debug_small"
        };

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            if (!Directory.Exists(debugOutputDir)) Directory.CreateDirectory(debugOutputDir);

            Console.WriteLine("=== SteamWorld Heist Font Master Tool | by EMP_UA ===");

            foreach (var name in fonts)
            {
                ProcessFont(name);
                GenerateDebugBoxes(name);
            }

            Console.WriteLine("\nAll font tasks completed. Press Enter...");
            Console.ReadLine();
        }

        static void ProcessFont(string fontName)
        {
            string fFnt = Path.Combine(baseDir, fontName + ".fnt");
            string fPng = Path.Combine(baseDir, fontName + ".png");
            if (!File.Exists(fFnt) || !File.Exists(fPng)) return;

            Console.WriteLine($"-> Processing: {fontName}");
            byte[] fileData = File.ReadAllBytes(fFnt);
            
            // Binary Parsing of .fnt structure
            short strLen = BitConverter.ToInt16(fileData, 10);
            int charsStart = 12 + strLen + 2;
            short numChars = BitConverter.ToInt16(fileData, charsStart - 2);

            List<byte[]> allBlocks = new List<byte[]>();
            for (int i = 0; i < numChars; i++)
            {
                byte[] block = new byte[32];
                Array.Copy(fileData, charsStart + (i * 32), block, 0, 32);
                allBlocks.Add(block);
            }

            using (Bitmap atlas = new Bitmap(fPng))
            using (Graphics g = Graphics.FromImage(atlas))
            using (PrivateFontCollection pfc = new PrivateFontCollection())
            {
                SetupGraphics(g);
                pfc.AddFontFile(GetFontPath(fontName));
                FontFamily family = pfc.Families[0];

                // Calculate global metrics based on uppercase 'A'
                byte[] blockA = allBlocks.FirstOrDefault(b => BitConverter.ToInt32(b, 0) == 1040) ?? allBlocks.FirstOrDefault(b => BitConverter.ToInt32(b, 0) == 65);
                float boxH = blockA != null ? BitConverter.ToSingle(blockA, 16) : 35f;
                float yoffsetA = blockA != null ? BitConverter.ToSingle(blockA, 24) : 0f;
                float globalGameBaseline = yoffsetA + (boxH * 0.80f);

                // Inject Ukrainian Alphabet
                var ukrIDs = GetUkrAlphabet();
                List<byte[]> modifiedBlocks = new List<byte[]>();
                var freeBlocks = allBlocks.Where(b => BitConverter.ToInt32(b, 0) >= 1024).ToList();

                foreach (int uaID in ukrIDs)
                {
                    byte[] target = perfectDonorMap.ContainsKey(uaID) ? 
                        freeBlocks.FirstOrDefault(b => BitConverter.ToInt32(b, 0) == perfectDonorMap[uaID]) : 
                        freeBlocks.FirstOrDefault(b => BitConverter.ToInt32(b, 0) == uaID);

                    if (target == null) target = freeBlocks.FirstOrDefault();

                    if (target != null)
                    {
                        freeBlocks.Remove(target);
                        byte[] newB = new byte[32];
                        Array.Copy(target, newB, 32);
                        Buffer.BlockCopy(BitConverter.GetBytes(uaID), 0, newB, 0, 4);
                        
                        DrawLocalizedChar(g, newB, (char)uaID, family, boxH, globalGameBaseline);
                        modifiedBlocks.Add(newB);
                    }
                }

                atlas.Save(Path.Combine(outputDir, fontName + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                SaveFnt(fontName, fileData, allBlocks, modifiedBlocks, charsStart);
            }
        }

        static void SetupGraphics(Graphics g)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        static void DrawLocalizedChar(Graphics g, byte[] block, char c, FontFamily family, float boxH, float baseline)
        {
            float x = BitConverter.ToSingle(block, 4);
            float y = BitConverter.ToSingle(block, 8);
            float w = BitConverter.ToSingle(block, 12);
            float h = BitConverter.ToSingle(block, 16);

            // Clear original slot
            g.CompositingMode = CompositingMode.SourceCopy;
            g.FillRectangle(Brushes.Transparent, x - 1, y - 1, w + 2, h + 2);
            g.CompositingMode = CompositingMode.SourceOver;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddString(c.ToString(), family, (int)FontStyle.Bold, 100, Point.Empty, StringFormat.GenericTypographic);
                RectangleF b = path.GetBounds();
                if (b.Width == 0 || b.Height == 0) return;

                // Technical Scale & Shift logic (Fine-tuned for SteamWorld Heist engine)
                float scale = (boxH * 0.60f) / b.Height;
                float drawX = x + (w - b.Width * scale) / 2f - b.X * scale;
                float drawY = (y + h * 0.80f) - ((b.Y + b.Height) * scale);

                Matrix m = new Matrix();
                m.Translate(drawX, drawY);
                m.Scale(scale, scale);
                path.Transform(m);

                g.FillPath(Brushes.White, path);
                using (Pen p = new Pen(Color.White, 0.5f)) g.DrawPath(p, path);
            }
        }

        static List<int> GetUkrAlphabet() => "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюя".Select(c => (int)c).ToList();

        static void SaveFnt(string name, byte[] original, List<byte[]> oldBlocks, List<byte[]> newBlocks, int start)
        {
            using (BinaryWriter bw = new BinaryWriter(File.Create(Path.Combine(outputDir, name + ".fnt"))))
            {
                var final = oldBlocks.Where(b => BitConverter.ToInt32(b, 0) < 1024).ToList();
                final.AddRange(newBlocks);
                bw.Write(original, 0, start);
                foreach (var b in final.OrderBy(x => BitConverter.ToInt32(x, 0))) bw.Write(b);
            }
        }

        static string GetFontPath(string name)
        {
            if (name.Contains("header")) return Path.Combine(fontFilesDir, "Oswald-Bold.ttf");
            if (name.Contains("indicator")) return Path.Combine(fontFilesDir, "Cuprum-Bold.ttf");
            return Path.Combine(fontFilesDir, "Comfortaa.ttf");
        }

        static void GenerateDebugBoxes(string name)
        {
            // Simplified Debug logic for GitHub clarity
            Console.WriteLine($"   -> Generating Debug view for {name}");
        }
    }
}

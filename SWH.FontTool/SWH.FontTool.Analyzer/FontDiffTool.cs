// =============================================================================
// SWH.FontTool.Analyzer — FontDiffTool.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: РАННЯ, легша версія бінарного сканера — писалась ще до FontAnalyzer.cs,
//     коли навіть базова структура .fnt була не до кінця ясна. Логіка
//     визначення stride тут ІНША за FontAnalyzer (тут — відстань між
//     знайденими ID=33 та ID=34; там — перевірка кандидатів 28/32/36 з
//     чітким пріоритетом "без padding" першим). Це не той самий баг, що був
//     виправлений у FontAnalyzer.FindTableStart, але й не гарантовано
//     надійніший підхід — просто інша, менш формалізована евристика.
//     Залишено для сумісності з Program.cs та як історичний références;
//     авторитетне джерело аналізу — FontAnalyzer (пункти меню 1/2).
// EN: An EARLY, lighter binary scanner — written before FontAnalyzer.cs
//     existed, back when even the basic .fnt structure wasn't fully clear.
//     The stride-detection logic here is DIFFERENT from FontAnalyzer's
//     (here — the distance between the found ID=33 and ID=34; there — a
//     check of 28/32/36 candidates, "no padding" checked first). This isn't
//     the same bug that was fixed in FontAnalyzer.FindTableStart, but it's
//     also not a guaranteed-more-reliable approach — just a different, less
//     formalized heuristic. Kept for Program.cs compatibility and as a
//     historical reference; the authoritative analysis source is
//     FontAnalyzer (menu items 1/2).
// =============================================================================

using System.Text;
using SWH.FontTool.Core;

namespace SWH.FontTool.Analyzer;

public class FontDiffTool
{
    /// <summary>
    /// UA: МАСОВИЙ АНАЛІЗ: Проходить по всіх .fnt файлах у Config.OriginalFontsDir і
    /// витягує їхню математику.
    /// EN: BULK ANALYSIS: Walks every .fnt file in Config.OriginalFontsDir and
    /// extracts its math.
    /// </summary>
    public static void BatchAnalyzeAll()
    {
        string[] files = Directory.GetFiles(Config.OriginalFontsDir, "*.fnt");
        Console.WriteLine($"\n{"FILE NAME",-20} | {"STRIDE",-6} | {"START",-7} | {"LH (at +4)",-8} | {"BL (at +8)",-8}");
        Console.WriteLine(new string('-', 65));

        foreach (var file in files)
        {
            byte[] data = File.ReadAllBytes(file);
            string name = Path.GetFileName(file);

            // UA: 1. Знаходимо початок таблиці (ID 32)
            // EN: 1. Find the table start (ID 32)
            int tableStart = -1;
            for (int i = 12; i < 500; i++)
            {
                if (data[i] == 0x20 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 0)
                {
                    tableStart = i;
                    break;
                }
            }

            if (tableStart == -1)
            {
                Console.WriteLine($"{name,-20} | [!] Не знайдено ID 32 / ID 32 not found");
                continue;
            }

            // UA: 2. Визначаємо Stride (відстань між 33 та 34)
            // EN: 2. Determine the stride (the distance between 33 and 34)
            int stride = 0;
            int p33 = -1, p34 = -1;
            for (int i = tableStart; i < Math.Min(data.Length, tableStart + 500); i++)
            {
                if (p33 == -1 && data[i] == 0x21 && data[i + 1] == 0 && data[i + 2] == 0) p33 = i;
                else if (p33 != -1 && data[i] == 0x22 && data[i + 1] == 0 && data[i + 2] == 0) { p34 = i; break; }
            }
            if (p33 != -1 && p34 != -1) stride = p34 - p33;
            else stride = (p33 != -1) ? (p33 - tableStart) : 0;

            // UA: 3. Читаємо метрики (Little Endian, як показав Deep Scan)
            // EN: 3. Read the metrics (little-endian, as Deep Scan showed)
            float lh = (tableStart + 7 < data.Length) ? BitConverter.ToSingle(data, tableStart + 4) : 0;
            float bl = (tableStart + 11 < data.Length) ? BitConverter.ToSingle(data, tableStart + 8) : 0;

            Console.WriteLine($"{name,-20} | {stride,-6} | 0x{tableStart:X4} | {lh,-8:F1} | {bl,-8:F1}");
        }
    }

    /// <summary>
    /// UA: Глибокий аналіз одного файлу (для детальної перевірки полів).
    /// EN: A deep analysis of a single file (for detailed field verification).
    /// </summary>
    public static void DeepScan(string enPath)
    {
        if (!File.Exists(enPath)) return;
        byte[] data = File.ReadAllBytes(enPath);

        // UA: Знаходимо початок (ID 32) / EN: Find the start (ID 32)
        int start = -1;
        for (int i = 12; i < 500; i++)
            if (data[i] == 0x20 && data[i + 1] == 0) { start = i; break; }

        if (start == -1) return;

        // UA: Визначаємо Stride / EN: Determine the stride
        int stride = 28;
        int p33 = -1;
        for (int i = start; i < start + 200; i++)
            if (data[i] == 0x21 && data[i + 1] == 0) { p33 = i; break; }
        if (p33 != -1) stride = p33 - start;

        Console.WriteLine($"\n=== ДЕТАЛЬНИЙ СКАН / DEEP SCAN: {Path.GetFileName(enPath)} (Stride {stride}) ===");

        int[] targets = { 32, 65 }; // UA: Пробіл та 'A' / EN: Space and 'A'
        foreach (int id in targets)
        {
            int off = -1;
            // UA: Шукаємо гліф перебором всієї таблиці
            // EN: Find the glyph by scanning the whole table
            for (int i = start; i < data.Length - stride; i += stride)
            {
                if (BitConverter.ToInt32(data, i) == id) { off = i; break; }
            }

            if (off == -1) continue;

            Console.WriteLine($"\nГліф/Glyph ID {id} ({(char)id}) на офсеті/at offset 0x{off:X4}:");
            for (int p = 0; p <= stride - 4; p += 4)
            {
                float val = BitConverter.ToSingle(data, off + p);
                int valInt = BitConverter.ToInt32(data, off + p);
                Console.WriteLine($"  +{p:D2} | HEX: {data[off + p]:X2}{data[off + p + 1]:X2}{data[off + p + 2]:X2}{data[off + p + 3]:X2} | Float: {val,8:F1} | Int: {valInt}");
            }
        }
    }

    // UA: Метод для Program.cs, щоб не було помилок компіляції
    // EN: A method for Program.cs, so there are no compile errors
    public static void CompareGlyphSmart(string en, string pl, string ua, int id) => DeepScan(en);
    public static void AnalyzeHeader(string path) => BatchAnalyzeAll();
}
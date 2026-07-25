// =============================================================================
// SWH.FontTool.Analyzer — IdSwapSanityExperiment.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Контекст (2026-07-21): ДВІ різні гіпотези щодо нової літери 'і'
//     провалились по черзі:
//       1. "Гліф занадто вузький" (MinGlyphBoxWidthPx/MinXAdvancePx) —
//          жодного ефекту.
//       2. "Рушій рахує координати за застарілим/кешованим розміром
//          текстури, тому нове місце за межами старих кордонів wrap-иться"
//          — спростовано: header_medium/header_small отримали 'і' У МЕЖАХ
//          старого розміру (напр. Y=410 з 512, узагалі без виходу за межі)
//          — і 'і' однаково "летить" так само, як і раніше.
//     Обидві гіпотези стосувались ГЕОМЕТРІЇ нової літери. Цей клас
//     перевіряє щось значно фундаментальніше: чи взагалі рушій показує
//     ПРАВИЛЬНИЙ гліф за ID field так, як припускається — повністю
//     ІЗОЛЬОВАНО від будь-якої нової геометрії чи нового простору PNG.
//
//     Метод: береться ДВІ вже існуючі, вже робочі українські літери
//     ('к' і 'л' — обидві common, обидві вже видно в грі щодня) і їхні
//     ID-поля МІНЯЮТЬСЯ МІСЦЯМИ — і більше НІЧОГО: жодна геометрія
//     (AtlasX/Y/W/H/XOffset/YOffset/XAdvance) не змінюється НІ для одного
//     запису, PNG не чіпається взагалі. Якщо рушій справді читає ID
//     кожного запису (а не якийсь інший, невідомий механізм
//     ідентифікації гліфа), то після цього тесту весь текст, де мало бути
//     'к', покаже 'л', і навпаки — видима, однозначна, легко перевірювана
//     різниця. Якщо цього НЕ станеться (текст лишиться незмінним, або
//     покаже щось третє) — це означає, що базове розуміння формату
//     (яке поле є ID, чи саме за ним рушій ідентифікує гліф) саме по собі
//     помилкове, і треба переглядати це з нуля.
// EN: Context (2026-07-21): TWO different hypotheses about the new letter
//     'і' failed one after another:
//       1. "The glyph is too narrow" (MinGlyphBoxWidthPx/MinXAdvancePx) —
//          no effect at all.
//       2. "The engine computes coordinates against a stale/cached texture
//          size, so the new spot beyond the old bounds wraps around" —
//          disproven: header_medium/header_small got 'і' WITHIN the old
//          size (e.g. Y=410 of 512, no overrun at all) — and 'і' still
//          "floats" exactly the same as before.
//     Both hypotheses were about the new letter's GEOMETRY. This class
//     tests something far more fundamental: does the engine even display
//     the CORRECT glyph by ID the way it's assumed to — fully ISOLATED from any
//     new geometry or new PNG space whatsoever.
//
//     Method: take TWO already-existing, already-working Ukrainian letters
//     ('к' and 'л' — both common, both visible in the game every day) and
//     SWAP their ID fields — and nothing else: no geometry
//     (AtlasX/Y/W/H/XOffset/YOffset/XAdvance) changes for either record,
//     the PNG isn't touched at all. If the engine really reads each
//     record's ID (rather than some other, unknown glyph
//     identification mechanism), then after this test every place that
//     used to show 'к' will show 'л', and vice versa — a visible,
//     unambiguous, easy-to-check difference. If this does NOT happen (the
//     text stays unchanged, or shows something else entirely) — that means
//     the basic understanding of the format (which field is the ID, and
//     whether the engine identifies the glyph by it at all) is itself
//     wrong, and needs to be revisited from scratch.
// =============================================================================

using SWH.FontTool.Core;
using IOPath = System.IO.Path;

namespace SWH.FontTool.Analyzer;

public static class IdSwapSanityExperiment
{
    private const int IdA = 1082; // к
    private const int IdB = 1083; // л

    public static void RunTest(string sourceDir, string outDir, TextWriter log)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Папка не знайдена / Folder not found: {sourceDir}");

        Directory.CreateDirectory(outDir);
        var fntFiles = Directory.GetFiles(sourceDir, "*.fnt");

        log.WriteLine($"Джерело (не змінюється) / Source (never modified): {sourceDir}");
        log.WriteLine($"Результат тесту / Test output: {outDir}");
        log.WriteLine($"Обмін ID / ID swap: {IdA} ('к') <-> {IdB} ('л') — БЕЗ жодної зміни геометрії, PNG не чіпається. / " +
                      "WITHOUT any geometry change, PNG untouched.");
        log.WriteLine();

        foreach (var fontPath in fntFiles)
        {
            string name = IOPath.GetFileNameWithoutExtension(fontPath);
            string srcPng = IOPath.Combine(sourceDir, name + ".png");

            FontAnalysisResult analysis;
            try { analysis = FontAnalyzer.Analyze(fontPath); }
            catch (Exception ex)
            {
                log.WriteLine($"{name,-20} пропущено / skipped ({ex.Message})");
                continue;
            }

            var recA = analysis.GetById(IdA);
            var recB = analysis.GetById(IdB);
            if (recA == null || recB == null)
            {
                log.WriteLine($"{name,-20} пропущено / skipped (немає 'к' або 'л' / no 'к' or 'л')");
                continue;
            }

            byte[] outData = File.ReadAllBytes(fontPath);
            int posA = -1, posB = -1;
            for (int i = 0; i < analysis.GlyphCount; i++)
            {
                int pos = analysis.TableStart + i * analysis.Stride;
                if (analysis.Records[i].ID == IdA) posA = pos;
                else if (analysis.Records[i].ID == IdB) posB = pos;
            }

            if (posA < 0 || posB < 0)
            {
                log.WriteLine($"{name,-20} пропущено / skipped (не вдалось знайти позиції записів / could not locate record positions)");
                continue;
            }

            // UA: РІВНО 4 байти в кожній з двох позицій — жодне інше поле
            //     не чіпається.
            // EN: EXACTLY 4 bytes at each of the two positions — no other
            //     field is touched.
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(outData.AsSpan(posA + analysis.IdOffset, 4), IdB);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(outData.AsSpan(posB + analysis.IdOffset, 4), IdA);

            SortGlyphTable(outData, analysis);

            string destFnt = IOPath.Combine(outDir, name + ".fnt");
            File.WriteAllBytes(destFnt, outData);

            if (File.Exists(srcPng))
                File.Copy(srcPng, IOPath.Combine(outDir, name + ".png"), overwrite: true);

            log.WriteLine($"{name,-20} 'к' і 'л' помінялись ID (геометрія обох записів незмінна, PNG незмінний)");
        }

        log.WriteLine();
        log.WriteLine($"Далі вручну / Next, manually: скопіюй файли з {outDir} у гру, знайди будь-який текст зі словами,");
        log.WriteLine("де є 'к' або 'л' (напр. 'Скасувати', 'клавіші', 'вимкнути').");
        log.WriteLine("  UA: 'к' і 'л' видимо поміняні місцями в тексті (де мало бути 'к' — тепер 'л', і навпаки) ->");
        log.WriteLine("      базове розуміння ID-поля правильне; проблема з 'і' специфічна саме для НОВИХ,");
        log.WriteLine("      ще не існуючих кодів символів, а не для перепризначення взагалі.");
        log.WriteLine("  EN: 'к' and 'л' are visibly swapped in the text (where 'к' should be — now 'л', and vice");
        log.WriteLine("      versa) -> the basic understanding of the ID field is correct; the 'і' problem is");
        log.WriteLine("      specific to NEW, not-yet-existing character codes, not to remapping in general.");
        log.WriteLine("  UA: Нічого не змінилось АБО зʼявилось щось третє -> саме розуміння формату (що таке ID,");
        log.WriteLine("      чи за ним рушій ідентифікує гліф) хибне — треба переглядати з нуля.");
        log.WriteLine("  EN: Nothing changed OR something else entirely appeared -> the very understanding of the");
        log.WriteLine("      format (what the ID is, whether the engine identifies the glyph by it) is wrong —");
        log.WriteLine("      needs to be revisited from scratch.");
    }

    private static void SortGlyphTable(byte[] data, FontAnalysisResult analysis)
    {
        int stride = analysis.Stride;
        int start = analysis.TableStart;
        int count = analysis.GlyphCount;
        int idOff = analysis.IdOffset;

        var records = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            records[i] = new byte[stride];
            Array.Copy(data, start + i * stride, records[i], 0, stride);
        }

        Array.Sort(records, (a, b) => BitConverter.ToInt32(a, idOff).CompareTo(BitConverter.ToInt32(b, idOff)));

        for (int i = 0; i < count; i++)
            Array.Copy(records[i], 0, data, start + i * stride, stride);
    }
}

// =============================================================================
// SWH.FontTool.Analyzer — LatinReferenceDiagnostic.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ПОВНИЙ ЕТАЛОННИЙ ЗРІЗ ЛАТИНИЦІ (діагностика, 2026-07-24).
//     Ідея: перш ніж будувати українську кирилицю, зібрати ПОВНІ, реальні дані
//     про латиницю (ASCII) кожного оригінального шрифту гри — і вшиті метрики
//     .fnt (AtlasX/Y/W/H, XOffset, YOffset, XAdvance), і фактичне піксельне
//     чорнило з .png. Латиниця — це "земля правди": англійська версія гри
//     відвантажується й виглядає правильно, тож саме її метрики є еталоном, до
//     якого треба прив'язувати кирилицю (а не оригінальну кирилицю, яка в цих
//     шрифтах подекуди сама невідповідного розміру/стиснута).
//
//     Модуль НІЧОГО не змінює — лише читає й вимірює. Виводить:
//       (а) на КОЖЕН шрифт — зведені вертикальні орієнтири:
//           baseline, cap-height, x-height, ascender, descender, dot-top ('i');
//       (б) на КОЖНУ ASCII-літеру — .fnt-метрики + піксельне чорнило поруч,
//           щоб бачити РОЗБІЖНІСТЬ між вшитим боксом і реальним чорнилом
//           (напр. верхній відступ AtlasY→ink, бічні відступи).
//     Повний звіт пишеться у debug/latin_reference.txt.
// EN: FULL LATIN REFERENCE SNAPSHOT (diagnostic, 2026-07-24).
//     Idea: before building the Ukrainian Cyrillic, collect COMPLETE, real data
//     about the Latin (ASCII) of every original game font — both the embedded
//     .fnt metrics (AtlasX/Y/W/H, XOffset, YOffset, XAdvance) AND the actual
//     pixel ink from the .png. Latin is the "ground truth": the English version
//     ships and looks correct, so its metrics are the reference the Cyrillic
//     must be anchored to (not the original Cyrillic, which in these fonts is
//     sometimes itself an off/compressed size).
//
//     This module changes NOTHING — it only reads and measures. It outputs:
//       (a) PER font — the summary vertical guides:
//           baseline, cap-height, x-height, ascender, descender, dot-top ('i');
//       (b) PER ASCII letter — the .fnt metrics alongside the pixel ink, so the
//           MISMATCH between the embedded box and the real ink is visible
//           (e.g. the AtlasY→ink top margin, the side bearings).
//     The full report is written to debug/latin_reference.txt.
// =============================================================================

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SWH.FontTool.Core;
using System.Text;

namespace SWH.FontTool.Analyzer;

public static class LatinReferenceDiagnostic
{
    /// <summary>UA: Зведені еталонні вертикальні метрики одного шрифту (у екранних координатах: YOffset + зсув чорнила). / EN: One font's summary reference vertical metrics (in screen coords: YOffset + ink offset).</summary>
    public readonly record struct FontReference(
        float Baseline, float CapHeight, float XHeight, float Ascender, float Descender, float DotTopAboveBaseline);

    /// <summary>UA: Головний вхід — обробляє всі шрифти, пише звіт і на консоль, і у файл. / EN: Main entry — processes all fonts, writes the report both to the console and a file.</summary>
    public static void RunTest(string originalFontsDir, TextWriter output)
    {
        var sb = new StringBuilder();
        void Line(string s) { output.WriteLine(s); sb.AppendLine(s); }

        Line("=== ЕТАЛОННИЙ ЗРІЗ ЛАТИНИЦІ / LATIN REFERENCE SNAPSHOT ===");
        Line($"Час/Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Line("");

        foreach (var fontName in Config.FontNames)
        {
            string fntPath = Config.GetFntPath(originalFontsDir, fontName);
            string pngPath = System.IO.Path.Combine(originalFontsDir, fontName + ".png");
            if (!File.Exists(fntPath) || !File.Exists(pngPath))
            {
                Line($"[-] {fontName}: немає .fnt або .png / missing .fnt or .png");
                continue;
            }

            FontAnalysisResult analysis;
            try { analysis = FontAnalyzer.Analyze(fntPath); }
            catch (Exception ex) { Line($"[!] {fontName}: {ex.Message}"); continue; }

            using var img = Image.Load<Rgba32>(pngPath);

            var reference = ProcessFont(fontName, analysis, img, Line);
            Line("");
            _ = reference;
        }

        try
        {
            Directory.CreateDirectory(Config.DebugDir);
            File.WriteAllText(System.IO.Path.Combine(Config.DebugDir, "latin_reference.txt"), sb.ToString(), Encoding.UTF8);
            output.WriteLine($"Повний звіт збережено / Full report saved: {System.IO.Path.Combine(Config.DebugDir, "latin_reference.txt")}");
        }
        catch (Exception ex) { output.WriteLine($"[!] Не вдалося записати звіт / Failed to write report: {ex.Message}"); }
    }

    /// <summary>UA: Тісні межі чорнила в боксі запису (екранні top/bottom = YOffset + зсув). / EN: Tight ink bounds within a record's box (screen top/bottom = YOffset + offset).</summary>
    private static (int Top, int Bottom, int Left, int Right) Ink(Image<Rgba32> img, GlyphRecord r)
    {
        int x0 = Math.Clamp((int)r.AtlasX, 0, img.Width - 1);
        int y0 = Math.Clamp((int)r.AtlasY, 0, img.Height - 1);
        int x1 = Math.Clamp((int)(r.AtlasX + r.AtlasW), 0, img.Width);
        int y1 = Math.Clamp((int)(r.AtlasY + r.AtlasH), 0, img.Height);
        int t = -1, b = -1, l = -1, rr = -1;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (img[x, y].A > 10)
                {
                    if (t < 0) t = y;
                    b = y;
                    if (l < 0 || x < l) l = x;
                    if (x > rr) rr = x;
                }
        return (t, b, l, rr);
    }

    private static float Median(IEnumerable<float> xs)
    {
        var list = xs.Where(v => !float.IsNaN(v)).OrderBy(v => v).ToList();
        return list.Count > 0 ? list[list.Count / 2] : float.NaN;
    }

    private static FontReference ProcessFont(string fontName, FontAnalysisResult analysis, Image<Rgba32> img, Action<string> Line)
    {
        // UA: Екранний низ/верх чорнила літери = YOffset + (ink_y - AtlasY).
        // EN: A letter's screen ink bottom/top = YOffset + (ink_y - AtlasY).
        float? ScreenTop(int id)
        {
            var r = analysis.GetById(id);
            if (r == null) return null;
            var ink = Ink(img, r);
            return ink.Top < 0 ? null : r.YOffset + (ink.Top - (int)r.AtlasY);
        }
        float? ScreenBot(int id)
        {
            var r = analysis.GetById(id);
            if (r == null) return null;
            var ink = Ink(img, r);
            return ink.Bottom < 0 ? null : r.YOffset + (ink.Bottom - (int)r.AtlasY);
        }
        IEnumerable<float> Tops(params int[] ids) { foreach (var i in ids) { var v = ScreenTop(i); if (v.HasValue) yield return v.Value; } }
        IEnumerable<float> Bots(params int[] ids) { foreach (var i in ids) { var v = ScreenBot(i); if (v.HasValue) yield return v.Value; } }

        // UA: Baseline — медіана низу пласких літер (без виносу). / EN: Baseline — median bottom of flat letters (no descender).
        float baseline = Median(Bots('A', 'B', 'E', 'H', 'a', 'e', 'o', 'n', 'm', 'u'));
        float capTop = Median(Tops('A', 'B', 'E', 'H', 'I'));
        float xTop = Median(Tops('a', 'e', 'o', 'n', 'm', 'u', 'v', 'x', 'z'));
        float ascTop = Median(Tops('b', 'd', 'h', 'k', 'l'));
        float descBot = Median(Bots('g', 'p', 'q', 'y'));
        float dotTop = ScreenTop('i') ?? float.NaN;

        var reference = new FontReference(
            Baseline: baseline,
            CapHeight: baseline - capTop,
            XHeight: baseline - xTop,
            Ascender: baseline - ascTop,
            Descender: descBot - baseline,
            DotTopAboveBaseline: baseline - dotTop);

        Line($"── {fontName} ── (stride={analysis.Stride}, гліфів/glyphs={analysis.Records.Count})");
        Line($"   baseline={baseline:F0}  cap-height={reference.CapHeight:F0}  x-height={reference.XHeight:F0}  " +
             $"ascender={reference.Ascender:F0}  descender={reference.Descender:F0}  dot('i')-top-above-baseline={reference.DotTopAboveBaseline:F0}");
        // UA: Найважливіший висновок для дот-літер: КУДИ сягає крапка 'i' відносно x-height.
        // EN: The key takeaway for dotted letters: WHERE the 'i' dot reaches relative to x-height.
        if (!float.IsNaN(reference.DotTopAboveBaseline) && !float.IsNaN(reference.XHeight))
        {
            bool aboveX = reference.DotTopAboveBaseline > reference.XHeight + 0.5f;
            Line($"   → крапка 'i' сягає {(aboveX ? "ВИЩЕ x-height (нормальний вигляд)" : "рівно x-height (оригінал стиснутий)")} " +
                 $"/ 'i' dot reaches {(aboveX ? "ABOVE x-height (normal)" : "at x-height (original is compressed)")}");
        }

        // UA: Детальна таблиця по кожній ASCII-літері: .fnt-метрики + піксельне чорнило.
        // EN: Detailed per-ASCII-letter table: .fnt metrics + pixel ink.
        Line("   симв| ID | .fnt: Atlas[x,y,w,h] XOff YOff XAdv | ink: relT,B,L,R  W×H | scrTop..Bot | advGap");
        foreach (var r in analysis.Records.Where(r => r.IsAsciiPrintable && r.ID != 32).OrderBy(r => r.ID))
        {
            var ink = Ink(img, r);
            if (ink.Top < 0)
            {
                Line($"   '{(char)r.ID}' |{r.ID,4}| [{r.AtlasX:F0},{r.AtlasY:F0},{r.AtlasW:F0},{r.AtlasH:F0}] {r.XOffset,4:F0} {r.YOffset,4:F0} {r.XAdvance,4} | (порожньо/empty)");
                continue;
            }
            int relT = ink.Top - (int)r.AtlasY, relB = ink.Bottom - (int)r.AtlasY;
            int relL = ink.Left - (int)r.AtlasX, relR = ink.Right - (int)r.AtlasX;
            int inkW = ink.Right - ink.Left + 1, inkH = ink.Bottom - ink.Top + 1;
            float scrTop = r.YOffset + relT, scrBot = r.YOffset + relB;
            int advGap = r.XAdvance - inkW; // UA: просування мінус ширина чорнила / EN: advance minus ink width
            Line($"   '{(char)r.ID}' |{r.ID,4}| [{r.AtlasX,3:F0},{r.AtlasY,3:F0},{r.AtlasW,3:F0},{r.AtlasH,3:F0}] {r.XOffset,4:F0} {r.YOffset,4:F0} {r.XAdvance,4} | " +
                 $"{relT,2},{relB,2},{relL,2},{relR,2}  {inkW,2}×{inkH,2} | {scrTop,5:F0}..{scrBot,5:F0} | {advGap,3}");
        }

        return reference;
    }
}

// =============================================================================
// SWH.FontTool.Analyzer — DescenderProbeExperiment.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Контекст (2026-07-21): SingleGlyphGrowthExperiment підтвердив робочу
//     модель для 'і'/'ї'/'є'/'ґ' — УКРАЇНСЬКИХ-УНІКАЛЬНИХ літер, які всі БЕЗ
//     нижнього виносу (низ чорнила = базова лінія). Але не варто забувати
//     принцип: переписується геометрія ВСІХ 66 літер (навіть "спільних" —
//     р, у, ф, ц, щ), а серед них є справжні виносні, де низ чорнила НИЖЧЕ
//     базової лінії. Пряма перевірка на реальних даних (з original-fonts)
//     підтвердила: "інваріант" YOff+(inkBottom-AtlasY) для р/у ЗАВИЩЕНИЙ
//     відносно о/н на 7-8 px (напр. header_medium: о/н ≈ 49-50, р/у ≈ 57-59)
//     — бо їхнє чорнило справді опускається нижче базової лінії. Отже
//     припущення "низ чорнила = базова лінія" НЕ підходить для виносних, і
//     пряме перенесення SingleGlyphGrowthExperiment-логіки на них дало б
//     хибне вертикальне розміщення.
//
//     Цей клас НЕ чіпає жодного файлу — він суто АНАЛІТИЧНИЙ. Ідея: для
//     ІСНУЮЧИХ виносних літер (р, у, ф, ц, щ), чия справжня, правильна
//     геометрія вже відома з оригінального файлу (це "земля правди" —
//     ground truth), запускається алгоритм передбачення YOffset/AtlasH
//     (наче ці літери генеруються заново з нуля через TTF) і порівнюється
//     передбачення з реальним значенням. Це дає миттєву перевірку коректності
//     алгоритму — БЕЗ повторного цикла "згенеруй -> скопіюй у гру -> запусти
//     -> зроби скріншот", який уже забрав кілька ітерацій на 'і'.
//
//     Метод визначення базової лінії для виносної літери: рендеримо ОДИН
//     виклик DrawText зі СПІЛЬНИМ рядком "н  <ціль>" (два пробіли-розділювача)
//     — оскільки це ОДИН виклик, обидва символи лягають на СПІЛЬНУ базову
//     лінію (на відміну від окремих викликів, де в ImageSharp кожен рядок
//     вирівнюється до ВЛАСНОЇ рамки — це та сама пастка, що зламала першу
//     спробу для 'і'). Низ чорнила 'н' (без виносу) = базова лінія. Далі
//     шукаємо порожню колонку між двома кластерами чорнила, щоб розділити
//     піксельний скан на "референс" і "ціль" — без потреби точно знати
//     ширину просування пера (advance width) наперед.
// EN: Context (2026-07-21): SingleGlyphGrowthExperiment validated a working
//     model for 'і'/'ї'/'є'/'ґ' — the UKRAINIAN-UNIQUE letters, all WITHOUT a
//     descender (ink bottom = baseline). But the guiding principle still
//     applies: the geometry of ALL 66 letters gets rewritten (even "shared"
//     ones — р, у, ф, ц, щ), and among those are genuine descenders, where
//     ink bottom sits BELOW the baseline. A direct check against real data
//     (from original-fonts) confirmed: the "invariant" YOff+(inkBottom-AtlasY)
//     for р/у is INFLATED relative to о/н by 7-8 px (e.g. header_medium: о/н
//     ≈ 49-50, р/у ≈ 57-59) — because their ink genuinely extends below the
//     baseline. So the "ink bottom = baseline" assumption does NOT hold for
//     descenders, and directly porting the SingleGlyphGrowthExperiment logic
//     to them would produce wrong vertical placement.
//
//     This class touches NO files — it's purely ANALYTICAL. Idea: for
//     EXISTING descender letters (р, у, ф, ц, щ), whose real, correct
//     geometry is already known from the original file (the "ground truth"),
//     a prediction algorithm for YOffset/AtlasH runs (as if generating them
//     fresh from the TTF) and the prediction is compared against the real value.
//     This gives an instant correctness check — WITHOUT another
//     "generate -> copy into the game -> launch -> screenshot" cycle, which
//     already cost several iterations on 'і'.
//
//     Baseline-detection method for a descender letter: render ONE DrawText
//     call with the COMBINED string "н  <target>" (two separator spaces) —
//     because it's a SINGLE call, both characters land on a SHARED baseline
//     (unlike separate calls, where ImageSharp aligns each run to its OWN
//     bounding box — the exact trap that broke the first attempt for 'і').
//     The bottom of 'н' ink (no descender) = the baseline. Then it finds the
//     blank column between the two ink clusters to split the pixel scan into
//     "reference" and "target" — without needing to know the pen advance
//     width in advance.
// =============================================================================

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SWH.FontTool.Core;
using IOPath = System.IO.Path;

namespace SWH.FontTool.Analyzer;

public static class DescenderProbeExperiment
{
    private const int PadTop = 2;
    private const int PadBottom = 2;

    // UA: Лишні (не UA-унікальні) виносні нижнього регістру для перевірки.
    // EN: Common (not UA-unique) lowercase descenders to check.
    private static readonly (char Ch, int Id)[] Descenders =
    {
        ('р', 1088), ('у', 1091), ('ф', 1092), ('ц', 1094), ('щ', 1097)
    };

    public static void RunTest(string sourceDir, TextWriter log)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Папка не знайдена / Folder not found: {sourceDir}");

        var fntFiles = Directory.GetFiles(sourceDir, "*.fnt");
        log.WriteLine("Джерело (не змінюється, файли не пишуться) / Source (never modified, no files written): " + sourceDir);
        log.WriteLine("Перевірка: чи алгоритм передбачає YOffset/AtlasH виносних (р,у,ф,ц,щ) близько до РЕАЛЬНИХ значень.");
        log.WriteLine("Check: does the algorithm predict descender (р,у,ф,ц,щ) YOffset/AtlasH close to the REAL values.");
        log.WriteLine();

        foreach (var fontPath in fntFiles)
        {
            string name = IOPath.GetFileNameWithoutExtension(fontPath);
            string srcPng = IOPath.Combine(sourceDir, name + ".png");
            if (!File.Exists(srcPng)) { log.WriteLine($"{name,-20} пропущено / skipped (немає PNG)"); continue; }

            FontAnalysisResult analysis;
            try { analysis = FontAnalyzer.Analyze(fontPath); }
            catch (Exception ex) { log.WriteLine($"{name,-20} пропущено / skipped ({ex.Message})"); continue; }

            FontFamily? loaded = AtlasProcessor.LoadTtfFont(name);
            if (loaded is null) { log.WriteLine($"{name,-20} пропущено / skipped (немає TTF)"); continue; }
            FontFamily family = loaded.Value;

            var refLower = analysis.GetById(1072) ?? analysis.GetById(97); // а
            if (refLower is null) { log.WriteLine($"{name,-20} пропущено / skipped (немає 'а')"); continue; }

            using var original = Image.Load<Rgba32>(srcPng);

            // UA: Базова лінія і розмір тіла — та сама логіка, що вже
            //     підтверджена в SingleGlyphGrowthExperiment.
            // EN: Baseline and body size — the same logic already confirmed
            //     in SingleGlyphGrowthExperiment.
            (int Top, int Bottom) RefInk(GlyphRecord r)
            {
                int rx0 = Math.Clamp((int)r.AtlasX, 0, original.Width - 1);
                int ry0 = Math.Clamp((int)r.AtlasY, 0, original.Height - 1);
                int rx1 = Math.Clamp((int)(r.AtlasX + r.AtlasW), 0, original.Width);
                int ry1 = Math.Clamp((int)(r.AtlasY + r.AtlasH), 0, original.Height);
                int t = -1, b = -1;
                for (int y = ry0; y < ry1; y++)
                    for (int x = rx0; x < rx1; x++)
                        if (original[x, y].A > 10) { if (t < 0) t = y; b = y; break; }
                return (t, b);
            }

            var luRef = RefInk(refLower);
            if (luRef.Bottom < 0) { log.WriteLine($"{name,-20} пропущено / skipped (не знайшов чорнило 'а')"); continue; }
            float baseTrue = refLower.YOffset + (luRef.Bottom - (int)refLower.AtlasY);
            // UA: ВИПРАВЛЕНО (2026-07-24): (baseTrue - YOffset) = (inkBottom -
            //     AtlasY), що включає верхній запас оригінального боксу —
            //     тепер тісна висота чорнила (inkBottom - inkTop + 1).
            // EN: FIXED (2026-07-24): (baseTrue - YOffset) = (inkBottom -
            //     AtlasY), which includes the original box's top margin —
            //     now the tight ink height (inkBottom - inkTop + 1).
            float lowerBodyPx = luRef.Bottom - luRef.Top + 1;

            var testFont = family.CreateFont(100f, FontStyle.Bold);

            int RasterHeight(string s, Font f)
            {
                using var scr = new Image<Rgba32>(300, 400);
                scr.Mutate(c =>
                {
                    var rto = new RichTextOptions(f)
                    {
                        Origin = new PointF(30f, 300f),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    c.DrawText(rto, s, Brushes.Solid(Color.White));
                });
                int top = -1, bottom = -1;
                for (int y = 0; y < 400; y++)
                    for (int x = 0; x < 300; x++)
                        if (scr[x, y].A > 10) { if (top < 0) top = y; bottom = y; break; }
                return bottom >= 0 ? bottom - top + 1 : 0;
            }

            int rasterLowerH100 = RasterHeight(((char)refLower.ID).ToString(), testFont);
            if (rasterLowerH100 <= 0) { log.WriteLine($"{name,-20} пропущено / skipped (растр 'а' не вдався)"); continue; }
            Font font = family.CreateFont(100f * lowerBodyPx / rasterLowerH100, FontStyle.Bold);

            log.WriteLine($"=== {name} ===  baseTrue={baseTrue:F1}  lowerBodyPx={lowerBodyPx:F1}");

            // UA: Комбо-зонд: "н  X" в ОДНОМУ виклику DrawText — спільна базова лінія.
            // EN: Combo probe: "н  X" in ONE DrawText call — shared baseline.
            const int scratchW = 400, scratchH = 300;
            const float penX = 20f, penY = 60f;

            foreach (var (ch, id) in Descenders)
            {
                var real = analysis.GetById(id);
                if (real is null) { log.WriteLine($"  {ch}: немає в цьому шрифті / not in this font"); continue; }

                using var scr = new Image<Rgba32>(scratchW, scratchH);
                string probe = "н  " + ch;
                scr.Mutate(c =>
                {
                    var rto = new RichTextOptions(font)
                    {
                        Origin = new PointF(penX, penY),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    c.DrawText(rto, probe, Brushes.Solid(Color.White));
                });

                // UA: Знаходимо колонки з чорнилом, потім розрив між кластерами.
                // EN: Find ink columns, then the gap between clusters.
                var inkCols = new bool[scratchW];
                for (int x = 0; x < scratchW; x++)
                    for (int y = 0; y < scratchH; y++)
                        if (scr[x, y].A > 10) { inkCols[x] = true; break; }

                int firstInk = Array.IndexOf(inkCols, true);
                if (firstInk < 0) { log.WriteLine($"  {ch}: нічого не намальовано / nothing rendered"); continue; }

                // UA: Перший розрив (>=3 порожніх колонок підряд) після першого чорнила.
                // EN: First gap (>=3 consecutive blank columns) after the first ink.
                int gapStart = -1;
                int run = 0;
                for (int x = firstInk; x < scratchW; x++)
                {
                    if (!inkCols[x]) { run++; if (run >= 3 && gapStart < 0) gapStart = x - run + 1; }
                    else { if (gapStart >= 0) break; run = 0; }
                }
                if (gapStart < 0) { log.WriteLine($"  {ch}: не знайшов розрив між кластерами / no gap found between clusters"); continue; }

                (int Top, int Bottom) ScanRange(int x0, int x1)
                {
                    int t = -1, b = -1;
                    for (int y = 0; y < scratchH; y++)
                        for (int x = x0; x < x1; x++)
                            if (scr[x, y].A > 10) { if (t < 0) t = y; b = y; break; }
                    return (t, b);
                }

                var refCluster = ScanRange(0, gapStart);
                var tgtCluster = ScanRange(gapStart, scratchW);
                if (refCluster.Bottom < 0 || tgtCluster.Top < 0) { log.WriteLine($"  {ch}: скан кластера не вдався / cluster scan failed"); continue; }

                int baselineRow = refCluster.Bottom; // UA: низ 'н' = базова лінія / EN: bottom of 'н' = baseline

                int predAtlasH = PadTop + (tgtCluster.Bottom - tgtCluster.Top) + PadBottom;
                float predYOffset = baseTrue - (PadTop + (baselineRow - tgtCluster.Top));
                int descenderDepth = tgtCluster.Bottom - baselineRow;

                float dYOff = predYOffset - real.YOffset;
                float dAtlasH = predAtlasH - real.AtlasH;

                log.WriteLine($"  {ch}: реальні/real YOff={real.YOffset,6:F1} AtlasH={real.AtlasH,4:F0}  |  " +
                              $"передбачені/predicted YOff={predYOffset,6:F1} AtlasH={predAtlasH,4:D}  |  " +
                              $"різниця/diff YOff={dYOff,+6:F1} AtlasH={dAtlasH,+5:F0}  descDepth={descenderDepth}px");
            }
            log.WriteLine();
        }

        log.WriteLine("Готово / Done. Якщо різниця YOff у межах ~2-3px для всіх виносних — комбо-зондова модель годиться");
        log.WriteLine("для повного генератора. Якщо різниця більша (5+ px) — модель потребує доопрацювання ПЕРЕД тим,");
        log.WriteLine("як переносити її на всі 66 літер.");
        log.WriteLine("If the YOff difference is within ~2-3px for all descenders — the combo-probe model is good");
        log.WriteLine("enough for the full generator. If the difference is bigger (5+ px) — the model needs more work");
        log.WriteLine("BEFORE porting it to all 66 letters.");
    }
}

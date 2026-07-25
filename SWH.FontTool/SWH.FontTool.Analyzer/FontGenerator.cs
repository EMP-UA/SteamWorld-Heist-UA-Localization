// =============================================================================
// SWH.FontTool.Analyzer — FontGenerator.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Генератор українізованих .fnt файлів.
//
//     ПЕРЕХІД НА ПОВНЕ ПЕРЕПАКУВАННЯ АТЛАСУ (2026-07-21): раніше лише 8
//     UA-унікальних літер (Є,І,Ї,Ґ і малі) отримували повністю нову
//     геометрію (AtlasX/Y/W/H, XOffset/YOffset/XAdvance), розраховану з
//     реальних метрик TTF-шрифту, у ще вільному місці PNG. Решта 58
//     "спільних" кириличних літер (є в обох алфавітах — а,б,в,у,р,й...)
//     лишались на СТАРІЙ позиції зі СТАРОЮ геометрією від оригінального
//     растрового шрифту — перемальовувались лише пікселі.
//     Це не влаштовувало з двох причин: (1) жоден байт старої геометрії
//     не повинен лишатись активним — стара геометрія означала, що нові
//     TTF-пікселі підганялись під чужі, невідповідні рамки (звідси ручні
//     "підгонки" для у, р, й, б, ґ — коробка не пасувала до нового гліфа);
//     (2) стиль мав бути єдиним для всіх 66 літер, а не лише для 8.
//
//     PngCanvasExperiment (2026-07-21) емпірично підтвердив: рушій
//     толерантний до збільшення висоти PNG-текстури (гра запускається,
//     решта UI рендериться коректно). Це відкрило шлях до головної зміни
//     цього файлу: ТЕПЕР УСІ 66 УКРАЇНСЬКИХ ЛІТЕР (а не лише 8) отримують
//     повністю нову геометрію від PlanAllUaSlots, розміщену у свіжому,
//     щойно доданому знизу просторі PNG. Стара позиція й геометрія кожного
//     "донора" (як спільного, так і RU-унікального) більше не
//     використовується для рендеру взагалі — а сам донорський запис у
//     таблиці .fnt лишається лише як "носій" (той самий byte-слот, той
//     самий ID або новий UA ID), повністю перезаписаний новими даними.
//     Кількість гліфів (numChars) як і раніше НЕ росте — росте сама
//     PNG-текстура, а не таблиця .fnt.
// EN: Generator of Ukrainian .fnt files.
//
//     SWITCH TO A FULL ATLAS REPACK (2026-07-21): previously only the 8
//     UA-unique letters (Є,І,Ї,Ґ and lowercase) got fully new geometry
//     (AtlasX/Y/W/H, XOffset/YOffset/XAdvance) computed from real TTF font
//     metrics, placed in still-free PNG space. The other 58 "shared"
//     Cyrillic letters (present in both alphabets — а,б,в,у,р,й...) stayed
//     at their OLD position with the OLD geometry from the original
//     bitmap font — only the pixels were repainted. This had two problems:
//     (1) not a single byte of the old geometry should
//     remain active — keeping the old geometry meant the new TTF pixels
//     had to be squeezed into a box that didn't fit them (hence the manual
//     "adjustments" for у, р, й, б, ґ — the box didn't match the new
//     glyph's shape); (2) the style was supposed to be unified across all
//     66 letters, not just 8.
//
//     PngCanvasExperiment (2026-07-21) empirically confirmed: the engine
//     tolerates a taller PNG texture (the game launches, the rest of the UI
//     renders correctly). That opened the path to this file's main change:
//     NOW ALL 66 UKRAINIAN LETTERS (not just 8) get fully new geometry from
//     PlanAllUaSlots, placed in fresh space newly added below the existing
//     PNG content. Each donor's old position and geometry (shared or
//     RU-unique alike) is no longer used for rendering at all — the donor's
//     table record only survives as a "host" (the same byte slot, same or a
//     new UA ID), entirely overwritten with new data. The glyph count
//     (numChars) still does NOT grow — the PNG texture itself grows, not
//     the .fnt table.
// =============================================================================

using System.Buffers.Binary;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SWH.FontTool.Core;

namespace SWH.FontTool.Analyzer;

public static class FontGenerator
{
    // UA: Невеликі, задокументовані константи компонування. Це наближення,
    //     не остаточна типографська точність — тонке підлаштування кернінгу
    //     (XOffset тощо) можна ітерувати пізніше за результатами візуальної
    //     перевірки в грі, коли базова структура (розмір/позиція боксу)
    //     вже правильна.
    // EN: Small, documented layout constants. These are approximations, not
    //     final typographic precision — fine kerning tuning (XOffset etc.)
    //     can be iterated later based on in-game visual review, once the
    //     basic structure (box size/position) is already correct.
    private const int GlyphBoxPaddingTop = 2;
    private const int GlyphBoxPaddingBottom = 2;
    private const int GlyphBoxPaddingSidesPx = 4;
    private const int SlotGapPx = 2;
    private const int RowMarginTopPx = 4;
    private const float DefaultXOffsetPx = 2f;

    // UA: 2026-07-25 — Запас під контурний рендер-прохід (AtlasProcessor.
    //     RenderGlyphSolo, FactionsOutlinePx=2) — той самий контур розширює
    //     реальне чорнило на ~2px З КОЖНОГО боку (лівий/правий/верх/низ)
    //     понад звичайну антиаліасинг-подушку. Якщо бокс тут не збільшити,
    //     контур physически вилазить за межі виділеної під літеру ділянки
    //     атласу й може "протекти" в сусідню літеру. Застосовується ЛИШЕ до
    //     "factions" (перевіряється по analysis.FontName нижче) — решта
    //     шрифтів контуру не мають і цей запас їм не потрібен.
    // EN: 2026-07-25 — Slack for the outline render pass (AtlasProcessor.
    //     RenderGlyphSolo, FactionsOutlinePx=2) — that outline expands the
    //     actual ink by ~2px on EACH side (left/right/top/bottom) beyond the
    //     normal anti-aliasing cushion. If the box isn't enlarged here, the
    //     outline would physically stick out past the letter's allocated
    //     atlas region and could bleed into a neighboring letter. Applies
    //     ONLY to "factions" (checked via analysis.FontName below) — every
    //     other font has no outline and doesn't need this slack.
    private const int FactionsOutlinePx = 2;

    // UA: Мінімальні підлоги ширини/просування — суто захист від виродження
    //     (наприклад, якщо растровий замір гліфа з якоїсь причини не вдався).
    //     Історична примітка: "гліф занадто вузький" (2026-07-21) була
    //     першою гіпотезою щодо бага "'і' летить у грі" — перевірена й
    //     СПРОСТОВАНА (нуль ефекту). Справжня причина (з'ясована через
    //     SingleGlyphGrowthExperiment та DescenderProbeExperiment): базова
    //     лінія й розмір шрифту рахувались із ненадійної евристики
    //     (BaselineConstant) та векторного MeasureBounds, що розходились із
    //     фактичним растровим рендером. Виправлено нижче — вся геометрія
    //     тепер рахується растрово, через комбо-зонд "н  X" (детальніше в
    //     коментарі до PlanAllUaSlots).
    // EN: Minimum width/advance floors — purely a degeneracy guard (e.g. if a
    //     glyph's raster measurement fails for some reason). Historical note:
    //     "the glyph is too narrow" (2026-07-21) was the FIRST hypothesis for
    //     the "'і' floats in-game" bug — tested and DISPROVEN (zero effect).
    //     The real cause (found via SingleGlyphGrowthExperiment and
    //     DescenderProbeExperiment): the baseline and font size were computed
    //     from an unreliable heuristic (BaselineConstant) and vector
    //     MeasureBounds, which diverged from the actual raster render. Fixed
    //     below — all geometry is now computed raster-side, via a "н  X"
    //     combo probe (see the PlanAllUaSlots comment for details).
    private const int MinGlyphBoxWidthPx = 12;
    private const int MinXAdvancePx = 9;

    /// <summary>UA: Генерує всі шрифти з Config.FontNames (крім Config.SkipFonts). / EN: Generates every font from Config.FontNames (except Config.SkipFonts).</summary>
    public static void ProcessAllFonts()
    {
        Console.WriteLine("\n>>> UA ГЕНЕРАЦІЯ: FNT + PNG + ЗВІТ / UA GENERATION: FNT + PNG + REPORT <<<\n");
        Console.WriteLine($"{"Шрифт/Font",-20} | {"UA/66",6} | {"Baseline",10} | Статус/Status");
        Console.WriteLine(new string('-', 65));

        foreach (var fontName in Config.FontNames)
        {
            try { ProcessSingleFont(fontName); }
            catch (Exception ex) { Console.WriteLine($" [!] {fontName,-18}: {ex.Message}"); }
        }

        Console.WriteLine("\nГенерацію завершено. Перевірте папку debug для звітів та PNG.");
    }

    /// <summary>
    /// UA: Генерує один шрифт: план перепризначень + повна геометрія для
    /// ВСІХ 66 UA-літер (PlanAllUaSlots) + патч .fnt + новий, вищий PNG +
    /// самоперевірка + звіт.
    /// EN: Generates a single font: remap plan + full geometry for ALL 66
    /// UA letters (PlanAllUaSlots) + .fnt patch + a taller PNG + self-check
    /// + report.
    /// </summary>
    private static void ProcessSingleFont(string fontName)
    {
        if (Config.SkipFonts.Contains(fontName)) return;

        string inPath = Config.GetFntPath(Config.OriginalFontsDir, fontName);
        string outPath = Config.GetFntPath(Config.OutputDir, fontName);
        string srcPng = System.IO.Path.Combine(Config.OriginalFontsDir, fontName + ".png");

        if (!File.Exists(inPath)) return;

        var analysis = FontAnalyzer.Analyze(inPath);
        var plan = AlphabetProcessor.BuildRemapPlan(analysis);

        var allSlots = new Dictionary<int, GlyphRecord>();
        int newImageHeight = 0;

        if (File.Exists(srcPng))
        {
            // UA: Повне завантаження пікселів (не лише Image.Identify) —
            //     потрібне тепер, бо PlanAllUaSlots вимірює справжню базову
            //     лінію напряму з пікселів референс-літери (той самий трюк,
            //     що вже підтверджений у SingleGlyphGrowthExperiment).
            // EN: A full pixel load (not just Image.Identify) — needed now
            //     because PlanAllUaSlots measures the true baseline directly
            //     from the reference letter's pixels (the same trick already
            //     validated in SingleGlyphGrowthExperiment).
            using var srcImage = Image.Load<Rgba32>(srcPng);
            (allSlots, newImageHeight) = PlanAllUaSlots(analysis, plan, srcImage, srcImage.Height);
        }

        byte[] outData = File.ReadAllBytes(inPath);
        PatchFnt(outData, analysis, plan, allSlots);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
        File.WriteAllBytes(outPath, outData);

        // UA: Пост-генераційна самоперевірка — див. FontAnalyzer.ValidateGeneratedFont.
        // EN: Post-generation self-check — see FontAnalyzer.ValidateGeneratedFont.
        var issues = FontAnalyzer.ValidateGeneratedFont(inPath, outPath);
        if (issues.Count > 0)
            throw new InvalidDataException(
                $"Самоперевірка провалена / self-check failed for {fontName}:\n  " +
                string.Join("\n  ", issues));

        if (File.Exists(srcPng))
            AtlasProcessor.GenerateUaPng(fontName, analysis, plan, allSlots, newImageHeight);

        int uaMapped = plan.Values.Count(AlphabetProcessor.IsUa);
        SaveReport(fontName, analysis, plan, allSlots);
        Console.WriteLine($" [+] {fontName,-20} | {uaMapped,4}/66 | {analysis.BaselineConstant,10:F3} | OK (самоперевірка пройдена/self-check passed)");
    }

    /// <summary>
    /// UA: Для КОЖНОЇ української літери в plan (усі 66 — і спільні, і
    /// унікальні, без різниці) вимірює РЕАЛЬНИЙ намальований гліф цільового
    /// TTF-шрифту РАСТРОВО (не векторно) і повертає карту готових слотів +
    /// підсумкову висоту зображення.
    ///
    /// ІСТОРІЯ ВИПРАВЛЕНЬ (2026-07-21..24, через SingleGlyphGrowthExperiment,
    /// DescenderProbeExperiment та реальні тести в грі на 'і'):
    ///   1. Базова лінія РАНІШЕ бралась з analysis.BaselineConstant — це
    ///      евристика (медіана по латинських великих), яка для реальних
    ///      шрифтів занижена на ~6 px відносно СПРАВЖНЬОЇ пікселної базової
    ///      лінії. Замінено на baseTrue — вимірюється НАПРЯМУ з пікселів
    ///      референс-літери 'а' в оригінальному PNG.
    ///   2. Розмір шрифту РАНІШЕ калібрувався через TextMeasurer.MeasureBounds
    ///      (векторний бокс гліфа), а фінальний рендер — растровий (DrawText +
    ///      піксельний скан). Це ДВА РІЗНІ методи вимірювання, і між ними
    ///      виявилась систематична розбіжність (~15-20% заниження розміру).
    ///      Замінено на растрове само-калібрування: малюємо саму
    ///      референс-літеру при 100pt, міряємо її РЕАЛЬНУ растрову висоту тим
    ///      самим методом, і масштабуємо звідти.
    ///   3. Вертикальне розміщення РАНІШЕ рахувалось через
    ///      font.FontMetrics.HorizontalMetrics.Ascender — константу висоти
    ///      РЯДКА шрифту, однакову для БУДЬ-ЯКОГО гліфа, а не специфічну для
    ///      конкретної літери. Це ламалось для двох категорій:
    ///        (a) літер БЕЗ нижнього виносу, де верх чорнила НЕ сягає повного
    ///            ascender (напр. 'і' з крапкою — верх вищий за x-height, але
    ///            нижчий за capHeight);
    ///        (b) літер З нижнім виносом (р,у,ф,ц,щ), де низ чорнила
    ///            опускається НИЖЧЕ базової лінії — на це ascender-формула не
    ///            зважає взагалі.
    ///      Замінено на КОМБО-ЗОНД: рендеримо ОДИН виклик DrawText з рядком
    ///      "н  X" (референс-літера без виносу + два пробіли-розділювача +
    ///      цільова літера X). Оскільки це ОДИН виклик, обидва символи
    ///      лягають на СПІЛЬНУ базову лінію (на відміну від окремих викликів,
    ///      де в ImageSharp кожен рядок вирівнюється до ВЛАСНОЇ рамки — пастка,
    ///      яка зламала першу спробу для 'і'). Низ чорнила 'н' = базова лінія;
    ///      верх/низ чорнила X вимірюються відносно НЕЇ — універсально, працює
    ///      однаково для звичайних літер, дот-літер (і,ї) і виносних (р,у,ф,
    ///      ц,щ), без жодних спеціальних гілок коду. Підтверджено математично
    ///      на реальних даних (DescenderProbeExperiment): різниця з реальними
    ///      YOffset — 1-2px для р/у/ц/щ, трохи більша (3-6px) для ф (єдина
    ///      літера з ОБОМА виносами — вгору й вниз одночасно).
    /// EN: For EVERY Ukrainian letter in plan (all 66 — shared and unique
    /// alike, no distinction), measures the REAL drawn glyph of the target
    /// TTF font RASTER-side (not vector-side) and returns the finished slot
    /// map + the final image height.
    ///
    /// FIX HISTORY (2026-07-21..24, via SingleGlyphGrowthExperiment,
    /// DescenderProbeExperiment, and real in-game tests on 'і'):
    ///   1. The baseline USED TO come from analysis.BaselineConstant — a
    ///      heuristic (median over Latin capitals) that underestimates the
    ///      REAL pixel baseline by ~6 px for these fonts. Replaced with
    ///      baseTrue — measured DIRECTLY from the 'а' reference letter's
    ///      pixels in the original PNG.
    ///   2. Font size USED TO be calibrated via TextMeasurer.MeasureBounds (a
    ///      vector glyph box), while the final render is raster (DrawText +
    ///      pixel scan). These are TWO DIFFERENT measurement methods, and a
    ///      systematic gap between them showed up (~15-20% undersizing).
    ///      Replaced with raster self-calibration: draw the reference letter
    ///      itself at 100pt, measure its REAL raster height with the SAME
    ///      method, and scale from there.
    ///   3. Vertical placement USED TO be computed via
    ///      font.FontMetrics.HorizontalMetrics.Ascender — a font-wide LINE
    ///      height constant, identical for ANY glyph, not specific to the
    ///      actual character. This broke for two categories:
    ///        (a) letters WITHOUT a descender whose ink top doesn't reach the
    ///            full ascender (e.g. dotted 'і' — taller than x-height but
    ///            shorter than capHeight);
    ///        (b) letters WITH a descender (р,у,ф,ц,щ), whose ink dips BELOW
    ///            the baseline — the ascender formula doesn't account for
    ///            this at all.
    ///      Replaced with a COMBO PROBE: render ONE DrawText call with the
    ///      string "н  X" (a no-descender reference letter + a two-space
    ///      separator + the target letter X). Because it's ONE call, both
    ///      characters land on a SHARED baseline (unlike separate calls,
    ///      where ImageSharp aligns each run to its OWN bounding box — the
    ///      trap that broke the first attempt at 'і'). The bottom of 'н'
    ///      ink = the baseline; X's ink top/bottom are measured relative to
    ///      THAT — universal, works the same for plain letters, dotted
    ///      letters (і,ї), and descenders (р,у,ф,ц,щ), with no special-case
    ///      branches. Confirmed mathematically against real data
    ///      (DescenderProbeExperiment): the difference from the real YOffset
    ///      is 1-2px for р/у/ц/щ, somewhat larger (3-6px) for ф (the only
    ///      letter with BOTH an ascender-like loop AND a descender at once).
    /// </summary>
    public static (Dictionary<int, GlyphRecord> Slots, int NewImageHeight) PlanAllUaSlots(
        FontAnalysisResult analysis, Dictionary<int, int> plan, Image<Rgba32> original, int originalImageHeight)
    {
        var result = new Dictionary<int, GlyphRecord>();
        int imageWidth = original.Width;

        FontFamily? loadedFamily = AtlasProcessor.LoadTtfFont(analysis.FontName);
        if (loadedFamily == null) return (result, originalImageHeight);
        FontFamily family = loadedFamily.Value;

        // UA: ВИПРАВЛЕНО (2026-07-24 — калібрування розміру/базової лінії
        //     досі йшло ВІД оригінальної кирилиці (1040/1072), лише як
        //     запасний варіант використовуючи латинку). Перевірено напряму
        //     пікселями: у шрифтах на Oswald-Bold (header_medium/
        //     header_small/factions) оригінальна 'а'(1072) на 13-17% НИЖЧА
        //     за 'a'(97) В ТОМУ Ж ФАЙЛІ (header_medium: 24 vs 29px;
        //     header_small: 22 vs 26px; factions: 34 vs 39px) — тобто
        //     оригінальна кирилиця в цих шрифтах сама була невідповідного/
        //     іншого розміру, і калібрування по ній успадковувало цей
        //     дефект як "справжній розмір". У Comfortaa-шрифтах (body_*,
        //     ingame*) різниці НЕМА (Latin і Cyrillic ідентичні), тож
        //     перехід на латинку там нічого не ламає — лише прибирає
        //     приховану залежність від оригінальної кирилиці там, де вона
        //     реально шкодила.
        //     Тепер ЛАТИНКА (гарантовано коректна — англійська версія гри
        //     відвантажується й виглядає правильно) — ПЕРШЕ джерело
        //     референсу; кирилиця — лише запасний варіант, якщо в шрифті
        //     раптом взагалі нема ASCII (не мало би траплятись).
        // EN: FIXED (2026-07-24 — size/baseline calibration was still
        //     happening FROM the original Cyrillic (1040/1072), using Latin
        //     only as a fallback). Verified directly with pixels: in the
        //     Oswald-Bold fonts (header_medium/header_small/factions) the
        //     original 'а'(1072) is 13-17% SHORTER than 'a'(97) IN THE SAME
        //     FILE (header_medium: 24 vs 29px; header_small: 22 vs 26px;
        //     factions: 34 vs 39px) — meaning the original Cyrillic itself
        //     was an inconsistent/off size in these fonts, and calibrating
        //     off it meant inheriting THAT defect as the "true size." In the
        //     Comfortaa fonts (body_*, ingame*) there's NO difference (Latin
        //     and Cyrillic are identical), so switching to Latin breaks
        //     nothing there — it just removes a hidden dependency on the
        //     original Cyrillic exactly where it was actually harmful.
        //     Now LATIN (guaranteed correct — the English version of the
        //     game ships and looks right) is the FIRST reference source;
        //     Cyrillic is only a fallback if the font somehow has no ASCII
        //     at all (shouldn't happen in practice).
        var refUpper = analysis.GetById(72) ?? analysis.GetById(1040); // 'H' (or 'А' as fallback)
        var refLower = analysis.GetById(97) ?? analysis.GetById(1072); // 'a' (or 'а' as fallback)
        if (refUpper == null || refLower == null) return (result, originalImageHeight);

        // UA: Крок 1 — справжня базова лінія з пікселів (не з BaselineConstant).
        // EN: Step 1 — the real baseline from pixels (not from BaselineConstant).
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
        var upRef = RefInk(refUpper);
        float baseTrue = luRef.Bottom >= 0
            ? refLower.YOffset + (luRef.Bottom - (int)refLower.AtlasY)
            : analysis.BaselineConstant;

        // UA: ІСТОРІЯ XAdvance (просування курсора = міжлітерний інтервал):
        //     v1 (флет-константа mWidth + padding/2) — роздувала інтервал на
        //         6-29% → UI ламався.
        //     v2 (boxW + медіана(XAdvance − AtlasW)) — ЗЛАМАЛА інтервали
        //         повністю в частині шрифтів (літери злиплись): база
        //         калібрувалась відносно AtlasW (бокс-спрайт з ВЕЛИКИМ,
        //         залежним від шрифту, полем — особливо в Oswald-Bold, де
        //         оригінальний спрайт значно ширший за чорнило), а
        //         застосовувалась до нового боксу з крихітним фіксованим
        //         полем 4px. Виміряно: Comfortaa-шрифти (body_*, ingame*) були
        //         ок (медіана відхилення 0), але Oswald-Bold (header_medium,
        //         factions, header_small) просував на 5-9px МЕНШЕ за оригінал —
        //         звідси "не всі шрифти повернули нормальну відстань".
        //     v3 (ЦЯ ВЕРСІЯ, 2026-07-24) — найнадійніша й концептуально
        //         правильна: для 58 СПІЛЬНИХ літер (а,б,в,н,о,р,у...), які
        //         вже існують в оригінальному файлі, береться напряму оригінальний
        //         XAdvance цієї ж літери. Це МОВНО-НЕЙТРАЛЬНИЙ інтервал
        //         (літера 'а' однаково широка в обох мовах), і він гарантує,
        //         що розкладка UI лишається ІДЕНТИЧНОЮ до оригіналу —
        //         тобто UI фізично не може переповнитись. Помилка для цих
        //         58 літер = рівно 0.
        //         Лише 8 УНІКАЛЬНИХ UA-літер (Є,І,Ї,Ґ + малі), яких в оригіналі
        //         нема, отримують ОБЧИСЛЕНИЙ інтервал: тісна ширина чорнила
        //         (mWidth) + inkGapPx (медіана "XAdvance − ширина ЧОРНИЛА"
        //         оригіналу — база "чорнило", та сама, що й mWidth, тому
        //         збігається коректно). Перевірено: для уніків дає 9-23px,
        //         жодного накладання.
        // EN: XAdvance HISTORY (cursor advance = inter-letter spacing):
        //     v1 (flat constant mWidth + padding/2) — inflated spacing 6-29%
        //         → the UI broke.
        //     v2 (boxW + median(XAdvance − AtlasW)) — COMPLETELY broke spacing
        //         in some fonts (letters ran together): the basis was
        //         calibrated against AtlasW (the sprite box with LARGE,
        //         font-specific padding — especially Oswald-Bold, where the
        //         original sprite is far wider than the ink), but applied to
        //         the new box with a tiny fixed 4px padding. Measured:
        //         Comfortaa fonts (body_*, ingame*) were fine (median deviation
        //         0), but Oswald-Bold (header_medium, factions, header_small)
        //         advanced 5-9px LESS than the original — hence "not all fonts
        //         got normal spacing back."
        //     v3 (THIS VERSION, 2026-07-24) — the most robust and conceptually
        //         correct: for the 58 SHARED letters (а,б,в,н,о,р,у...) that
        //         already exist in the original file, JUST REUSE that same
        //         letter's original XAdvance. This is a
        //         LANGUAGE-NEUTRAL advance (the letter 'а' is equally
        //         wide in both languages), and it guarantees the UI layout
        //         stays IDENTICAL to the original — so the UI
        //         physically can't overflow. Error for those 58 letters = 0.
        //         Only the 8 UNIQUE UA letters (Є,І,Ї,Ґ + lowercase), absent
        //         from the original, get a COMPUTED advance: tight ink width
        //         (mWidth) + inkGapPx (median of "XAdvance − INK width" in the
        //         original — an "ink" basis, the same as mWidth, so they match
        //         correctly). Verified: yields 9-23px for the uniques, no
        //         overlap anywhere.
        // UA: ТРЕКІНГ-КОЕФІЦІЄНТ ЦЬОГО ШРИФТУ З ЕТАЛОНУ (2026-07-24, за повним
        //     зрізом латиниці — пункт меню 11). Діагностика показала: щільність
        //     міжлітерного інтервалу РАДИКАЛЬНО різна по шрифтах —
        //     відношення XAdvance/ширина_чорнила в оригіналі коливається від
        //     ~0.64 (factions — сильно конденсований) до ~1.00 (body_*). Тобто
        //     "єдиний підхід" (чи то фікс-константа, чи то природний advance
        //     TTF ≈1.0) НЕ може відтворити оригінал: для Oswald-шрифтів текст
        //     виходив на 25-55% ШИРШИМ за оригінал → переповнення UI
        //     (напр. ім'я "Пайпер" налазило на рамку).
        //
        //     Рішення (індивідуально на шрифт, не універсально): рахуємо
        //     МЕДІАННЕ відношення XAdvance/ширина_чорнила по всіх латинських
        //     літерах ЦЬОГО шрифту в оригіналі — це і є його справжня щільність
        //     трекінгу. XAdvance нової літери = ширина_її_чорнила × цей
        //     коефіцієнт. Множник (а не додаток) масштабується коректно з
        //     шириною літери: вузька 'т' і широка 'ш' обидві отримують
        //     інтервал, пропорційний власній ширині, у тій самій щільності, що
        //     й оригінал. Для factions коеф. ≈0.64 навмисно відтворює
        //     конденсоване накладання — так само, як в оригінальній
        //     англійській (яка з таким трекінгом читабельна).
        // EN: THIS FONT'S TRACKING RATIO FROM THE REFERENCE (2026-07-24, from
        //     the full Latin snapshot — menu item 11). The diagnostic showed:
        //     inter-letter spacing density varies RADICALLY per font — the
        //     original's XAdvance/ink-width ratio ranges from ~0.64 (factions,
        //     heavily condensed) to ~1.00 (body_*). So a "single approach"
        //     (whether a fixed constant or the TTF's natural advance ≈1.0)
        //     CAN'T reproduce the original: for the Oswald fonts the text came
        //     out 25-55% WIDER than the original → UI overflow (e.g. the name
        //     "Пайпер" overran its frame).
        //
        //     Solution (per-font, not universal): compute the MEDIAN
        //     XAdvance/ink-width ratio over all Latin letters of THIS font in
        //     the original — that is its true tracking density. A new letter's
        //     XAdvance = its_ink_width × this ratio. A multiplier (not an
        //     addend) scales correctly with letter width: a narrow 'т' and a
        //     wide 'ш' both get spacing proportional to their own width, at the
        //     same density as the original. For factions the ratio ≈0.64
        //     deliberately reproduces the condensed overlap — exactly like the
        //     original English (which is readable at that tracking).
        int InkWidthOf(GlyphRecord r)
        {
            int rx0 = Math.Clamp((int)r.AtlasX, 0, original.Width - 1);
            int ry0 = Math.Clamp((int)r.AtlasY, 0, original.Height - 1);
            int rx1 = Math.Clamp((int)(r.AtlasX + r.AtlasW), 0, original.Width);
            int ry1 = Math.Clamp((int)(r.AtlasY + r.AtlasH), 0, original.Height);
            int l = -1, rr = -1;
            for (int x = rx0; x < rx1; x++)
                for (int y = ry0; y < ry1; y++)
                    if (original[x, y].A > 10) { if (l < 0) l = x; rr = x; break; }
            return l >= 0 ? rr - l + 1 : 0;
        }
        var ratioSamples = new List<float>();
        foreach (var r in analysis.Records.Where(r => r.AtlasW > 0 && r.IsAsciiPrintable && r.XAdvance > 0))
        {
            int iw = InkWidthOf(r);
            if (iw > 2) ratioSamples.Add((float)r.XAdvance / iw);
        }
        float advanceRatio = ratioSamples.Count > 0 ? Median(ratioSamples) : 1.0f;
        bool isFactionsFont = analysis.FontName.Equals("factions", StringComparison.OrdinalIgnoreCase);

        // UA: ВИПРАВЛЕНО (2026-07-24, після реального тесту в грі на 66
        //     літерах — "усі літери завеликі на ~10-15%"): цільова висота
        //     тіла РАНІШЕ рахувалась як (inkBottom - AtlasY), тобто "від низу
        //     чорнила до ВЕРХУ БОКСУ" — а не до верху САМОГО ЧОРНИЛА. В
        //     оригінальних боксах завжди був запас (~3px) між
        //     AtlasY і реальним верхом чорнила (перевірено напряму:
        //     body_medium — 3px, header_medium — 3px), і стара формула
        //     трактувала цей запас як частину "висоти тіла літери",
        //     систематично роздуваючи ціль калібрування на ~10-17% (виміряно:
        //     20 замість справжніх 18 у body_medium, 26 замість 24 у
        //     header_medium — саме ці ~10-12% і відповідають поскарженому
        //     "завеликому" розміру). Виправлено: тепер це ТІСНА висота
        //     чорнила референс-літери (inkBottom - inkTop + 1), без жодної
        //     залежності від старого AtlasY/padding.
        // EN: FIXED (2026-07-24, after a real in-game test on all 66 letters
        //     — "every letter is ~10-15% too big"): the target body height
        //     USED TO be computed as (inkBottom - AtlasY), i.e. "from the ink
        //     bottom to the BOX TOP" — not to the top of the ACTUAL INK. The
        //     original boxes always had some margin (~3px) between
        //     AtlasY and the real ink top (confirmed directly: body_medium —
        //     3px, header_medium — 3px), and the old formula treated that
        //     margin as part of the "letter body height," systematically
        //     inflating the calibration target by ~10-17% (measured: 20
        //     instead of the true 18 in body_medium, 26 instead of 24 in
        //     header_medium — exactly the ~10-12% matching the reported
        //     "too big" size). Fixed: this is now the TIGHT ink height of the
        //     reference letter (inkBottom - inkTop + 1), with no dependency
        //     on the old AtlasY/padding at all.
        float lowerBodyPx = luRef.Bottom >= 0 ? (luRef.Bottom - luRef.Top + 1) : (baseTrue - refLower.YOffset);
        float upperBodyPx = upRef.Bottom >= 0 ? (upRef.Bottom - upRef.Top + 1) : (baseTrue - refUpper.YOffset);

        // UA: Крок 2 — растрове само-калібрування розміру шрифту.
        // EN: Step 2 — raster self-calibration of the font size.
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
        int rasterUpperH100 = RasterHeight(((char)refUpper.ID).ToString(), testFont);

        Font lowerFont = family.CreateFont(rasterLowerH100 > 0 ? 100f * lowerBodyPx / rasterLowerH100 : lowerBodyPx, FontStyle.Bold);
        Font upperFont = family.CreateFont(rasterUpperH100 > 0 ? 100f * upperBodyPx / rasterUpperH100 : upperBodyPx, FontStyle.Bold);

        // UA: ВИСОТА ЛІТЕР З КРАПКОЮ (і,ї) — БЕЗ ШТУЧНОГО ОБМЕЖЕННЯ (2026-07-24).
        //     Була спроба "обмежити висоту і/ї висотою латинської i" — і саме
        //     вона й ЗЛАМАЛА вигляд у частині шрифтів. Діагностика еталону
        //     (вимір усієї латиниці) показала ЧОМУ: у шрифтах на Oswald
        //     оригінальна латинська 'i' САМА "стиснута" — її крапка сидить рівно
        //     на x-height (dotTop == xTop). Тобто, обмежуючи нову 'і' до висоти
        //     тієї 'i', копіювалась стиснутість оригіналу. Натомість природний
        //     рендер із TTF (розмір калібрований по 'a' = x-height) сам по собі
        //     дає ПРАВИЛЬНИЙ вигляд для ОБОХ родин шрифтів: паличка = x-height
        //     (як решта малих), а крапка природно сідає ≈ на cap-height. Тому
        //     ЖОДНОГО масштабування дот-літер більше не робиться — рендериться як є.
        //     (Залишковий нюанс: precomposed 'ї' у Comfortaa має діакритику
        //     трохи вищу за cap-height — це вже властивість самого TTF;
        //     уніфікувати її без композитингу з окремих гліфів не можна, і
        //     паличка свідомо НЕ стискається заради цього.)
        // EN: HEIGHT OF DOTTED LETTERS (і,ї) — NO ARTIFICIAL CAP (2026-07-24).
        //     A "cap і/ї to the Latin i height" attempt was exactly what BROKE
        //     the look in some fonts. The reference diagnostic (measuring all
        //     Latin letters) showed WHY: in the Oswald fonts the original Latin
        //     'i' is ITSELF "compressed" — its dot sits right at x-height
        //     (dotTop == xTop). So capping the new 'і' to that 'i' copied the
        //     original's compression. The natural TTF render (size calibrated
        //     on 'a' = x-height) already gives the CORRECT look for BOTH font
        //     families: the stick = x-height (like other lowercase) and the dot
        //     naturally lands ≈ at cap-height. So dotted letters are no longer
        //     scaled at all — they render as-is. (Residual nuance: the
        //     precomposed 'ї' in Comfortaa has a diacritic slightly above
        //     cap-height — that's a property of the TTF itself; unifying it
        //     without compositing from separate glyphs isn't possible, and the
        //     stick is deliberately NOT compressed just for that.)
        // UA: Стеля висоти для ї/Ї = висота великих літер (2026-07-24). і/ю
        //     природний рендер лишається (паличка=x-height, крапка ≈cap-height —
        //     правильно). АЛЕ саме ї/Ї у Comfortaa мають precomposed-діакритику,
        //     що сидить ЗАВИСОКО: виміряно ї/cap = 1.20-1.27 у body/ingame
        //     ("мала ї розміром з велику літеру"). В Oswald цього нема
        //     (ї/cap≈1.05), тож стеля спрацює лише там, де реально треба.
        //     Обмежуємо повну висоту ї до висоти великої літери (діакритика не
        //     має перевищувати cap-height) — легке стиснення палички прийнятне
        //     й помітно краще за "ї вища за У".
        // EN: Height ceiling for ї/Ї = cap height (2026-07-24). і keeps its
        //     natural render (stick=x-height, dot ≈cap-height — correct). BUT
        //     ї/Ї specifically have a precomposed diacritic in Comfortaa that
        //     sits TOO HIGH: measured ї/cap = 1.20-1.27 in body/ingame ("small
        //     ї as tall as a capital"). Oswald doesn't have this (ї/cap≈1.05),
        //     so the ceiling only fires where actually needed. Cap ї's full
        //     height to the cap height (a diacritic shouldn't exceed cap-height)
        //     — a slight stick squeeze is acceptable and clearly better than
        //     "ї taller than У".
        Font GlyphFont(int uaId, bool isUpper)
        {
            Font bf = isUpper ? upperFont : lowerFont;
            if (uaId is 1111 or 1031) // ї Ї
            {
                int ceil = (int)Math.Round(upperBodyPx); // UA: висота великих / EN: cap height
                int natural = RasterHeight(((char)uaId).ToString(), bf);
                if (ceil > 0 && natural > ceil)
                    return family.CreateFont(bf.Size * ceil / natural, FontStyle.Bold);
            }
            return bf;
        }

        // UA: Крок 3 — комбо-зонд "н  X" / "Н  X" для КОЖНОЇ літери:
        //     ширина чорнила + відстань від базової лінії до верху/низу.
        // EN: Step 3 — the "н  X" / "Н  X" combo probe for EVERY letter:
        //     ink width + the distance from the baseline to the top/bottom.
        (int Width, int AscentAboveBaseline, int DescentBelowBaseline) MeasureGlyph(char target, Font f, bool isUpper)
        {
            char refCh = isUpper ? 'Н' : 'н';
            const int scratchW = 400, scratchH = 300;
            const float penX = 20f, penY = 60f;

            using var scr = new Image<Rgba32>(scratchW, scratchH);
            string probe = refCh + "  " + target;
            scr.Mutate(c =>
            {
                var rto = new RichTextOptions(f)
                {
                    Origin = new PointF(penX, penY),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                c.DrawText(rto, probe, Brushes.Solid(Color.White));
            });

            var inkCols = new bool[scratchW];
            for (int x = 0; x < scratchW; x++)
                for (int y = 0; y < scratchH; y++)
                    if (scr[x, y].A > 10) { inkCols[x] = true; break; }

            int firstInk = Array.IndexOf(inkCols, true);
            if (firstInk < 0) return (0, 0, 0);

            int gapStart = -1, run = 0;
            for (int x = firstInk; x < scratchW; x++)
            {
                if (!inkCols[x]) { run++; if (run >= 3 && gapStart < 0) gapStart = x - run + 1; }
                else { if (gapStart >= 0) break; run = 0; }
            }
            if (gapStart < 0) return (0, 0, 0);

            (int Top, int Bottom, int Left, int Right) ScanRange(int x0, int x1)
            {
                int t = -1, b = -1, l = -1, r = -1;
                for (int y = 0; y < scratchH; y++)
                    for (int x = x0; x < x1; x++)
                        if (scr[x, y].A > 10) { if (t < 0) t = y; b = y; if (l < 0 || x < l) l = x; if (x > r) r = x; }
                return (t, b, l, r);
            }

            var refCluster = ScanRange(0, gapStart);
            var tgtCluster = ScanRange(gapStart, scratchW);
            if (refCluster.Bottom < 0 || tgtCluster.Top < 0) return (0, 0, 0);

            int baselineRow = refCluster.Bottom;
            int ascent = Math.Max(0, baselineRow - tgtCluster.Top);
            int descent = Math.Max(0, tgtCluster.Bottom - baselineRow);
            int width = tgtCluster.Right - tgtCluster.Left + 1;
            return (width, ascent, descent);
        }

        // UA: Усі 66 українських літер з плану, у стабільному алфавітному
        //     порядку (для передбачуваного, легко читаного дебаг-атласу) —
        //     а не в порядку числових ID.
        // EN: All 66 Ukrainian letters from the plan, in stable alphabetic
        //     order (for a predictable, easy-to-read debug atlas) — not in
        //     numeric ID order.
        var uaIds = AlphabetProcessor.UaAlphabet
            .Select(c => (int)c)
            .Where(id => plan.Values.Contains(id))
            .Distinct()
            .ToList();

        int curX = SlotGapPx;
        int curY = originalImageHeight + RowMarginTopPx;
        int rowHeight = 0;

        foreach (int uaId in uaIds)
        {
            bool isUpper = char.IsUpper((char)uaId);
            // UA: Кегль ЦІЄЇ літери (для і/ї — зменшений під висоту латинської i/I).
            // EN: This letter's render font (for і/ї — shrunk to Latin i/I height).
            Font glyphFont = GlyphFont(uaId, isUpper);
            var (mWidth, ascent, descent) = MeasureGlyph((char)uaId, glyphFont, isUpper);

            // UA: Аварійний запасний варіант, якщо растровий замір не вдався
            //     (напр. TTF не має цього гліфа) — не даємо розмір "нуль".
            // EN: Emergency fallback if the raster measurement fails (e.g.
            //     the TTF lacks this glyph) — don't allow a "zero" size.
            if (mWidth <= 0)
            {
                mWidth = MinGlyphBoxWidthPx;
                ascent = (int)Math.Round(isUpper ? upperBodyPx : lowerBodyPx);
                descent = 0;
            }

            // UA: Для factions додаємо 2×FactionsOutlinePx з кожного боку
            //     (ліво+право для ширини, верх+низ для висоти) — контур
            //     розширює чорнило на FactionsOutlinePx з ОБОХ країв кожної осі.
            // EN: For factions, add 2×FactionsOutlinePx per axis (left+right
            //     for width, top+bottom for height) — the outline expands the
            //     ink by FactionsOutlinePx on BOTH edges of each axis.
            int outlineSlack = isFactionsFont ? FactionsOutlinePx * 2 : 0;
            int boxW = Math.Max(MinGlyphBoxWidthPx, mWidth + GlyphBoxPaddingSidesPx + outlineSlack);
            int boxH = GlyphBoxPaddingTop + (ascent + descent + 1) + GlyphBoxPaddingBottom + outlineSlack;

            // UA: Перенесення рядка, якщо бокс не влазить по ширині.
            // EN: Wrap to the next row if the box doesn't fit width-wise.
            if (curX + boxW + SlotGapPx > imageWidth)
            {
                curX = SlotGapPx;
                curY += rowHeight + SlotGapPx;
                rowHeight = 0;
            }

            // UA: YOffset = відстань від базової лінії до верху боксу.
            //     Ascent — ІНДИВІДУАЛЬНИЙ для цієї літери (виміряний растрово
            //     відносно спільної базової лінії), тому дот-літери (і,ї)
            //     природно отримують вищий бокс, а виносні (р,у) — нижчий
            //     низ, без жодного спеціального коду для жодної категорії.
            // EN: YOffset = the distance from the baseline to the box top.
            //     Ascent is INDIVIDUAL to this letter (raster-measured
            //     relative to the shared baseline), so dotted letters (і,ї)
            //     naturally get a taller box, and descenders (р,у) naturally
            //     get a lower bottom edge — no special-case code for either
            //     category.
            float yOffset = baseTrue - (GlyphBoxPaddingTop + ascent);

            // UA: Просування курсора = ширина чорнила цієї літери × трекінг-
            //     коефіцієнт ЦЬОГО шрифту з еталону (радикально різний по
            //     шрифтах — див. великий коментар вище). Так відтворюється
            //     справжня щільність оригіналу для кожного шрифту окремо.
            // EN: Cursor advance = this letter's ink width × THIS font's
            //     reference tracking ratio (radically per-font — see the big
            //     comment above). This reproduces the original's true density
            //     for each font individually.
            int xAdvance = Math.Max(MinXAdvancePx, (int)Math.Round(mWidth * advanceRatio));

            // UA: 2026-07-25 — ПІДЛОГА БЕЗПЕКИ, знайдена реальним замiром ГОТОВОГО
            //     PNG (не теорії): для header_medium/header_small/factions/ingame/
            //     ingame_small коефіцієнт з оригіналу (0.64-0.85) множився на
            //     inkWidth НОВОГО (Fira) шрифту й дав від'ємний проміжок
            //     (XAdvance − inkWidth) ПРАКТИЧНО НА КОЖНІЙ літері — реальне
            //     накладання, не гіпотетичне (виміряно скриптом по
            //     output/*.png: header_medium −1..−8px, factions −10..−23px,
            //     ingame −1..−2px, ingame_small −3..−8px). Причина: та щільність
            //     (0.64 у factions) в ОРИГІНАЛІ трималась на ручному, посимвольному
            //     кернінгу художників гри під КОНКРЕТНУ форму їхніх літер — один
            //     медіанний коефіцієнт на шрифт не відтворює це для ІНШИХ обрисів
            //     (Fira), незалежно від того, який шрифт обрано. body_*
            //     (Comfortaa/FiraSans SemiBold) настільки агресивного коефіцієнта
            //     не мають (~0.9-1.0) — там усе гаразд без підлоги.
            //     Тому: гарантуємо МІНІМУМ 1px проміжку між чорнилом (з
            //     урахуванням контуру factions) і XAdvance — де формула й так
            //     давала достатньо, підлога нічого не міняє; де вона давала
            //     накладання — підлога рятує читабельність ціною трохи менш
            //     "тісного" вигляду за оригінал.
            // EN: 2026-07-25 — SAFETY FLOOR, found by measuring the ACTUAL
            //     rendered PNG (not theory): for header_medium/header_small/
            //     factions/ingame/ingame_small the original's ratio (0.64-0.85)
            //     multiplied by the NEW (Fira) font's ink width produced a
            //     NEGATIVE gap (XAdvance − inkWidth) on PRACTICALLY EVERY letter —
            //     real overlap, not hypothetical (measured by a script against
            //     output/*.png: header_medium −1..−8px, factions −10..−23px,
            //     ingame −1..−2px, ingame_small −3..−8px). Reason: that density
            //     (0.64 in factions) in the ORIGINAL was held together by the
            //     game artists' manual, per-character kerning tuned to their
            //     SPECIFIC letterforms — a single per-font median ratio can't
            //     reproduce that for a DIFFERENT outline (Fira), regardless of
            //     which font is chosen. body_* (Comfortaa/FiraSans SemiBold)
            //     never had such an aggressive ratio (~0.9-1.0) — those are fine
            //     without a floor.
            //     So: guarantee a MINIMUM 1px gap between the ink (factions'
            //     outline included) and XAdvance — where the formula already
            //     gave enough room, the floor changes nothing; where it caused
            //     overlap, the floor trades a bit of the original's "tight" look
            //     for guaranteed readability.
            const int MinAdvanceGapPx = 1;
            int inkWidthForAdvance = mWidth + (isFactionsFont ? FactionsOutlinePx * 2 : 0);
            xAdvance = Math.Max(xAdvance, inkWidthForAdvance + MinAdvanceGapPx);

            result[uaId] = new GlyphRecord
            {
                Padding = 0,
                ID = uaId,
                AtlasX = curX,
                AtlasY = curY,
                AtlasW = boxW,
                AtlasH = boxH,
                XOffset = DefaultXOffsetPx,
                YOffset = yOffset,
                XAdvance = xAdvance
            };

            curX += boxW + SlotGapPx;
            rowHeight = Math.Max(rowHeight, boxH);
        }

        int newImageHeight = curY + rowHeight + RowMarginTopPx;
        return (result, Math.Max(newImageHeight, originalImageHeight));
    }

    /// <summary>
    /// UA: Проходить по ВСІХ оригінальних записах (analysis.GlyphCount — кількість
    /// НЕ змінюється) і для кожного, кому план призначив УКРАЇНСЬКУ роль,
    /// повністю переписує запис новою геометрією з allSlots (свіже місце в
    /// PNG, реальні TTF-метрики) — включно зі "спільними" літерами, які
    /// раніше лишались недоторканими. Запис лишається "носієм" (той самий
    /// byte-слот у таблиці), але жоден байт старої геометрії в
    /// ньому більше не виживає.
    /// EN: Walks EVERY original record (analysis.GlyphCount — the count is
    /// NOT changed) and, for each one the plan assigned a UKRAINIAN role,
    /// fully rewrites the record with new geometry from allSlots (fresh PNG
    /// space, real TTF metrics) — including the "shared" letters that used
    /// to be left untouched. The record survives only as a "host" (the same
    /// byte slot in the table), but not a single byte of its old
    /// geometry survives inside it.
    /// </summary>
    private static void PatchFnt(byte[] data, FontAnalysisResult analysis, Dictionary<int, int> plan, Dictionary<int, GlyphRecord> allSlots)
    {
        for (int i = 0; i < analysis.GlyphCount; i++)
        {
            var original = analysis.Records[i];
            int pos = analysis.TableStart + i * analysis.Stride;

            if (!plan.TryGetValue(original.ID, out int newId)) continue;
            if (!AlphabetProcessor.IsUa(newId)) continue;

            if (allSlots.TryGetValue(newId, out var slot))
            {
                WriteRecord(data, pos, slot, analysis.IdOffset, analysis.HasXAdvance, analysis.HasPaddingPrefix);
            }
            else
            {
                // UA: Рідкісний запасний варіант (TTF/PNG недоступні) — бодай
                //     підміняємо ID, щоб текст не показував зовсім чужий
                //     символ; геометрія лишиться від донора (не ідеально,
                //     але видимо в логах, а не тихо).
                // EN: A rare fallback (TTF/PNG unavailable) — at least swap
                //     the ID so the text doesn't show a completely unrelated
                //     character; the geometry stays the donor's (not ideal,
                //     but visible in the logs, not silent).
                WriteInt32LE(data, pos + analysis.IdOffset, newId);
                Console.WriteLine(
                    $"   [!] UA [{analysis.FontName}] Немає розрахованого слота для UA {newId} ({(char)newId}) — лишив геометрію донора.");
                Console.WriteLine(
                    $"   [!] EN [{analysis.FontName}] No computed slot for UA {newId} ({(char)newId}) — kept the donor's geometry.");
            }
        }
        SortGlyphTable(data, analysis);
    }

    /// <summary>UA: Пересортовує записи в таблиці гліфів за ID — рушій/дебаг-інструменти очікують зростаючий порядок. / EN: Re-sorts the glyph table's records by ID — the engine/debug tools expect ascending order.</summary>
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

    /// <summary>UA: Записує один GlyphRecord у байти за форматом, визначеним analysis (idOff/hasXAdv/hasPad) — жодних припущень про фіксований stride. / EN: Writes a single GlyphRecord into bytes using the layout determined by analysis (idOff/hasXAdv/hasPad) — no assumption of a fixed stride.</summary>
    private static void WriteRecord(byte[] data, int pos, GlyphRecord r, int idOff, bool hasXAdv, bool hasPad)
    {
        if (hasPad) WriteInt32LE(data, pos, r.Padding);
        WriteInt32LE(data, pos + idOff, r.ID);
        WriteSingleLE(data, pos + idOff + 4, r.AtlasX);
        WriteSingleLE(data, pos + idOff + 8, r.AtlasY);
        WriteSingleLE(data, pos + idOff + 12, r.AtlasW);
        WriteSingleLE(data, pos + idOff + 16, r.AtlasH);
        WriteSingleLE(data, pos + idOff + 20, r.XOffset);
        WriteSingleLE(data, pos + idOff + 24, r.YOffset);
        if (hasXAdv) WriteInt32LE(data, pos + idOff + 28, r.XAdvance);
    }

    private static void WriteInt32LE(byte[] data, int pos, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(pos, 4), value);
    private static void WriteSingleLE(byte[] data, int pos, float value) => BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos, 4), value);

    /// <summary>UA: Зберігає текстовий звіт генерації (повна мапа всіх нових слотів) у Config.DebugDir/{fontName}_generation.txt. / EN: Saves the generation text report (the full map of every new slot) to Config.DebugDir/{fontName}_generation.txt.</summary>
    private static void SaveReport(string fontName, FontAnalysisResult analysis, Dictionary<int, int> plan, Dictionary<int, GlyphRecord> allSlots)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== ЗВІТ ГЕНЕРАЦІЇ / GENERATION REPORT: {fontName} ===");
        sb.AppendLine($"Stride: {analysis.Stride} | Гліфів/Glyphs: {analysis.GlyphCount} | Baseline: {analysis.BaselineConstant:F4}");
        sb.AppendLine($"Час/Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("Усі 66 літер нижче отримали ПОВНІСТЮ нову геометрію (повний перепак атласу). / " +
                       "All 66 letters below got FULLY new geometry (a full atlas repack).\n");

        sb.AppendLine("── Нові слоти (усі 66 UA-літер) / New slots (all 66 UA letters) ──");
        foreach (var (uaId, slot) in allSlots.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"  {(char)uaId} (U+{uaId:X4}) | [{slot.AtlasX},{slot.AtlasY} {slot.AtlasW}×{slot.AtlasH}] | YOff: {slot.YOffset:F2} | XAdv: {slot.XAdvance}");
        }

        var missing = plan.Values.Where(AlphabetProcessor.IsUa).Distinct().Where(id => !allSlots.ContainsKey(id)).OrderBy(id => id).ToList();
        if (missing.Count > 0)
        {
            sb.AppendLine("\n── УВАГА: без розрахованого слота (лишили геометрію донора) / WARNING: no computed slot (kept the donor's geometry) ──");
            foreach (var id in missing)
                sb.AppendLine($"  {(char)id} (U+{id:X4})");
        }

        Directory.CreateDirectory(Config.DebugDir);
        File.WriteAllText(System.IO.Path.Combine(Config.DebugDir, fontName + "_generation.txt"), sb.ToString(), Encoding.UTF8);
    }

    /// <summary>UA: Медіана — стійка до викидів (напр. пробіл із AtlasW=0, який і так відфільтровано вище). / EN: Median — robust to outliers (e.g. a space with AtlasW=0, already filtered out above).</summary>
    private static float Median(IEnumerable<float> source)
    {
        var list = source.OrderBy(v => v).ToList();
        return list.Count > 0 ? list[list.Count / 2] : 0f;
    }
}

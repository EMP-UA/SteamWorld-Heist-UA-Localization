// =============================================================================
// SWH.FontTool.CLI — Program.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Головне меню інструменту SteamWorld Heist Font Tool (UA Project).
//     Консольний UI повністю двомовний (UA/EN разом на кожному рядку) — цей
//     інструмент публікується для інших локалізаторів/моддерів, тож меню,
//     підказки та повідомлення про помилки не повинні вимагати знання
//     української, щоб просто ним скористатися.
// EN: The main menu for the SteamWorld Heist Font Tool (UA Project). The
//     console UI is fully bilingual (UA/EN together on every line) — this
//     tool is published for other localizers/modders, so the menu, hints,
//     and error messages shouldn't require knowing Ukrainian just to use it.
// =============================================================================

using System.Text;
using SWH.FontTool.Core;
using SWH.FontTool.Analyzer;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "SteamWorld Heist — UA Font Tool";

Config.EnsureDirectoriesExist();
PrintSetupHintIfNeeded();

while (true)
{
    Console.Clear();
    Console.WriteLine("══════════════════════════════════════════════════════════════════════════");
    Console.WriteLine("     SteamWorld Heist Font Tool  (UA Project)");
    Console.WriteLine("══════════════════════════════════════════════════════════════════════════");
    Console.WriteLine(" 1. [UA: Аналіз / EN: Analysis]        Пакетний аналіз усіх 11 шрифтів / Batch analysis of all 11 fonts");
    Console.WriteLine(" 2. [UA: Скан / EN: Scan]               Детальний скан одного шрифту / Detailed scan of a single font");
    Console.WriteLine(" 3. [UA: Перегляд / EN: Preview]        Попередній перегляд плану перепризначень / Preview the remap plan");
    Console.WriteLine(" 4. [UA: Генерація / EN: Generate]      ГЕНЕРАЦІЯ УКРАЇНІЗОВАНИХ .FNT+.PNG / GENERATE UKRAINIANIZED .FNT+.PNG");
    Console.WriteLine("──────────────────────────────────────────────────────────────────────────");
    Console.WriteLine(" 5. [UA: Експеримент / EN: Experiment]  Тест толерантності рушія до numChars / Engine tolerance test for numChars");
    Console.WriteLine("     UA: пише в окрему теку experiment-numchars, original-fonts не чіпає (відкат не потрібен)");
    Console.WriteLine("     EN: writes to a separate experiment-numchars folder, never touches original-fonts (no rollback needed)");
    Console.WriteLine(" 6. [UA: Експеримент / EN: Experiment]  Тест толерантності рушія до розміру PNG / Engine tolerance test for PNG size");
    Console.WriteLine("     UA: пише в окрему теку experiment-png-resize, .fnt не змінюється взагалі");
    Console.WriteLine("     EN: writes to a separate experiment-png-resize folder, .fnt is never touched");
    Console.WriteLine(" 7. [UA: Експеримент / EN: Experiment]  Одна нова літера у вирощеному просторі / One new letter in grown PNG space");
    Console.WriteLine("     UA: пише в окрему теку experiment-single-glyph / EN: writes to a separate experiment-single-glyph folder");
    Console.WriteLine(" 8. [UA: Експеримент / EN: Experiment]  Одна нова літера В МЕЖАХ старого PNG / One new letter WITHIN the old PNG");
    Console.WriteLine("     UA: пише в окрему теку experiment-in-place-glyph / EN: writes to a separate experiment-in-place-glyph folder");
    Console.WriteLine(" 9. [UA: Санітарний тест / EN: Sanity test]  Обмін ID двох уже робочих літер / Swap the IDs of two already-working letters");
    Console.WriteLine("     UA: пише в окрему теку experiment-id-swap / EN: writes to a separate experiment-id-swap folder");
    Console.WriteLine("10. [UA: Аналітика / EN: Analytics]     Перевірка моделі виносних (р,у,ф,ц,щ) / Descender model check (р,у,ф,ц,щ)");
    Console.WriteLine("     UA: нічого не пише — лише порівнює з реальними даними / EN: writes nothing — only compares against real data");
    Console.WriteLine("11. [UA: Діагностика / EN: Diagnostic]  Еталонний зріз латиниці, усі шрифти / Full Latin reference snapshot, all fonts");
    Console.WriteLine("     UA: .fnt метрики + піксельне чорнило → debug/latin_reference.txt");
    Console.WriteLine("     EN: .fnt metrics + pixel ink → debug/latin_reference.txt");
    Console.WriteLine("──────────────────────────────────────────────────────────────────────────");
    Console.WriteLine(" 0. UA: Вихід / EN: Exit");
    Console.WriteLine("══════════════════════════════════════════════════════════════════════════");
    Console.Write("\n UA: Оберіть дію / EN: Choose an option: ");

    switch (Console.ReadLine())
    {
        case "1": RunBatchAnalysis(); break;
        case "2": RunDetailedScan(); break;
        case "3": RunRemapPreview(); break;
        case "4":
            FontGenerator.ProcessAllFonts();
            Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
            Console.ReadKey();
            break;
        case "5": RunSlotInjectionTest(); break;
        case "6": RunPngResizeTest(); break;
        case "7": RunSingleGlyphGrowthTest(); break;
        case "8": RunInPlaceGlyphTest(); break;
        case "9": RunIdSwapSanityTest(); break;
        case "10": RunDescenderProbeTest(); break;
        case "11": RunLatinReferenceDiagnostic(); break;
        case "0": return;
    }
}

// UA: Показує підказку одразу після старту, якщо вхідні теки (original-fonts,
//     ttf-fonts) порожні — типовий стан одразу після клонування репозиторію.
//     Без цього перший запуск просто мовчки не знаходив би файлів і
//     новачок не зрозумів би, що й куди класти.
// EN: Shows a hint right after startup if the input folders (original-fonts,
//     ttf-fonts) are empty — the typical state right after cloning the repo.
//     Without this, the first run would silently fail to find any files and
//     a newcomer wouldn't know what to put where.
void PrintSetupHintIfNeeded()
{
    bool missingOriginal = Config.OriginalFontsMissing();
    bool missingTtf = Config.TtfFontsMissing();
    if (!missingOriginal && !missingTtf) return;

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine(" UA: НАЛАШТУВАННЯ / EN: SETUP");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    if (missingOriginal)
    {
        Console.WriteLine($" UA: Тека '{Config.OriginalFontsDir}' порожня.");
        Console.WriteLine("     Скопіюй туди оригінальні .fnt + .png шрифти з гри");
        Console.WriteLine("     (напр. з bundle\\data01\\Fonts).");
        Console.WriteLine($" EN: Folder '{Config.OriginalFontsDir}' is empty.");
        Console.WriteLine("     Copy the original .fnt + .png fonts from the game there");
        Console.WriteLine("     (e.g. from bundle\\data01\\Fonts).");
        Console.WriteLine();
    }
    if (missingTtf)
    {
        Console.WriteLine($" UA: Тека '{Config.TtfDir}' порожня.");
        Console.WriteLine("     Поклади туди FiraSansExtraCondensed-Bold.ttf (+ -BoldItalic.ttf для factions)");
        Console.WriteLine("     та FiraSans-SemiBold.ttf (детальніше — Config.GetFontTtfPath).");
        Console.WriteLine($" EN: Folder '{Config.TtfDir}' is empty.");
        Console.WriteLine("     Put FiraSansExtraCondensed-Bold.ttf (+ -BoldItalic.ttf for factions)");
        Console.WriteLine("     and FiraSans-SemiBold.ttf there (see Config.GetFontTtfPath for details).");
        Console.WriteLine();
    }
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("UA: Натисніть будь-яку клавішу, щоб продовжити... / EN: Press any key to continue...");
    Console.ReadKey();
}

// ── Пункт 5: експеримент толерантності numChars ─────────────────────────────
// UA: Читає .fnt із Config.OriginalFontsDir (НЕ чіпаючи їх), додає тестові
//     слоти для відсутніх українських літер (донор-клони, PNG не чіпається)
//     і пише результат у Config.ExperimentDir — окрему теку біля .exe. Тому
//     що original-fonts лишається незайманим, бекап/відкат тут більше не
//     потрібні. Див. SlotInjectionExperiment.cs.
// EN: Reads .fnt files from Config.OriginalFontsDir (WITHOUT touching them),
//     adds test slots for missing Ukrainian letters (donor clones, PNG
//     untouched), and writes the result into Config.ExperimentDir — a
//     separate folder next to the .exe. Because original-fonts stays
//     untouched, backup/restore are no longer needed here. See
//     SlotInjectionExperiment.cs.
void RunSlotInjectionTest()
{
    Console.Clear();
    Console.WriteLine("=== UA: ЕКСПЕРИМЕНТ — ЧИ ТОЛЕРАНТНИЙ РУШІЙ ДО ЗРОСТАННЯ numChars / EN: EXPERIMENT — ENGINE TOLERANCE FOR numChars GROWTH ===\n");
    try { SlotInjectionExperiment.RunInjectionTest(Config.OriginalFontsDir, Config.ExperimentDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 6: експеримент толерантності до розміру PNG ───────────────────────
// UA: Читає .png/.fnt із Config.OriginalFontsDir (НЕ чіпаючи їх), збільшує
//     висоту PNG (прозорий простір знизу, вміст не зсувається), .fnt копіює
//     без жодної зміни, і пише результат у Config.PngResizeExperimentDir.
//     Мета — перевірити, чи можна безпечно збільшити текстуру ПЕРЕД тим, як
//     переписувати генератор під повне перепакування атласу для всіх 66
//     українських літер (а не лише 8 унікальних). Див. PngCanvasExperiment.cs.
// EN: Reads .png/.fnt files from Config.OriginalFontsDir (WITHOUT touching
//     them), grows the PNG's height (transparent space at the bottom,
//     content not shifted), copies the .fnt with zero changes, and writes
//     the result into Config.PngResizeExperimentDir. The goal is to check
//     whether the texture can be safely grown BEFORE rewriting the generator
//     to fully repack the atlas for all 66 Ukrainian letters (not just the 8
//     unique ones). See PngCanvasExperiment.cs.
void RunPngResizeTest()
{
    Console.Clear();
    Console.WriteLine("=== UA: ЕКСПЕРИМЕНТ — ЧИ ТОЛЕРАНТНИЙ РУШІЙ ДО БІЛЬШОЇ ТЕКСТУРИ PNG / EN: EXPERIMENT — ENGINE TOLERANCE FOR A LARGER PNG TEXTURE ===\n");
    try { PngCanvasExperiment.RunResizeTest(Config.OriginalFontsDir, Config.PngResizeExperimentDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 7: ізольований тест одного нового гліфа ───────────────────────────
// UA: Додає РІВНО одну нову літеру ('і') у щойно вирощений простір PNG,
//     не чіпаючи жодного з інших гліфів шрифту. Мета — з'ясувати, чи
//     дефект "сірий верх/обрізання", який з'явився після повного перепаку
//     66 літер, стосується самого факту розміщення контенту за межами
//     старого розміру текстури, чи це щось специфічне для повного перепаку.
//     Див. SingleGlyphGrowthExperiment.cs.
// EN: Adds EXACTLY one new letter ('і') into the freshly grown PNG space,
//     without touching any other glyph of the font. The goal is to find
//     out whether the "gray top/clipping" defect that appeared after the
//     full 66-letter repack is about the very fact of placing content
//     beyond the old texture size, or something specific to the full
//     repack. See SingleGlyphGrowthExperiment.cs.
void RunSingleGlyphGrowthTest()
{
    Console.Clear();
    Console.WriteLine("=== UA: ЕКСПЕРИМЕНТ — ОДНА НОВА ЛІТЕРА У ВИРОЩЕНОМУ ПРОСТОРІ / EN: EXPERIMENT — ONE NEW LETTER IN GROWN SPACE ===\n");
    try { SingleGlyphGrowthExperiment.RunTest(Config.OriginalFontsDir, Config.SingleGlyphExperimentDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 8: нова літера в межах старого PNG (без росту полотна) ────────────
// UA: Перевіряє гіпотезу застарілого/кешованого розміру текстури: розміщує
//     нову 'і' у вже наявному вільному запасі ВСЕРЕДИНІ старих кордонів
//     PNG, не збільшуючи полотно взагалі. Див. InPlaceSingleGlyphExperiment.cs.
// EN: Tests the stale/cached texture size hypothesis: places a new 'і' in
//     already-existing free margin WITHIN the old PNG bounds, without
//     growing the canvas at all. See InPlaceSingleGlyphExperiment.cs.
void RunInPlaceGlyphTest()
{
    Console.Clear();
    Console.WriteLine("=== UA: ЕКСПЕРИМЕНТ — НОВА ЛІТЕРА В МЕЖАХ СТАРОГО PNG / EN: EXPERIMENT — NEW LETTER WITHIN THE OLD PNG ===\n");
    try { InPlaceSingleGlyphExperiment.RunTest(Config.OriginalFontsDir, Config.InPlaceGlyphExperimentDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 9: санітарний тест обміну ID ───────────────────────────────────────
// UA: Міняє місцями ID двох уже існуючих, уже робочих літер ('к'/'л') без
//     жодної зміни геометрії. Мета — перевірити саму основу: чи рушій
//     ідентифікує гліф за ID-полем так, як припускається, повністю ізольовано
//     від будь-якої нової геометрії чи нового простору PNG. Див.
//     IdSwapSanityExperiment.cs.
// EN: Swaps the IDs of two already-existing, already-working letters
//     ('к'/'л') with zero geometry changes. The goal is to test the very
//     foundation: does the engine identify a glyph by its ID field the way
//     it's assumed to, fully isolated from any new geometry or new PNG space. See
//     IdSwapSanityExperiment.cs.
void RunIdSwapSanityTest()
{
    Console.Clear();
    Console.WriteLine("=== UA: САНІТАРНИЙ ТЕСТ — ОБМІН ID ДВОХ УЖЕ РОБОЧИХ ЛІТЕР / EN: SANITY TEST — SWAP THE IDs OF TWO WORKING LETTERS ===\n");
    try { IdSwapSanityExperiment.RunTest(Config.OriginalFontsDir, Config.IdSwapExperimentDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 10: аналітична перевірка моделі виносних ──────────────────────────
// UA: Нічого не пише і не потребує запуску гри — суто аналітичний тест.
//     Порівнює передбачення YOffset/AtlasH для ІСНУЮЧИХ виносних літер
//     (р,у,ф,ц,щ) з їхніми РЕАЛЬНИМИ значеннями в оригінальному файлі (земля
//     правди). Якщо різниця мала — комбо-зондова модель базової лінії годиться
//     для повного перепаку всіх 66 літер. Див. DescenderProbeExperiment.cs.
// EN: Writes nothing and needs no game launch — a purely analytical test.
//     Compares the predicted YOffset/AtlasH for EXISTING descender letters
//     (р,у,ф,ц,щ) against their REAL values in the original file (ground
//     truth). If the difference is small — the combo-probe baseline model is
//     good enough for the full 66-letter repack. See DescenderProbeExperiment.cs.
void RunDescenderProbeTest()
{
    Console.Clear();
    Console.WriteLine("=== UA: АНАЛІТИКА — ПЕРЕВІРКА МОДЕЛІ ВИНОСНИХ (р,у,ф,ц,щ) / EN: ANALYTICS — DESCENDER MODEL CHECK (р,у,ф,ц,щ) ===\n");
    try { DescenderProbeExperiment.RunTest(Config.OriginalFontsDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 11: еталонний зріз латиниці ───────────────────────────────────────
// UA: Повний вимір латиниці (ASCII) кожного шрифту: вшиті .fnt-метрики + реальне
//     піксельне чорнило .png, + зведені орієнтири (baseline, x-height,
//     cap-height, ascender, descender, висота крапки 'i'). Це еталон, до якого
//     прив'язується побудова української кирилиці. Нічого не змінює.
// EN: A full measurement of each font's Latin (ASCII): embedded .fnt metrics +
//     real .png pixel ink, + summary guides (baseline, x-height, cap-height,
//     ascender, descender, 'i' dot height). This is the reference the Ukrainian
//     Cyrillic build anchors to. Changes nothing.
void RunLatinReferenceDiagnostic()
{
    Console.Clear();
    Console.WriteLine("=== UA: ДІАГНОСТИКА — ЕТАЛОННИЙ ЗРІЗ ЛАТИНИЦІ / EN: DIAGNOSTIC — FULL LATIN REFERENCE SNAPSHOT ===\n");
    try { LatinReferenceDiagnostic.RunTest(Config.OriginalFontsDir, Console.Out); }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.WriteLine("\nUA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 1: пакетний аналіз ────────────────────────────────────────────────
// UA: Прогонює FontAnalyzer.BatchReport по всій Config.OriginalFontsDir і зберігає
//     детальний звіт для кожного шрифту в Config.DebugDir.
// EN: Runs FontAnalyzer.BatchReport across all of Config.OriginalFontsDir and saves
//     a detailed report for every font into Config.DebugDir.
void RunBatchAnalysis()
{
    Console.Clear();
    Console.WriteLine("=== UA: ПАКЕТНИЙ АНАЛІЗ УСІХ ШРИФТІВ / EN: BATCH ANALYSIS OF ALL FONTS ===\n");
    FontAnalyzer.BatchReport(Config.OriginalFontsDir, Config.DebugDir);
    Console.WriteLine($"\nUA: Детальні звіти збережено в / EN: Detailed reports saved to: {Config.DebugDir}");
    Console.WriteLine("UA: Натисніть будь-яку клавішу... / EN: Press any key...");
    Console.ReadKey();
}

// ── Пункт 2: детальний скан одного шрифту ───────────────────────────────────
// UA: Повний аналіз одного шрифту за іменем + друк GenerateReport в консоль
//     і у файл.
// EN: Full analysis of a single font by name + prints GenerateReport to the
//     console and to a file.
void RunDetailedScan()
{
    Console.Clear();
    Console.Write("UA: Ім'я шрифту (наприклад, body_large) / EN: Font name (e.g. body_large): ");
    string name = Console.ReadLine() ?? "body_large";

    string path = Config.GetFntPath(Config.OriginalFontsDir, name);
    if (!File.Exists(path)) { Console.WriteLine($"UA: Файл не знайдено / EN: File not found: {path}"); Console.ReadKey(); return; }

    try
    {
        var result = FontAnalyzer.Analyze(path);
        Console.WriteLine(FontAnalyzer.GenerateReport(result));

        // UA: Також зберігаємо у файл / EN: Also save to a file
        string reportPath = Path.Combine(Config.DebugDir, name + "_analysis.txt");
        File.WriteAllText(reportPath, FontAnalyzer.GenerateReport(result), Encoding.UTF8);
        Console.WriteLine($"\nUA: Звіт збережено / EN: Report saved: {reportPath}");
    }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.ReadKey();
}

// ── Пункт 3: попередній перегляд плану ──────────────────────────────────────
// UA: Показує план перепризначень AlphabetProcessor.BuildRemapPlan без
//     запису файлів — щоб перевірити мапу донорів перед генерацією.
// EN: Shows the AlphabetProcessor.BuildRemapPlan remap plan without writing
//     any files — to review the donor map before generation.
void RunRemapPreview()
{
    Console.Clear();
    Console.Write("UA: Ім'я шрифту (або Enter для body_large) / EN: Font name (or Enter for body_large): ");
    string name = Console.ReadLine()?.Trim() is { Length: > 0 } s ? s : "body_large";

    try
    {
        var analysis = FontAnalyzer.Analyze(Config.GetFntPath(Config.OriginalFontsDir, name));
        var plan = AlphabetProcessor.BuildRemapPlan(analysis);

        Console.WriteLine($"\n=== UA: ПЛАН ПЕРЕПРИЗНАЧЕНЬ / EN: REMAP PLAN: {name} ===");
        Console.WriteLine($"Baseline: {analysis.BaselineConstant:F4}\n");

        Console.WriteLine($"{"UA: Донор ID / EN: Donor ID",-28} {"UA: Донор Ch / EN: Donor Ch",-28} {"UA: Ch",-8} {"UA ID",-8} {"UA: Дія / EN: Action",-20}");
        Console.WriteLine(new string('-', 96));

        foreach (var (dId, uaId) in plan.OrderBy(kv => kv.Value))
        {
            if (!AlphabetProcessor.IsUa(uaId)) continue;
            char dCh = dId is >= 32 and <= 65535 ? (char)dId : '·';
            string action = dId == uaId ? "UA: перемальовати / EN: repaint" : $"ID {dId}→{uaId}";
            Console.WriteLine($"{dId,-28} {dCh,-28} {(char)uaId,-8} {uaId,-8} {action,-20}");
        }

        Console.WriteLine($"\nUA: UA-символів призначено / EN: UA characters assigned: {plan.Values.Count(AlphabetProcessor.IsUa)}/66");
    }
    catch (Exception ex) { Console.WriteLine($"UA: Помилка / EN: Error: {ex.Message}"); }

    Console.ReadKey();
}

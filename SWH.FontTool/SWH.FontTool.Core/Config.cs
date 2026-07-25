// =============================================================================
// SWH.FontTool.Core — Config.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Централізована конфігурація інструменту (шляхи, список шрифтів,
//     винятки). FontProfile та KnownProfiles видалені — FontAnalyzer
//     визначає всі параметри автоматично, ручні профілі більше не потрібні.
// EN: Centralized tool configuration (paths, font list, exceptions).
//     FontProfile and KnownProfiles have been removed — FontAnalyzer
//     determines all parameters automatically, manual profiles are no
//     longer needed.
// =============================================================================

namespace SWH.FontTool.Core;

public static class Config
{
    // =========================================================================
    // UA: Шляхи. ВСІ обчислюються відносно розташування .exe (AppContext.
    //     BaseDirectory) — жодних хардкод-шляхів з локального диска автора.
    //     Саме це дозволяє публікувати код у публічному репо: людина, що
    //     клонує репозиторій і компілює проєкт, просто створює ці теки біля
    //     скомпільованого .exe (або дозволяє EnsureDirectoriesExist() зробити
    //     це автоматично) і кладе туди свої файли — жодних правок коду не
    //     потрібно.
    //
    //     original-fonts/ — сюди ВРУЧНУ копіюються оригінальні .fnt + .png
    //         шрифти з гри (вони в бандлі й так лежать поруч, напр.
    //         bundle\data01\Fonts). Інструмент їх тільки читає.
    //         НЕ комітити в git — це видобуті файли гри, авторське право не
    //         моє (додай у .gitignore).
    //     ttf-fonts/ — джерельні TTF для рендеру нових гліфів. АКТУАЛЬНИЙ
    //         набір (2026-07-25, після заміни Oswald/Comfortaa — причина
    //         заміни й перевірка походження описані в README.md, розділ
    //         "Чому не Oswald/Comfortaa" — див. також GetFontTtfPath):
    //         FiraSansExtraCondensed-Bold.ttf (+ -BoldItalic.ttf для
    //         factions) та FiraSans-SemiBold.ttf — уся родина Fira Sans під
    //         відкритою ліцензією OFL, з чітким, задокументованим авторством
    //         (Carrois Type Design/Mozilla, кирилиця від Nikoltchev/Kateliev,
    //         Болгарія), тому можуть бути закомічені в репозиторій напряму;
    //         Cuprum-Bold.ttf лишається як мертвий запасний шлях (див.
    //         коментар у GetFontTtfPath) — перед комітом варто самостійно
    //         звірити текст ліцензії кожного шрифту.
    //     output/ — результат генерації (.fnt + .png + debug-звіти). Звідси
    //         готові файли ВРУЧНУ копіюються назад у гру. Так само не
    //         комітити (похідні файли гри).
    // EN: Paths. ALL of them are computed relative to the .exe location
    //     (AppContext.BaseDirectory) — no hardcoded paths from the author's
    //     local disk. This is exactly what makes it safe to publish in a
    //     public repo: anyone who clones the repository and compiles the
    //     project just creates these folders next to the compiled .exe (or
    //     lets EnsureDirectoriesExist() do it automatically) and drops their
    //     own files in — no code changes needed.
    //
    //     original-fonts/ — the original .fnt + .png fonts from the game are
    //         manually copied in here (they already sit next to each other
    //         in the bundle, e.g. bundle\data01\Fonts). The tool only reads
    //         them. Do NOT commit to git — these are extracted game files,
    //         the copyright isn't mine (add to .gitignore).
    //     ttf-fonts/ — the source TTFs used to render new glyphs. CURRENT
    //         set (2026-07-25, after replacing Oswald/Comfortaa — the reason
    //         for the swap and the provenance check are described in
    //         README.md, "Why not Oswald/Comfortaa" section — see also
    //         GetFontTtfPath):
    //         FiraSansExtraCondensed-Bold.ttf (+ -BoldItalic.ttf for
    //         factions) and FiraSans-SemiBold.ttf — the whole Fira Sans
    //         family is OFL-licensed with clean, documented authorship
    //         (Carrois Type Design/Mozilla, Cyrillic by Nikoltchev/Kateliev,
    //         Bulgaria), so they can be committed to the repo directly;
    //         Cuprum-Bold.ttf remains as a dead fallback path (see the
    //         comment in GetFontTtfPath) — still worth double-checking each
    //         font's license text yourself before committing.
    //     output/ — the generation result (.fnt + .png + debug reports). The
    //         finished files are manually copied from here back into the
    //         game. Also should not be committed (derivative game files).
    // =========================================================================

    public static string BaseDir => AppContext.BaseDirectory;

    public static string OriginalFontsDir => Path.Combine(BaseDir, "original-fonts");
    public static string TtfDir => Path.Combine(BaseDir, "ttf-fonts");
    public static string OutputDir => Path.Combine(BaseDir, "output");
    public static string DebugDir => Path.Combine(OutputDir, "debug");

    // UA: Окрема тека для SlotInjectionExperiment (пункт меню 5). original-fonts/
    //     ЗАВЖДИ лишається недоторканим — інструмент його тільки читає; результат
    //     тесту (файли зі штучно доданими слотами) пишеться сюди, в окрему підтеку
    //     біля .exe. Саме тому бекап/відкат тут більше не потрібні: нема що
    //     відновлювати, якщо оригінал ніхто й не чіпав.
    // EN: A dedicated folder for SlotInjectionExperiment (menu item 5).
    //     original-fonts/ ALWAYS stays untouched — the tool only reads from it;
    //     the test result (files with artificially added slots) is written here,
    //     into a separate subfolder next to the .exe. This is exactly why
    //     backup/restore are no longer needed: there's nothing to restore if the
    //     original was never touched in the first place.
    public static string ExperimentDir => Path.Combine(BaseDir, "experiment-numchars");

    // UA: Окрема тека для PngCanvasExperiment (тест толерантності рушія до
    //     більшого розміру текстури PNG — крок перед можливим переписуванням
    //     генератора під повне перепакування атласу для всіх 66 літер, а не
    //     лише 8 унікальних). Так само не чіпає original-fonts.
    // EN: A dedicated folder for PngCanvasExperiment (a test of the engine's
    //     tolerance for a larger PNG texture size — a step before possibly
    //     rewriting the generator to fully repack the atlas for all 66
    //     letters, not just the 8 unique ones). Also never touches
    //     original-fonts.
    public static string PngResizeExperimentDir => Path.Combine(BaseDir, "experiment-png-resize");

    // UA: Окрема тека для SingleGlyphGrowthExperiment (ізольований тест: чи
    //     рушій коректно показує ОДНУ нову літеру, розміщену за межами
    //     старого розміру текстури — після того, як повний перепак 66 літер
    //     дав видимий дефект рендеру в грі). original-fonts так само не
    //     чіпається.
    // EN: A dedicated folder for SingleGlyphGrowthExperiment (an isolated
    //     test: does the engine correctly display ONE new letter placed
    //     beyond the old texture size — after the full 66-letter repack
    //     produced a visible rendering defect in-game). original-fonts is
    //     also never touched.
    public static string SingleGlyphExperimentDir => Path.Combine(BaseDir, "experiment-single-glyph");

    // UA: Окрема тека для InPlaceSingleGlyphExperiment (тест: нова літера
    //     у вже наявному запасі ВСЕРЕДИНІ старих кордонів текстури, без
    //     жодного росту PNG — перевірка гіпотези застарілого/кешованого
    //     розміру текстури, знайденої після SingleGlyphGrowthExperiment).
    // EN: A dedicated folder for InPlaceSingleGlyphExperiment (a test:
    //     a new letter in already-existing margin WITHIN the old texture
    //     bounds, with no PNG growth at all — testing the stale/cached
    //     texture size hypothesis found after SingleGlyphGrowthExperiment).
    public static string InPlaceGlyphExperimentDir => Path.Combine(BaseDir, "experiment-in-place-glyph");

    // UA: Окрема тека для IdSwapSanityExperiment (санітарний тест: чи
    //     взагалі рушій ідентифікує гліф за ID-полем так, як припускається —
    //     без жодної нової геометрії чи нового простору PNG).
    // EN: A dedicated folder for IdSwapSanityExperiment (a sanity test:
    //     does the engine identify a glyph by its ID field the way it's
    //     assumed to at all — without any new geometry or new PNG space).
    public static string IdSwapExperimentDir => Path.Combine(BaseDir, "experiment-id-swap");

    public static readonly string[] FontNames =
    {
        "header_medium", "factions", "header_small", "body_large",
        "ingame", "body_medium", "indicator", "body_small",
        "ingame_small", "body_xsmall", "debug_small"
    };

    // UA: Шрифти без кирилиці в оригінальній грі — пропускаємо повністю.
    //     Підтверджено побайтовим парсингом усіх 12 .fnt з original-fonts
    //     (2026-07-21): жоден з трьох нижче не містить жодного гліфа з
    //     кириличного блоку (1024–1279), тож локалізувати в них нічого.
    //     indicator   — шрифт HUD-цифр, лише ASCII + латиниця-1 (161-254).
    //     debug_small — технічний/дебаг-шрифт, той самий набір, без кирилиці.
    //     cooldown    — лише 10 гліфів: цифри '0'-'9', більше нічого. Немає
    //                   навіть у Config.FontNames (генератор його й так не
    //                   чіпав) — додано сюди лише для документації/повноти
    //                   списку, на випадок якщо хтось колись переключить
    //                   обхід з FontNames на пряме сканування original-fonts.
    // EN: Fonts with no Cyrillic in the original game — skipped entirely.
    //     Confirmed by a byte-level parse of all 12 .fnt files in
    //     original-fonts (2026-07-21): none of the three below contain a
    //     single glyph from the Cyrillic block (1024–1279), so there is
    //     nothing to localize in them.
    //     indicator   — HUD digits font, ASCII + Latin-1 supplement (161-254) only.
    //     debug_small — technical/debug font, same charset, no Cyrillic.
    //     cooldown    — only 10 glyphs total: digits '0'-'9', nothing else.
    //                   Not even listed in Config.FontNames (the generator
    //                   never touched it anyway) — added here purely for
    //                   documentation/completeness, in case the iteration
    //                   ever switches from FontNames to scanning
    //                   original-fonts directly.
    public static readonly HashSet<string> SkipFonts = new()
    {
        "indicator",
        "debug_small",
        "cooldown"
    };

    /// <summary>
    /// UA: Створює всі робочі теки біля .exe, якщо їх ще немає (у т.ч.
    /// вхідні original-fonts/ttf-fonts — порожні теки самі по собі
    /// підказують, куди класти файли).
    /// EN: Creates every working folder next to the .exe if it doesn't
    /// exist yet (including the input original-fonts/ttf-fonts — empty
    /// folders on their own hint at where files should go).
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(OriginalFontsDir);
        Directory.CreateDirectory(TtfDir);
        Directory.CreateDirectory(OutputDir);
        Directory.CreateDirectory(DebugDir);
        Directory.CreateDirectory(ExperimentDir);
        Directory.CreateDirectory(PngResizeExperimentDir);
        Directory.CreateDirectory(SingleGlyphExperimentDir);
        Directory.CreateDirectory(InPlaceGlyphExperimentDir);
        Directory.CreateDirectory(IdSwapExperimentDir);
    }

    /// <summary>
    /// UA: true, якщо в original-fonts/ ще немає жодного .fnt — типовий
    /// стан одразу після клонування репозиторію. Викликається в Program.cs,
    /// щоб показати підказку замість незрозумілого падіння.
    /// EN: true if original-fonts/ has no .fnt files yet — the typical
    /// state right after cloning the repo. Called from Program.cs to show
    /// a hint instead of a confusing crash.
    /// </summary>
    public static bool OriginalFontsMissing() =>
        !Directory.Exists(OriginalFontsDir) || Directory.GetFiles(OriginalFontsDir, "*.fnt").Length == 0;

    /// <summary>UA: те саме, але для ttf-fonts/. / EN: the same check, but for ttf-fonts/.</summary>
    public static bool TtfFontsMissing() =>
        !Directory.Exists(TtfDir) || Directory.GetFiles(TtfDir, "*.ttf").Length == 0;

    public static string GetFntPath(string dir, string name) =>
        Path.Combine(dir, name + ".fnt");

    // UA: 2026-07-25 — Oswald-Bold ЗАМІНЕНО на Fira Sans Extra Condensed Bold
    //     для header_medium/header_small. Причини (усі виміряні, не
    //     припущені):
    //     1) Походження: перевірка авторства кириличного розширення Oswald
    //        виявила невідповідність вимозі проєкту щодо походження шрифтів
    //        (деталі — README.md, розділ "Чому не Oswald/Comfortaa"). Fira
    //        Sans Extra Condensed — Carrois Type Design (Німеччина) для
    //        Mozilla/Telefonica, кирилицю розширювали Nikoltchev/Kateliev
    //        (Болгарія) — чітке, задокументоване авторство обох частин.
    //     2) Ліцензія: обидва OFL, але Fira Sans Extra Condensed — СПРАВЖНІЙ
    //        конденсований крій (а не синтетично стиснутий), тому здатний
    //        відтворити щільність трекінгу оригіналу (0.64-0.78) без
    //        накладання літер одна на одну — саме це Oswald-Bold не тягнув
    //        (дефект "Пайпер налазить на рамку").
    //     3) Жирність (header_medium/header_small): виміряно векторно
    //        (fontTools, bbox 'I' / capHeight): Oswald-Bold = 0.221,
    //        Fira Sans Extra Condensed Bold = 0.227 — практично ідентична
    //        "жирність", Bold підходить без змін.
    //     4) factions — ОКРЕМИЙ випадок, ще НЕ вирішений: піксельний вимір
    //        оригіналу дав cтроук/cap-height=0.479 проти 0.389-0.406 у
    //        header_medium/header_small — і саме в factions cap-height
    //        НАЙБІЛЬШИЙ (48px), тобто похибка антиаліасингу там мінімальна,
    //        а не навпаки — отже це, найімовірніше, реально жирніше
    //        накреслення в оригіналі. Тимчасово лишено на Bold (як і решта
    //        заголовків), доки в ttf-fonts/ не з'явиться FiraSansExtraCondensed-
    //        ExtraBold.ttf/Black.ttf для точного векторного підбору саме
    //        для factions.
    // EN: 2026-07-25 — Oswald-Bold REPLACED with Fira Sans Extra Condensed
    //     Bold for header_medium/header_small. Reasons (all measured, not
    //     assumed):
    //     1) Origin: a provenance check on Oswald's Cyrillic extension found
    //        it didn't meet the project's font-provenance requirement
    //        (details — README.md, "Why not Oswald/Comfortaa" section). Fira
    //        Sans Extra Condensed is by Carrois Type Design (Germany) for
    //        Mozilla/Telefonica, Cyrillic extended by Nikoltchev/Kateliev
    //        (Bulgaria) — clean, documented authorship for both parts.
    //     2) License: both OFL, but Fira Sans Extra Condensed is a GENUINE
    //        condensed cut (not synthetically squeezed), so it can reproduce
    //        the original's tracking density (0.64-0.78) without letters
    //        overlapping — exactly what Oswald-Bold could not do (the
    //        "Piper overlaps the dialogue frame" defect).
    //     3) Weight (header_medium/header_small): verified in vector space
    //        (fontTools, 'I' bbox / capHeight): Oswald-Bold = 0.221,
    //        Fira Sans Extra Condensed Bold = 0.227 — practically identical,
    //        Bold fits as-is.
    //     4) factions — ВИРІШЕНО, і не так, як спершу здавалося. Прямий
    //        перегляд оригінальних гліфів 'H'/'I'/'o' з factions.png (не
    //        просто цифри, а самі пікселі) показав: це НЕ жирніше
    //        накреслення, а (а) курсивний/похилий нахил і (б) чорна ОБВЕДЕНА
    //        обводка/контур навколо білого заповнення — саме ця обводка
    //        накручувала виміряний stroke/cap-height до 0.479 (ExtraBold=
    //        0.255, Black=0.284 виміряно векторно — обидва все одно НЕ
    //        відтворюють ефект контуру простим потовщенням штриха, тож гнатись
    //        за важчою вагою тут — хибний шлях). Лишено на Bold (як і решта
    //        заголовків) — сама товщина літери під контуром відповідає
    //        header_medium/header_small. Нахил + контур — це ОКРЕМЕ питання
    //        рендеру, не підбору ваги шрифту (вирішено 2026-07-25, деталі —
    //        нижче, у нотатці про factions).
    //
    // UA: 2026-07-25 — Comfortaa ЗАМІНЕНА на Fira Sans SemiBold для
    //     body_large/body_medium/body_small/body_xsmall/ingame/ingame_small.
    //     Причина заміни: перевірка походження показала, що кирилицю до
    //     Comfortaa (як і до Oswald) розширювала та сама студія — та сама
    //     невідповідність вимозі проєкту, тільки в іншому шрифті (деталі —
    //     README.md). Fira Sans (звичайної ширини) — та сама родина/автори,
    //     що й Fira Sans Extra Condensed (Carrois/Mozilla, кирилиця від
    //     Nikoltchev/Kateliev, Болгарія) — чітке, окреме авторство.
    //     Вага підібрана виміряно, не на око:
    //     Regular=0.138, Medium=0.191, SemiBold=0.212 проти Comfortaa=0.224 —
    //     SemiBold найближчий (і підтверджено прямим рендер-порівнянням
    //     поруч з оригіналом). Regular/Medium лишені в ttf-fonts/ як
    //     проміжні кроки вимірювання, але фактично не використовуються.
    // EN: 2026-07-25 — Comfortaa REPLACED with Fira Sans SemiBold for
    //     body_large/body_medium/body_small/body_xsmall/ingame/ingame_small.
    //     Reason: an origin check showed that Comfortaa's Cyrillic extension
    //     (like Oswald's) was done by the same studio — the same
    //     provenance mismatch, just in a different font (details in
    //     README.md). Fira Sans (normal width) is the same family/authors
    //     as Fira Sans Extra Condensed (Carrois/Mozilla, Cyrillic by
    //     Nikoltchev/Kateliev, Bulgaria) — clean, separate authorship.
    //     The weight was picked by measurement, not by eye: Regular=0.138, Medium=0.191,
    //     SemiBold=0.212 vs Comfortaa=0.224 — SemiBold is the closest match
    //     (confirmed with a direct render comparison next to the original).
    //     Regular/Medium remain in ttf-fonts/ as intermediate measurement
    //     steps but are not actually used.
    // UA: 2026-07-25 — factions: пряма перевірка на реальних пікселях
    //     оригіналу (H/I/o з factions.png) підтвердила, що там курсивний нахил + чорний
    //     контур навколо білого заповнення (не просто товщий штрих — ExtraBold
    //     0.255/Black 0.284 виміряно, і жоден не відтворює контур). Обрано
    //     "повна відповідність": окремий статичний файл *-BoldItalic.ttf
    //     (нахил "запечений" у контурах гліфів — на відміну від variable-font,
    //     FontStyle.Italic на прямому Bold-файлі нічого не нахилив би) +
    //     контурний рендер-прохід у AtlasProcessor.RenderGlyphSolo (товщина
    //     контуру ~2px виміряна напряму зі зрізу пікселів 'I').
    //     Безпечний фолбек: якщо BoldItalic.ttf ще не покладено в ttf-fonts/,
    //     повертаємось на прямий Bold (як і було), щоб нічого не зламати.
    // EN: 2026-07-25 — factions: direct inspection of the real original
    //     pixels (H/I/o from factions.png) confirmed it has an italic slant + a black
    //     outline around the white fill (not just a heavier stroke — ExtraBold
    //     0.255/Black 0.284 measured, and neither reproduces an outline).
    //     "Full fidelity" was chosen: a separate static *-BoldItalic.ttf file
    //     (the slant is baked into the glyph outlines — unlike a variable
    //     font, FontStyle.Italic on the upright Bold file would not slant
    //     anything) + an outline render pass in AtlasProcessor.RenderGlyphSolo
    //     (outline thickness ~2px, measured directly from an 'I' pixel
    //     cross-section).
    //     Safe fallback: if BoldItalic.ttf isn't in ttf-fonts/ yet, falls back
    //     to the upright Bold (as before) so nothing breaks.
    public static string GetFontTtfPath(string name)
    {
        name = name.ToLowerInvariant();
        if (name.Contains("factions"))
        {
            string italicPath = Path.Combine(TtfDir, "FiraSansExtraCondensed-BoldItalic.ttf");
            return File.Exists(italicPath) ? italicPath : Path.Combine(TtfDir, "FiraSansExtraCondensed-Bold.ttf");
        }
        if (name.Contains("header"))
            return Path.Combine(TtfDir, "FiraSansExtraCondensed-Bold.ttf");
        // UA: Насправді МЕРТВИЙ шлях — "indicator" у Config.SkipFonts (немає
        //     жодного кириличного гліфа в оригіналі), тому генератор сюди
        //     ніколи не заходить. Файл Cuprum-Bold.ttf лишається в ttf-fonts/
        //     про всяк випадок, але фактично не рендериться — отже питання
        //     походження шрифту Cuprum на практиці неактуальне.
        // EN: Actually a DEAD path — "indicator" is in Config.SkipFonts (no
        //     Cyrillic glyphs in the original at all), so the generator
        //     never reaches this branch. Cuprum-Bold.ttf stays in ttf-fonts/
        //     just in case, but is never actually rendered — so the question
        //     of Cuprum's font provenance is moot in practice.
        if (name.Contains("indicator"))
            return Path.Combine(TtfDir, "Cuprum-Bold.ttf");
        return Path.Combine(TtfDir, "FiraSans-SemiBold.ttf");
    }
}
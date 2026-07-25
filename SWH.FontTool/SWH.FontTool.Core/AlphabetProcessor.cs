// =============================================================================
// SWH.FontTool.Core — AlphabetProcessor.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Логіка перепризначення слотів .fnt для української локалізації.
//
//     Пріоритет донорів:
//       1. Весь кириличний блок — первинне джерело (знищуємо повністю).
//       2. Інші не-ASCII символи — лише якщо кириличних слотів не вистачило.
//          При виборі резервного донора враховуємо розмір слота:
//          великі UA літери → шукаємо слот з AtlasH ≈ uppercase height,
//          малі → ≈ lowercase height. Перевага ширшим слотам.
//
//     ASCII (32–126) — недоторканні за будь-яких обставин.
// EN: The logic for remapping .fnt slots for Ukrainian localization.
//
//     Donor priority:
//       1. The entire Cyrillic block — the primary source (fully consumed).
//       2. Other non-ASCII characters — only if Cyrillic slots run short.
//          When picking a reserve donor, slot size is taken into account:
//          uppercase UA letters → look for a slot with AtlasH ≈ uppercase
//          height, lowercase → ≈ lowercase height. Wider slots preferred.
//
//     ASCII (32–126) — untouchable under any circumstances.
// =============================================================================

namespace SWH.FontTool.Core;

public static class AlphabetProcessor
{
    public const string UaUppercase =
        "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯ";

    public const string UaLowercase =
        "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя";

    public static string UaAlphabet => UaUppercase + UaLowercase;

    private static readonly HashSet<int> UaIds =
        new(UaAlphabet.Select(c => (int)c));

    private static readonly HashSet<int> SharedCyrillicIds = new(
        UaAlphabet.Select(c => (int)c).Where(id => id is >= 1040 and <= 1103)
    );

    // UA: RU-унікальні слоти → UA-унікальні codepoints.
    //       Ё→Є, Э→І, Ъ→Ї, Ы→Ґ  (верхній регістр)
    //       ё→є, э→і, ъ→ї, ы→ґ  (нижній регістр)
    // EN: RU-unique slots -> UA-unique codepoints.
    //       Ё→Є, Э→І, Ъ→Ї, Ы→Ґ  (uppercase)
    //       ё→є, э→і, ъ→ї, ы→ґ  (lowercase)
    private static readonly Dictionary<int, int> RuUniqueToUa = new()
    {
        { 1025, 1028 }, { 1069, 1030 }, { 1066, 1031 }, { 1067, 1168 },
        { 1105, 1108 }, { 1101, 1110 }, { 1098, 1111 }, { 1099, 1169 },
    };

    public static Dictionary<int, int> BuildRemapPlan(FontAnalysisResult analysis)
    {
        var plan = new Dictionary<int, int>();
        var records = analysis.Records;

        // UA: ASCII — недоторканні / EN: ASCII — untouchable
        foreach (var r in records.Where(r => r.IsAsciiPrintable))
            plan[r.ID] = r.ID;

        // UA: Спільні UA/RU кириличні слоти / EN: Shared UA/RU Cyrillic slots
        foreach (var r in records.Where(r => SharedCyrillicIds.Contains(r.ID)))
            plan[r.ID] = r.ID;

        // UA: RU-унікальні → UA-унікальні / EN: RU-unique -> UA-unique
        foreach (var (ruId, uaId) in RuUniqueToUa)
        {
            if (records.Any(r => r.ID == ruId))
                plan[ruId] = uaId;
            else
            {
                Console.WriteLine(
                    $"   UA [{analysis.FontName}] Кириличний донор {ruId} ({(char)ruId}) " +
                    $"відсутній → шукаємо резерв для UA {uaId} ({(char)uaId})");
                Console.WriteLine(
                    $"   EN [{analysis.FontName}] Cyrillic donor {ruId} ({(char)ruId}) " +
                    $"is missing → looking for a reserve donor for UA {uaId} ({(char)uaId})");
            }
        }

        // UA: Перевірка покриття / EN: Coverage check
        var covered = new HashSet<int>(plan.Values.Where(IsUa));
        var missing = UaIds.Where(id => !covered.Contains(id)).OrderBy(id => id).ToList();

        if (missing.Count == 0) return plan;

        // UA: КРИТИЧНО ВИПРАВЛЕНО (2026-07-24, реальний баг у грі: у мовному
        //     меню зникла 'ñ' з "Español"): попередня версія шукала резерв
        //     серед БУДЬ-ЯКОГО не-ASCII не-кириличного слота, підбираючи лише
        //     за розміром. Але Ё/Ъ/ё відсутні як окремі гліфи майже в КОЖНОМУ
        //     шрифті цієї гри (перевірено побайтово: body_*, header_*,
        //     factions, ingame_* — всі без Ё/Ъ/ё) — тобто резервний шлях
        //     спрацьовував ПОСТІЙНО, а єдиний пул не-ASCII не-кириличних
        //     символів у цих шрифтах — це САМЕ Latin-1 Supplement
        //     (ñ,ç,ü,é,à,ö,ß...), тобто акцентовані літери, які АКТИВНО
        //     використовують Español/Français/Deutsch/Italiano. Забираючи
        //     такий слот під українську літеру — одночасно ЛАМАЄТЬСЯ текст
        //     іншої мови в тому самому файлі — рушій же не знає про "мову",
        //     він завжди дивиться в ОДИН спільний .fnt.
        //
        //     Тепер резерв шукається за ПРІОРИТЕТОМ БЕЗПЕКИ, не лише розміру:
        //       1. "Мертві" записи (AtlasW<=0 або AtlasH<=0) — рушій і так
        //          нічого не показує на цьому ID, тому жодна мова нічого не
        //          втрачає. Є практично в кожному шрифті (по одному,
        //          підтверджено аналізом: IPA/Extended-B "сирітки" на кшталт
        //          U+0230/U+019E/U+01F3 — залишки від майстер-шрифту,
        //          ніколи не використовуються жодною з підтримуваних мов).
        //       2. Не-Latin-1-Supplement слоти (ID поза 0x00A0–0x00FF) —
        //          екзотичні Latin Extended-A/B/IPA символи (ʀ,ǳ,Ɲ,Ʉ тощо),
        //          яких Іспанська/Французька/Німецька/Італійська НЕ
        //          використовують.
        //       3. ОСТАННІЙ ЗАСІБ — Latin-1 Supplement. Використовується
        //          лише якщо перші два варіанти вичерпані, і супроводжується
        //          ГОЛОСНИМ попередженням, бо це МОЖЕ зламати іншу мову.
        // EN: CRITICALLY FIXED (2026-07-24, a real in-game bug: 'ñ' vanished
        //     from "Español" in the language menu): the previous version
        //     searched for a reserve among ANY non-ASCII non-Cyrillic slot,
        //     picking purely by size. But Ё/Ъ/ё are missing as standalone
        //     glyphs in almost EVERY font in this game (checked byte-by-byte:
        //     body_*, header_*, factions, ingame_* — all without Ё/Ъ/ё) —
        //     meaning the reserve path fired CONSTANTLY, and the only pool of
        //     non-ASCII non-Cyrillic characters in these fonts IS the Latin-1
        //     Supplement (ñ,ç,ü,é,à,ö,ß...) — the accented letters ACTIVELY
        //     used by Español/Français/Deutsch/Italiano. Taking such a slot
        //     for a Ukrainian letter simultaneously BREAKS another language's
        //     text in the same file — the engine doesn't know about
        //     "languages", it always looks into ONE shared .fnt.
        //
        //     The reserve search now goes by SAFETY PRIORITY, not just size:
        //       1. "Dead" records (AtlasW<=0 or AtlasH<=0) — the engine
        //          already shows nothing for this ID, so no language loses
        //          anything. Present in almost every font (one each,
        //          confirmed by analysis: IPA/Extended-B "orphans" like
        //          U+0230/U+019E/U+01F3 — leftovers from the master font,
        //          never used by any of the supported languages).
        //       2. Non-Latin-1-Supplement slots (ID outside 0x00A0–0x00FF) —
        //          exotic Latin Extended-A/B/IPA characters (ʀ,ǳ,Ɲ,Ʉ, etc.)
        //          that Spanish/French/German/Italian do NOT use.
        //       3. LAST RESORT — Latin-1 Supplement. Used only if the first
        //          two options are exhausted, and comes with a LOUD warning,
        //          because this MAY break another language.
        Console.WriteLine(
            $"   UA [{analysis.FontName}] {missing.Count} UA символів без кириличного донора " +
            "— підбираємо резерв за пріоритетом безпеки (мертвий слот > екзотичний Latin Extended > Latin-1).");
        Console.WriteLine(
            $"   EN [{analysis.FontName}] {missing.Count} UA characters have no Cyrillic donor " +
            "— picking a reserve by safety priority (dead slot > exotic Latin Extended > Latin-1).");

        // UA: Референсні висоти: медіана uppercase та lowercase слотів
        // EN: Reference heights: median of uppercase and lowercase slots
        float upRefH = Median(records
            .Where(r => r.IsLatinCapital && r.AtlasH > 0).Select(r => r.AtlasH));
        float loRefH = Median(records
            .Where(r => r.IsLatinLower && r.AtlasH > 0).Select(r => r.AtlasH));

        // UA: Мінімально допустима висота (50% від відповідного регістру)
        // EN: Minimum acceptable height (50% of the matching case)
        float upMinH = upRefH * 0.5f;
        float loMinH = loRefH * 0.5f;

        // UA: "Ризиковані" діапазони — усе, що ПРАВДОПОДІБНО активно
        //     використовується якоюсь із реальних мов інтерфейсу:
        //       0x00A0–0x00FF Latin-1 Supplement — ñ,ç,ü,é,à,ö,ß... (іспанська/
        //         французька/німецька/італійська);
        //       0x0100–0x017F Latin Extended-A — ł,ń,ś,ź,ż,ą,ę,ő,ű...
        //         (польська/угорська/чеська — у шрифті 'ingame' є повний набір
        //         таких символів, тож підтримка цих мов цілком можлива);
        //       Œ/œ (0x0152/0x0153) — французька лігатура;
        //       0x2010–0x2027 загальна пунктуація (тире, лапки, три крапки) —
        //         використовується практично в БУДЬ-ЯКІЙ мові;
        //       € (0x20AC) — знак валюти, може відображатись у цінах.
        //     Поза цими діапазонами лишаються тільки СПРАВДІ екзотичні
        //     Latin Extended-B / IPA Extensions "сирітки" (ʀ,ǳ,Ɲ,Ʉ,Ƙ...) —
        //     саме вони й формують безпечний резервний пул рівня 2.
        // EN: "Risky" ranges — anything PLAUSIBLY actively used by one of the
        //     actual interface languages:
        //       0x00A0–0x00FF Latin-1 Supplement — ñ,ç,ü,é,à,ö,ß... (Spanish/
        //         French/German/Italian);
        //       0x0100–0x017F Latin Extended-A — ł,ń,ś,ź,ż,ą,ę,ő,ű...
        //         (Polish/Hungarian/Czech — the 'ingame' font has a full set
        //         of such characters, so support for these languages is
        //         entirely plausible);
        //       Œ/œ (0x0152/0x0153) — the French ligature;
        //       0x2010–0x2027 general punctuation (dashes, quotes, ellipsis) —
        //         used in practically ANY language;
        //       € (0x20AC) — the currency sign, may show up in prices.
        //     Outside these ranges, only genuinely exotic Latin Extended-B /
        //     IPA Extensions "orphans" remain (ʀ,ǳ,Ɲ,Ʉ,Ƙ...) — those form the
        //     safe tier-2 reserve pool.
        static bool IsRiskyDonor(int id) =>
            (id is >= 0x00A0 and <= 0x00FF) ||
            (id is >= 0x0100 and <= 0x017F) ||
            id is 0x0152 or 0x0153 ||
            (id is >= 0x2010 and <= 0x2027) ||
            id == 0x20AC;

        // UA: ДОДАНО (2026-07-24, реальний тест: 'ñ' і надалі зникає з
        //     "Español" навіть із 3-рівневою системою вище) — причина: у цих
        //     шрифтах взагалі НЕМА жодного "мертвого" чи екзотичного
        //     Latin Extended слота у достатній кількості (підтверджено:
        //     деяким шрифтам треба 3-4 резерви, а знаходиться лише 1 мертвий
        //     слот), тому Рівень 3 (Latin-1) спрацьовує ЗАВЖДИ для 2-3
        //     символів на шрифт — це вже не рідкісний виняток, а стабільна
        //     закономірність. Але й УСЕРЕДИНІ Рівня 3 не всі символи
        //     однаково ризиковані: Œ/œ (французька лігатура, яку носії
        //     французької в побуті майже НІКОЛИ не набирають — пишуть "oe")
        //     значно безпечніша за ñ/é/ü/ö/ä/ç/ß — голосні з діакритикою, що
        //     трапляються в КОЖНОМУ другому слові Іспанської/Французької/
        //     Німецької/Італійської/Португальської. Стара сортування
        //     Рівня 3 (лише за розміром) не робила різниці між ними — тому
        //     ñ і потрапляв під роздачу, щойно Œ/œ вичерпувались.
        //
        //     Тепер усередині Рівня 3 сортування йде СПОЧАТКУ за оціночним
        //     "рейтингом ризику" (RiskScore, нижче=безпечніше), і лише
        //     ПОТІМ — за розміром. Це не гарантія (немає доступу до
        //     реальних рядків локалізації гри, щоб перевірити напевно, який
        //     символ де використовується), а евристика за реальною частотою
        //     цих символів у природній мові — але вона систематично
        //     ПОКРАЩУЄ шанси, замінюючи "перший-ліпший за розміром" на
        //     "найменш вживаний спочатку".
        // EN: ADDED (2026-07-24, a real test: 'ñ' still vanishes from
        //     "Español" even with the 3-tier system above) — reason: these
        //     fonts simply don't have enough "dead" or exotic Latin Extended
        //     slots to go around (confirmed: some fonts need 3-4 reserves,
        //     but only 1 dead slot exists), so Tier 3 (Latin-1) fires ALWAYS
        //     for 2-3 characters per font — no longer a rare exception but a
        //     stable pattern. But not every character WITHIN Tier 3 is
        //     equally risky: Œ/œ (the French ligature, which French speakers
        //     almost NEVER type day-to-day — they write "oe" instead) is far
        //     safer than ñ/é/ü/ö/ä/ç/ß — accented vowels that show up in
        //     every other word of Spanish/French/German/Italian/Portuguese.
        //     The old Tier-3 ordering (by size only) made no distinction —
        //     so ñ got handed out the moment Œ/œ ran out.
        //
        //     Now, within Tier 3, sorting goes FIRST by an estimated "risk score"
        //     (RiskScore, lower = safer), and only THEN by size. This isn't
        //     a guarantee (there's no access to the game's actual
        //     localization string files to verify for certain which
        //     character is used where) — it's a heuristic based on the real
        //     natural-language frequency of these characters — but it
        //     systematically IMPROVES the odds by replacing "first match by
        //     size" with "least-used first."
        static int RiskScore(int id) => id switch
        {
            0x0178 => 0, // Ÿ — майже ніколи не набирається / almost never typed
            0x00FF => 0, // ÿ
            0x00AA => 0, // ª
            0x00BA => 0, // º
            0x00A4 => 1, // ¤ — узагальнений знак валюти, ігри показують конкретний ($/€/£) / generic currency sign
            0x00AC => 1, // ¬
            0x00A6 => 1, // ¦
            0x00B5 => 2, // µ
            0x00A7 => 2, // §
            0x00B1 => 2, // ±
            0x00B0 => 3, // ° — можливий (температура), але малоймовірний у цій грі / possible (temperature) but unlikely here
            0x0152 => 3, // Œ
            0x0153 => 3, // œ
            0x00D8 => 4, // Ø — данська/норвезька, ймовірно не підтримується / Danish/Norwegian, likely unsupported
            0x00F8 => 4, // ø
            0x00C6 => 4, // Æ
            0x00E6 => 4, // æ
            _ when id is >= 0x2010 and <= 0x2027 => 6, // тире/лапки/три крапки — вживані, але не голосні / dashes/quotes/ellipsis — used, but not vowels
            0x20AC => 6, // €
            _ when id is >= 0x0100 and <= 0x017F => 7, // Latin Extended-A (ł,ń,ś,ź,ą,ę...) — польська/угорська/чеська
            _ when id is >= 0x00A0 and <= 0x00FF => 9, // решта Latin-1 Supplement — типові голосні з діакритикою (é,è,ñ,ü,ö,ä,ç,ß) / the rest of Latin-1 Supplement — common accented vowels
            _ => 5,
        };

        foreach (int uaId in missing)
        {
            bool isUpper = char.IsUpper((char)uaId);
            float targetH = isUpper ? upRefH : loRefH;
            float minH = isUpper ? upMinH : loMinH;

            var eligible = records
                .Where(r => !r.IsAsciiPrintable && !r.IsCyrillicBlock && !plan.ContainsKey(r.ID))
                .ToList();

            // UA: Рівень 1 — мертвий слот (нульова/від'ємна площа). Розмір не
            //     має значення: уся геометрія однаково буде перезаписана.
            // EN: Tier 1 — a dead slot (zero/negative area). Size doesn't
            //     matter: its geometry is about to be overwritten entirely anyway.
            var donor = eligible
                .Where(r => r.AtlasW <= 0 || r.AtlasH <= 0)
                .FirstOrDefault();
            string tier = "мертвий слот/dead slot";

            // UA: Рівень 2 — будь-що ПОЗА Latin-1 Supplement, найближче за розміром.
            // EN: Tier 2 — anything OUTSIDE Latin-1 Supplement, closest by size.
            if (donor is null)
            {
                donor = eligible
                    .Where(r => !IsRiskyDonor(r.ID) && r.AtlasH >= minH)
                    .OrderBy(r => Math.Abs(r.AtlasH - targetH))
                    .ThenByDescending(r => r.AtlasW)
                    .FirstOrDefault();
                tier = "екзотичний Latin Extended/exotic Latin Extended";
            }

            // UA: Рівень 3 (ОСТАННІЙ ЗАСІБ) — Latin-1 Supplement. Може зламати
            //     текст іншої мови, що використовує цей символ — попереджаємо голосно.
            // EN: Tier 3 (LAST RESORT) — Latin-1 Supplement. May break another
            //     language's text that uses this character — warn loudly.
            if (donor is null)
            {
                donor = eligible
                    .Where(r => r.AtlasH >= minH)
                    .OrderBy(r => RiskScore(r.ID))
                    .ThenBy(r => Math.Abs(r.AtlasH - targetH))
                    .ThenByDescending(r => r.AtlasW)
                    .FirstOrDefault();
                tier = "ОСТАННІЙ ЗАСІБ Latin-1/LAST-RESORT Latin-1";
            }

            if (donor != null)
            {
                plan[donor.ID] = uaId;
                bool risky = IsRiskyDonor(donor.ID);
                string riskTag = risky ? " [!] РИЗИКОВАНО/RISKY" : "";
                Console.WriteLine(
                    $"   UA [{analysis.FontName}] Резерв ({tier}): ID={donor.ID} ({(char)donor.ID}) " +
                    $"[{donor.AtlasW}×{donor.AtlasH}] → UA {uaId} ({(char)uaId}) (target H={targetH:F0}){riskTag}");
                Console.WriteLine(
                    $"   EN [{analysis.FontName}] Reserve ({tier}): ID={donor.ID} ({(char)donor.ID}) " +
                    $"[{donor.AtlasW}×{donor.AtlasH}] → UA {uaId} ({(char)uaId}) (target H={targetH:F0}){riskTag}");
                if (risky)
                {
                    Console.WriteLine(
                        $"   [!] UA: символ {(char)donor.ID} (U+{donor.ID:X4}) міг активно використовуватись " +
                        "іншою мовою (напр. Español/Français/Deutsch/Italiano) — перевірте цей екран у грі.");
                    Console.WriteLine(
                        $"   [!] EN: character {(char)donor.ID} (U+{donor.ID:X4}) may have been actively used " +
                        "by another language (e.g. Español/Français/Deutsch/Italiano) — check that screen in-game.");
                }
            }
            else
            {
                Console.WriteLine(
                    $"   UA [{analysis.FontName}] УВАГА: {(char)uaId} (U+{uaId:X4}) " +
                    $"— придатного слота не знайдено (target H={targetH:F0}, min={minH:F0})");
                Console.WriteLine(
                    $"   EN [{analysis.FontName}] WARNING: {(char)uaId} (U+{uaId:X4}) " +
                    $"— no suitable slot found (target H={targetH:F0}, min={minH:F0})");
            }
        }

        return plan;
    }

    private static float Median(IEnumerable<float> source)
    {
        var list = source.OrderBy(v => v).ToList();
        return list.Count > 0 ? list[list.Count / 2] : 0f;
    }

    public static bool IsUa(int id) => UaIds.Contains(id);
    public static bool IsCyrillicBlock(int id) => id is >= 1024 and <= 1119;
    public static bool IsAscii(int id) => id is >= 32 and <= 126;
    public static bool IsLatin(int id) =>
        (id is >= 65 and <= 90) || (id is >= 97 and <= 122);
}
// =============================================================================
// SWH.LocEditor.Core — LocEntry.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Один рядок оригінального мовного CSV гри. Зберігає ВСІ колонки як є —
//     редагується лише колонка перекладу (індекс 1), решта переноситься
//     побайтово при збірці назад, так само як у TextValidator.
// EN: A single row of the game's original language CSV. Keeps ALL columns
//     as-is — only the translation column (index 1) is edited, everything
//     else is carried through unchanged when rebuilding, same as in
//     TextValidator.
// =============================================================================

using System.Text.RegularExpressions;

namespace SWH.LocEditor.Core;

public class LocEntry
{
    public int RowIndex { get; }
    public string[] Columns { get; }

    public string Key => Columns.Length > 0 ? Columns[0].Trim() : "";
    public string Original => Columns.Length > 1 ? Columns[1] : "";
    public string Comment => Columns.Length > 2 ? Columns[2] : "";

    /// <summary>
    /// UA: Рядок вважається структурним (не текстовим), якщо в ньому менше
    ///     2 колонок — такі рядки завжди переносяться без змін і ніколи не
    ///     потребують перекладу.
    /// EN: A row is structural (non-text) if it has fewer than 2 columns —
    ///     such rows always pass through unchanged and never need translation.
    /// </summary>
    public bool IsStructural => Columns.Length < 2 || string.IsNullOrEmpty(Key);

    // UA: ВИДАЛЕНО: попередня версія вважала технічним будь-який рядок, чий
    //     КЛЮЧ починається з "#" (IsSectionHeader). Це виявилось хибним —
    //     багато цілком реальних діалогових рядків (напр. ключ
    //     "#\"peddler_small_talk_02_01") теж починаються з "#" за
    //     конвенцією іменування гри, і мали справжній текст для перекладу.
    //     Повертаємось до чистого правила користувача: "тільки цифри та
    //     символи, не літери = технічний" — жодних винятків за префіксом
    //     ключа. Порожні структурні "# ----- Generic -----" рядки й так
    //     ловляться цим правилом, бо їхній Original порожній.
    // EN: REMOVED: the previous version treated any row whose KEY started
    //     with "#" (IsSectionHeader) as technical. That was wrong — many
    //     genuinely real dialogue rows (e.g. key "#\"peddler_small_talk_02_01")
    //     also start with "#" per the game's own key-naming convention, and
    //     had real text needing translation. Back to the user's original,
    //     clean rule: "only digits and symbols, no letters = technical" — no
    //     exceptions based on key prefix. Empty structural
    //     "# ----- Generic -----" rows are still caught by this same rule,
    //     since their Original is empty.

    // UA: Теги/плейсхолдери, які треба ВИРІЗАТИ перед перевіркою на літери —
    //     інакше слова всередині самих тегів (напр. "color" у "{color
    //     0.55,0.91,1,1}", "money"/"image" у "{image money}") хибно
    //     рахуються як "текст" і ламають визначення технічного рядка (саме
    //     тому term_money/loot_water/mission_info_desc не потрапляли в
    //     "Технічні" — вони містять {image ...}/{color ...} з латинськими
    //     літерами всередині тегу, які не є текстом для перекладу).
    // EN: Tags/placeholders that must be STRIPPED before the letter check —
    //     otherwise words inside the tags themselves (e.g. "color" in
    //     "{color 0.55,0.91,1,1}", "money"/"image" in "{image money}") get
    //     falsely counted as "text" and break the technical-row detection
    //     (this is exactly why term_money/loot_water/mission_info_desc
    //     weren't landing in "Technical" — they contain {image ...}/{color
    //     ...} tags with Latin letters inside the tag itself, which is not
    //     translatable text).
    private static readonly Regex TagsAndPlaceholders =
        new(@"\{[^}]*\}|%[A-Za-z0-9_]*%?|\\n", RegexOptions.Compiled);

    // UA: Ключі-винятки, що завжди технічні незалежно від вмісту Original —
    //     "свідомо технічні" за прямою вказівкою користувача. За аналогією з
    //     EaW (там теж є кілька хардкод-ключів на кшталт TEXT_END_OF_DATA):
    //     "string" — не діалог, а перший рядок-метадані файлу (тип/мовна
    //     декларація); menu_contact_signature/menu_facebook_text/
    //     menu_email_text/menu_options_vsync/menu_twitter_text/
    //     menu_website_text/menu_youtube_text — назви брендів (Facebook,
    //     Twitter, YouTube), технічний термін (VSync) і підпис студії — усі
    //     МАЮТЬ реальні літери (тому "немає літер" їх не ловить), але за
    //     змістом ніколи не перекладаються.
    // EN: Key exceptions that are always technical regardless of Original
    //     content — "intentionally technical" per explicit user
    //     instruction. Same pattern as EaW (which also has a few hardcoded
    //     keys like TEXT_END_OF_DATA): "string" — not dialogue, but the
    //     file's first metadata row (type/language declaration);
    //     menu_contact_signature/menu_facebook_text/menu_email_text/
    //     menu_options_vsync/menu_twitter_text/menu_website_text/
    //     menu_youtube_text — brand names (Facebook, Twitter, YouTube), a
    //     technical term (VSync), and the studio signature — all DO contain
    //     real letters (so "no letters" doesn't catch them), but are never
    //     translated by meaning.
    private static readonly HashSet<string> TechnicalKeyExceptions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "string",
            "menu_contact_signature",
            "menu_facebook_text",
            "menu_email_text",
            "menu_options_vsync",
            "menu_twitter_text",
            "menu_website_text",
            "menu_youtube_text",
        };

    // UA: ЗАГАЛЬНЕ правило замість подальшого хардкоду ключів — перевірено
    //     на реальних даних гри: language_russian/portuguese/german/italian/
    //     spanish/english/french, weapon_scoped_rare_06, weapon_rpg_rare_08,
    //     difficulty_demo, difficulty_demo_desc — усі мають у 3-й колонці
    //     (Developer Comments) точну фразу "Do not translate"/"Don't
    //     translate". Розробник сам явно позначає такі рядки в коментарі —
    //     тож замість того щоб довічно хардкодити кожен новий ключ, одне
    //     правило "коментар містить 'do not translate'" ловить їх усі, і
    //     будь-які майбутні аналогічні рядки теж, автоматично.
    // EN: GENERAL rule instead of further hardcoding keys — verified against
    //     real game data: language_russian/portuguese/german/italian/
    //     spanish/english/french, weapon_scoped_rare_06, weapon_rpg_rare_08,
    //     difficulty_demo, difficulty_demo_desc — all have the exact phrase
    //     "Do not translate"/"Don't translate" in the 3rd column (Developer
    //     Comments). The developer explicitly flags such rows in the
    //     comment himself — so instead of hardcoding every new key forever,
    //     one rule "comment contains 'do not translate'" catches all of
    //     them, and any future similar rows automatically too.
    private static readonly Regex DoNotTranslateComment =
        new(@"do\s*n[o']?t\s+translate", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool CommentSaysDoNotTranslate(string comment) =>
        !string.IsNullOrEmpty(comment) && DoNotTranslateComment.IsMatch(comment);

    /// <summary>
    /// UA: Рядок технічний — не має жодного РЕАЛЬНОГО тексту для перекладу:
    ///     оригінал порожній, або після вирізання тегів/плейсхолдерів
    ///     ({...}, %1%, \n) не лишається жодної літери, або ключ входить у
    ///     TechnicalKeyExceptions, або сам розробник написав у коментарі
    ///     "do not translate" / "don't translate". Такі рядки падають в
    ///     окремий фільтр "Технічні", а НЕ в "Без перекладу", і виключаються
    ///     з підрахунку дублікатів.
    /// EN: A technical row — has no ACTUAL text to translate: the original
    ///     is empty, or after stripping tags/placeholders ({...}, %1%, \n)
    ///     no letters remain, or the key is in TechnicalKeyExceptions, or the
    ///     developer himself wrote "do not translate" / "don't translate" in
    ///     the comment. Such rows fall into a separate "Technical" filter,
    ///     NOT "Untranslated", and are excluded from duplicate-group counting.
    /// </summary>
    public bool IsTechnical => !IsStructural &&
        (TechnicalKeyExceptions.Contains(Key) || HasNoLetters(Original) || CommentSaysDoNotTranslate(Comment));

    private static bool HasNoLetters(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;
        string stripped = TagsAndPlaceholders.Replace(s, "");
        foreach (char c in stripped)
            if (char.IsLetter(c)) return false;
        return true;
    }

    public string Translated { get; set; }

    /// <summary>
    /// UA: П'ята колонка review TSV — існує ЛИШЕ в review, не в оригіналі
    ///     гри. У Google Таблицях це позначка проходження вичитки (напр.
    ///     "версія 0.20", "версія 1.00" чи прогрес-бар "████ 100,00%") —
    ///     тобто ознака того, ЩО й КОЛИ саме перевірялося, а не сам
    ///     переклад. Порожньо, якщо рядок ще не проходив review.
    /// EN: The 5th column of the review TSV — exists ONLY in the review
    ///     file, not in the game's original. In Google Sheets this is a
    ///     proofreading-pass marker (e.g. "version 0.20", "version 1.00", or
    ///     a progress bar "████ 100.00%") — i.e. a marker of WHAT and WHEN
    ///     it was checked, not the translation itself. Empty if the row
    ///     hasn't gone through review yet.
    /// </summary>
    public string ReviewNote { get; set; } = "";

    public bool WasReviewed => !string.IsNullOrWhiteSpace(ReviewNote);

    // ── Дублікати оригіналу / Duplicate originals ───────────────────────────
    //
    // UA: Обчислюється в CsvLocDocument.RecomputeDuplicates() — окремий
    //     рядок сам по собі не знає про інші рядки документа, тому ці поля
    //     заповнює документ після Parse()/MergeReview().
    // EN: Computed in CsvLocDocument.RecomputeDuplicates() — a single row
    //     has no knowledge of the rest of the document, so these fields are
    //     filled in by the document after Parse()/MergeReview().

    /// <summary>
    /// UA: Кількість рядків (включно з цим) з ідентичним оригінальним
    ///     текстом. 1 — унікальний рядок.
    /// EN: Number of rows (including this one) sharing identical original
    ///     text. 1 — a unique row.
    /// </summary>
    public int DuplicateGroupSize { get; internal set; } = 1;

    public bool IsDuplicateOriginal => DuplicateGroupSize > 1;

    /// <summary>
    /// UA: True, якщо рядки з однаковим оригіналом мають РІЗНІ переклади —
    ///     ймовірна недбалість (один із дублікатів забули узгодити).
    /// EN: True if rows sharing the same original have DIFFERENT
    ///     translations — a likely oversight (one duplicate wasn't
    ///     brought in sync with the others).
    /// </summary>
    public bool HasInconsistentDuplicateTranslation { get; internal set; }

    public LocEntry(int rowIndex, string[] columns)
    {
        RowIndex = rowIndex;
        Columns = columns;
        // UA: За замовчуванням переклад = оригінал (як і в TextValidator) —
        //     доки не буде явно перезаписаний з review чи вручну в GUI.
        // EN: Translation defaults to the original (same as TextValidator) —
        //     until explicitly overwritten from review or manually in the GUI.
        Translated = Original;
    }

    /// <summary>
    /// UA: Збирає колонки для запису — оригінальні колонки з підміненою
    ///     колонкою 1 (переклад). Структурні рядки повертаються без змін.
    /// EN: Builds the columns for output — original columns with column 1
    ///     (translation) swapped in. Structural rows are returned unchanged.
    /// </summary>
    public string[] BuildOutputColumns()
    {
        if (IsStructural) return Columns;
        var result = (string[])Columns.Clone();
        result[1] = Translated;
        return result;
    }
}

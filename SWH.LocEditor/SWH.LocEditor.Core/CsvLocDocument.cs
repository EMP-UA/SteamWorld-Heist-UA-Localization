// =============================================================================
// SWH.LocEditor.Core — CsvLocDocument.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Розібраний мовний CSV гри (з .csv або з розпакованого .csv.z) — список
//     рядків із підтримкою злиття review-перекладу (TSV з Google Таблиць/
//     Excel), пошуку пропущених ключів та збірки назад у байти для запису.
//     Логіка злиття/пошуку пропущених — те саме, що й у TextValidator,
//     винесене сюди для повторного використання (у т.ч. живого редагування
//     в GUI).
// EN: A parsed game language CSV (from .csv or a decompressed .csv.z) — a
//     list of rows supporting review-translation merging (TSV from Google
//     Sheets/Excel), finding missing keys, and rebuilding back into bytes
//     for saving. The merge/missing-keys logic mirrors TextValidator,
//     extracted here for reuse (including live editing in the GUI).
// =============================================================================

using System.Text;

namespace SWH.LocEditor.Core;

public class CsvLocDocument
{
    private static readonly string[] LineSeparators = { "\r\n", "\n" };

    public List<LocEntry> Entries { get; }

    /// <summary>
    /// UA: Ключі, для яких останнє злиття review дало переклад — усе інше
    ///     вважається "пропущеним" (FindMissingTranslations).
    /// EN: Keys for which the last review merge produced a translation —
    ///     everything else counts as "missing" (FindMissingTranslations).
    /// </summary>
    public HashSet<string> LastMergedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    private CsvLocDocument(List<LocEntry> entries) => Entries = entries;

    /// <summary>
    /// UA: Розбирає сирі байти CSV (уже розпаковані, якщо файл був .csv.z)
    ///     на список рядків. Останній порожній "рядок" після спліту (через
    ///     завершальний \r\n у файлі) пропускається, щоб не додати зайвий
    ///     порожній рядок при збірці назад.
    /// EN: Parses raw CSV bytes (already decompressed if the source was
    ///     .csv.z) into a list of rows. The trailing empty "line" produced
    ///     by splitting on a file's final \r\n is skipped, so rebuilding
    ///     doesn't add an extra blank line.
    /// </summary>
    public static CsvLocDocument Parse(byte[] csvBytes)
    {
        string text = new UTF8Encoding(false).GetString(csvBytes);
        string[] lines = text.Split(LineSeparators, StringSplitOptions.None);

        var entries = new List<LocEntry>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == lines.Length - 1 && lines[i].Length == 0) continue;
            entries.Add(new LocEntry(i, lines[i].Split('\t')));
        }
        var doc = new CsvLocDocument(entries);
        doc.RecomputeDuplicates();
        return doc;
    }

    // ── Дублікати оригіналу / Duplicate originals ───────────────────────────
    //
    // UA: Рядки оригінального тексту в мовних CSV гри часто повторюються
    //     дослівно (той самий текст під різними ключами — наприклад,
    //     однакові підписи кнопок у кількох меню). Це дозволяє швидко
    //     розповсюдити один переклад на всі такі рядки одним кліком, замість
    //     ручного пошуку й копіювання кожного окремо.
    // EN: Original-text rows in the game's language CSVs often repeat
    //     verbatim (the same text under different keys — e.g. identical
    //     button captions across several menus). This allows propagating
    //     one translation to all such rows with a single click, instead of
    //     manually finding and copying each one.

    /// <summary>
    /// UA: Перераховує групи дублікатів оригіналу й позначає рядки з
    ///     неузгодженим перекладом. Викликається автоматично після Parse()
    ///     та MergeReview(); GUI має викликати це також після ручного
    ///     редагування перекладу в клітинці.
    /// EN: Recomputes duplicate-original groups and flags rows with
    ///     inconsistent translations. Called automatically after Parse()
    ///     and MergeReview(); the GUI should also call this after manually
    ///     editing a translation cell.
    /// </summary>
    public void RecomputeDuplicates()
    {
        // UA: Технічні рядки (порожній оригінал, лише змінні, заголовки
        //     розділів) виключаються — інакше вони хибно об'єднались би в
        //     один величезний "дублікат" за спільним порожнім/символьним
        //     оригіналом (див. LocEntry.IsTechnical).
        // EN: Technical rows (empty original, variables-only, section
        //     headers) are excluded — otherwise they'd falsely cluster into
        //     one giant "duplicate" group sharing an empty/symbolic original
        //     (see LocEntry.IsTechnical).
        var groups = Entries
            .Where(e => !e.IsStructural && !e.IsTechnical)
            .GroupBy(e => e.Original, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var members = group.ToList();
            int size = members.Count;
            bool inconsistent = size > 1 &&
                members.Select(e => e.Translated).Distinct(StringComparer.Ordinal).Count() > 1;

            foreach (var entry in members)
            {
                entry.DuplicateGroupSize = size;
                entry.HasInconsistentDuplicateTranslation = inconsistent;
            }
        }
    }

    /// <summary>
    /// UA: Усі групи рядків з ідентичним оригіналом (розмір групи > 1),
    ///     відсортовані за розміром — для фільтра "Дублікати" в GUI.
    /// EN: All groups of rows sharing identical original text (group size
    ///     > 1), sorted by size — for the "Duplicates" filter in the GUI.
    /// </summary>
    public List<List<LocEntry>> FindDuplicateGroups() =>
        Entries
            .Where(e => !e.IsStructural && !e.IsTechnical)
            .GroupBy(e => e.Original, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Select(g => g.ToList())
            .ToList();

    /// <summary>
    /// UA: Усі інші рядки з тим самим оригінальним текстом, що й у source
    ///     (без самого source).
    /// EN: All other rows sharing the same original text as source
    ///     (excluding source itself).
    /// </summary>
    public List<LocEntry> GetDuplicatesOf(LocEntry source) =>
        Entries.Where(e => !e.IsStructural && !e.IsTechnical && !ReferenceEquals(e, source) &&
                            string.Equals(e.Original, source.Original, StringComparison.Ordinal))
               .ToList();

    /// <summary>
    /// UA: Узгоджує переклад однакового оригінального тексту, що
    ///     повторюється під різними ключами: переклад із source
    ///     поширюється на решту рядків цієї групи дублікатів, щоб один і
    ///     той самий вислів не був перекладений по-різному в різних місцях
    ///     гри. Викликається лише явно, за дією користувача (контекстне
    ///     меню в GUI) — ніколи не відбувається автоматично при
    ///     завантаженні файлу чи злитті review. Повертає кількість змінених
    ///     рядків (без source). Викликач має самостійно викликати
    ///     RecomputeDuplicates() після цього.
    /// EN: Synchronizes the translation of identical original text that
    ///     repeats under different keys: source's translation is
    ///     propagated to the rest of that duplicate-original group, so the
    ///     same phrase isn't translated differently in different places in
    ///     the game. Only invoked explicitly by a user action (context menu
    ///     in the GUI) — never runs automatically when loading a file or
    ///     merging review. Returns the number of rows changed (excluding
    ///     source). The caller is responsible for calling
    ///     RecomputeDuplicates() afterwards.
    /// </summary>
    public int ApplyTranslationToDuplicates(LocEntry source)
    {
        var duplicates = GetDuplicatesOf(source);
        foreach (var entry in duplicates)
            entry.Translated = source.Translated;
        return duplicates.Count;
    }

    // ── Структура review TSV / Review TSV layout ────────────────────────────
    //
    // UA: Реальні колонки експорту з Google Таблиць (підтверджено читанням
    //     самого файлу «REVIEW_en_uk_UPDATED V0.2 - Main Game.tsv»):
    //       0  ID                            ← з оригінального CSV гри
    //       1  Original English              ← з оригінального CSV гри
    //       2  Ukrainian Translation         ← ПЕРЕКЛАД (береться з review)
    //       3  Developer Comments            ← з оригінального CSV гри
    //       4  Перевірено?                   ← СТАТУС ВИЧИТКИ (з review):
    //                                          +, -, +/- або довільний коментар
    //       5  Побажання щодо перекладу      ← НЕ дані програми
    //       6  ████████████████████ 100,00%  ← НЕ дані програми
    //
    //     Програма володіє лише колонками 0-4. Колонки 5 і 6 існують
    //     ВИКЛЮЧНО в Google Таблиці — це місце для коментарів і зведеної
    //     інформації для читачів локалізації, а не робочі поля редактора.
    //     Тому вони НЕ зчитуються, НЕ зберігаються і НЕ пишуться: якби
    //     програма їх експортувала (хай навіть порожніми), вона б затирала
    //     чужі дані в таблиці.
    //
    //     Колонки 0, 1, 3 програма бере з ОРИГІНАЛЬНОГО CSV гри, а не з
    //     review — review є джерелом тільки для 2 і 4.
    // EN: The real Google Sheets export columns (confirmed by reading the
    //     actual "REVIEW_en_uk_UPDATED V0.2 - Main Game.tsv" file):
    //       0  ID                            ← from the game's original CSV
    //       1  Original English              ← from the game's original CSV
    //       2  Ukrainian Translation         ← THE TRANSLATION (from review)
    //       3  Developer Comments            ← from the game's original CSV
    //       4  Перевірено? (checked)         ← REVIEW STATUS (from review):
    //                                          +, -, +/- or a free comment
    //       5  Побажання щодо перекладу      ← NOT the program's data
    //       6  ████████████████████ 100,00%  ← NOT the program's data
    //
    //     The program owns columns 0-4 only. Columns 5 and 6 exist SOLELY
    //     in the Google Sheet — they're a place for comments and summary
    //     info aimed at readers of the localization, not editor working
    //     fields. So they are NOT read, NOT stored and NOT written: if the
    //     program exported them (even as empty), it would wipe out other
    //     people's data in the sheet.
    //
    //     Columns 0, 1, 3 are taken by the program from the game's ORIGINAL
    //     CSV, not from the review — the review is the source only for 2 and 4.
    public const int ReviewColKey = 0;
    public const int ReviewColOriginal = 1;
    public const int ReviewColTranslated = 2;
    public const int ReviewColDevComment = 3;
    public const int ReviewColChecked = 4;
    private const int ReviewColCount = 5;

    private const string ReviewHeader =
        "ID\tOriginal English\tUkrainian Translation\tDeveloper Comments\tПеревірено?";

    /// <summary>
    /// UA: Зливає дані з review TSV: колонка 2 → переклад, колонка 4 →
    ///     статус вичитки. Більше нічого з review не береться: 0/1/3 і так
    ///     є в оригінальному CSV гри, а 5/6 належать Google Таблиці й
    ///     програми не стосуються (див. розкладку вище). Повертає кількість
    ///     зіставлених рядків.
    /// EN: Merges data from a review TSV: column 2 → translation, column 4
    ///     → review status. Nothing else is taken from the review: 0/1/3
    ///     already come from the game's original CSV, and 5/6 belong to the
    ///     Google Sheet and are none of the program's business (see the
    ///     layout above). Returns the number of matched rows.
    /// </summary>
    public int MergeReview(byte[] reviewTsvBytes)
    {
        string text = new UTF8Encoding(false).GetString(reviewTsvBytes);
        string[] lines = text.Split(LineSeparators, StringSplitOptions.None);

        var rows = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split('\t');
            if (cols.Length < 3) continue;
            string key = cols[ReviewColKey].Trim();
            if (string.IsNullOrEmpty(key)) continue;
            rows[key] = cols;
        }

        LastMergedKeys.Clear();
        int merged = 0;
        foreach (var entry in Entries)
        {
            if (entry.IsStructural) continue;
            if (!rows.TryGetValue(entry.Key, out var cols)) continue;

            entry.Translated = cols[ReviewColTranslated];
            entry.ReviewNote = ReviewColChecked < cols.Length ? cols[ReviewColChecked] : "";
            LastMergedKeys.Add(entry.Key);
            merged++;
        }
        RecomputeDuplicates();
        return merged;
    }

    /// <summary>
    /// UA: Ключі оригіналу, яких не було знайдено при останньому злитті
    ///     review — тобто ще не перекладені/не звірені рядки.
    /// EN: Original keys not found during the last review merge — i.e. rows
    ///     not yet translated/checked.
    /// </summary>
    public List<LocEntry> FindMissingTranslations() =>
        Entries.Where(e => !e.IsStructural && !LastMergedKeys.Contains(e.Key)).ToList();

    /// <summary>
    /// UA: Експортує документ у TSV з 5 колонками, якими володіє програма
    ///     (ID, оригінал, переклад, коментар розробника, статус вичитки) —
    ///     це РОБОЧИЙ файл проєкту: саме його пише автозбереження і саме
    ///     він редагується вручну в Excel/Google Таблицях. Формат збігається
    ///     з тим, що читає MergeReview, тож файл можна завантажити назад.
    ///
    ///     Колонки 5-6 Google Таблиці («Побажання щодо перекладу» і зведена
    ///     інформація/версія) свідомо НЕ виводяться: вони не є даними
    ///     програми, і запис навіть порожніх значень затер би те, що там
    ///     ведуть люди.
    ///
    ///     Експортуються ВСІ нехструктурні рядки, включно з технічними:
    ///     робочий файл має бути повним зрізом стану, інакше при
    ///     наступному злитті технічні рядки та їхні позначки зникли б.
    ///     Табуляції всередині значень замінюються пробілом, щоб не
    ///     зламати розкладку колонок.
    /// EN: Exports the document to TSV with the 5 columns the program owns
    ///     (ID, original, translation, developer comment, review status) —
    ///     this is the project's WORKING file: what autosave writes and
    ///     what gets hand-edited in Excel/Google Sheets. The format matches
    ///     what MergeReview reads, so the file can be loaded back in.
    ///
    ///     The Google Sheet's columns 5-6 ("translation suggestions" and
    ///     the summary/version info) are deliberately NOT emitted: they are
    ///     not the program's data, and writing even empty values there
    ///     would wipe out what people maintain by hand.
    ///
    ///     ALL non-structural rows are exported, technical ones included:
    ///     a working file must be a complete snapshot of the state,
    ///     otherwise technical rows and their markers would vanish on the
    ///     next merge. Tabs inside values are replaced with spaces so the
    ///     column layout can't break.
    /// </summary>
    public byte[] ToReviewTsvBytes()
    {
        var sb = new StringBuilder();
        sb.Append(ReviewHeader).Append("\r\n");

        foreach (var entry in Entries)
        {
            if (entry.IsStructural) continue;

            var cols = new string[ReviewColCount];
            cols[ReviewColKey] = entry.Key;
            cols[ReviewColOriginal] = entry.Original;
            cols[ReviewColTranslated] = entry.Translated;
            cols[ReviewColDevComment] = entry.Comment;
            cols[ReviewColChecked] = entry.ReviewNote;

            for (int i = 0; i < cols.Length; i++)
            {
                if (i > 0) sb.Append('\t');
                sb.Append((cols[i] ?? "").Replace('\t', ' '));
            }
            sb.Append("\r\n");
        }
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }

    /// <summary>
    /// UA: Збирає документ назад у сирі байти CSV (UTF-8 без BOM, \r\n між
    ///     рядками) — готові для LanguageArchive.SaveRaw.
    /// EN: Rebuilds the document back into raw CSV bytes (UTF-8 without
    ///     BOM, \r\n between rows) — ready for LanguageArchive.SaveRaw.
    /// </summary>
    public byte[] ToCsvBytes()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Entries.Count; i++)
        {
            if (i > 0) sb.Append("\r\n");
            sb.Append(string.Join('\t', Entries[i].BuildOutputColumns()));
        }
        return new UTF8Encoding(false).GetBytes(sb.ToString());
    }
}

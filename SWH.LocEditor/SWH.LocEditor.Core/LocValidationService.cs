// =============================================================================
// SWH.LocEditor.Core — LocValidationService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Технічна валідація перекладеного рядка відносно оригіналу — та сама
//     логіка, що й у TextValidator.ValidateString, винесена сюди для
//     повторного використання (зокрема для живої перевірки під час
//     редагування в GUI).
// EN: Technical validation of a translated row against the original — the
//     same logic as TextValidator.ValidateString, extracted here for reuse
//     (including live validation while editing in the GUI).
// =============================================================================

using System.Text.RegularExpressions;

namespace SWH.LocEditor.Core;

public static class LocValidationService
{
    private static readonly char[] BalancedTags = { '<', '>', '[', ']', '{', '}' };

    // UA: Кирилиця всередині змінної (напр. %д замість %d) — падіння рушія.
    // EN: Cyrillic character inside a variable (e.g. %д instead of %d) — engine crash.
    private static readonly Regex CyrillicInVariable = new(@"%[а-яА-ЯіІїЇєЄґҐ]", RegexOptions.Compiled);

    private static readonly Regex NewlineEscape = new(@"\\n", RegexOptions.Compiled);

    // UA: Поріг довжини перекладу — за зразком EntryRow.LengthRatioThreshold/
    //     LengthMarginThreshold у BF1LocalizationTool.GUI: переклад
    //     вважається підозріло довгим, якщо перевищує
    //     original.Length * ratio + margin символів. Статичні поля (а не
    //     константи) — щоб згодом можна було віддати їх під налаштування у
    //     GUI, як і там.
    //
    //     Початкові значення 1.0/+2 виявились ЗАНАДТО чутливими —
    //     на 60-символьному оригіналі це лише +2 символи (~3%), що ловило б
    //     майже КОЖЕН переклад (українська/слов'янська мова природно довша
    //     за англійську на 15-30% — це нормальне мовне явище, а не помилка).
    //     1.2 / +4 — на 60 символів поріг ~76 (+16, ~27%) — ловить лише
    //     дійсно надмірне роздуття, а не звичайне мовне розширення.
    // EN: Initial 1.0/+2 values turned out TOO sensitive — on a 60-char
    //     original that's only +2 characters (~3%), which would flag
    //     almost EVERY translation (Ukrainian/Slavic is naturally 15-30%
    //     longer than English — that's normal linguistic expansion, not a
    //     bug). 1.2 / +4 — on 60 chars the threshold is ~76 (+16, ~27%) —
    //     catches only genuinely excessive bloat, not routine expansion.
    public static double LengthRatioThreshold = 1.2;
    public static int LengthMarginThreshold = 4;

    // UA: Симетричний поріг для "занадто КОРОТКОГО" перекладу — за прямим
    //     проханням користувача ("розумно також додати примітку якщо рядок
    //     перекладу значно коротше оригіналу"). Занадто короткий переклад
    //     часто означає обрізаний/забутий/скопійований не той рядок.
    //     Перевіряється лише для оригіналів від MinLengthForShortCheck
    //     символів — на дуже коротких рядках (напр. "OK"→"Так") відсоткова
    //     різниця завжди величезна й нічого не каже про проблему.
    // EN: Symmetric threshold for a translation that's too SHORT — per the
    //     user's explicit request ("also sensible to add a note if the
    //     translation is significantly shorter than the original"). An
    //     overly short translation often means truncated/forgotten/wrong
    //     copied text. Only checked for originals of at least
    //     MinLengthForShortCheck characters — on very short strings (e.g.
    //     "OK"→"Так") the percentage difference is always huge and says
    //     nothing about an actual problem.
    public static double ShortRatioThreshold = 0.6;
    public static int ShortMarginThreshold = 3;
    public static int MinLengthForShortCheck = 5;

    /// <summary>
    /// UA: Відносна різниця довжини перекладу відносно оригіналу у
    ///     відсотках зі знаком: додатне — переклад ДОВШИЙ, від'ємне —
    ///     КОРОТШИЙ (напр. +18.3 — на 18.3% довший, -35 — на 35% коротший).
    ///     0, якщо оригінал порожній. Використовується як числовий ключ
    ///     сортування для колонки "Попередження" (щоб сортувати за
    ///     СЕРЙОЗНІСТЮ, а не за алфавітом тексту попередження).
    /// EN: Signed relative length difference of the translation vs. the
    ///     original, in percent: positive — translation is LONGER,
    ///     negative — SHORTER (e.g. +18.3 — 18.3% longer, -35 — 35%
    ///     shorter). 0 if the original is empty. Used as the numeric sort
    ///     key for the "Warning" column (to sort by SEVERITY, not
    ///     alphabetically by the warning text).
    /// </summary>
    public static double LengthDeltaPercent(string original, string translated)
    {
        if (string.IsNullOrEmpty(original)) return 0;
        return (translated.Length - original.Length) / (double)original.Length * 100.0;
    }

    /// <summary>
    /// UA: Перевіряє переклад відносно оригіналу. Повертає (true, "опис")
    ///     при проблемі, (false, "") якщо все гаразд. Порожній переклад не
    ///     вважається помилкою — для нього є окремий фільтр "Без перекладу".
    /// EN: Validates the translation against the original. Returns
    ///     (true, "description") on an issue, (false, "") if everything is
    ///     fine. An empty translation is not a validation error — there is
    ///     a separate "Untranslated" filter for that.
    /// </summary>
    public static (bool HasIssue, string Description) Check(string original, string translated)
    {
        if (string.IsNullOrEmpty(translated)) return (false, "");

        var issues = new List<string>();

        // UA: Переклад суттєво довший за оригінал — імовірна ознака
        //     майбутньої проблеми в UI гри (обрізаний текст, переповнення
        //     кнопки/поля тощо).
        // EN: Translation is significantly longer than the original — a
        //     likely sign of a future in-game UI problem (truncated text,
        //     overflowing button/field, etc.).
        if (original.Length > 0)
        {
            int longThreshold = (int)Math.Round(original.Length * LengthRatioThreshold + LengthMarginThreshold);
            if (translated.Length > longThreshold)
                issues.Add($"довше / longer: {original.Length}→{translated.Length} (+{translated.Length - original.Length}, {LengthDeltaPercent(original, translated):+0;-0}%)");

            // UA: Переклад суттєво коротший — можлива ознака обрізаного чи
            //     забутого тексту. Лише для оригіналів від
            //     MinLengthForShortCheck символів (див. коментар вище).
            // EN: Translation is significantly shorter — a possible sign of
            //     truncated or forgotten text. Only for originals of at
            //     least MinLengthForShortCheck characters (see comment above).
            if (original.Length >= MinLengthForShortCheck)
            {
                int shortThreshold = (int)Math.Round(original.Length * ShortRatioThreshold - ShortMarginThreshold);
                if (translated.Length < shortThreshold)
                    issues.Add($"коротше / shorter: {original.Length}→{translated.Length} ({translated.Length - original.Length}, {LengthDeltaPercent(original, translated):+0;-0}%)");
            }
        }

        foreach (var tag in BalancedTags)
        {
            int o = original.Count(c => c == tag);
            int t = translated.Count(c => c == tag);
            if (o != t) issues.Add($"'{tag}': {o}→{t}");
        }

        int origPct = original.Count(c => c == '%');
        int transPct = translated.Count(c => c == '%');
        if (origPct != transPct) issues.Add($"%: {origPct}→{transPct}");

        if (CyrillicInVariable.IsMatch(translated))
            issues.Add("кирилиця у змінній / Cyrillic in variable (e.g. %д)");

        int origNl = NewlineEscape.Matches(original).Count;
        int transNl = NewlineEscape.Matches(translated).Count;
        if (origNl != transNl) issues.Add($"\\n: {origNl}→{transNl}");

        return (issues.Count > 0, string.Join("  ·  ", issues));
    }
}

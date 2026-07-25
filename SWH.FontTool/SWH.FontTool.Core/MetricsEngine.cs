// =============================================================================
// SWH.FontTool.Core — MetricsEngine.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Розрахунок базової лінії шрифту з латинських великих літер (A–Z).
//
//     ВИДАЛЕНО (2026-07-24 — переглянуто підхід до зсуву базової лінії відносно
//     символів пунктуації — ручні відсоткові коефіцієнти виявились зайвими):
//     ComputeYOffset/ApplyMetrics/DebugMetrics/ComputeXAdvance та коефіцієнти
//     DescenderRatio(0.65)/DotAboveRatio(0.85) — це були РУЧНІ, захардкоджені
//     припущення "на скільки% висоти гліфа типовий виносний елемент чи
//     крапка-зверху зсуває базову лінію", застосовувані ПО ТИПУ СИМВОЛУ
//     (GlyphRecord.IsDescenderChar/IsDotAboveChar — теж видалені як більше не
//     потрібні). Перевірка показала: ці методи вже НЕ викликаються в активному
//     коді генератора (FontGenerator.cs/AtlasProcessor.cs) — лишались тільки
//     в застарілій копії /files (не частина збірки). Актуальний конвеєр
//     (FontGenerator.PlanAllUaSlots + AtlasProcessor.GenerateUaPng) вимірює
//     ФАКТИЧНЕ положення чорнила растрово для КОЖНОЇ літери окремо (комбо-зонд
//     "н  X") — це строго точніше за фіксовані відсоткові коефіцієнти і працює
//     однаково для будь-якого гліфа (звичайного, з крапкою, з виносом) без
//     спеціальних гілок за типом символу.
//
//     Лишився тільки ComputeBaseline — він і далі потрібен як:
//     (а) BaselineConstant у звітах/логах для людини;
//     (б) запасний варіант (fallback), якщо піксельний замір референс-літери
//         не вдався (напр. немає 'а' в шрифті).
// EN: Computes the font's baseline constant from the Latin uppercase letters
//     (A–Z).
//
//     REMOVED (2026-07-24 — revisited the approach to baseline shift relative
//     to punctuation characters, since the manual percentage
//     coefficients turned out to be unnecessary): ComputeYOffset/ApplyMetrics/DebugMetrics/
//     ComputeXAdvance and the DescenderRatio(0.65)/DotAboveRatio(0.85)
//     coefficients — these were MANUAL, hardcoded assumptions about "what %
//     of glyph height a typical descender or dot-above shifts the baseline
//     by," applied BY CHARACTER TYPE (GlyphRecord.IsDescenderChar/
//     IsDotAboveChar — also removed, no longer needed). A check confirmed
//     these methods are no longer called anywhere in the active generator
//     code (FontGenerator.cs/AtlasProcessor.cs) — they only survived in the
//     stale /files copy (not part of the build). The current pipeline
//     (FontGenerator.PlanAllUaSlots + AtlasProcessor.GenerateUaPng) measures
//     the ACTUAL ink position raster-side for EVERY letter individually (the
//     "н  X" combo probe) — strictly more accurate than fixed percentage
//     ratios, and works identically for any glyph (plain, dotted, or a
//     descender) with no character-type special-casing.
//
//     Only ComputeBaseline remains — it's still needed as:
//     (a) BaselineConstant in reports/logs, for humans to read;
//     (b) a fallback if the reference letter's pixel measurement fails
//         (e.g. no 'а' glyph in the font).
// =============================================================================

namespace SWH.FontTool.Core;

/// <summary>
/// UA: Обчислення базової лінії шрифту. Чиста функція, без стану.
/// EN: Computes the font's baseline. A pure function, with no state.
/// </summary>
public static class MetricsEngine
{
    /// <summary>
    /// UA: Стандартне відношення висоти над базовою лінією (80% висоти гліфа).
    /// Підтверджено для латинських великих A–Z та більшості кириличних.
    /// EN: The standard height-above-baseline ratio (80% of glyph height).
    /// Confirmed for Latin uppercase A–Z and most Cyrillic letters.
    /// </summary>
    public const float BaselineRatio = 0.80f;

    /// <summary>
    /// UA: Обчислює константу базової лінії з латинських великих літер (A–Z).
    ///
    /// Використовує медіану, а не середнє — стійкіше до аномальних гліфів
    /// (деякі символи на кшталт J можуть мати нестандартні метрики).
    ///
    /// ВАЖЛИВО: ця константа є якорем для ВСІХ кириличних гліфів шрифту.
    /// Ніколи не обчислюйте baseline з кириличних символів — це циклічна похибка.
    /// EN: Computes the baseline constant from the Latin uppercase letters (A–Z).
    ///
    /// Uses the median rather than the mean — more robust against anomalous
    /// glyphs (some characters like J can have non-standard metrics).
    ///
    /// IMPORTANT: this constant is the anchor for ALL Cyrillic glyphs in the
    /// font. Never compute the baseline from Cyrillic characters — that's a
    /// circular error.
    /// </summary>
    public static float ComputeBaseline(IEnumerable<GlyphRecord> records)
    {
        var values = records
            .Where(r => r.IsLatinCapital && r.AtlasH > 0)
            .Select(r => r.YOffset + r.AtlasH * BaselineRatio)
            .OrderBy(v => v)
            .ToList();

        if (values.Count == 0)
        {
            Console.WriteLine("[!] MetricsEngine (UA): латинські великі літери не знайдені — baseline = 0");
            Console.WriteLine("[!] MetricsEngine (EN): no Latin uppercase letters found — baseline = 0");
            return 0f;
        }

        return values[values.Count / 2]; // UA: медіана / EN: median
    }
}

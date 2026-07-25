// =============================================================================
// SWH.FontTool.Core — GlyphRecord.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Повна бінарна структура одного гліфа у форматі SteamWorld Heist .fnt.
//     Замінює RawGlyph.cs — додає всі поля, підтверджені реверс-аналізом.
// EN: Full binary structure of a single glyph in the SteamWorld Heist .fnt
//     format. Replaces RawGlyph.cs — adds every field confirmed by
//     reverse-engineering analysis.
// =============================================================================

namespace SWH.FontTool.Core;

/// <summary>
/// UA: Один запис у таблиці гліфів .fnt файлу SteamWorld Heist.
/// Розмір запису (stride) — 28, 32 або 36 байт, і ВИЗНАЧАЄТЬСЯ АВТОМАТИЧНО
/// на файл окремо через FontAnalyzer (див. FontAnalyzer.DetectStride) —
/// не покладайтесь на прив'язку "цей шрифт завжди має такий-то stride".
/// Приклад раніше в цьому коментарі помилково відносив "ingame" до stride=28:
/// це було наслідком бага в FontAnalyzer.FindTableStart (виправлено
/// 2026-07 — див. коментар у тому файлі), через який будь-який справжній
/// 32-байтний шрифт міг хибно розпізнаватись як 36-байтний з padding.
/// Після виправлення авторитетним джерелом stride для кожного шрифту є
/// лише BatchReport/GenerateReport (пункт меню 1/2 у SWH.FontTool.CLI).
///
/// ВАЖЛИВО: XAdvance зберігається як int32, а не float — підтверджено
/// бінарним аналізом (баг v.040).
/// EN: A single record in a SteamWorld Heist .fnt glyph table.
/// Record size (stride) is 28, 32, or 36 bytes, and is DETECTED
/// AUTOMATICALLY per file by FontAnalyzer (see FontAnalyzer.DetectStride) —
/// don't rely on "this font always has this stride" assumptions.
/// An earlier version of this comment incorrectly listed "ingame" under
/// stride=28: that was a side effect of a bug in
/// FontAnalyzer.FindTableStart (fixed 2026-07 — see the comment in that
/// file), which could misdetect any genuine 32-byte font as a 36-byte one
/// with padding. After the fix, the authoritative source for each font's
/// stride is BatchReport/GenerateReport (menu items 1/2 in
/// SWH.FontTool.CLI) — not this comment.
///
/// IMPORTANT: XAdvance is stored as an int32, not a float — confirmed by
/// binary analysis (the v.040 bug).
/// </summary>
public class GlyphRecord
{
    // -----------------------------------------------------------------------
    // Бінарні поля (читаються/записуються в .fnt)
    // -----------------------------------------------------------------------

    /// <summary>UA: [Тільки stride=36] 4-байтовий префікс перед ID. / EN: [stride=36 only] 4-byte prefix before ID.</summary>
    public int Padding { get; set; }

    /// <summary>UA: Unicode codepoint символу (int32 LE). / EN: The character's Unicode codepoint (int32 LE).</summary>
    public int ID { get; set; }

    /// <summary>UA: X-координата в PNG-атласі (float LE). / EN: X coordinate in the PNG atlas (float LE).</summary>
    public float AtlasX { get; set; }

    /// <summary>UA: Y-координата в PNG-атласі (float LE). / EN: Y coordinate in the PNG atlas (float LE).</summary>
    public float AtlasY { get; set; }

    /// <summary>UA: Ширина спрайта в PNG-атласі (float LE). / EN: Sprite width in the PNG atlas (float LE).</summary>
    public float AtlasW { get; set; }

    /// <summary>UA: Висота спрайта в PNG-атласі (float LE). / EN: Sprite height in the PNG atlas (float LE).</summary>
    public float AtlasH { get; set; }

    /// <summary>UA: Горизонтальний зсув від курсора до лівого краю гліфа (float LE). / EN: Horizontal offset from the cursor to the glyph's left edge (float LE).</summary>
    public float XOffset { get; set; }

    /// <summary>
    /// UA: Вертикальний зсув від курсора до верхнього краю гліфа (float LE).
    /// Формула базової лінії: YOffset + AtlasH * 0.80 = baseline_constant
    /// EN: Vertical offset from the cursor to the glyph's top edge (float LE).
    /// Baseline formula: YOffset + AtlasH * 0.80 = baseline_constant
    /// </summary>
    public float YOffset { get; set; }

    /// <summary>
    /// UA: Просування курсора після рендеру гліфа (int32 LE, НЕ float!).
    /// Присутній тільки при stride >= 32.
    /// EN: Cursor advance after rendering the glyph (int32 LE, NOT a float!).
    /// Present only when stride >= 32.
    /// </summary>
    public int XAdvance { get; set; }

    // -----------------------------------------------------------------------
    // UA: Статичні множини для класифікації символів
    // EN: Static sets for character classification
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Символи, наявні в російській, але відсутні в українській абетці.
    /// Це первинні донорські слоти для UA-унікальних символів.
    /// EN: Characters present in Russian but absent from the Ukrainian
    /// alphabet. These are the primary donor slots for UA-unique characters.
    /// </summary>
    public static readonly HashSet<int> RussianOnlyIds = new()
    {
        1025, 1066, 1067, 1069, // Ё Ъ Ы Э (верхній регістр / uppercase)
        1105, 1098, 1099, 1101  // ё ъ ы э (нижній регістр / lowercase)
    };

    // -----------------------------------------------------------------------
    // UA: Властивості-помічники для класифікації
    // EN: Helper properties for classification
    // -----------------------------------------------------------------------

    public bool IsLatinCapital => ID is >= 65 and <= 90;
    public bool IsLatinLower => ID is >= 97 and <= 122;
    public bool IsAsciiPrintable => ID is >= 32 and <= 126;

    /// <summary>UA: Будь-який символ кириличного Unicode-блоку (U+0400–U+045F). / EN: Any character in the Cyrillic Unicode block (U+0400–U+045F).</summary>
    public bool IsCyrillicBlock => ID is >= 1024 and <= 1119;

    // UA: ВИДАЛЕНО (2026-07-24): IsDescenderChar/IsDotAboveChar — використовувались
    //     лише старими MetricsEngine.ComputeYOffset/DebugMetrics (захардкоджені
    //     відсоткові коефіцієнти за типом символу), які теж видалені — активний
    //     генератор тепер вимірює справжнє положення чорнила растрово для
    //     КОЖНОЇ літери окремо (комбо-зонд), без потреби класифікувати символи
    //     за типом. Див. коментар у MetricsEngine.cs.
    // EN: REMOVED (2026-07-24): IsDescenderChar/IsDotAboveChar — were only used
    //     by the old MetricsEngine.ComputeYOffset/DebugMetrics (hardcoded
    //     percentage coefficients by character type), which have also been
    //     removed — the active generator now measures the real ink position
    //     raster-side for EVERY letter individually (the combo probe), with no
    //     need to classify characters by type. See the comment in MetricsEngine.cs.

    public bool IsRussianOnly => RussianOnlyIds.Contains(ID);

    public override string ToString()
    {
        string ch = ID is >= 32 and <= 65535 ? ((char)ID).ToString() : "·";
        return $"ID={ID,5} ({ch}) " +
               $"Atlas[{AtlasX:F0},{AtlasY:F0} {AtlasW:F0}×{AtlasH:F0}] " +
               $"Off[{XOffset:F2},{YOffset:F2}] XAdv={XAdvance}";
    }
}
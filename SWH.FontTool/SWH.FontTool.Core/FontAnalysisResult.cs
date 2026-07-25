// =============================================================================
// SWH.FontTool.Core — FontAnalysisResult.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Результат автоматичного самоаналізу .fnt файлу (без ручних профілів).
// EN: The result of automatic .fnt self-analysis (no manual profiles).
// =============================================================================

namespace SWH.FontTool.Core;

/// <summary>
/// UA: Повний результат бінарного аналізу одного .fnt файлу SteamWorld Heist.
/// Містить усі параметри, необхідні для читання, патчингу та генерації
/// нових файлів.
/// EN: The complete result of binary analysis of a single SteamWorld Heist
/// .fnt file. Contains every parameter needed to read, patch, and generate
/// new files.
/// </summary>
public class FontAnalysisResult
{
    /// <summary>UA: Ім'я шрифту (без розширення). / EN: The font's name (without extension).</summary>
    public string FontName { get; init; } = "";

    /// <summary>UA: Байтове зміщення початку таблиці гліфів у файлі. / EN: Byte offset of the glyph table's start within the file.</summary>
    public int TableStart { get; init; }

    /// <summary>UA: Розмір одного запису гліфа в байтах (28, 32 або 36). / EN: Size of a single glyph record in bytes (28, 32, or 36).</summary>
    public int Stride { get; init; }

    /// <summary>UA: true для файлів із 4-байтовим padding-префіксом: ID знаходиться на +4 від початку запису. / EN: true for files with a 4-byte padding prefix: the ID sits at +4 from the record's start.</summary>
    public bool HasPaddingPrefix { get; init; }

    /// <summary>UA: Кількість гліфів у таблиці (визначена автоматично). / EN: Number of glyphs in the table (auto-detected).</summary>
    public int GlyphCount { get; init; }

    /// <summary>UA: Усі записи гліфів у порядку читання з файлу. / EN: All glyph records, in the order they were read from the file.</summary>
    public List<GlyphRecord> Records { get; init; } = new();

    /// <summary>
    /// UA: Константа базової лінії, обчислена з латинських великих літер (A–Z).
    /// Формула: baseline = YOffset + AtlasH * 0.80
    /// Використовується як якір для розрахунку YOffset кириличних гліфів.
    /// EN: The baseline constant, computed from the Latin uppercase letters
    /// (A–Z). Formula: baseline = YOffset + AtlasH * 0.80. Used as the
    /// anchor for computing the YOffset of Cyrillic glyphs.
    /// </summary>
    public float BaselineConstant { get; init; }

    // -----------------------------------------------------------------------
    // UA: Похідні властивості
    // EN: Derived properties
    // -----------------------------------------------------------------------

    /// <summary>UA: Зміщення поля ID відносно початку запису (0 або 4, якщо є padding-префікс). / EN: Offset of the ID field relative to the record's start (0, or 4 if there's a padding prefix).</summary>
    public int IdOffset => HasPaddingPrefix ? 4 : 0;

    /// <summary>UA: Чи зберігається поле XAdvance (тільки stride >= 32). / EN: Whether the XAdvance field is present (stride >= 32 only).</summary>
    public bool HasXAdvance => Stride >= 32;

    // -----------------------------------------------------------------------
    // UA: Методи запиту
    // EN: Query methods
    // -----------------------------------------------------------------------

    /// <summary>UA: Знаходить запис гліфа за Unicode ID. Повертає null, якщо не знайдено. / EN: Finds a glyph record by Unicode ID. Returns null if not found.</summary>
    public GlyphRecord? GetById(int id) =>
        Records.FirstOrDefault(r => r.ID == id);

    /// <summary>UA: Усі латинські великі літери (A–Z) — джерело для розрахунку базової лінії. / EN: All Latin uppercase letters (A–Z) — the source for computing the baseline.</summary>
    public IEnumerable<GlyphRecord> LatinCapitals =>
        Records.Where(r => r.IsLatinCapital && r.AtlasH > 0);

    /// <summary>UA: Усі кириличні слоти (потенційні донори для UA символів). / EN: All Cyrillic slots (potential donors for UA characters).</summary>
    public IEnumerable<GlyphRecord> CyrillicSlots =>
        Records.Where(r => r.IsCyrillicBlock);

    /// <summary>UA: Слоти, що належать виключно російській абетці (первинні донори UA-унікальних символів). / EN: Slots that belong exclusively to the Russian alphabet (primary donors for UA-unique characters).</summary>
    public IEnumerable<GlyphRecord> RussianOnlySlots =>
        Records.Where(r => r.IsRussianOnly);

    /// <summary>UA: Слоти поза ASCII та кирилицею (резервні донори, якщо кириличних не вистачає). / EN: Slots outside ASCII and Cyrillic (reserve donors, if Cyrillic ones run short).</summary>
    public IEnumerable<GlyphRecord> NonStandardSlots =>
        Records.Where(r => !r.IsAsciiPrintable && !r.IsCyrillicBlock);
}
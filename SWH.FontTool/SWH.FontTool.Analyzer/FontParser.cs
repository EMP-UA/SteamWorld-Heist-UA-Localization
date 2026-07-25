// =============================================================================
// SWH.FontTool.Analyzer — FontParser.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Оновлений парсер: використовує FontAnalyzer для повного зчитування.
//     Зберігає сумісність із FontProfile (для поступового переходу).
// EN: The updated parser: uses FontAnalyzer for the full read. Kept for
//     compatibility with FontProfile (for a gradual transition).
// =============================================================================

using SWH.FontTool.Core;

namespace SWH.FontTool.Analyzer;

/// <summary>
/// UA: Фасад для читання .fnt файлів.
/// Делегує роботу FontAnalyzer — більше ручного профілювання не потрібно.
/// Результати кешуються: кожен файл читається з диску лише один раз.
/// EN: A facade for reading .fnt files.
/// Delegates the work to FontAnalyzer — manual profiling is no longer needed.
/// Results are cached: each file is read from disk only once.
/// </summary>
public static class FontParser
{
    // UA: Кеш: шлях → результат аналізу / EN: Cache: path -> analysis result
    private static readonly Dictionary<string, FontAnalysisResult> Cache = new();

    // -----------------------------------------------------------------------
    // UA: Головний публічний метод
    // EN: Main public method
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Повний аналіз файлу. Результат кешується для повторних звернень.
    /// EN: Full analysis of the file. The result is cached for repeat calls.
    /// </summary>
    public static FontAnalysisResult ParseFull(string filePath)
    {
        if (!Cache.TryGetValue(filePath, out var cached))
        {
            cached = FontAnalyzer.Analyze(filePath);
            Cache[filePath] = cached;
        }
        return cached;
    }

    /// <summary>
    /// UA: Зручний метод: аналіз за директорією та іменем шрифту.
    /// EN: A convenience method: analysis by directory and font name.
    /// </summary>
    public static FontAnalysisResult ParseFull(string fontDir, string fontName) =>
        ParseFull(Config.GetFntPath(fontDir, fontName));

    /// <summary>UA: Очищує кеш (наприклад, після запису нових файлів). / EN: Clears the cache (e.g. after writing new files).</summary>
    public static void ClearCache() => Cache.Clear();
}
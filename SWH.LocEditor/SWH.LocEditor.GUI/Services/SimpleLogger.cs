// =============================================================================
// SWH.LocEditor.GUI — Services/SimpleLogger.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Простий файловий логер для діагностики проблем — той самий підхід, що
//     й у BF1LocalizationTool.GUI/EaWLocalizationTool.GUI (Services/
//     SimpleLogger.cs), яким у SWH.LocEditor раніше просто забули обзавестись.
//     Пише в logs/swhloceditor_YYYYMMDD.log поруч з .exe.
// EN: Simple file logger for troubleshooting — the same approach as in
//     BF1LocalizationTool.GUI/EaWLocalizationTool.GUI (Services/
//     SimpleLogger.cs), which SWH.LocEditor simply hadn't been given yet.
//     Writes to logs/swhloceditor_YYYYMMDD.log next to the .exe.
// =============================================================================

using System.IO;

namespace SWH.LocEditor.GUI.Services;

public static class SimpleLogger
{
    private static readonly string LogDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static string LogPath =>
        Path.Combine(LogDir, $"swhloceditor_{DateTime.Now:yyyyMMdd}.log");

    private static readonly object _lock = new();

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message);
        if (ex is not null)
            Write("STACK", ex.ToString());
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
            lock (_lock)
                File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // UA: логер не має падати сам по собі
            // EN: the logger must not crash itself
        }
    }
}

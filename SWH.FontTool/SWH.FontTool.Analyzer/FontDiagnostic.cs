// =============================================================================
// SWH.FontTool.Analyzer — FontDiagnostic.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Найпростіший hex-дамп заголовка .fnt файлу — швидкий погляд на перші
//     байти без запуску повного FontAnalyzer. Здебільшого замінений
//     FontAnalyzer.GenerateReport (він робить те саме й набагато більше),
//     але лишений як легкий інструмент для першого візуального огляду.
// EN: The simplest hex dump of a .fnt file's header — a quick look at the
//     first bytes without running the full FontAnalyzer. Largely superseded
//     by FontAnalyzer.GenerateReport (which does the same and much more),
//     but kept around as a lightweight tool for a first visual look.
// =============================================================================

using System.Text;

namespace SWH.FontTool.Analyzer;

public static class FontDiagnostic
{
    public static void DumpHeader(string filePath)
    {
        if (!File.Exists(filePath)) return;

        byte[] data = File.ReadAllBytes(filePath);
        int length = Math.Min(data.Length, 80); // UA: Перших 80 байт достатньо / EN: The first 80 bytes are enough

        Console.WriteLine($"\n=== DIAGNOSTIC DUMP: {Path.GetFileName(filePath)} ===");
        Console.WriteLine("Offs | 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F | ASCII");
        Console.WriteLine("-----------------------------------------------------------------------");

        for (int i = 0; i < length; i += 16)
        {
            // UA: Вивід зміщення / EN: Print the offset
            Console.Write($"{i:X4} | ");

            // UA: Вивід HEX / EN: Print the HEX bytes
            for (int j = 0; j < 16; j++)
            {
                if (i + j < length)
                    Console.Write($"{data[i + j]:X2} ");
                else
                    Console.Write("   ");
            }

            Console.Write("| ");

            // UA: Вивід ASCII / EN: Print the ASCII representation
            for (int j = 0; j < 16; j++)
            {
                if (i + j < length)
                {
                    byte b = data[i + j];
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    Console.Write(c);
                }
            }
            Console.WriteLine();
        }
    }
}
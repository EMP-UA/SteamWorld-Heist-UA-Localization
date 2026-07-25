// =============================================================================
// SteamWorld Heist — ImpakRepacker.cs (Smart Packer)
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Частина проєкту українізації SteamWorld Heist. Перепаковує локалізовані
//     файли назад у .impak (DLC-архіви — насправді звичайний ZIP), зберігаючи
//     ПОТОЧНИЙ рівень стиснення кожного файлу з оригінального архіву. Це
//     важливо: якщо перепакувати все з однаковим (наприклад, максимальним)
//     стисненням, рушій гри може не прочитати деякі файли правильно — звідси
//     і "Smart" у назві: рівень стиснення підбирається евристично для
//     КОЖНОГО файлу окремо, за зразком оригіналу.
// EN: Part of the SteamWorld Heist Ukrainian localization project. Repacks
//     localized files back into .impak (DLC archives — actually plain ZIP),
//     preserving the ORIGINAL compression level for each file in the source
//     archive. This matters: repacking everything with one uniform level
//     (e.g. maximum) can make the game engine fail to read some files
//     correctly — hence "Smart" in the name: the compression level is
//     picked heuristically per file, matching the original.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace SteamWorldUA_SmartPacker
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== SteamWorld Heist Smart Packer | by EMP_UA ===");

            // UA: --- НАЛАШТУВАННЯ --- Жодних хардкод-шляхів з локального
            //     диска: відносні теки біля скомпільованого .exe, самі
            //     створюються нижче. Поклади свої файли сюди.
            // EN: --- Configuration Section --- No hardcoded personal paths:
            //     relative folders next to the compiled .exe, auto-created
            //     below. Drop your files in.
            string ogDlcDir = "original-dlc";
            string sourceBaseDir = "localized";
            string outputBaseDir = "output";

            Directory.CreateDirectory(ogDlcDir);
            Directory.CreateDirectory(sourceBaseDir);
            Directory.CreateDirectory(outputBaseDir);

            string[] dlcNames = { "data01", "data02", "data03" };

            foreach (var name in dlcNames)
            {
                string originalImpak = Path.Combine(ogDlcDir, name + ".impak");
                string sourceFolder = Path.Combine(sourceBaseDir, name);
                string outputImpak = Path.Combine(outputBaseDir, name + ".impak");

                if (File.Exists(originalImpak) && Directory.Exists(sourceFolder))
                {
                    ProcessSmartPacking(originalImpak, sourceFolder, outputImpak);
                }
                else
                {
                    Console.WriteLine($"[!] Skipping {name}: original file or source folder not found.");
                    Console.WriteLine($"    Expected: {originalImpak}  and  {sourceFolder}\\");
                }
            }

            Console.WriteLine("\nAll operations completed. Press Enter to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// UA: Аналізує оригінальний .impak і створює новий, використовуючи
        /// для кожного файлу той самий рівень стиснення, що й в оригіналі.
        /// EN: Analyzes the original .impak file and creates a new one using
        /// the same compression level for each file as in the original.
        /// </summary>
        static void ProcessSmartPacking(string originalPath, string sourceDir, string outputPath)
        {
            Console.WriteLine($"\n[>] Analyzing and Packing: {Path.GetFileName(outputPath)}");

            // UA: 1. Аналізуємо методи стиснення оригінального архіву
            // EN: 1. Analyze the original archive's compression methods
            var compressionMap = new Dictionary<string, CompressionLevel>(StringComparer.OrdinalIgnoreCase);

            using (ZipArchive archive = ZipFile.OpenRead(originalPath))
            {
                foreach (var entry in archive.Entries)
                {
                    // UA: Евристика: якщо стиснутий розмір дорівнює
                    //     оригінальному — це, найімовірніше, NoCompression (Store)
                    // EN: Heuristic check: if compressed size equals original
                    //     size, it's likely NoCompression (Store)
                    if (entry.CompressedLength == entry.Length)
                        compressionMap[entry.FullName] = CompressionLevel.NoCompression;
                    else
                        compressionMap[entry.FullName] = CompressionLevel.Optimal;
                }
            }

            // UA: 2. Створюємо новий архів із синхронізованими рівнями стиснення
            // EN: 2. Create the new archive with synchronized compression levels
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (ZipArchive newArchive = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // UA: Відносний шлях для структури архіву
                    // EN: Get relative path for the archive structure
                    string relativePath = file.Substring(sourceDir.Length + 1).Replace('\\', '/');

                    CompressionLevel levelToUse = CompressionLevel.Optimal;
                    if (compressionMap.ContainsKey(relativePath))
                    {
                        levelToUse = compressionMap[relativePath];
                    }

                    newArchive.CreateEntryFromFile(file, relativePath, levelToUse);
                    Console.WriteLine($"   [+] {relativePath} (Compression: {levelToUse})");
                }
            }
            Console.WriteLine($"[OK] File saved: {Path.GetFileName(outputPath)}");
        }
    }
}

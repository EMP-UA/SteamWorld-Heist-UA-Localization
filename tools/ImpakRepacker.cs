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
        // UA: У самій грі кожен DLC лежить у своїй теці (.../DLC/dlc01/,
        //     dlc02/, dlc03/), а архів усередині ЗАВЖДИ називається
        //     однаково — data01.impak, незалежно від номера DLC. Тому
        //     теки тут дзеркалять структуру гри 1:1: original-dlc/dlcNN/,
        //     localized/dlcNN/, output/dlcNN/ — і готовий output/ можна
        //     скопіювати поверх теки DLC/ гри без жодного перейменування.
        // EN: In the game itself, each DLC lives in its own folder
        //     (.../DLC/dlc01/, dlc02/, dlc03/), but the archive inside is
        //     ALWAYS named the same — data01.impak, regardless of the DLC
        //     number. So the folders here mirror the game's layout 1:1:
        //     original-dlc/dlcNN/, localized/dlcNN/, output/dlcNN/ — the
        //     finished output/ can be copied straight over the game's
        //     DLC/ folder with zero manual renaming.
        private const string ImpakFileName = "data01.impak";
        private static readonly string[] DlcFolders = { "dlc01", "dlc02", "dlc03" };

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

            // UA: Меню вибору замість автоматичного пакування всього при
            //     старті — так само, як у TextValidator: явний вибір, що
            //     саме пакувати зараз, і статус готовності кожного DLC видно
            //     одразу в списку.
            // EN: A selection menu instead of auto-packing everything on
            //     startup — same pattern as TextValidator: an explicit
            //     choice of what to pack right now, with each DLC's
            //     readiness status visible right in the list.
            while (true)
            {
                Console.WriteLine("\n--- МЕНЮ ВИБОРУ DLC / DLC SELECTION MENU ---");
                for (int i = 0; i < DlcFolders.Length; i++)
                {
                    string originalImpak = Path.Combine(ogDlcDir, DlcFolders[i], ImpakFileName);
                    string sourceFolder = Path.Combine(sourceBaseDir, DlcFolders[i]);
                    bool ready = File.Exists(originalImpak) && Directory.Exists(sourceFolder);
                    string status = ready ? "[OK]" : "[! немає файлів / missing files]";
                    Console.WriteLine($"{i + 1}. {DlcFolders[i]}  {status}");
                }
                Console.WriteLine("A. Обробити ВСІ DLC / Process ALL DLC");
                Console.WriteLine("0. Вихід / Exit");
                Console.Write("Виберіть номер / Select a number: ");

                string? choice = Console.ReadLine()?.Trim().ToUpperInvariant();

                if (choice == "0") break;

                if (choice == "A")
                {
                    foreach (var dlcFolder in DlcFolders)
                        TryProcessDlc(dlcFolder, ogDlcDir, sourceBaseDir, outputBaseDir);
                }
                else if (int.TryParse(choice, out int idx) && idx > 0 && idx <= DlcFolders.Length)
                {
                    // UA: Пакуємо лише один вибраний DLC
                    // EN: Pack only the selected DLC
                    TryProcessDlc(DlcFolders[idx - 1], ogDlcDir, sourceBaseDir, outputBaseDir);
                }
                else
                {
                    Console.WriteLine("Невірний вибір. Спробуйте ще раз. / Invalid choice. Try again.");
                    continue;
                }

                Console.WriteLine("\nОперацію завершено. Натисніть Enter... / Operation completed. Press Enter...");
                Console.ReadLine();
            }
        }

        // UA: Перевіряє наявність оригіналу й теки з локалізованими файлами
        //     для одного DLC та, якщо все на місці, запускає пакування.
        // EN: Checks that the original archive and the localized-files
        //     folder exist for one DLC and, if so, runs the packing.
        static void TryProcessDlc(string dlcFolder, string ogDlcDir, string sourceBaseDir, string outputBaseDir)
        {
            string originalImpak = Path.Combine(ogDlcDir, dlcFolder, ImpakFileName);
            string sourceFolder = Path.Combine(sourceBaseDir, dlcFolder);
            string outputImpak = Path.Combine(outputBaseDir, dlcFolder, ImpakFileName);

            if (File.Exists(originalImpak) && Directory.Exists(sourceFolder))
            {
                Directory.CreateDirectory(Path.Combine(outputBaseDir, dlcFolder));
                ProcessSmartPacking(originalImpak, sourceFolder, outputImpak);
            }
            else
            {
                Console.WriteLine($"[!] Пропущено {dlcFolder}: не знайдено оригінал або джерело. / Skipped {dlcFolder}: original file or source folder not found.");
                Console.WriteLine($"    Очікується / Expected: {originalImpak}  та/and  {sourceFolder}\\");
            }
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

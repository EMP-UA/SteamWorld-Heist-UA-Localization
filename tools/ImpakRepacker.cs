using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

/* * SteamWorld Heist Smart Packer
 * Developed by EMP_UA (Yevhenii)
 * * This tool is part of the Ukrainian Localization project for SteamWorld Heist.
 * It ensures that repacked .impak (DLC) files maintain the original compression 
 * levels for each file to prevent game crashes and visual artifacts.
 */

namespace SteamWorldUA_SmartPacker
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== SteamWorld Heist Smart Packer | by EMP_UA ===");

            // --- Configuration Section ---
            // Replace these placeholders with your actual local paths
            string ogDlcDir = @"C:\Path\To\Original\Game\dlc";
            string sourceBaseDir = @"C:\Path\To\Your\Localized\Folders";
            string outputBaseDir = @"C:\Path\To\Output\Pack";

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
                }
            }

            Console.WriteLine("\nAll operations completed. Press Enter to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// Analyzes the original .impak file and creates a new one using the same compression levels.
        /// </summary>
        static void ProcessSmartPacking(string originalPath, string sourceDir, string outputPath)
        {
            Console.WriteLine($"\n[>] Analyzing and Packing: {Path.GetFileName(outputPath)}");

            // 1. Analyze the original archive's compression methods
            var compressionMap = new Dictionary<string, CompressionLevel>(StringComparer.OrdinalIgnoreCase);

            using (ZipArchive archive = ZipFile.OpenRead(originalPath))
            {
                foreach (var entry in archive.Entries)
                {
                    // Heuristic check: if compressed size equals original size, it's likely NoCompression (Store)
                    if (entry.CompressedLength == entry.Length)
                        compressionMap[entry.FullName] = CompressionLevel.NoCompression;
                    else
                        compressionMap[entry.FullName] = CompressionLevel.Optimal;
                }
            }

            // 2. Create the new archive with synchronized compression levels
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (ZipArchive newArchive = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Get relative path for the archive structure
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/* * SteamWorld Heist Localization & Validation Tool
 * Developed by EMP_UA (Yevhenii)
 * * This tool manages the merging of reviewed translations (TSV) back into 
 * the game's CSV format while performing critical technical validations.
 * * Key Features:
 * - Syncs row counts between original and translated files.
 * - Validates technical tags and placeholders (<>, [], {}, %).
 * - Detects "Cyrillic variables" (e.g., %д instead of %d) which cause game crashes.
 * - Verifies newline (\n) consistency.
 */

namespace SteamWorldUA_MasterTool_v50
{
    class TranslationTask
    {
        public string Name { get; set; }
        public string OriginalFile { get; set; }
        public string ReviewFile { get; set; }
        public string OutputFile { get; set; }
    }

    class ReviewData
    {
        public string UkrainianTranslation { get; set; }
        public string CheckedStatus { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== SteamWorld Heist Localization Tool | by EMP_UA ===");

            // --- CONFIGURATION SECTION ---
            // Set your local paths here
            string baseOgPath = @"C:\Path\To\Original\CSV";
            string baseReviewPath = @"C:\Path\To\Your\Translations\TSV";
            string baseOutputPath = @"C:\Path\To\Output\Pack";

            if (!Directory.Exists(baseOutputPath)) Directory.CreateDirectory(baseOutputPath);

            var tasks = new List<TranslationTask>
            {
                new TranslationTask {
                    Name = "Main Game",
                    OriginalFile = Path.Combine(baseOgPath, "en.csv"),
                    ReviewFile = Path.Combine(baseReviewPath, "Main_Game_Review.tsv"),
                    OutputFile = Path.Combine(baseOutputPath, "en.csv")
                },
                new TranslationTask {
                    Name = "DLC 01",
                    OriginalFile = Path.Combine(baseOgPath, "en_dlc01.csv"),
                    ReviewFile = Path.Combine(baseReviewPath, "DLC01_Review.tsv"),
                    OutputFile = Path.Combine(baseOutputPath, "en_dlc01.csv")
                }
                // Add more DLC tasks as needed...
            };

            foreach (var task in tasks)
            {
                ProcessTask(task);
            }

            Console.WriteLine("\nAll tasks completed. Press Enter to exit...");
            Console.ReadLine();
        }

        static void ProcessTask(TranslationTask task)
        {
            Console.WriteLine($"\n--- PROCESSING: {task.Name} ---");

            if (!File.Exists(task.OriginalFile) || !File.Exists(task.ReviewFile))
            {
                Console.WriteLine($"[!] SKIPPED: Required files not found for {task.Name}");
                return;
            }

            // 1. Load Review Data (TSV from Google Sheets/Notepad++)
            var tableData = new Dictionary<string, ReviewData>(StringComparer.OrdinalIgnoreCase);
            var reviewLines = File.ReadAllLines(task.ReviewFile, Encoding.UTF8);

            for (int i = 1; i < reviewLines.Length; i++)
            {
                var cols = reviewLines[i].Split('\t');
                if (cols.Length >= 3)
                {
                    string keyId = cols[0].Trim();
                    if (string.IsNullOrEmpty(keyId)) continue;

                    tableData[keyId] = new ReviewData
                    {
                        UkrainianTranslation = cols[2],
                        CheckedStatus = cols.Length > 4 ? cols[4] : ""
                    };
                }
            }

            // 2. Generation & Validation
            int totalRowsOriginal = 0;
            int totalRowsOutput = 0;
            int errorsFound = 0;
            int mergedCount = 0;

            var originalLines = File.ReadAllLines(task.OriginalFile, Encoding.UTF8);
            totalRowsOriginal = originalLines.Length;

            using (StreamWriter writer = new StreamWriter(task.OutputFile, false, new UTF8Encoding(false)))
            {
                foreach (var line in originalLines)
                {
                    totalRowsOutput++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        writer.WriteLine(line);
                        continue;
                    }

                    var cols = line.Split('\t');
                    if (cols.Length < 2)
                    {
                        writer.WriteLine(line);
                        continue;
                    }

                    string keyId = cols[0].Trim();
                    string originalEng = cols[1];
                    string finalTranslation = originalEng;

                    if (tableData.ContainsKey(keyId))
                    {
                        finalTranslation = tableData[keyId].UkrainianTranslation;
                        mergedCount++;

                        // Critical Technical Validation
                        errorsFound += ValidateString(keyId, originalEng, finalTranslation);

                        // Auto-fix period inconsistencies (optional)
                        if (!originalEng.Trim().EndsWith(".") && finalTranslation.Trim().EndsWith("."))
                        {
                            finalTranslation = finalTranslation.TrimEnd('.');
                        }
                    }

                    cols[1] = finalTranslation;
                    writer.WriteLine(string.Join("\t", cols));
                }
            }

            // 3. File Summary
            Console.WriteLine($"[+] Result: {task.Name}");
            Console.WriteLine($"    Original Rows: {totalRowsOriginal} | Output Rows: {totalRowsOutput}");
            if (totalRowsOriginal != totalRowsOutput)
                Console.WriteLine($"    [WARNING] ROW COUNT MISMATCH!");

            Console.WriteLine($"    Translations Merged: {mergedCount}");

            if (errorsFound > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    Validation Errors Found: {errorsFound}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"    Validation Clean: No technical errors found.");
            }
            Console.ResetColor();
        }

        static int ValidateString(string id, string eng, string ukr)
        {
            int errors = 0;

            // Check for Tag/Bracket Mismatch
            char[] tags = { '<', '>', '[', ']', '{', '}' };
            foreach (var tag in tags)
            {
                if (eng.Count(f => f == tag) != ukr.Count(f => f == tag))
                {
                    LogValidationError(id, $"Tag/Bracket mismatch '{tag}'", eng, ukr);
                    errors++;
                }
            }

            // Check for Percent Placeholder Mismatch (%)
            if (eng.Count(f => f == '%') != ukr.Count(f => f == '%'))
            {
                LogValidationError(id, "Placeholder '%' count mismatch", eng, ukr);
                errors++;
            }

            // Detect Cyrillic characters in variables (e.g., %д instead of %d)
            if (Regex.IsMatch(ukr, @"%[а-яА-ЯіІїЇєЄґҐ]"))
            {
                LogValidationError(id, "Cyrillic character in variable (Potential Crash)", eng, ukr);
                errors++;
            }

            // Verify Newline character (\n) consistency
            int engNewLines = Regex.Matches(eng, @"\\n").Count;
            int ukrNewLines = Regex.Matches(ukr, @"\\n").Count;
            if (engNewLines != ukrNewLines)
            {
                LogValidationError(id, $"Newline \\n mismatch (Orig: {engNewLines}, UA: {ukrNewLines})", eng, ukr);
                errors++;
            }

            return errors;
        }

        static void LogValidationError(string id, string message, string eng, string ukr)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"    [VALIDATION ERROR] ID: {id} | {message}");
            Console.WriteLine($"      ORIG: {eng}");
            Console.WriteLine($"      TRANSLATION: {ukr}");
            Console.ResetColor();
        }
    }
}

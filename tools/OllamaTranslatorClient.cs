// =============================================================================
// SteamWorld Heist — OllamaTranslatorClient.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Клієнт для першого чорнового проходу перекладу через локальну модель
//     Ollama ('translategemma:12b'). Це не заміна ручного редагування —
//     чорновий переклад завжди проходить окрему ревізію (звідси REVIEW_*.tsv
//     поруч з основним виводом).
//     Ключові технічні риси:
//     - Керування станом потоку, щоб Windows не заснула під час довгого
//       пакетного завдання.
//     - Захист тегів рушія гри регулярними виразами (щоб модель не
//       пошкодила {теги}/<теги>/\n/[теги] під час перекладу).
//     - Пост-обробка: чистка "галюцинацій" моделі та відновлення регістру.
//     - Автоматична QA-перевірка: кириличні літери, властиві лише
//       російській мові (ы, э, ъ, ё), явна різниця довжини, забагато
//       латиниці в результаті.
// EN: A client for the first draft translation pass via a local Ollama
//     instance ('translategemma:12b'). This does not replace manual
//     editing — the draft translation always goes through a separate
//     review pass (hence REVIEW_*.tsv next to the main output).
//     Key technical features:
//     - Thread state management to prevent system sleep during long batch
//       jobs.
//     - Regex-based tag protection (prevents the model from corrupting game
//       engine tags — {tags}/<tags>/\n/[tags] — during translation).
//     - Post-processing: hallucination cleanup and case restoration.
//     - Automated QA checks: letters unique to Russian (ы, э, ъ, ё), length
//       mismatches, too much Latin script in the result.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text.Json;

namespace SteamWorldUA_AITranslator
{
    class Program
    {
        // UA: Не даємо Windows заснути під час довгого перекладу.
        // EN: Prevent system from sleeping during long translation tasks.
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;

        static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);

            Console.WriteLine("Checking Ollama connection (model: translategemma:12b)...");
            try
            {
                await httpClient.GetAsync("http://localhost:11434/");
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[!] ERROR: Ollama is not running. Please start the service.");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            // UA: --- НАЛАШТУВАННЯ --- Жодних хардкод-шляхів з локального
            //     диска: відносні теки біля скомпільованого .exe, самі
            //     створюються нижче. Поклади вихідні CSV у "original-csv" і
            //     запусти.
            // EN: --- CONFIGURATION SECTION --- No hardcoded personal paths:
            //     relative folders next to the compiled .exe, auto-created
            //     below. Drop your source CSVs into "original-csv" and run.
            string inDir = "original-csv";
            string outDir = "output";
            string[] dlcFiles = { "en_dlc01.csv", "en_dlc02.csv", "en_dlc03.csv" };

            Directory.CreateDirectory(inDir);
            Directory.CreateDirectory(outDir);

            foreach (var file in dlcFiles)
            {
                await ProcessFileAsync(inDir, outDir, file);
            }

            SetThreadExecutionState(ES_CONTINUOUS);
            Console.WriteLine("\n=== COMPLETED! ===\nAll translation tasks finished successfully.");
            Console.ReadLine();
        }

        /// <summary>
        /// UA: Обробляє один CSV-файл рядок за рядком: захищає теги,
        /// перекладає через Ollama, відновлює теги й регістр, рахує
        /// QA-прапорці, пише основний вивід + окремий файл для ревізії.
        /// Підтримує продовження з місця зупинки (startLine рахується з
        /// уже наявного outputPath).
        /// EN: Processes one CSV file line by line: protects tags,
        /// translates via Ollama, restores tags and case, computes QA
        /// flags, writes the main output plus a separate review file.
        /// Supports resuming from where it left off (startLine is derived
        /// from the existing outputPath).
        /// </summary>
        static async Task ProcessFileAsync(string inDir, string outDir, string file)
        {
            string inputPath = Path.Combine(inDir, file);
            if (!File.Exists(inputPath)) return;

            string outputPath = Path.Combine(outDir, file);
            string reviewPath = Path.Combine(outDir, $"REVIEW_en_uk_{Path.GetFileNameWithoutExtension(file)}.tsv");
            string logPath = Path.Combine(outDir, $"log_{Path.GetFileNameWithoutExtension(file)}.txt");

            var allLines = File.ReadAllLines(inputPath, Encoding.UTF8);
            int totalLines = allLines.Length;

            int startLine = 0;
            if (File.Exists(outputPath))
            {
                startLine = File.ReadAllLines(outputPath, Encoding.UTF8).Length;
            }

            using (StreamWriter sw = new StreamWriter(outputPath, true, Encoding.UTF8))
            using (StreamWriter swReview = new StreamWriter(reviewPath, true, Encoding.UTF8))
            using (StreamWriter log = new StreamWriter(logPath, true, Encoding.UTF8))
            {
                if (startLine == 0) swReview.WriteLine("ID\tOriginal English\tUkrainian Translation\tDeveloper Comments\tQA Flags");

                for (int i = startLine; i < totalLines; i++)
                {
                    string line = allLines[i];
                    var parts = line.Split('\t');

                    if (parts.Length < 2)
                    {
                        sw.WriteLine(line);
                        continue;
                    }

                    string id = parts[0];
                    string englishText = parts[1];
                    string comment = parts.Length > 2 ? parts[2] : "";

                    if (string.IsNullOrWhiteSpace(englishText))
                    {
                        sw.WriteLine(line);
                        swReview.WriteLine($"{id}\t \t \t{comment}\t");
                        continue;
                    }

                    try
                    {
                        // UA: 1. Захист тегів (кольори, теги гри, переноси рядків)
                        // EN: 1. Tag Protection (Colors, Game Tags, Newlines)
                        var tags = new List<string>();
                        string protectedText = Regex.Replace(englishText, @"(\{.*?\}|<.*?>|\\n|\[.*?\])", m => {
                            tags.Add(m.Value);
                            return $"[[{tags.Count - 1}]]";
                        });

                        // UA: 2. Переклад через AI
                        // EN: 2. AI Translation
                        string translatedText = await SafeTranslateAsync(protectedText, comment, log);
                        translatedText = CleanHallucinations(translatedText);
                        translatedText = RestoreCase(englishText, translatedText);

                        // UA: Прибираємо крапку в кінці, якщо в оригіналі її не було
                        // EN: Fix trailing periods if original didn't have them
                        if (!englishText.Trim().EndsWith(".") && translatedText.EndsWith("."))
                        {
                            translatedText = translatedText.TrimEnd('.');
                        }

                        // UA: 3. Повертаємо теги на місце
                        // EN: 3. Restore Tags
                        for (int t = 0; t < tags.Count; t++)
                        {
                            translatedText = Regex.Replace(translatedText, $@"\[\[\s?{t}\s?\]\]", tags[t]);
                        }

                        // UA: 4. Автоматичні QA-перевірки
                        // EN: 4. Automated QA Controls
                        string qaFlags = "";
                        if (Regex.IsMatch(translatedText, "[ыэъёЫЭЪЁ]")) qaFlags += "[RUS] ";
                        if (englishText.Length > 5 && translatedText.Length > englishText.Length * 2.5) qaFlags += "[LEN] ";

                        int latinCount = translatedText.Count(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
                        if (translatedText.Length > 0 && (double)latinCount / translatedText.Length > 0.4) qaFlags += "[LAT] ";

                        sw.WriteLine($"{id}\t{translatedText}\t{comment}");
                        swReview.WriteLine($"{id}\t{englishText}\t{translatedText}\t{comment}\t{qaFlags.Trim()}");

                        sw.Flush();
                        swReview.Flush();

                        // UA: Прогрес у консоль (скорочено)
                        // EN: Progress logging (Shortened for brevity)
                        Console.WriteLine($"Progress: {i + 1}/{totalLines} ({(double)(i + 1) / totalLines * 100:F1}%) | ID: {id}");
                    }
                    catch (Exception ex)
                    {
                        log.WriteLine($"{DateTime.Now}: Error at {id}: {ex.Message}");
                        sw.WriteLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// UA: Викликає локальний Ollama API. У разі будь-якої помилки
        /// (сервер недоступний, невдалий парсинг) повертає оригінальний
        /// англійський текст без змін — рядок ніколи не втрачається.
        /// EN: Calls the local Ollama API. On any failure (server
        /// unreachable, parsing error) returns the original English text
        /// unchanged — a line is never lost.
        /// </summary>
        static async Task<string> SafeTranslateAsync(string text, string context, StreamWriter log)
        {
            var requestBody = new
            {
                model = "translategemma:12b",
                prompt = $"You are a strict game localization system. Translate to Ukrainian.\n" +
                         $"Context: {context}\n" +
                         $"Text to translate: {text}\n\n" +
                         $"RULES: Output ONLY the translation. No notes. No Russian letters.\n" +
                         $"Translation:",
                stream = false,
                options = new { temperature = 0.05, num_thread = 6 }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
                if (!response.IsSuccessStatusCode) return text;

                string responseString = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(responseString))
                {
                    string result = doc.RootElement.GetProperty("response").GetString();
                    return result.Trim().Replace("\n", " ").Replace("\r", "");
                }
            }
            catch { return text; }
        }

        /// <summary>
        /// UA: Прибирає типові вступні фрази-"галюцинації" моделі
        /// ("переклад:", "ось переклад:" тощо) та зайві лапки.
        /// EN: Strips typical model "hallucination" preambles
        /// ("translation:", "here is the translation:" etc.) and stray quotes.
        /// </summary>
        static string CleanHallucinations(string text)
        {
            string[] prefixes = { "переклад:", "translation:", "ось переклад:", "here is the translation:" };
            foreach (var prefix in prefixes)
            {
                if (text.ToLower().StartsWith(prefix)) text = text.Substring(prefix.Length).Trim();
            }
            return text.Trim('\"');
        }

        /// <summary>
        /// UA: Відновлює регістр перекладу за зразком оригіналу
        /// (ВСЕ ВЕЛИКИМИ / Перша велика).
        /// EN: Restores the translation's case to match the original
        /// (ALL CAPS / First letter capitalized).
        /// </summary>
        static string RestoreCase(string original, string translated)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translated)) return translated;
            if (original.Length > 1 && original.All(c => !char.IsLetter(c) || char.IsUpper(c))) return translated.ToUpper();
            if (char.IsUpper(original[0])) return char.ToUpper(translated[0]) + (translated.Length > 1 ? translated.Substring(1) : "");
            return translated;
        }
    }
}

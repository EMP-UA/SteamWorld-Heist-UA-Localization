// =============================================================================
// SteamWorld Heist — Ukrainian Localization Toolset
// TextValidator.cs — Translation Merge & Technical QA Tool
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Зливає перевірені переклади (TSV з Google Таблиць / Excel) назад у
//     формат гри (CSV) і водночас виконує технічну валідацію (щоб рушій не
//     впав через биту змінну чи тег). Усі шляхи — у config.json поруч із
//     .exe, ЖОДНОГО хардкоду особистих шляхів у коді: якщо конфігу немає,
//     створюється дефолтний із ВІДНОСНИМИ теками (original/, review/,
//     output/) поруч із .exe — так само, як original-fonts/ttf-fonts у
//     SWH.FontTool. Просто клонуй, збери, кинь файли у ці теки — працює без
//     жодної правки коду.
// EN: Merges reviewed translations (TSV from Google Sheets / Excel) back
//     into the game's CSV format while performing technical validation (so
//     the engine doesn't crash on a broken variable or tag). All paths live
//     in config.json next to the .exe — NO hardcoded personal paths in the
//     code: if the config doesn't exist, a default one is created with
//     RELATIVE folders (original/, review/, output/) next to the .exe —
//     the same convention as original-fonts/ttf-fonts in SWH.FontTool. Just
//     clone, build, drop your files into those folders — works with zero
//     code changes.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SteamWorldUA_TextValidator;

#region Models & Config / Моделі та Конфігурація

/// <summary>UA: Налаштування у config.json — усі шляхи відносні до .exe за замовчуванням. / EN: Settings in config.json — all paths relative to the .exe by default.</summary>
public class AppConfig
{
    public string BaseOgPath { get; set; } = "";
    public string BaseReviewPath { get; set; } = "";
    public string BaseOutputPath { get; set; } = "";
    public List<TranslationTask> Tasks { get; set; } = new();
}

public class TranslationTask
{
    public string Name { get; set; } = "";
    public string OriginalFile { get; set; } = "";
    public string ReviewFile { get; set; } = "";
    public string OutputFile { get; set; } = "";
}

public class ReviewData
{
    public string UkrainianTranslation { get; set; } = "";
    public string CheckedStatus { get; set; } = "";
}

#endregion

#region Localization / Локалізація

public enum AppLanguage { Ukrainian, English }

/// <summary>UA: Керування мовою інтерфейсу консолі. / EN: Manages the console UI language.</summary>
public static class Loc
{
    public static AppLanguage CurrentLang { get; set; } = AppLanguage.Ukrainian;

    private static readonly Dictionary<string, (string Ua, string En)> Strings = new()
    {
        { "Title", ("=== SteamWorld Heist Localization Tool v0.50 | by EMP_UA ===", "=== SteamWorld Heist Localization Tool v0.50 | by EMP_UA ===") },
        { "ConfigCreated", ("Створено стандартний config.json (з відносними теками original/review/output поруч із .exe). Поклади файли туди й перезапусти програму.", "Default config.json created (with relative original/review/output folders next to the .exe). Drop your files there and restart the app.") },
        { "ConfigLoaded", ("Конфігурацію завантажено.", "Configuration loaded.") },
        { "MissingInputs", ("[!] Увага: деякі вхідні файли з config.json ще не знайдено — перевір теки original/ та review/.", "[!] Warning: some input files from config.json were not found yet — check the original/ and review/ folders.") },
        { "MenuHeader", ("\n--- МЕНЮ ВИБОРУ ЗАВДАНЬ ---", "\n--- TASK SELECTION MENU ---") },
        { "MenuAll", ("Обробити ВСІ файли", "Process ALL files") },
        { "MenuExit", ("Вихід", "Exit") },
        { "MenuPrompt", ("Виберіть номер завдання: ", "Select a task number: ") },
        { "InvalidChoice", ("Невірний вибір. Спробуйте ще раз.", "Invalid choice. Try again.") },
        { "Processing", ("\n--- ОБРОБКА: {0} ---", "\n--- PROCESSING: {0} ---") },
        { "Skipped", ("[!] ПРОПУЩЕНО: Не знайдено файли для {0}", "[!] SKIPPED: Files not found for {0}") },
        { "Done", ("[+] Готово: {0}", "[+] Done: {0}") },
        { "RowsInfo", (" Рядків в оригіналі: {0} | В результаті: {1}", " Original rows: {0} | Output rows: {1}") },
        { "RowWarning", (" [УВАГА] Кількість рядків не збігається!", " [WARNING] Row count mismatch!") },
        { "Integrated", (" Інтегровано перекладів: {0}", " Translations integrated: {0}") },
        { "Errors", (" Помилок валідації: {0}", " Validation errors: {0}") },
        { "MissingKeysHeader", ("\n [ПРОПУЩЕНО В REVIEW] {0}: знайдено {1} ключів без перекладу (з {2} рядків оригіналу):", "\n [MISSING FROM REVIEW] {0}: found {1} keys without a review translation (out of {2} original rows):") },
        { "MissingKeysMore", ("    ... і ще {0} (повний список — у файлі)", "    ... and {0} more (full list in the report file)") },
        { "MissingKeysSaved", (" [+] Список пропущених ключів збережено: {0}", " [+] Missing-keys list saved to: {0}") },
        { "MissingKeysNone", (" [OK] Усі ключі оригіналу знайдено в review.", " [OK] All original keys were found in the review file.") },
        { "PressEnter", ("\nОперацію завершено. Натисніть Enter...", "\nOperation completed. Press Enter...") }
    };

    public static string Get(string key, params object[] args)
    {
        if (!Strings.TryGetValue(key, out var values)) return key;
        string text = CurrentLang == AppLanguage.Ukrainian ? values.Ua : values.En;
        return args.Length > 0 ? string.Format(text, args) : text;
    }
}

#endregion

class Program
{
    private const string ConfigFileName = "config.json";
    private const string ReportsDirName = "reports";

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // UA: Вибір мови інтерфейсу
        // EN: UI language selection
        Console.WriteLine("Select language / Виберіть мову:");
        Console.WriteLine("1. Українська");
        Console.WriteLine("2. English");
        Console.Write("> ");
        if (Console.ReadLine()?.Trim() == "2")
            Loc.CurrentLang = AppLanguage.English;

        Console.WriteLine(Loc.Get("Title"));

        // UA: Завантаження або створення конфігу (жодного хардкоду шляхів —
        //     див. LoadOrCreateConfig).
        // EN: Loading or creating the config (no hardcoded paths at all —
        //     see LoadOrCreateConfig).
        AppConfig? config = LoadOrCreateConfig();
        if (config == null) return;

        Console.WriteLine(Loc.Get("ConfigLoaded"));

        // UA: Створюємо вхідні й вихідну теки, якщо їх ще нема — так само,
        //     як original-fonts/ttf-fonts/output у SWH.FontTool: порожні
        //     теки самі підказують користувачу, куди класти файли.
        // EN: Create the input and output folders if they don't exist yet —
        //     the same as original-fonts/ttf-fonts/output in SWH.FontTool:
        //     empty folders hint the user where files belong.
        Directory.CreateDirectory(config.BaseOgPath);
        Directory.CreateDirectory(config.BaseReviewPath);
        Directory.CreateDirectory(config.BaseOutputPath);

        if (config.Tasks.Any(t => !File.Exists(t.OriginalFile) || !File.Exists(t.ReviewFile)))
            Console.WriteLine(Loc.Get("MissingInputs"));

        // UA: Головний цикл меню
        // EN: Main menu loop
        while (true)
        {
            Console.WriteLine(Loc.Get("MenuHeader"));
            for (int i = 0; i < config.Tasks.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {config.Tasks[i].Name}");
            }
            Console.WriteLine($"A. {Loc.Get("MenuAll")}");
            Console.WriteLine($"0. {Loc.Get("MenuExit")}");
            Console.Write(Loc.Get("MenuPrompt"));

            string? choice = Console.ReadLine()?.Trim().ToUpper();

            if (choice == "0") break;

            if (choice == "A")
            {
                foreach (var task in config.Tasks) ProcessTask(task);
            }
            else if (int.TryParse(choice, out int taskIndex) && taskIndex > 0 && taskIndex <= config.Tasks.Count)
            {
                // UA: Обробка лише одного вибраного завдання
                // EN: Process only the selected task
                ProcessTask(config.Tasks[taskIndex - 1]);
            }
            else
            {
                Console.WriteLine(Loc.Get("InvalidChoice"));
                continue;
            }

            Console.WriteLine(Loc.Get("PressEnter"));
            Console.ReadLine();
        }
    }

    // UA: Завантажує config.json, якщо він є, або створює дефолтний.
    //     ВАЖЛИВО: дефолтні шляхи — ВІДНОСНІ теки поруч із .exe (original/,
    //     review/, output/), а не чийсь особистий диск. Це те, що робить
    //     безпечним публікувати інструмент у публічному репо — після клону
    //     й збірки він одразу працює для БУДЬ-КОГО, досить створити ці теки
    //     (чи дати програмі створити їх самій) і покласти туди свої файли.
    // EN: Loads config.json if present, or creates a default one.
    //     IMPORTANT: the default paths are RELATIVE folders next to the
    //     .exe (original/, review/, output/), not anyone's personal disk.
    //     This is what makes it safe to publish in a public repo — after
    //     cloning and building it works for ANYONE right away: just create
    //     these folders (or let the app create them) and drop your files in.
    static AppConfig? LoadOrCreateConfig()
    {
        if (File.Exists(ConfigFileName))
        {
            string json = File.ReadAllText(ConfigFileName, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }

        const string og = "original";
        const string review = "review";
        const string output = "output";

        var defaultConfig = new AppConfig
        {
            BaseOgPath = og,
            BaseReviewPath = review,
            BaseOutputPath = output,
            Tasks = new List<TranslationTask>
            {
                new() { Name = "Main Game", OriginalFile = Path.Combine(og, "en.csv"), ReviewFile = Path.Combine(review, "Main_Game_Review.tsv"), OutputFile = Path.Combine(output, "en.csv") },
                new() { Name = "DLC 01", OriginalFile = Path.Combine(og, "en_dlc01.csv"), ReviewFile = Path.Combine(review, "DLC01_Review.tsv"), OutputFile = Path.Combine(output, "en_dlc01.csv") },
                new() { Name = "DLC 02", OriginalFile = Path.Combine(og, "en_dlc02.csv"), ReviewFile = Path.Combine(review, "DLC02_Review.tsv"), OutputFile = Path.Combine(output, "en_dlc02.csv") },
                new() { Name = "DLC 03", OriginalFile = Path.Combine(og, "en_dlc03.csv"), ReviewFile = Path.Combine(review, "DLC03_Review.tsv"), OutputFile = Path.Combine(output, "en_dlc03.csv") }
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigFileName, JsonSerializer.Serialize(defaultConfig, options), Encoding.UTF8);

        Console.WriteLine(Loc.Get("ConfigCreated"));
        return defaultConfig;
    }

    static void ProcessTask(TranslationTask task)
    {
        Console.WriteLine(Loc.Get("Processing", task.Name));

        if (!File.Exists(task.OriginalFile) || !File.Exists(task.ReviewFile))
        {
            Console.WriteLine(Loc.Get("Skipped", task.Name));
            return;
        }

        // 1. UA: Завантажуємо правки з таблиці / EN: Load review data
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

        // 2. UA: Генерація та валідація / EN: Generation and validation
        int totalRowsOriginal;
        int totalRowsOutput = 0;
        int errorsFound = 0;
        int mergedCount = 0;

        // UA: Ключі оригіналу, для яких не знайшлось запису в review —
        //     тобто ще не перекладені/не звірені рядки.
        // EN: Original keys with no matching review entry — i.e. rows not
        //     yet translated/checked.
        var missingKeys = new List<(string Id, string Eng)>();

        var originalLines = File.ReadAllLines(task.OriginalFile, Encoding.UTF8);
        totalRowsOriginal = originalLines.Length;

        // UA: using без фігурних дужок (сучасна фіча C#)
        // EN: using declaration without braces (modern C# feature)
        using StreamWriter writer = new StreamWriter(task.OutputFile, false, new UTF8Encoding(false));

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

            if (tableData.TryGetValue(keyId, out var reviewData))
            {
                finalTranslation = reviewData.UkrainianTranslation;
                mergedCount++;

                // UA: Технічна валідація / EN: Technical validation
                errorsFound += ValidateString(keyId, originalEng, finalTranslation);

                // UA: Авто-фікс зайвої крапки в кінці / EN: Auto-fix a trailing dot
                if (!originalEng.Trim().EndsWith('.') && finalTranslation.Trim().EndsWith('.'))
                {
                    finalTranslation = finalTranslation.TrimEnd('.');
                }
            }
            else if (!string.IsNullOrEmpty(keyId))
            {
                missingKeys.Add((keyId, originalEng));
            }

            cols[1] = finalTranslation;
            writer.WriteLine(string.Join("\t", cols));
        }

        // 3. UA: Підсумки по файлу / EN: File summary
        Console.WriteLine(Loc.Get("Done", task.Name));
        Console.WriteLine(Loc.Get("RowsInfo", totalRowsOriginal, totalRowsOutput));

        if (totalRowsOriginal != totalRowsOutput)
            Console.WriteLine(Loc.Get("RowWarning"));

        Console.WriteLine(Loc.Get("Integrated", mergedCount));

        Console.ForegroundColor = errorsFound > 0 ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine(Loc.Get("Errors", errorsFound));
        Console.ResetColor();

        // 4. UA: Звіт про ключі, відсутні в review (консоль + файл)
        // EN: Report for keys missing from the review file (console + file)
        ReportMissingKeys(task.Name, missingKeys, totalRowsOriginal);
    }

    // UA: Виводить у консоль (з обмеженим превʼю) і зберігає в reports/
    //     повний список ключів оригіналу, яких немає в review-файлі —
    //     щоб одразу бачити, що ще не перекладено/не звірено.
    // EN: Prints (with a limited preview) to the console and saves the full
    //     list of original keys missing from the review file into reports/ —
    //     so gaps in translation/review are visible right away.
    static void ReportMissingKeys(string taskName, List<(string Id, string Eng)> missingKeys, int totalRows)
    {
        if (missingKeys.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(Loc.Get("MissingKeysNone"));
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(Loc.Get("MissingKeysHeader", taskName, missingKeys.Count, totalRows));

        const int previewLimit = 15;
        foreach (var (id, eng) in missingKeys.Take(previewLimit))
        {
            Console.WriteLine($"    {id}: {Truncate(eng, 70)}");
        }
        if (missingKeys.Count > previewLimit)
            Console.WriteLine(Loc.Get("MissingKeysMore", missingKeys.Count - previewLimit));
        Console.ResetColor();

        Directory.CreateDirectory(ReportsDirName);
        string safeName = string.Join("_", taskName.Split(Path.GetInvalidFileNameChars()));
        string reportPath = Path.Combine(ReportsDirName, $"MissingKeys_{safeName}.txt");

        using (var reportWriter = new StreamWriter(reportPath, false, new UTF8Encoding(false)))
        {
            reportWriter.WriteLine($"# Missing in review / Відсутні в review — {taskName}");
            reportWriter.WriteLine($"# {missingKeys.Count} / {totalRows}");
            reportWriter.WriteLine("# ID\tEnglish");
            foreach (var (id, eng) in missingKeys)
                reportWriter.WriteLine($"{id}\t{eng}");
        }

        Console.WriteLine(Loc.Get("MissingKeysSaved", reportPath));
    }

    static string Truncate(string s, int maxLen) => s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";

    // UA: Технічна валідація одного рядка перекладу — запобігає крашам
    //     рушія через биті теги/змінні/переноси рядків.
    // EN: Technical validation of a single translated row — prevents
    //     engine crashes from broken tags/variables/newlines.
    static int ValidateString(string id, string eng, string ukr)
    {
        int errors = 0;

        // UA: Баланс тегів/дужок / EN: Tag/bracket balance
        char[] tags = { '<', '>', '[', ']', '{', '}' };
        foreach (var tag in tags)
        {
            if (eng.Count(f => f == tag) != ukr.Count(f => f == tag))
            {
                LogValidationError(id, $"Tag mismatch '{tag}'", eng, ukr);
                errors++;
            }
        }

        // UA: Кількість змінних % / EN: Count of '%' placeholders
        if (eng.Count(f => f == '%') != ukr.Count(f => f == '%'))
        {
            LogValidationError(id, "Mismatch in '%' count", eng, ukr);
            errors++;
        }

        // UA: Кирилиця всередині змінної (напр. %д замість %d) — падіння рушія.
        // EN: Cyrillic character inside a variable (e.g. %д instead of %d) — engine crash.
        if (Regex.IsMatch(ukr, @"%[а-яА-ЯіІїЇєЄґҐ]"))
        {
            LogValidationError(id, "Cyrillic variable (e.g. %д)", eng, ukr);
            errors++;
        }

        // UA: Перевірка переносів рядка \n / EN: Newline \n check
        int engNewLines = Regex.Matches(eng, @"\\n").Count;
        int ukrNewLines = Regex.Matches(ukr, @"\\n").Count;
        if (engNewLines != ukrNewLines)
        {
            LogValidationError(id, $"Newline \\n mismatch (Orig: {engNewLines}, Ukr: {ukrNewLines})", eng, ukr);
            errors++;
        }

        return errors;
    }

    static void LogValidationError(string id, string message, string eng, string ukr)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        string valText = Loc.CurrentLang == AppLanguage.Ukrainian ? "ВАЛІДАЦІЯ" : "VALIDATION";
        Console.WriteLine($" [{valText}] ID: {id} | {message}");
        Console.WriteLine($" ENG: {eng}");
        Console.WriteLine($" UKR: {ukr}");
        Console.ResetColor();
    }
}

// =============================================================================
// SWH.FontTool.Analyzer — FontAnalyzer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Автоматичний бінарний аналіз .fnt файлів SteamWorld Heist.
//     Не потребує ручних профілів: stride, зміщення та кількість гліфів
//     визначаються безпосередньо з байтів файлу.
// EN: Automatic binary analysis of SteamWorld Heist .fnt files. Requires no
//     manual profiles: stride, offsets, and glyph count are all determined
//     directly from the file's bytes.
// =============================================================================

using SWH.FontTool.Core;

namespace SWH.FontTool.Analyzer;

/// <summary>
/// UA: Аналізує бінарний .fnt файл SteamWorld Heist без будь-яких апріорних
/// знань про конкретний шрифт. Алгоритм:
///   1. Шукає ID=32 (пробіл, 0x20 0x00 0x00 0x00) як маркер початку таблиці.
///   2. Перевіряє позицію наступного запису (ID=33) для визначення stride.
///   3. Читає всі записи до першого невалідного.
///   4. Обчислює константу базової лінії з латинських великих літер.
/// EN: Analyzes a binary SteamWorld Heist .fnt file with no prior knowledge
/// of the specific font. Algorithm:
///   1. Looks for ID=32 (space, 0x20 0x00 0x00 0x00) as the table-start marker.
///   2. Checks the position of the next record (ID=33) to determine the stride.
///   3. Reads every record up to the first invalid one.
///   4. Computes the baseline constant from the Latin uppercase letters.
/// </summary>
public static class FontAnalyzer
{
    /// <summary>
    /// UA: Головний метод: повний аналіз файлу.
    /// EN: The main method: full analysis of a file.
    /// </summary>
    public static FontAnalysisResult Analyze(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл не знайдено / File not found: {path}");

        byte[] data = File.ReadAllBytes(path);
        string name = Path.GetFileNameWithoutExtension(path);

        // UA: Крок 1: знаходимо початок таблиці гліфів
        // EN: Step 1: find the glyph table's start
        int tableStart = FindTableStart(data, out bool hasPadding);
        if (tableStart < 0)
            throw new InvalidDataException(
                $"{name}: не вдалося знайти ID=32 у перших 512 байтах / could not find ID=32 in the first 512 bytes. " +
                "Перевірте, чи це коректний .fnt файл SteamWorld Heist. / Check that this is a valid SteamWorld Heist .fnt file.");

        // UA: Крок 2: визначаємо розмір одного запису (stride)
        // EN: Step 2: determine the size of a single record (stride)
        int stride = DetectStride(data, tableStart, hasPadding);

        // UA: Крок 3: рахуємо гліфи до першого невалідного запису
        // EN: Step 3: count glyphs up to the first invalid record
        int count = CountGlyphs(data, tableStart, stride, hasPadding);

        // UA: Крок 4: зчитуємо всі записи повністю (усі поля)
        // EN: Step 4: read every record in full (all fields)
        var records = ReadRecords(data, tableStart, stride, count, hasPadding);

        // UA: Крок 5: обчислюємо константу базової лінії з латинських A–Z
        // EN: Step 5: compute the baseline constant from Latin A–Z
        float baseline = MetricsEngine.ComputeBaseline(records);

        return new FontAnalysisResult
        {
            FontName = name,
            TableStart = tableStart,
            Stride = stride,
            HasPaddingPrefix = hasPadding,
            GlyphCount = count,
            Records = records,
            BaselineConstant = baseline
        };
    }

    // -----------------------------------------------------------------------
    // UA: Крок 1: Пошук початку таблиці
    // EN: Step 1: Finding the table start
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Шукаємо байт-послідовність 20 00 00 00 (ID=32 у little-endian).
    /// Додатково визначаємо, чи є 4-байтовий padding перед ID (stride=36).
    ///
    /// Алгоритм розрізнення padding vs no-padding:
    ///   - Якщо наступний запис (ID=33) знаходиться на відстані 36 байт
    ///     від позиції [знайдений_байт - 4] → це stride=36, запис починається на -4.
    ///   - Якщо ID=33 на відстані 28 або 32 від знайденого байта → stride без padding.
    /// EN: Looks for the byte sequence 20 00 00 00 (ID=32 in little-endian).
    /// Additionally determines whether there's a 4-byte padding before the
    /// ID (stride=36).
    ///
    /// Algorithm for distinguishing padding vs no-padding:
    ///   - If the next record (ID=33) sits 36 bytes from [found_byte - 4]
    ///     → this is stride=36, the record starts at -4.
    ///   - If ID=33 sits 28 or 32 bytes from the found byte → stride with no padding.
    /// </summary>
    private static int FindTableStart(byte[] data, out bool hasPaddingPrefix)
    {
        hasPaddingPrefix = false;
        int limit = Math.Min(data.Length - 48, 512);

        // -------------------------------------------------------------------
        // UA: ВИПРАВЛЕНО (діагностика 2026-07): порядок перевірок нижче має
        //     значення, і раніше він був неправильним. Стара версія тестувала
        //     "stride=36 з padding" ПЕРШОЮ, а ця перевірка читає той самий
        //     офсет (data[i+32]), що й перевірка "stride=32 без padding".
        //     Але next-ID рівно на +32 — це не ознака padding, а буквально
        //     ОЗНАЧЕННЯ stride=32! Тобто для БУДЬ-ЯКОГО справжнього
        //     32-байтного шрифту (без жодного padding) перша перевірка
        //     спрацьовувала першою і хибно повертала hasPaddingPrefix=true,
        //     зсуваючи початок таблиці на -4 байти. Це трактувало останні
        //     4 байти заголовка як фіктивне поле "Padding" гліфа №0 і ламало
        //     вирівнювання геть усіх наступних полів для кожного 32-байтного
        //     шрифту (підтверджено відтворенням на реальних байтах ingame.fnt
        //     — до фіксу: Padding=true, Start зсунутий на -4; після фіксу:
        //     Padding=false, Start точно збігається з charsStart за strLen).
        //     Це — вельми ймовірна причина того, чому генерація на основі
        //     цього аналізу раніше ламала гру. Виправлення: спершу перевіряємо
        //     "прості" варіанти (28/32 без padding), і лише якщо жоден з них
        //     не підійшов — інтерпретуємо збіг як stride=36 з padding.
        // EN: FIXED (2026-07 diagnostic pass): the order of checks below
        //     matters, and it used to be wrong. The old code tested
        //     "stride=36 with padding" FIRST, and that check reads the exact
        //     same offset (data[i+32]) as the "stride=32 without padding"
        //     check. But the next ID sitting exactly at +32 isn't a sign of
        //     padding — it's literally the DEFINITION of stride=32! So for
        //     ANY genuine 32-byte-stride font (no padding at all), the first
        //     check fired first and wrongly returned hasPaddingPrefix=true,
        //     shifting the table start back by 4 bytes. That treated the last
        //     4 header bytes as a bogus "Padding" field on glyph #0 and broke
        //     alignment for every subsequent field, for every 32-byte-stride
        //     font (confirmed by reproducing this on real ingame.fnt bytes —
        //     before the fix: Padding=true, Start shifted by -4; after the
        //     fix: Padding=false, Start matches charsStart computed from
        //     strLen exactly). This is a strong candidate for why generation
        //     built on this analysis used to break the game. Fix: check the
        //     "simple" variants (28/32, no padding) first, and only fall back
        //     to interpreting a match as stride=36-with-padding if neither
        //     simple explanation fits.
        // -------------------------------------------------------------------

        for (int i = 4; i < limit; i++)
        {
            // UA: Шукаємо 0x20 0x00 0x00 0x00 / EN: Looking for 0x20 0x00 0x00 0x00
            if (data[i] != 0x20 || data[i + 1] != 0 || data[i + 2] != 0 || data[i + 3] != 0)
                continue;

            // UA: Варіант stride=28: наступний запис (ID=33) одразу на i+28
            // EN: stride=28 case: the next record (ID=33) sits right at i+28
            if (i + 28 + 4 <= data.Length && BitConverter.ToInt32(data, i + 28) == 33)
                return i;

            // UA: Варіант stride=32: наступний запис (ID=33) на i+32 (без padding)
            // EN: stride=32 case: the next record (ID=33) sits at i+32 (no padding)
            if (i + 32 + 4 <= data.Length && BitConverter.ToInt32(data, i + 32) == 33)
                return i;

            // UA: Варіант stride=36: запис починається на i-4, ID на i, наступний
            //     ID=33 на i+32. Перевіряємо ОСТАННІМ — лише якщо жодне просте
            //     пояснення (28/32 без padding) не підійшло.
            // EN: stride=36 case: the record starts at i-4, the ID is at i, the
            //     next ID=33 is at i+32. Checked LAST — only if neither simple
            //     explanation (28/32, no padding) fit.
            if (i >= 4 && i + 32 + 4 <= data.Length)
            {
                int nextId36 = BitConverter.ToInt32(data, i + 32);
                if (nextId36 == 33)
                {
                    hasPaddingPrefix = true;
                    return i - 4; // UA: початок запису — 4 байти до ID / EN: the record starts 4 bytes before the ID
                }
            }
        }

        return -1;
    }

    // -----------------------------------------------------------------------
    // UA: Крок 2: Визначення stride
    // EN: Step 2: Determining the stride
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Визначаємо stride, перевіряючи позицію ID=33 відносно початку
    /// таблиці. Підтримуються значення 28, 32, 36.
    /// EN: Determines the stride by checking the position of ID=33 relative
    /// to the table start. Values 28, 32, and 36 are supported.
    /// </summary>
    private static int DetectStride(byte[] data, int tableStart, bool hasPadding)
    {
        int idOff = hasPadding ? 4 : 0;

        foreach (int candidate in new[] { 28, 32, 36 })
        {
            int nextIdPos = tableStart + candidate + idOff;
            if (nextIdPos + 4 <= data.Length && BitConverter.ToInt32(data, nextIdPos) == 33)
                return candidate;
        }

        // UA: Fallback: логуємо попередження та повертаємо мінімальний stride
        // EN: Fallback: log a warning and return the smallest stride
        Console.WriteLine($"[!] FontAnalyzer (UA): не вдалося визначити stride автоматично, використовуємо 28.");
        Console.WriteLine($"[!] FontAnalyzer (EN): could not auto-detect the stride, defaulting to 28.");
        return 28;
    }

    // -----------------------------------------------------------------------
    // UA: Крок 3: Підрахунок гліфів
    // EN: Step 3: Counting glyphs
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Читаємо записи доти, доки зустрінемо невалідний:
    ///   - ID поза діапазоном [32..65535]
    ///   - AtlasX є NaN або від'ємне число (ознака кінця таблиці або сміттєвих даних)
    /// EN: Reads records until an invalid one is encountered:
    ///   - ID outside the [32..65535] range
    ///   - AtlasX is NaN or negative (a sign of the table's end, or garbage data)
    /// </summary>
    private static int CountGlyphs(byte[] data, int tableStart, int stride, bool hasPadding)
    {
        int idOff = hasPadding ? 4 : 0;
        int count = 0;

        for (int pos = tableStart; pos + stride <= data.Length; pos += stride)
        {
            int id = BitConverter.ToInt32(data, pos + idOff);
            if (id < 32 || id > 65535) break; // UA: невалідний ID — кінець таблиці / EN: invalid ID — end of the table

            float x = BitConverter.ToSingle(data, pos + idOff + 4);
            if (float.IsNaN(x) || x < -1f || x > 16384f) break; // UA: невалідна координата / EN: invalid coordinate

            count++;
        }

        return count;
    }

    // -----------------------------------------------------------------------
    // UA: Крок 4: Зчитування повних записів
    // EN: Step 4: Reading full records
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Зчитує всі поля кожного запису відповідно до stride.
    /// Для stride=28: немає поля XAdvance.
    /// Для stride=32: XAdvance є int32 на +28 від початку запису.
    /// Для stride=36: padding(4) + ID + поля + XAdvance.
    /// EN: Reads every field of each record according to the stride.
    /// For stride=28: no XAdvance field.
    /// For stride=32: XAdvance is an int32 at +28 from the record's start.
    /// For stride=36: padding(4) + ID + fields + XAdvance.
    /// </summary>
    private static List<GlyphRecord> ReadRecords(
        byte[] data, int tableStart, int stride, int count, bool hasPadding)
    {
        var list = new List<GlyphRecord>(count);
        int idOff = hasPadding ? 4 : 0;
        bool hasXA = stride >= 32;

        for (int i = 0; i < count; i++)
        {
            int pos = tableStart + i * stride;

            list.Add(new GlyphRecord
            {
                Padding = hasPadding ? BitConverter.ToInt32(data, pos) : 0,
                ID = BitConverter.ToInt32(data, pos + idOff),
                AtlasX = BitConverter.ToSingle(data, pos + idOff + 4),
                AtlasY = BitConverter.ToSingle(data, pos + idOff + 8),
                AtlasW = BitConverter.ToSingle(data, pos + idOff + 12),
                AtlasH = BitConverter.ToSingle(data, pos + idOff + 16),
                XOffset = BitConverter.ToSingle(data, pos + idOff + 20),
                YOffset = BitConverter.ToSingle(data, pos + idOff + 24),
                // UA: XAdvance — int32, не float (підтверджено бінарним аналізом v.040 bug)
                // EN: XAdvance — int32, not a float (confirmed by binary analysis, the v.040 bug)
                XAdvance = hasXA ? BitConverter.ToInt32(data, pos + idOff + 28) : 0
            });
        }

        return list;
    }

    // -----------------------------------------------------------------------
    // UA: Публічні утиліти
    // EN: Public utilities
    // -----------------------------------------------------------------------

    /// <summary>
    /// UA: Генерує детальний текстовий звіт про структуру файлу.
    /// Зручно для порівняння EN/PL/UA файлів та верифікації після патчингу.
    /// EN: Generates a detailed text report on the file's structure.
    /// Handy for comparing EN/PL/UA files and for verification after patching.
    /// </summary>
    public static string GenerateReport(FontAnalysisResult r)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"=== АНАЛІЗ / ANALYSIS: {r.FontName} ===");
        sb.AppendLine($"Stride: {r.Stride} | Start: 0x{r.TableStart:X4} | " +
                      $"Гліфів: {r.GlyphCount} | Padding: {r.HasPaddingPrefix} | XAdv: {r.HasXAdvance}");
        sb.AppendLine($"Базова лінія / Baseline (константа/constant): {r.BaselineConstant:F4}");
        sb.AppendLine();
        sb.AppendLine($"{"ID",6} {"Ch",3} {"AtlasX",8} {"AtlasY",8} {"W",6} {"H",6} " +
                      $"{"XOff",7} {"YOff",7} {"XAdv",6}  Категорія/Category");
        sb.AppendLine(new string('-', 80));

        foreach (var g in r.Records)
        {
            char ch = g.ID is >= 32 and <= 65535 ? (char)g.ID : '·';
            string cat = g.IsLatinCapital ? "Latin-Cap" :
                         g.IsLatinLower ? "Latin-lo" :
                         g.IsRussianOnly ? "RU-only" :
                         g.IsCyrillicBlock ? "Cyrillic" :
                         g.IsAsciiPrintable ? "ASCII" : "Other";

            sb.AppendLine($"{g.ID,6} {ch,3} {g.AtlasX,8:F1} {g.AtlasY,8:F1} " +
                          $"{g.AtlasW,6:F1} {g.AtlasH,6:F1} " +
                          $"{g.XOffset,7:F2} {g.YOffset,7:F2} {g.XAdvance,6}  {cat}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// UA: Пост-генераційна самоперевірка. Перечитує ВЖЕ НАПИСАНИЙ .fnt файл
    /// з нуля (окремий Analyze, а не перевикористання об'єкта з пам'яті — щоб
    /// перевірка була про те, що реально лежить на диску) і звіряє його з
    /// оригіналом за інваріантами, які патчинг (лише перепризначення слотів,
    /// БЕЗ росту numChars — див. SlotInjectionExperiment.cs про те, чому
    /// зростання numChars неможливе) не мав права порушити:
    ///   - довжина файлу в байтах не змінилась (патчинг суто in-place);
    ///   - кількість гліфів (numChars) та формат запису (stride/padding/
    ///     XAdvance) не змінились;
    ///   - у таблиці немає ДУБЛІКАТІВ ID (два записи з одним ID — рушій
    ///     використає лише один, інший стає мертвим сміттям або конфліктом);
    ///   - усі ID результату в межах [32..65535].
    /// Не ловить усі можливі баги (напр. невдалі координати атласу — це вже
    /// відповідальність AtlasProcessor/FontGenerator.PlanAllUaSlots), але
    /// ловить саме той клас помилок, що вже спричиняв реальні краші в цьому
    /// проєкті.
    /// Повертає порожній список, якщо все гаразд.
    /// EN: Post-generation self-check. Re-reads the ALREADY WRITTEN .fnt file
    /// from scratch (a separate Analyze call, not the in-memory object — so
    /// the check is about what's actually on disk) and compares it to the
    /// original on invariants that patching (slot remapping only, WITHOUT
    /// numChars growth — see SlotInjectionExperiment.cs for why numChars
    /// growth isn't possible) must not violate:
    ///   - file length in bytes is unchanged (patching is purely in-place);
    ///   - glyph count (numChars) and record layout (stride/padding/
    ///     XAdvance) are unchanged;
    ///   - the table has no DUPLICATE IDs (two records sharing one ID — the
    ///     engine will only use one, the other becomes dead weight or a
    ///     conflict);
    ///   - every resulting ID is within [32..65535].
    /// Doesn't catch every possible bug (e.g. bad atlas coordinates are
    /// AtlasProcessor/FontGenerator.PlanAllUaSlots's responsibility), but it
    /// does catch exactly the class of error that has already caused real crashes in
    /// this project. Returns an empty list if everything checks out.
    /// </summary>
    public static List<string> ValidateGeneratedFont(string originalPath, string generatedPath)
    {
        var issues = new List<string>();

        var original = Analyze(originalPath);
        FontAnalysisResult generated;
        try
        {
            generated = Analyze(generatedPath);
        }
        catch (Exception ex)
        {
            issues.Add(
                $"UA: результуючий файл не вдалося повторно розпарсити: {ex.Message} / " +
                $"EN: could not re-parse the resulting file: {ex.Message}");
            return issues;
        }

        long origLen = new FileInfo(originalPath).Length;
        long genLen = new FileInfo(generatedPath).Length;
        if (origLen != genLen)
            issues.Add(
                $"UA: довжина файлу змінилась ({origLen} -> {genLen} байт), хоча патчинг мав бути лише in-place. / " +
                $"EN: file length changed ({origLen} -> {genLen} bytes), even though patching was supposed to be in-place only.");

        if (generated.GlyphCount != original.GlyphCount)
            issues.Add(
                $"UA: кількість гліфів змінилась ({original.GlyphCount} -> {generated.GlyphCount}) — numChars не мав рости. / " +
                $"EN: glyph count changed ({original.GlyphCount} -> {generated.GlyphCount}) — numChars wasn't supposed to grow.");

        if (generated.Stride != original.Stride ||
            generated.HasPaddingPrefix != original.HasPaddingPrefix ||
            generated.HasXAdvance != original.HasXAdvance)
            issues.Add(
                $"UA: формат запису змінився (stride {original.Stride}->{generated.Stride}, " +
                $"padding {original.HasPaddingPrefix}->{generated.HasPaddingPrefix}, " +
                $"XAdv {original.HasXAdvance}->{generated.HasXAdvance}). / " +
                $"EN: the record layout changed (stride {original.Stride}->{generated.Stride}, " +
                $"padding {original.HasPaddingPrefix}->{generated.HasPaddingPrefix}, " +
                $"XAdv {original.HasXAdvance}->{generated.HasXAdvance}).");

        var dupes = generated.Records.GroupBy(r => r.ID).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0)
            issues.Add(
                $"UA: дублікати ID у результаті: {string.Join(",", dupes)} — рушій використає лише один запис на ID. / " +
                $"EN: duplicate IDs in the result: {string.Join(",", dupes)} — the engine will only use one record per ID.");

        // UA: КРИТИЧНО (знайдено після реального тесту в грі, 2026-07-21):
        //     сам по собі патчинг ID без пересортування таблиці лишає
        //     запис фізично не на своєму місці за зростанням — рушій,
        //     судячи з усього, покладається на цей порядок (імовірно,
        //     бінарний пошук): один-єдиний "вибитий" запис ламає пошук не
        //     лише для себе, а й для сусідніх ID після нього в таблиці.
        //     Ні довжина файлу, ні кількість гліфів, ні дублікати ID цього
        //     не ловлять — тому потрібна саме окрема перевірка порядку.
        // EN: CRITICAL (found after a real in-game test, 2026-07-21):
        //     patching an ID without re-sorting the table on its own leaves
        //     a record physically out of ascending order — the engine
        //     apparently relies on this order (quite possibly a binary
        //     search): a single "out of place" record breaks lookup not
        //     just for itself, but for neighboring IDs after it in the
        //     table too. Neither file length, nor glyph count, nor
        //     duplicate IDs catch this — hence a dedicated ordering check.
        for (int i = 1; i < generated.Records.Count; i++)
        {
            if (generated.Records[i].ID < generated.Records[i - 1].ID)
            {
                issues.Add(
                    $"UA: таблиця гліфів НЕ відсортована за зростанням ID (запис #{i}: ID {generated.Records[i - 1].ID} -> {generated.Records[i].ID}) — " +
                    "рушій, схоже, покладається на цей порядок (можливо, бінарний пошук); один невідсортований запис ламає пошук і для сусідніх ID. / " +
                    $"EN: the glyph table is NOT sorted by ascending ID (record #{i}: ID {generated.Records[i - 1].ID} -> {generated.Records[i].ID}) — " +
                    "the engine appears to rely on this order (possibly a binary search); one out-of-order record breaks lookup for neighboring IDs too.");
                break;
            }
        }

        var badIds = generated.Records.Where(r => r.ID < 32 || r.ID > 65535).Select(r => r.ID).ToList();
        if (badIds.Count > 0)
            issues.Add(
                $"UA: гліфи з ID поза межами [32..65535]: {string.Join(",", badIds)}. / " +
                $"EN: glyphs with IDs outside [32..65535]: {string.Join(",", badIds)}.");

        return issues;
    }

    /// <summary>
    /// UA: Аналізує всі шрифти в теці і виводить зведену таблицю.
    /// Зручно для швидкого огляду розбіжностей між файлами.
    /// EN: Analyzes every font in the folder and prints a summary table.
    /// Handy for a quick overview of differences between files.
    /// </summary>
    public static void BatchReport(string fntDir, string outputDir)
    {
        Console.WriteLine($"\n{"Шрифт/Font",-20} | {"Stride",6} | {"Start",7} | {"Гліфів/Glyphs",13} | " +
                          $"{"Baseline",10} | {"XAdv?",6}");
        Console.WriteLine(new string('-', 72));

        string[] files = Directory.GetFiles(fntDir, "*.fnt");
        var reportLines = new List<string>();

        foreach (var file in files.OrderBy(f => f))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var result = Analyze(file);
                string line = $"{name,-20} | {result.Stride,6} | 0x{result.TableStart:X4} | " +
                              $"{result.GlyphCount,13} | {result.BaselineConstant,10:F4} | " +
                              $"{result.HasXAdvance,6}";
                Console.WriteLine(line);
                reportLines.Add(line);

                // UA: Зберігаємо детальний звіт для кожного файлу
                // EN: Save a detailed report for each file
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    File.WriteAllText(
                        Path.Combine(outputDir, name + "_analysis.txt"),
                        GenerateReport(result),
                        System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{name,-20} | [!] {ex.Message}");
            }
        }
    }
}
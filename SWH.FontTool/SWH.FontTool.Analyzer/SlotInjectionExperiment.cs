// =============================================================================
// SWH.FontTool.Analyzer — SlotInjectionExperiment.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: РЕЗУЛЬТАТ ЕКСПЕРИМЕНТУ (перевірено на реальній грі, 2026-07-21):
//     рушій НЕ толерантний до зростання numChars. Файли з ExperimentDir,
//     скопійовані в гру, спричиняють краш ОДРАЗУ при старті гри з вікном
//     "Unexpected exception: vector<T> too long" (C++ std::length_error —
//     рушій, судячи з усього, звіряє кількість гліфів з якимось внутрішнім
//     очікуваним значенням і при розбіжності отримує сміттєвий/величезний
//     розмір для вектора). Висновок: ДОДАВАТИ нові записи в таблицю не
//     можна ні за яких обставин — єдиний робочий шлях це перепризначення
//     вже наявних слотів (так, як і робить FontGenerator сьогодні). Клас
//     лишається в репозиторії як задокументований негативний результат і
//     готовий інструмент, якщо колись знадобиться перевірити щось подібне
//     ще раз (напр. після оновлення гри).
// EN: EXPERIMENT RESULT (verified against the real game, 2026-07-21): the
//     engine is NOT tolerant of numChars growing. Files from ExperimentDir,
//     copied into the game, crash the game IMMEDIATELY on launch with
//     "Unexpected exception: vector<T> too long" (a C++ std::length_error —
//     the engine apparently cross-checks the glyph count against some
//     internal expected value and, on mismatch, ends up with a garbage/huge
//     size for a vector). Conclusion: appending new records to the table is
//     not viable under any circumstances — the only working path is
//     remapping already-existing slots (exactly what FontGenerator does
//     today). This class stays in the repo as a documented negative result
//     and a ready-made tool in case something similar ever needs re-testing
//     (e.g. after a game update).
// =============================================================================
// UA: Контрольований експеримент: чи толерантний рушій SteamWorld Heist до
//     ЗРОСТАННЯ кількості гліфів (numChars) у .fnt файлі — тобто чи можна
//     ДОДАВАТИ нові записи в таблицю, а не тільки перепризначати вже наявні
//     (як зараз робить AlphabetProcessor/FontGenerator).
//
//     Навіщо це окремо від FontGenerator: поточний FontGenerator ніколи не
//     збільшує кількість записів — він перевикористовує байти вже існуючих
//     слотів, змінюючи їхні координати на нові ділянки PNG. Це свідомо уникає
//     питання "чи можна рости numChars" узагалі. Цей клас — окремий,
//     ізольований тест САМЕ цього питання: корисний діагностичний інструмент
//     на випадок, якщо в майбутньому знадобиться справді додавати нові слоти
//     (а не тільки перевикористовувати наявні), і як спосіб виключити ще одну
//     потенційну причину крашу під час пошуку багів.
//
//     Метод: для кожної відсутньої української літери береться донор (вже
//     існуючий гліф зі схожою формою) і його запис КЛОНУЄТЬСЯ під новим ID.
//     PNG НЕ змінюється: новий слот просто вказує на вже намальований
//     піксельний прямокутник донора — це перевірка тільки бінарної структури
//     .fnt, не рендеру.
//
//     UA: original-fonts/ НІКОЛИ не редагується. Тест читає звідти й пише
//     результат в окрему теку Config.ExperimentDir (біля .exe) — так само,
//     як основний генератор пише в output/, а не назад у джерело. Саме тому
//     тут більше немає бекапу/відкату: нема що відновлювати, якщо оригінал
//     ніхто й не чіпав. Щоб перевірити результат у грі — вручну скопіювати
//     файли з ExperimentDir у гру (і так само вручну повернути назад
//     оригінали з original-fonts, коли перевірка завершена).
// EN: A controlled experiment: is the SteamWorld Heist engine tolerant of the
//     glyph COUNT (numChars) in a .fnt file GROWING — i.e. can new records be
//     APPENDED to the table, rather than only remapping IDs of already-
//     existing records (which is what AlphabetProcessor/FontGenerator do
//     today).
//
//     Why this is separate from FontGenerator: the current FontGenerator
//     never increases the record count — it reuses the bytes of already-
//     existing slots, repointing their coordinates at new PNG regions. That
//     deliberately sidesteps the "can numChars grow" question entirely. This
//     class is a separate, isolated test of exactly that question: useful if
//     truly adding new slots (rather than only repurposing existing ones) is
//     ever needed, and as a way to rule out one more potential crash cause
//     while debugging.
//
//     Method: for each missing Ukrainian letter, a donor (an existing glyph
//     with a similar shape) is picked and its record is CLONED under the new
//     ID. The PNG is NOT touched: the new slot simply points at the donor's
//     already-rendered pixel rectangle — this tests only the binary .fnt
//     structure, not rendering.
//
//     EN: original-fonts/ is NEVER edited. The test reads from there and
//     writes the result into a separate Config.ExperimentDir folder (next to
//     the .exe) — exactly like the main generator writes to output/ instead
//     of back into the source. This is why there's no backup/restore anymore:
//     there's nothing to restore if the original was never touched. To test
//     in-game, manually copy the files from ExperimentDir into the game (and
//     manually put the originals from original-fonts back once done).
//
//     IMPORTANT: does not assume a fixed 32-byte record size — it uses
//     Stride/HasPaddingPrefix/IdOffset from FontAnalysisResult (28/32/36
//     bytes depending on the font), so it works correctly for every variant
//     that's been observed. While building this class, a related bug was
//     found and fixed in FontAnalyzer.FindTableStart — see the comment there.
// =============================================================================

using SWH.FontTool.Core;

namespace SWH.FontTool.Analyzer;

public static class SlotInjectionExperiment
{
    // UA: Українська літера (ID) -> ID донора зі схожою формою, який майже
    //     напевно вже присутній у кожному .fnt. Це навмисно грубі двійники
    //     (форма не ідеальна) — мета тесту суто структурна, не косметична.
    // EN: Ukrainian letter (ID) -> ID of a similarly-shaped donor that is
    //     almost certainly already present in every .fnt. These are
    //     deliberately rough stand-ins (shape isn't perfect) — the test's
    //     goal is purely structural, not cosmetic.
    public static readonly Dictionary<int, int> MissingUaDonorMap = new()
    {
        { 1030, 73 },   // І <- I  (велика, форма ідентична / uppercase, identical shape)
        { 1110, 105 },  // і <- i
        { 1031, 73 },   // Ї <- I  (грубий двійник лише для тесту / rough stand-in, test only)
        { 1111, 105 },  // ї <- i
        { 1028, 1069 }, // Є <- Э
        { 1108, 1101 }, // є <- э
        { 1168, 1043 }, // Ґ <- Г
        { 1169, 1075 }, // ґ <- г
    };

    // -----------------------------------------------------------------------
    // UA: Головний тест
    // EN: Main test
    // -----------------------------------------------------------------------

    // UA: Застосовує тест до УСІХ .fnt у sourceDir (зазвичай Config.OriginalFontsDir,
    //     завжди read-only) і пише результат у outDir (Config.ExperimentDir).
    //     Разом з модифікованим .fnt копіюється й відповідний .png без змін —
    //     щоб у outDir лежала повна, готова до ручного копіювання в гру пара
    //     файлів на кожен шрифт.
    // EN: Applies the test to ALL .fnt files in sourceDir (typically
    //     Config.OriginalFontsDir, always read-only) and writes the result
    //     into outDir (Config.ExperimentDir). The matching .png is copied
    //     alongside the modified .fnt unchanged — so outDir ends up holding a
    //     complete, ready-to-copy-into-the-game pair of files per font.
    public static void RunInjectionTest(string sourceDir, string outDir, TextWriter log)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Папка не знайдена / Folder not found: {sourceDir}");

        Directory.CreateDirectory(outDir);
        FontParser.ClearCache(); // UA: на випадок, якщо файли вже читались раніше в цій сесії / EN: in case files were already read earlier this session

        var fntFiles = Directory.GetFiles(sourceDir, "*.fnt");
        if (fntFiles.Length == 0)
        {
            log.WriteLine("У папці немає .fnt файлів. / No .fnt files in this folder.");
            return;
        }

        log.WriteLine($"Джерело (не змінюється) / Source (never modified): {sourceDir}");
        log.WriteLine($"Результат тесту / Test output: {outDir}");
        log.WriteLine();

        foreach (var fontPath in fntFiles)
        {
            string name = Path.GetFileNameWithoutExtension(fontPath);
            string destFntPath = Path.Combine(outDir, name + ".fnt");

            FontAnalysisResult analysis;
            try { analysis = FontAnalyzer.Analyze(fontPath); }
            catch (Exception ex)
            {
                log.WriteLine($"{name,-20} пропущено / skipped ({ex.Message})");
                continue;
            }

            var existingIds = analysis.Records.Select(r => r.ID).ToHashSet();
            var newRecords = new List<GlyphRecord>();

            foreach (var (uaId, donorId) in MissingUaDonorMap)
            {
                if (existingIds.Contains(uaId)) continue;

                var donor = analysis.GetById(donorId) ?? analysis.Records.FirstOrDefault();
                if (donor == null) continue;

                newRecords.Add(new GlyphRecord
                {
                    Padding = donor.Padding,
                    ID = uaId,
                    AtlasX = donor.AtlasX,
                    AtlasY = donor.AtlasY,
                    AtlasW = donor.AtlasW,
                    AtlasH = donor.AtlasH,
                    XOffset = donor.XOffset,
                    YOffset = donor.YOffset,
                    XAdvance = donor.XAdvance
                });
            }

            // UA: Копіюємо PNG без змін (якщо є) — навіть коли .fnt не потребує
            //     нових слотів, пара файлів у outDir має бути повною.
            // EN: Copy the PNG unchanged (if present) — even when the .fnt
            //     needs no new slots, the pair of files in outDir should be
            //     complete.
            string srcPng = Path.Combine(sourceDir, name + ".png");
            if (File.Exists(srcPng))
                File.Copy(srcPng, Path.Combine(outDir, name + ".png"), overwrite: true);

            if (newRecords.Count == 0)
            {
                File.Copy(fontPath, destFntPath, overwrite: true);
                log.WriteLine($"{name,-20} без змін / unchanged (усі тестові слоти вже присутні / all test slots already present)");
                continue;
            }

            try
            {
                int before = analysis.GlyphCount;
                WriteExpandedFnt(fontPath, destFntPath, analysis, newRecords);
                log.WriteLine($"{name,-20} numChars {before} -> {before + newRecords.Count}  " +
                              $"(stride={analysis.Stride}, +{newRecords.Count}: {string.Join(",", newRecords.Select(r => r.ID))})");
            }
            catch (Exception ex)
            {
                log.WriteLine($"{name,-20} ПОМИЛКА запису / write error: {ex.Message}");
            }
        }

        log.WriteLine();
        log.WriteLine($"Далі вручну / Next, manually: скопіюй файли з {outDir} у гру, запусти її, пройдись меню + місію з укр. текстом.");
        log.WriteLine("  UA: Краш на старті/вході -> рушій чутливий до numChars, слоти НЕ додаємо.");
        log.WriteLine("  EN: Crash on launch/entry -> the engine is sensitive to numChars, do not add slots.");
        log.WriteLine("  UA: Гра йде, текст порожній/битий -> проблема окрема, не в лічильнику.");
        log.WriteLine("  EN: Game runs, text is blank/broken -> a separate issue, not the counter.");
        log.WriteLine("  UA: Все ок, нові літери показуються (як форми донора) -> зростання numChars дозволене.");
        log.WriteLine("  EN: All fine, new letters render (as the donor's shape) -> numChars growth is allowed.");
        log.WriteLine();
        log.WriteLine("UA: Оригінали в original-fonts не чіпались — після перевірки просто поверни в гру");
        log.WriteLine("    файли, які там лежали раніше (жодного відкату через цей інструмент не потрібно).");
        log.WriteLine("EN: Originals in original-fonts were never touched — after testing, just put back");
        log.WriteLine("    whatever was in the game before (no rollback through this tool is needed).");
    }

    // -----------------------------------------------------------------------
    // UA: Дописування нових записів у таблицю (не залежить від stride)
    // EN: Appending new records to the table (stride-agnostic)
    // -----------------------------------------------------------------------

    // UA: Читає .fnt із srcPath (не чіпаючи його), дописує newRecords до вже
    //     наявних analysis.Records, патчить заголовковий лічильник кількості
    //     гліфів і пише результат у destPath. Формат кожного запису (28/32/36
    //     байт, з/без padding-префікса, з/без XAdvance) береться з analysis —
    //     жодних припущень про фіксовані 32 байти.
    // EN: Reads the .fnt from srcPath (without touching it), appends
    //     newRecords to the already-existing analysis.Records, patches the
    //     header's glyph-count field, and writes the result to destPath. Each
    //     record's layout (28/32/36 bytes, with/without a padding prefix,
    //     with/without XAdvance) is taken from analysis — no assumption of a
    //     fixed 32 bytes anywhere.
    private static void WriteExpandedFnt(string srcPath, string destPath, FontAnalysisResult analysis, List<GlyphRecord> newRecords)
    {
        byte[] original = File.ReadAllBytes(srcPath);

        int oldCount = analysis.GlyphCount;
        int tableStart = analysis.TableStart;
        int stride = analysis.Stride;
        int footerStart = tableStart + oldCount * stride;
        if (footerStart > original.Length)
            throw new InvalidDataException("footerStart виходить за межі файлу — результат аналізу недостовірний / footerStart is out of file bounds — the analysis result is unreliable");

        byte[] footer = original[footerStart..];
        byte[] header = original[..tableStart];
        PatchDeclaredCount(header, tableStart, oldCount + newRecords.Count);

        var allRecords = new List<GlyphRecord>(analysis.Records);
        allRecords.AddRange(newRecords);
        allRecords.Sort((a, b) => a.ID.CompareTo(b.ID));

        using var ms = new MemoryStream();
        ms.Write(header, 0, header.Length);
        foreach (var r in allRecords)
            ms.Write(SerializeRecord(r, stride, analysis.HasPaddingPrefix, analysis.HasXAdvance));
        ms.Write(footer, 0, footer.Length);

        File.WriteAllBytes(destPath, ms.ToArray());
    }

    // UA: Патчить int16-поле "заявлена кількість гліфів", яке в стандартному
    //     заголовку bfnt лежить за 2 байти до початку таблиці (charsStart-2,
    //     де charsStart = 12 + strLen(@10) + 2). Звіряє це з TableStart із
    //     FontAnalyzer і попереджає, якщо вони розійшлися — саме такий
    //     конфлікт свого часу виявив баг у FontAnalyzer.FindTableStart
    //     (див. коментар у тому файлі).
    // EN: Patches the int16 "declared glyph count" field, which in the
    //     standard bfnt header sits 2 bytes before the table start
    //     (charsStart-2, where charsStart = 12 + strLen(@10) + 2). Cross-
    //     checks this against FontAnalyzer's TableStart and warns if they
    //     disagree — this exact mismatch is what originally surfaced the bug
    //     in FontAnalyzer.FindTableStart (see the comment in that file).
    private static void PatchDeclaredCount(byte[] header, int tableStart, int newCount)
    {
        if (header.Length >= 12)
        {
            short strLen = BitConverter.ToInt16(header, 10);
            int expectedCharsStart = 12 + strLen + 2;
            if (expectedCharsStart != tableStart)
            {
                Console.WriteLine(
                    $"   [!] UA: charsStart за strLen ({expectedCharsStart}) != TableStart із FontAnalyzer ({tableStart}) " +
                    "— формат заголовка цього файлу варто звірити вручну перед довірою до результату.");
                Console.WriteLine(
                    $"   [!] EN: charsStart from strLen ({expectedCharsStart}) != TableStart from FontAnalyzer ({tableStart}) " +
                    "— this file's header format is worth double-checking manually before trusting the result.");
            }
        }

        if (tableStart < 2)
            throw new InvalidDataException("tableStart замалий для патчу лічильника гліфів / tableStart is too small to patch the glyph counter");

        var bytes = BitConverter.GetBytes((short)newCount);
        header[tableStart - 2] = bytes[0];
        header[tableStart - 1] = bytes[1];
    }

    private static byte[] SerializeRecord(GlyphRecord r, int stride, bool hasPadding, bool hasXAdvance)
    {
        byte[] b = new byte[stride];
        int idOff = hasPadding ? 4 : 0;

        if (hasPadding)
            Buffer.BlockCopy(BitConverter.GetBytes(r.Padding), 0, b, 0, 4);

        Buffer.BlockCopy(BitConverter.GetBytes(r.ID), 0, b, idOff, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(r.AtlasX), 0, b, idOff + 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(r.AtlasY), 0, b, idOff + 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(r.AtlasW), 0, b, idOff + 12, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(r.AtlasH), 0, b, idOff + 16, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(r.XOffset), 0, b, idOff + 20, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(r.YOffset), 0, b, idOff + 24, 4);

        if (hasXAdvance)
            Buffer.BlockCopy(BitConverter.GetBytes(r.XAdvance), 0, b, idOff + 28, 4);

        return b;
    }
}

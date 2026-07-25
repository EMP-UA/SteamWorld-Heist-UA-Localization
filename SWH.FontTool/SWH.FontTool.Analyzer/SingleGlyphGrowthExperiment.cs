// =============================================================================
// SWH.FontTool.Analyzer — SingleGlyphGrowthExperiment.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Контекст (2026-07-21): повний перепак атласу під усі 66 літер
//     (FontGenerator.PlanAllUaSlots + AtlasProcessor.GenerateUaPng) дав у грі
//     видимий брак — верх кожної літери сірий/наче обрізаний, низ чіткий,
//     причому однаково на КОЖНІЙ літері. Версія "рушій використовує
//     фіксовану 'висоту рядка' із заголовка .fnt (там є константа =32)"
//     СПРОСТОВАНА: ця константа однакова у ВСІХ 12 шрифтів, включно з дуже
//     різними за розміром (body_xsmall і header_medium) — якби вона
//     справді визначала висоту показу гліфа, вона б відрізнялась.
//
//     PngCanvasExperiment раніше довів: сам факт БІЛЬШОГО полотна PNG
//     (без жодного нового гліфа в новому просторі) рушій приймає нормально.
//     Але жодного НОВОГО контенту в тому новому просторі там не було —
//     а саме це й лишилось неперевіреним припущенням у повному перепаку.
//
//     Цей клас ізолює РІВНО ОДНУ змінну: чи коректно рушій показує
//     контент, розміщений у щойно доданому (за межами старого розміру
//     текстури) просторі PNG — додаючи ОДНУ нову літеру (і) в НОВОМУ місці,
//     і не чіпаючи жодного з інших 65+ гліфів шрифту (їхня позиція,
//     розмір, ID — усе лишається байт-в-байт як в оригіналі). Якщо
//     артефакт "сірий верх" з'явиться навіть для цієї єдиної літери —
//     проблема в самому факті розміщення за межами старих кордонів
//     текстури (чи то у математиці геометрії, чи то в чомусь, про
//     що рушій судить інакше, ніж припускається). Якщо ні — проблема
//     специфічна для повного перепаку (сортування, кількість одночасних
//     змін, чи щось інше).
// EN: Context (2026-07-21): the full atlas repack for all 66 letters
//     (FontGenerator.PlanAllUaSlots + AtlasProcessor.GenerateUaPng) produced
//     a visible defect in-game — the top of every letter looks gray/clipped,
//     the bottom crisp, uniformly on EVERY letter. The theory "the engine
//     uses a fixed 'line height' from the .fnt header (there's a constant
//     =32 there)" has been DISPROVEN: that constant is identical across ALL
//     12 fonts, including very differently sized ones (body_xsmall and
//     header_medium) — if it really determined the glyph's display height,
//     it would differ.
//
//     PngCanvasExperiment earlier proved: the mere fact of a LARGER PNG
//     canvas (with no new glyph in the new space) is accepted fine by the
//     engine. But no NEW content in that new space existed there — and
//     that's exactly the untested assumption in the full repack.
//
//     This class isolates EXACTLY ONE variable: does the engine correctly
//     display content placed in the newly added (beyond the old texture
//     size) PNG space — by adding ONE new letter (і) in a NEW spot, without
//     touching any of the font's other 65+ glyphs (their position, size, ID
//     — all stay byte-for-byte identical to the original). If the "gray
//     top" artifact shows up even for this single letter — the problem is
//     in the very fact of placing content beyond the old texture bounds
//     (whether in the geometry math, or in something the engine judges
//     differently than assumed). If not — the problem is specific to the
//     full repack (sorting, the number of simultaneous changes, or
//     something else).
// =============================================================================

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SWH.FontTool.Core;
using IOPath = System.IO.Path;

namespace SWH.FontTool.Analyzer;

public static class SingleGlyphGrowthExperiment
{
    // UA: 'э' (RU-унікальний, напевно присутній) -> 'і' (UA-унікальний,
    //     якого немає в оригіналі) — та сама пара, що вже в реальному
    //     AlphabetProcessor.RuUniqueToUa.
    // EN: 'э' (RU-unique, almost certainly present) -> 'і' (UA-unique,
    //     absent from the original) — the same pair already used in the
    //     real AlphabetProcessor.RuUniqueToUa.
    private const int DonorId = 1101; // э
    private const int UaId = 1110;    // і

    public static void RunTest(string sourceDir, string outDir, TextWriter log)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Папка не знайдена / Folder not found: {sourceDir}");

        Directory.CreateDirectory(outDir);
        var fntFiles = Directory.GetFiles(sourceDir, "*.fnt");

        log.WriteLine($"Джерело (не змінюється) / Source (never modified): {sourceDir}");
        log.WriteLine($"Результат тесту / Test output: {outDir}");
        log.WriteLine($"Донор -> нова літера / Donor -> new letter: {DonorId} ('э') -> {UaId} ('і')");
        log.WriteLine();

        foreach (var fontPath in fntFiles)
        {
            string name = IOPath.GetFileNameWithoutExtension(fontPath);
            string srcPng = IOPath.Combine(sourceDir, name + ".png");

            if (!File.Exists(srcPng))
            {
                log.WriteLine($"{name,-20} пропущено / skipped (немає PNG / no PNG)");
                continue;
            }

            FontAnalysisResult analysis;
            try { analysis = FontAnalyzer.Analyze(fontPath); }
            catch (Exception ex)
            {
                log.WriteLine($"{name,-20} пропущено / skipped ({ex.Message})");
                continue;
            }

            if (analysis.GetById(UaId) != null)
            {
                log.WriteLine($"{name,-20} пропущено / skipped ('і' вже є в оригіналі / 'і' already exists)");
                continue;
            }

            var donor = analysis.GetById(DonorId);
            if (donor == null)
            {
                log.WriteLine($"{name,-20} пропущено / skipped (немає донора 'э' / no 'э' donor)");
                continue;
            }

            // UA: Один-єдиний запис у плані — FontGenerator.PlanAllUaSlots
            //     порахує геометрію РІВНО для цієї однієї літери, точно тим
            //     самим алгоритмом (реальні TTF-метри, нове місце знизу),
            //     що й повний перепак.
            // EN: A single-entry plan — FontGenerator.PlanAllUaSlots will
            //     compute geometry for EXACTLY this one letter, using the
            //     exact same algorithm (real TTF metrics, fresh space at the
            //     bottom) as the full repack.
            var plan = new Dictionary<int, int> { { DonorId, UaId } };

            // UA: PlanAllUaSlots тепер приймає завантажене зображення (не
            //     лише Image.Identify) — потрібне для растрового виміру
            //     бази/розміру. Тут це лише для приблизного AtlasX/AtlasW;
            //     RenderAndCorrect нижче все одно перераховує геометрію сам.
            // EN: PlanAllUaSlots now takes a loaded image (not just
            //     Image.Identify) — needed for the raster baseline/size
            //     measurement. Here it's only for a rough AtlasX/AtlasW;
            //     RenderAndCorrect below recomputes the geometry itself anyway.
            Dictionary<int, GlyphRecord> allSlots;
            int newHeight;
            using (var planImg = Image.Load<Rgba32>(srcPng))
                (allSlots, newHeight) = FontGenerator.PlanAllUaSlots(analysis, plan, planImg, planImg.Height);

            if (!allSlots.TryGetValue(UaId, out var slot))
            {
                log.WriteLine($"{name,-20} пропущено / skipped (не вдалось порахувати геометрію / could not compute geometry)");
                continue;
            }

            // UA: Патчимо РІВНО один запис (той самий byte-слот донора 'э'),
            //     решта файлу лишається байт-в-байт як в оригіналі.
            // EN: Patch EXACTLY one record (the same byte slot as the 'э'
            //     donor), the rest of the file stays byte-for-byte identical
            //     to the original.
            // UA: НОВЕ (2026-07-21): спершу малюємо PNG і ПІКСЕЛЬНО заміряємо
            //     реальну базову лінію намальованого гліфа — і рахуємо
            //     геометрію (AtlasY/AtlasH/YOffset) від НЕЇ, а не від
            //     line-ascender метрики шрифту. Підтверджено прямими даними:
            //     інваріант YOff + (ink_bottom - AtlasY) у ВСІХ наявних літер
            //     дорівнює сталій шрифту (BaselineConstant ≈ 50/29/32), а стара
            //     формула на HorizontalMetrics.Ascender давала YOffset на
            //     13–18 px менший — саме тому 'і' "левітувала".
            // EN: NEW (2026-07-21): first draw the PNG and PIXEL-measure the
            //     drawn glyph's real baseline — then compute geometry
            //     (AtlasY/AtlasH/YOffset) from THAT, not from the font's
            //     line-ascender metric. Confirmed directly by data: the
            //     invariant YOff + (ink_bottom - AtlasY) equals the font
            //     constant (BaselineConstant ≈ 50/29/32) for EVERY existing
            //     letter, while the old HorizontalMetrics.Ascender-based
            //     formula produced a YOffset 13–18 px too small — exactly why
            //     'і' "floated".
            GlyphRecord corrected = RenderAndCorrect(name, srcPng, outDir, analysis, slot, newHeight);

            byte[] outData = File.ReadAllBytes(fontPath);
            for (int i = 0; i < analysis.GlyphCount; i++)
            {
                if (analysis.Records[i].ID != DonorId) continue;
                int pos = analysis.TableStart + i * analysis.Stride;
                WriteRecord(outData, pos, corrected, analysis.IdOffset, analysis.HasXAdvance, analysis.HasPaddingPrefix);
                break;
            }

            // UA: КРИТИЧНО (знайдено після реального тесту в грі, 2026-07-21):
            //     запис донора 'э'(1101) фізично сидів у таблиці МІЖ 'ъ'(1100)
            //     і 'ю'(1102)/'я'(1103) — саме там, де сортування за ID його й
            //     лишає. Змінивши тільки ID на 1110 ('і') і НЕ пересортувавши
            //     таблицю, я лишив запис із ID=1110 фізично СЕРЕД записів із
            //     меншими ID (1100, 1102, 1103) — таблиця більше не зростає
            //     монотонно. Рушій, судячи з усього, покладається на
            //     впорядкованість за ID (можливо, бінарний пошук): один-єдиний
            //     "вибитий" запис ламає пошук не лише для нього самого
            //     (звідси "і" бере не ті координати — "левітує"), а й для
            //     сусідніх ID ПІСЛЯ нього в таблиці (звідси зникли ю, я і
            //     "кілька інших"). Реальний FontGenerator.PatchFnt це вже
            //     враховує (викликає SortGlyphTable) — тут пропустив, і саме
            //     тому цей ізольований тест сам зламав гру.
            // EN: CRITICAL (found after a real in-game test, 2026-07-21): the
            //     'э'(1101) donor record physically sat in the table BETWEEN
            //     'ъ'(1100) and 'ю'(1102)/'я'(1103) — exactly where ID-sorted
            //     order leaves it. By only changing the ID to 1110 ('і') and
            //     NOT re-sorting the table, I left a record with ID=1110
            //     physically SITTING AMONG records with smaller IDs (1100,
            //     1102, 1103) — the table no longer increases monotonically.
            //     The engine apparently relies on ID ordering (quite possibly
            //     a binary search): a single "out of place" record breaks
            //     lookup not just for itself (hence 'і' picking up the wrong
            //     coordinates — "floating"), but for neighboring IDs AFTER it
            //     in the table too (hence ю, я, and "a few others" vanishing).
            //     The real FontGenerator.PatchFnt already accounts for this
            //     (it calls SortGlyphTable) — this test skipped it, which is
            //     exactly why this isolated test broke the game on its own.
            SortGlyphTable(outData, analysis);

            string destFnt = IOPath.Combine(outDir, name + ".fnt");
            File.WriteAllBytes(destFnt, outData);

            log.WriteLine($"{name,-20} 'і' у [{corrected.AtlasX},{corrected.AtlasY} {corrected.AtlasW}x{corrected.AtlasH}] " +
                          $"YOff {slot.YOffset:F1} -> {corrected.YOffset:F1} (виправлено за пікселями / pixel-corrected), висота PNG / PNG height: {newHeight}");
        }

        log.WriteLine();
        log.WriteLine($"Далі вручну / Next, manually: скопіюй файли з {outDir} у гру, запусти, знайди екран із текстом,");
        log.WriteLine("де раніше бракувало 'і', і подивись саме на цю літеру (і на решту тексту поруч).");
        log.WriteLine("  UA: Так само сірий верх/обрізання ТІЛЬКИ у нової 'і', а решта тексту чітка ->");
        log.WriteLine("      проблема саме в розміщенні за межами старих кордонів текстури.");
        log.WriteLine("  EN: The same gray top/clipping ONLY on the new 'і', rest of the text crisp ->");
        log.WriteLine("      the problem is specifically about placement beyond the old texture bounds.");
        log.WriteLine("  UA: 'і' виглядає чисто, увесь інший текст теж чистий (як і мав бути, адже його");
        log.WriteLine("      не чіпали) -> проблема специфічна саме для повного перепаку 66 літер.");
        log.WriteLine("  EN: 'і' looks clean, and every other letter is clean too (as expected, since it");
        log.WriteLine("      wasn't touched) -> the problem is specific to the full 66-letter repack.");
    }

    /// <summary>UA: Пересортовує записи в таблиці гліфів за ID — та сама логіка, що у FontGenerator.SortGlyphTable. Обов'язково після зміни ID будь-якого запису: рушій, судячи з усього, покладається на зростаючий порядок. / EN: Re-sorts the glyph table's records by ID — the same logic as FontGenerator.SortGlyphTable. Mandatory after changing any record's ID: the engine apparently relies on ascending order.</summary>
    private static void SortGlyphTable(byte[] data, FontAnalysisResult analysis)
    {
        int stride = analysis.Stride;
        int start = analysis.TableStart;
        int count = analysis.GlyphCount;
        int idOff = analysis.IdOffset;

        var records = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            records[i] = new byte[stride];
            Array.Copy(data, start + i * stride, records[i], 0, stride);
        }

        Array.Sort(records, (a, b) => BitConverter.ToInt32(a, idOff).CompareTo(BitConverter.ToInt32(b, idOff)));

        for (int i = 0; i < count; i++)
            Array.Copy(records[i], 0, data, start + i * stride, stride);
    }

    /// <summary>UA: Записує один GlyphRecord у байти — та сама логіка, що у FontGenerator.WriteRecord. / EN: Writes a single GlyphRecord into bytes — the same logic as FontGenerator.WriteRecord.</summary>
    private static void WriteRecord(byte[] data, int pos, GlyphRecord r, int idOff, bool hasXAdv, bool hasPad)
    {
        if (hasPad) System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(pos, 4), r.Padding);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(pos + idOff, 4), r.ID);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos + idOff + 4, 4), r.AtlasX);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos + idOff + 8, 4), r.AtlasY);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos + idOff + 12, 4), r.AtlasW);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos + idOff + 16, 4), r.AtlasH);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos + idOff + 20, 4), r.XOffset);
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(pos + idOff + 24, 4), r.YOffset);
        if (hasXAdv) System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(pos + idOff + 28, 4), r.XAdvance);
    }

    private const int PadTop = 2;
    private const int PadBottom = 2;

    /// <summary>
    /// UA: Малює РІВНО одну нову літеру у вирощене полотно ТА повертає
    /// виправлену геометрію запису. Ключова відмінність від старого підходу:
    /// вертикальне розміщення рахується НЕ з line-ascender метрики шрифту
    /// (HorizontalMetrics.Ascender — це метрика висоти РЯДКА, значно більша за
    /// реальну висоту чорнила літери), а з ПІКСЕЛЬНОГО заміру реальної базової
    /// лінії. Базова лінія визначається за референсною літерою без нижнього
    /// виносу ('н'/'Н'), намальованою тим самим пером і шрифтом: низ її чорнила
    /// = базова лінія. Далі AtlasY/AtlasH/YOffset рахуються так, щоб виконувався
    /// інваріант усіх наявних гліфів: YOffset + (baseline - AtlasY) == BaselineConstant.
    /// EN: Draws EXACTLY one new letter into the grown canvas AND returns the
    /// corrected record geometry. The key difference from the old approach:
    /// vertical placement is computed NOT from the font's line-ascender metric
    /// (HorizontalMetrics.Ascender is a LINE-height metric, much larger than the
    /// letter's actual ink height) but from a PIXEL measurement of the real
    /// baseline. The baseline is found from a no-descender reference letter
    /// ('н'/'Н') drawn with the same pen and font: the bottom of its ink = the
    /// baseline. AtlasY/AtlasH/YOffset are then computed so the invariant every
    /// existing glyph obeys holds: YOffset + (baseline - AtlasY) == BaselineConstant.
    /// </summary>
    private static GlyphRecord RenderAndCorrect(string fontName, string srcPng, string outDir, FontAnalysisResult analysis, GlyphRecord slot, int newHeight)
    {
        FontFamily? loaded = AtlasProcessor.LoadTtfFont(fontName);
        using var original = Image.Load<Rgba32>(srcPng);

        // UA: Якщо TTF недоступний — просто копіюємо оригінал і лишаємо слот як є.
        // EN: If the TTF is unavailable — just copy the original and keep the slot.
        if (loaded is null)
        {
            Directory.CreateDirectory(outDir);
            original.Save(IOPath.Combine(outDir, fontName + ".png"));
            return slot;
        }
        FontFamily family = loaded.Value;

        var refUpper = analysis.GetById(1040) ?? analysis.GetById(72);
        var refLower = analysis.GetById(1072) ?? analysis.GetById(97);
        if (refUpper is null || refLower is null)
        {
            Directory.CreateDirectory(outDir);
            original.Save(IOPath.Combine(outDir, fontName + ".png"));
            return slot;
        }

        // UA: КЛЮЧОВЕ (2026-07-21, підтверджено консоллю гри): раніше і розмір,
        //     і базову лінію брали з analysis.BaselineConstant. Але це
        //     ЕВРИСТИКА — медіана (YOffset + AtlasH*0.80) по латинських
        //     великих — і для цих шрифтів вона занижена на ~6 px відносно
        //     РЕАЛЬНОЇ базової лінії в пікселях (напр. header_medium: константа
        //     43.6, а справжня базова 50). Через це нова 'і' виходила і надто
        //     мала, і надто висока. Тому міряємо ПРЯМО з пікселів наявних
        //     референс-літер в оригінальному атласі: висоту тіла (розмір
        //     шрифту) і справжню відстань від верху рядка до базової лінії.
        // EN: KEY (2026-07-21, confirmed by the game console): previously both
        //     the size and the baseline came from analysis.BaselineConstant.
        //     But that's a HEURISTIC — the median of (YOffset + AtlasH*0.80)
        //     over Latin capitals — and for these fonts it underestimates the
        //     REAL pixel baseline by ~6 px (e.g. header_medium: constant 43.6,
        //     real baseline 50). That made the new 'і' both too small and too
        //     high. So the measurement is taken DIRECTLY from the pixels of existing
        //     reference letters in the original atlas: the body height (font
        //     size) and the true line-top-to-baseline distance.
        (int Top, int Bottom) RefInk(GlyphRecord r)
        {
            int rx0 = Math.Clamp((int)r.AtlasX, 0, original.Width - 1);
            int ry0 = Math.Clamp((int)r.AtlasY, 0, original.Height - 1);
            int rx1 = Math.Clamp((int)(r.AtlasX + r.AtlasW), 0, original.Width);
            int ry1 = Math.Clamp((int)(r.AtlasY + r.AtlasH), 0, original.Height);
            int t = -1, b = -1;
            for (int y = ry0; y < ry1; y++)
                for (int x = rx0; x < rx1; x++)
                    if (original[x, y].A > 10) { if (t < 0) t = y; b = y; break; }
            return (t, b);
        }

        var luRef = RefInk(refLower);
        var upRef = RefInk(refUpper);

        // UA: Справжня базова лінія = YOffset нижньої референс-літери + відстань
        //     від її AtlasY до реального низу її чорнила (вона без виносу, тож
        //     низ чорнила = базова). Fallback на евристику, лише якщо чорнило
        //     не знайдено.
        // EN: The true baseline = the lower reference letter's YOffset + the
        //     distance from its AtlasY to the real bottom of its ink (it has no
        //     descender, so ink bottom = baseline). Fall back to the heuristic
        //     only if no ink found.
        float baseTrue = luRef.Bottom >= 0
            ? refLower.YOffset + (luRef.Bottom - (int)refLower.AtlasY)
            : analysis.BaselineConstant;

        // UA: ВИПРАВЛЕНО (2026-07-21, друга ітерація, підтверджено консоллю
        //     гри): розмір тіла (upperBodyPx/lowerBodyPx) НАВМИСНО лишається
        //     на основі BaselineConstant (векторна логіка, як і в
        //     PlanAllUaSlots), а НЕ на растровій висоті чорнила референсу.
        //     Причина: растрове вимірювання додає 1–3 px розмиття
        //     згладжування (antialiasing bleed) з кожного боку, і це розмиття
        //     відрізняється від шрифту до шрифту — тому 'і' вийшла на ~14%
        //     завеликою саме в factions і header_small (де AA-розмиття
        //     найпомітніше), хоча в інших була майже точною. baseTrue (вище)
        //     і далі рахується з пікселів — це геометрично правильно
        //     (базова лінія — це один Y-рядок, а не висота, тож AA-розмиття
        //     на неї не впливає так само сильно). А розмір шрифту рахуємо, як
        //     і раніше, векторно.
        // EN: FIXED (2026-07-21, second pass, confirmed by the game console):
        //     body size (upperBodyPx/lowerBodyPx) DELIBERATELY stays based on
        //     BaselineConstant (vector logic, matching PlanAllUaSlots), NOT
        //     the reference's rasterized ink height. Reason: the rasterized
        //     measurement adds 1–3 px of antialiasing bleed on each side, and
        //     that bleed varies font-to-font — which is exactly why 'і' came
        //     out ~14% too large specifically in factions and header_small
        //     (where the AA bleed is most visible), while staying nearly
        //     exact in the others. baseTrue (above) still comes from pixels —
        //     that's geometrically sound (the baseline is a single Y row, not
        //     a height, so AA bleed doesn't skew it the same way). Font size
        //     is computed vector-based, same as before.
        // UA: ВИПРАВЛЕНО (2026-07-21, третя ітерація, підтверджено консоллю
        //     гри — "розміри досі не як у інших літер"): дві накопичені
        //     похибки калібрування розміру.
        //     (1) Цільова висота тіла рахувалась від analysis.BaselineConstant
        //         (та сама занижена евристика — для header_medium константа
        //         43.6 замість реальних 50). Замінено на baseTrue (вище) —
        //         справжню, заміряну за пікселями базову лінію.
        //     (2) КЛЮЧОВЕ: коефіцієнт масштабу (capHRatio/xHRatio) рахувався
        //         через TextMeasurer.MeasureBounds — векторний (пошрифтовий)
        //         бокс гліфа. А фінальна висота нової літери міряється
        //         РАСТРОВО (сканування альфа-каналу намальованого пікселя).
        //         Пряма перевірка на реальних TTF (Oswald-Bold, Comfortaa)
        //         через freetype підтвердила: співвідношення і/о ≈ 1.30–1.33x
        //         СТАБІЛЬНЕ на будь-якому розмірі (100pt..15pt) — тобто це не
        //         ефект хінтингу на малих розмірах. Але коли калібрування
        //         (MeasureBounds, векторне) і фінальний замір (растровий скан)
        //         — це ДВА РІЗНІ методи вимірювання, будь-яка систематична
        //         різниця між ними (напр. MeasureBounds рахує інший бокс, ніж
        //         фактично лягає на растр при DrawText) напряму псує
        //         обчислений розмір шрифту. Тому калібрування ТЕПЕР теж
        //         растрове: малюємо саму референс-літеру (той самий 'а'/'А',
        //         що дав lowerBodyPx/upperBodyPx) при розмірі 100pt, міряємо
        //         її реальну растрову висоту чорнила ТИМ САМИМ методом
        //         (сканування альфи), і масштабуємо звідти. Обидва виміри
        //         тепер йдуть через ОДИН і той самий конвеєр рендеру — жодної
        //         розбіжності між "як порахували" і "як намалювали" більше
        //         немає.
        // EN: FIXED (2026-07-21, third pass, confirmed by the game console —
        //     "sizes still don't match other letters"): two compounding
        //     calibration errors.
        //     (1) The target body height was computed from
        //         analysis.BaselineConstant (the same underestimating
        //         heuristic — 43.6 instead of the real 50 for header_medium).
        //         Replaced with baseTrue (above) — the real, pixel-measured
        //         baseline.
        //     (2) KEY: the scale factor (capHRatio/xHRatio) was computed via
        //         TextMeasurer.MeasureBounds — a vector (font-design) glyph
        //         box. But the new letter's final height is measured
        //         RASTER-side (scanning the alpha channel of the drawn
        //         pixels). A direct check against the real TTFs (Oswald-Bold,
        //         Comfortaa) via freetype confirmed the і/о ratio is a STABLE
        //         ≈1.30–1.33x at every size from 100pt down to 15pt — so this
        //         isn't a small-size hinting effect. But when calibration
        //         (MeasureBounds, vector) and the final measurement (raster
        //         scan) are TWO DIFFERENT methods, any systematic difference
        //         between them (e.g. MeasureBounds computing a different box
        //         than what actually lands on the raster during DrawText)
        //         directly corrupts the computed font size. So calibration is
        //         now ALSO raster-based: draw the reference letter itself
        //         (the same 'а'/'А' that produced lowerBodyPx/upperBodyPx) at
        //         100pt, measure its real raster ink height with the SAME
        //         method (alpha scan), and scale from there. Both
        //         measurements now go through ONE identical render pipeline —
        //         no more gap between "how it was computed" and "how it
        //         actually got drawn".
        // UA: ВИПРАВЛЕНО (2026-07-24, після реального тесту генерації всіх
        //     66 літер — "усі літери завеликі на ~10-15%"): (baseTrue -
        //     YOffset) фактично дорівнює (inkBottom - AtlasY) — тобто
        //     включає верхній запас ОРИГІНАЛЬНОГО боксу (~3px) як частину
        //     "висоти тіла". Замінено на ТІСНУ висоту чорнила референсу
        //     (inkBottom - inkTop + 1).
        // EN: FIXED (2026-07-24, after a real full-66-letter generation test
        //     — "every letter is ~10-15% too big"): (baseTrue - YOffset)
        //     effectively equals (inkBottom - AtlasY) — i.e. it includes the
        //     ORIGINAL box's top margin (~3px) as part of the "body height".
        //     Replaced with the TIGHT ink height of the reference letter
        //     (inkBottom - inkTop + 1).
        float lowerBodyPx = luRef.Bottom >= 0 ? (luRef.Bottom - luRef.Top + 1) : (baseTrue - refLower.YOffset);
        float upperBodyPx = upRef.Bottom >= 0 ? (upRef.Bottom - upRef.Top + 1) : (baseTrue - refUpper.YOffset);

        var testFont = family.CreateFont(100f, FontStyle.Bold);

        int RasterHeight(string s, Font f)
        {
            using var scr = new Image<Rgba32>(300, 400);
            scr.Mutate(c =>
            {
                var rto = new RichTextOptions(f)
                {
                    Origin = new PointF(30f, 300f),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                c.DrawText(rto, s, Brushes.Solid(Color.White));
            });
            int top = -1, bottom = -1;
            for (int y = 0; y < 400; y++)
                for (int x = 0; x < 300; x++)
                    if (scr[x, y].A > 10) { if (top < 0) top = y; bottom = y; break; }
            return bottom >= 0 ? bottom - top + 1 : 0;
        }

        int rasterLowerH100 = RasterHeight(((char)refLower.ID).ToString(), testFont);
        int rasterUpperH100 = RasterHeight(((char)refUpper.ID).ToString(), testFont);

        bool isUpper = char.IsUpper((char)UaId);
        Font font = isUpper
            ? family.CreateFont(rasterUpperH100 > 0 ? 100f * upperBodyPx / rasterUpperH100 : upperBodyPx, FontStyle.Bold)
            : family.CreateFont(rasterLowerH100 > 0 ? 100f * lowerBodyPx / rasterLowerH100 : lowerBodyPx, FontStyle.Bold);

        char uaChar = (char)UaId;

        // UA: ВАЖЛИВО (2026-07-21, після реального тесту): попередній підхід
        //     "заміряти базову лінію за окремо намальованою референс-літерою
        //     ('н')" виявився хибним. Причина: RichTextOptions з
        //     VerticalAlignment.Top у ImageSharp вирівнює до пера ВЕРХ ВЛАСНОЇ
        //     рамки КОЖНОГО рядка тексту (content-relative), а не спільну
        //     лінію шрифту. Тому висока літера ('і' зі стеблом+крапкою) і
        //     низька ('н', x-height), намальовані ОКРЕМИМИ викликами DrawText,
        //     сідають на РІЗНІ базові лінії (різниця ~6 px) — саме звідси
        //     лишковий зсув, через який 'і' усе одно "трохи левітувала".
        //
        //     Правильно: НЕ порівнювати з іншою літерою взагалі. Українські
        //     УНІКАЛЬНІ літери (і, ї, є, ґ) — усі БЕЗ нижнього виносу, тобто
        //     низ їхнього чорнила лежить РІВНО на базовій лінії (підтверджено
        //     freetype: bottom(і)=bottom(н)=bottom('o') на спільній базовій).
        //     Отже базова лінія в боксі = PadTop + висота_чорнила, і
        //     YOffset = BaselineConstant - (PadTop + висота_чорнила). Це дає
        //     інваріант YOff + (низ - AtlasY) == BaselineConstant, як у ВСІХ
        //     наявних гліфів.
        // EN: IMPORTANT (2026-07-21, after a real in-game test): the previous
        //     "measure the baseline from a separately drawn reference letter
        //     ('н')" approach was wrong. Reason: RichTextOptions with
        //     VerticalAlignment.Top in ImageSharp aligns each text run's OWN
        //     bounding-box top to the pen (content-relative), not a shared
        //     font line. So a tall glyph ('і', stem+dot) and a short one ('н',
        //     x-height) drawn in SEPARATE DrawText calls land on DIFFERENT
        //     baselines (~6 px apart) — the exact residual that kept 'і'
        //     slightly "floating".
        //
        //     Correct: do NOT compare against another letter at all. The
        //     Ukrainian-UNIQUE letters (і, ї, є, ґ) all have NO descender, so
        //     the bottom of their ink lies EXACTLY on the baseline (confirmed
        //     with freetype: bottom(і)=bottom(н)=bottom('o') share the
        //     baseline). Thus the baseline within the box = PadTop +
        //     ink_height, and YOffset = BaselineConstant - (PadTop +
        //     ink_height). This makes the invariant YOff + (bottom - AtlasY)
        //     == BaselineConstant hold, just like EVERY existing glyph.
        int scratchW = Math.Max(64, (int)Math.Ceiling(slot.AtlasW) + 48);
        const int scratchH = 200;
        const float penX = 12f;
        const float penY = 24f;

        (int Top, int Bottom, int Left, int Right) MeasureInk(string s)
        {
            using var scr = new Image<Rgba32>(scratchW, scratchH);
            scr.Mutate(c =>
            {
                var rto = new RichTextOptions(font)
                {
                    Origin = new PointF(penX, penY),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                c.DrawText(rto, s, Brushes.Solid(Color.White));
            });
            int top = -1, bottom = -1, left = -1, right = -1;
            for (int y = 0; y < scratchH; y++)
                for (int x = 0; x < scratchW; x++)
                    if (scr[x, y].A > 10)
                    {
                        if (top < 0) top = y;
                        bottom = y;
                        if (left < 0 || x < left) left = x;
                        if (x > right) right = x;
                    }
            return (top, bottom, left, right);
        }

        var tgtInk = MeasureInk(uaChar.ToString());

        // UA: Аварійний випадок — нічого не намалювалось: лишаємо слот.
        // EN: Fallback — nothing rendered: keep the slot.
        if (tgtInk.Top < 0)
        {
            int hc0 = Math.Max(newHeight, original.Height);
            using var img0 = new Image<Rgba32>(original.Width, hc0);
            img0.Mutate(ctx => ctx.DrawImage(original, new Point(0, 0), 1f));
            Directory.CreateDirectory(outDir);
            img0.Save(IOPath.Combine(outDir, fontName + ".png"));
            return slot;
        }

        int inkTop = tgtInk.Top;
        int inkBottom = tgtInk.Bottom;
        int inkHeight = inkBottom - inkTop;
        int inkWidth = tgtInk.Right - tgtInk.Left + 1;

        // UA: Виправлена геометрія. Бокс щільно охоплює реальне чорнило
        //     (+padding); базова лінія (для літер без виносу) = низ чорнила.
        // EN: Corrected geometry. The box tightly bounds the real ink
        //     (+padding); the baseline (for no-descender letters) = ink bottom.
        int atlasY = (int)slot.AtlasY;
        int atlasH = PadTop + inkHeight + PadBottom;
        int boxW = Math.Max((int)slot.AtlasW, inkWidth + 2 * PadTop);
        float baselineInBox = PadTop + inkHeight;
        float yOffset = baseTrue - baselineInBox;

        var corrected = new GlyphRecord
        {
            Padding = 0,
            ID = UaId,
            AtlasX = slot.AtlasX,
            AtlasY = atlasY,
            AtlasW = boxW,
            AtlasH = atlasH,
            XOffset = slot.XOffset,
            YOffset = yOffset,
            XAdvance = slot.XAdvance
        };

        // UA: Малюємо у вирощене полотно так, щоб верх чорнила ліг рівно на
        //     AtlasY + PadTop (те саме зміщення, що заклав бокс).
        // EN: Draw into the grown canvas so the ink top lands exactly at
        //     AtlasY + PadTop (the same offset the box assumes).
        int canvasHeight = Math.Max(Math.Max(newHeight, original.Height), atlasY + atlasH + PadBottom);
        using var image = new Image<Rgba32>(original.Width, canvasHeight);
        image.Mutate(ctx => ctx.DrawImage(original, new Point(0, 0), 1f));

        // UA: ВИПРАВЛЕНО (2026-07-21, третя ітерація, підтверджено консоллю
        //     гри — "в інших вона практично прозора"): попередній підхід
        //     семплив колір з ОДНОГО пікселя в центрі боксу 'H' — а це
        //     ненадійно: той конкретний піксель міг випадково потрапити на
        //     згладжений край (низька альфа) чи проміжок штриха, а не на
        //     суцільну серцевину букви. Заміряно напряму: header_small дав
        //     alpha=42 (майже невидимо), body_large — alpha=139 (напівпрозоро),
        //     ingame_small — (188,188,188,255) (не чисто білий). Правильний
        //     підхід — не семплити взагалі: чорнило гри це БІЛИЙ RGB зі змінним
        //     альфа-каналом (вже підтверджено раніше), тому малюємо суцільним
        //     непрозорим білим — рушій сам застосує згладжування на краях через
        //     альфа-канал рендеру шрифту, і саме так намальовані всі оригінальні
        //     гліфи.
        // EN: FIXED (2026-07-21, third pass, confirmed by the game console —
        //     "practically transparent in others"): the previous approach
        //     sampled color from a SINGLE pixel at the center of the 'H' box —
        //     unreliable, since that specific pixel could land on an
        //     antialiased edge (low alpha) or a stroke gap rather than solid
        //     letter core. Measured directly: header_small gave alpha=42
        //     (nearly invisible), body_large — alpha=139 (translucent),
        //     ingame_small — (188,188,188,255) (not pure white). The correct
        //     approach is to not sample at all: the game's ink is WHITE RGB
        //     with a variable alpha channel (already confirmed earlier), so the
        //     drawing uses solid OPAQUE white — the renderer applies its own edge
        //     antialiasing via the font's alpha, exactly like every original
        //     glyph.
        Color glyphColor = Color.White;

        // UA: penY_atlas зміщуємо так, щоб (inkTop у скретчі) відобразився на
        //     (atlasY + PadTop). По горизонталі центруємо чорнило в боксі.
        // EN: Shift penY_atlas so the scratch inkTop maps to (atlasY + PadTop).
        //     Horizontally, center the ink within the box.
        float penYAtlas = (atlasY + PadTop) - (inkTop - penY);
        float penXAtlas = corrected.AtlasX + (boxW / 2f) - (inkWidth / 2f) - (tgtInk.Left - penX);

        image.Mutate(ctx =>
        {
            var rto = new RichTextOptions(font)
            {
                Origin = new PointF(penXAtlas, penYAtlas),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            ctx.DrawText(rto, uaChar.ToString(), Brushes.Solid(glyphColor));
        });

        Directory.CreateDirectory(outDir);
        image.Save(IOPath.Combine(outDir, fontName + ".png"));
        return corrected;
    }
}

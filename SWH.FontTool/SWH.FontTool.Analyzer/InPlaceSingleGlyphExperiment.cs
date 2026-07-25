// =============================================================================
// SWH.FontTool.Analyzer — InPlaceSingleGlyphExperiment.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Контекст (2026-07-21): SingleGlyphGrowthExperiment додав одну нову
//     літеру ('і') за межами старого розміру PNG (у щойно вирощеному
//     просторі знизу) — і в грі вона показалась як крихітна "летюча"
//     мітка. Мінімальна ширина/просування (MinGlyphBoxWidthPx/MinXAdvancePx
//     у FontGenerator) на це ніяк не вплинули — отже, річ не в розмірі
//     гліфа. Пряма перевірка пікселів: для body_medium (оригінал 512×256)
//     нова "і" сиділа на AtlasY=260; 260 mod 256 = 4 — і рівно там, на
//     x≈2, y≈4, у ЖИВОМУ (не зміненому) оригінальному PNG лежить верхній
//     кінчик літери "l". Це збігається один в один з тим, що видно в грі.
//     Висновок: рушій, судячи з усього, рахує координати текстури не за
//     реальним розміром щойно записаного PNG-файлу, а за якимось
//     застарілим/кешованим значенням висоти (можливо, десь поза .fnt і
//     .png — у скомпільованому атласі чи кеші імпорту, який простий
//     перезапис файлу на диску не оновлює).
//
//     Цей клас перевіряє ІНШИЙ підхід: НЕ рости PNG-полотно взагалі, а
//     розмістити нову літеру у вже наявному, невикористаному запасі
//     ВСЕРЕДИНІ старих кордонів текстури (для деяких шрифтів, напр.
//     body_large, реальний контент займає лише ~275 з 512 px висоти — там
//     є багато вже "законного", довіреного простору). Якщо саме тут "і"
//     покажеться коректно — це остаточно підтвердить гіпотезу застарілого
//     розміру текстури і дасть робочий шлях уперед: використовувати вже
//     наявний запас замість росту полотна.
// EN: Context (2026-07-21): SingleGlyphGrowthExperiment added one new
//     letter ('і') beyond the old PNG size (in the freshly grown space
//     below) — and in-game it showed up as a tiny "floating" mark. Minimum
//     width/advance (MinGlyphBoxWidthPx/MinXAdvancePx in FontGenerator) had
//     no effect at all — so it isn't about glyph size. A direct pixel check:
//     for body_medium (original 512×256), the new "і" sat at AtlasY=260;
//     260 mod 256 = 4 — and right there, at x≈2, y≈4, in the LIVE
//     (unmodified) original PNG sits the top tip of the letter "l". This
//     matches exactly what's visible in-game. Conclusion: the engine
//     apparently computes texture coordinates not against the real size of
//     the just-written PNG file, but against some stale/cached height value
//     (possibly somewhere outside the .fnt and .png — a compiled atlas or
//     import cache that a plain file overwrite doesn't refresh).
//
//     This class tests a DIFFERENT approach: don't grow the PNG canvas at
//     all — place the new letter in the already-existing, unused margin
//     WITHIN the old texture bounds (for some fonts, e.g. body_large, real
//     content only occupies ~275 of 512px height — there's plenty of
//     already-"legitimate", trusted space there). If "і" displays correctly
//     here, that finally confirms the stale-texture-size hypothesis and
//     gives a working way forward: use existing margin instead of growing
//     the canvas.
// =============================================================================

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SWH.FontTool.Core;
using IOPath = System.IO.Path;

namespace SWH.FontTool.Analyzer;

public static class InPlaceSingleGlyphExperiment
{
    private const int DonorId = 1101; // э
    private const int UaId = 1110;    // і
    private const int MarginPx = 4;

    public static void RunTest(string sourceDir, string outDir, TextWriter log)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Папка не знайдена / Folder not found: {sourceDir}");

        Directory.CreateDirectory(outDir);
        var fntFiles = Directory.GetFiles(sourceDir, "*.fnt");

        log.WriteLine($"Джерело (не змінюється) / Source (never modified): {sourceDir}");
        log.WriteLine($"Результат тесту / Test output: {outDir}");
        log.WriteLine("Розмір PNG НЕ змінюється взагалі — нова літера йде у вже наявний запас усередині старих кордонів. / " +
                       "PNG size is NOT changed at all — the new letter goes into already-existing margin within the old bounds.");
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
                log.WriteLine($"{name,-20} пропущено / skipped ('і' вже є / already present)");
                continue;
            }

            var donor = analysis.GetById(DonorId);
            if (donor == null)
            {
                log.WriteLine($"{name,-20} пропущено / skipped (немає донора 'э' / no 'э' donor)");
                continue;
            }

            // UA: Image.Load (не лише Identify) — PlanAllUaSlots тепер
            //     вимагає завантажене зображення для растрового виміру
            //     базової лінії/розміру.
            // EN: Image.Load (not just Identify) — PlanAllUaSlots now
            //     requires a loaded image for the raster baseline/size
            //     measurement.
            using var info = Image.Load<Rgba32>(srcPng);

            // UA: Реальна нижня межа вже використаного контенту — усе, що
            //     нижче цього (і все ще в межах info.Height), гарантовано
            //     "законний", довірений простір усередині оригінальних
            //     кордонів текстури.
            // EN: The real bottom edge of already-used content — everything
            //     below it (and still within info.Height) is guaranteed
            //     "legitimate", trusted space inside the original texture
            //     bounds.
            float lowestUsedBottom = analysis.Records
                .Where(r => r.AtlasH > 0 && r.AtlasY >= 0)
                .Select(r => r.AtlasY + r.AtlasH)
                .DefaultIfEmpty(0)
                .Max();

            var plan = new Dictionary<int, int> { { DonorId, UaId } };

            // UA: Передаємо (lowestUsedBottom) замість повної висоти
            //     зображення як "originalImageHeight" — PlanAllUaSlots
            //     почне розкладку саме звідти, а не з самого низу канви.
            // EN: (lowestUsedBottom) is passed instead of the full image
            //     height as "originalImageHeight" — PlanAllUaSlots will
            //     start laying out from there, not from the very bottom of
            //     the canvas.
            var (allSlots, neededHeight) = FontGenerator.PlanAllUaSlots(analysis, plan, info, (int)Math.Ceiling(lowestUsedBottom));

            if (!allSlots.TryGetValue(UaId, out var slot))
            {
                log.WriteLine($"{name,-20} пропущено / skipped (не вдалось порахувати геометрію / could not compute geometry)");
                continue;
            }

            if (neededHeight > info.Height)
            {
                log.WriteLine($"{name,-20} пропущено / skipped (недостатньо вільного запасу всередині старих кордонів: " +
                              $"потрібно {neededHeight}, є {info.Height} / not enough free margin within the old bounds: needs {neededHeight}, has {info.Height})");
                continue;
            }

            byte[] outData = File.ReadAllBytes(fontPath);
            for (int i = 0; i < analysis.GlyphCount; i++)
            {
                if (analysis.Records[i].ID != DonorId) continue;
                int pos = analysis.TableStart + i * analysis.Stride;
                WriteRecord(outData, pos, slot, analysis.IdOffset, analysis.HasXAdvance, analysis.HasPaddingPrefix);
                break;
            }
            SortGlyphTable(outData, analysis);

            string destFnt = IOPath.Combine(outDir, name + ".fnt");
            File.WriteAllBytes(destFnt, outData);

            RenderOnePng(name, srcPng, outDir, analysis, slot);

            log.WriteLine($"{name,-20} 'і' додано В МЕЖАХ старого PNG у [{slot.AtlasX},{slot.AtlasY} {slot.AtlasW}x{slot.AtlasH}] " +
                          $"(використаний контент до Y={lowestUsedBottom:F0} з {info.Height})");
        }

        log.WriteLine();
        log.WriteLine($"Далі вручну / Next, manually: скопіюй файли з {outDir} у гру, перевір той самий екран.");
        log.WriteLine("  UA: 'і' тепер стоїть правильно (не летить) -> підтверджено: рушій використовує застарілий/");
        log.WriteLine("      кешований розмір текстури, і рости полотно вниз небезпечно. Слід працювати тільки в межах");
        log.WriteLine("      наявного вільного запасу всередині оригінальних розмірів.");
        log.WriteLine("  EN: 'і' now sits correctly (not floating) -> confirmed: the engine uses a stale/cached");
        log.WriteLine("      texture size, and growing the canvas downward is unsafe. Work should stay only within the");
        log.WriteLine("      existing free margin inside the original dimensions.");
        log.WriteLine("  UA: Досі летить -> гіпотеза застарілого розміру неправильна, причина в чомусь іншому.");
        log.WriteLine("  EN: Still floating -> the stale-size hypothesis is wrong, the cause is something else.");
    }

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

    /// <summary>UA: Малює нову літеру БЕЗ жодної зміни розміру полотна — пряма правка вже наявного зображення. / EN: Draws the new letter WITHOUT any canvas resize — a direct edit of the already-existing image.</summary>
    private static void RenderOnePng(string fontName, string srcPng, string outDir, FontAnalysisResult analysis, GlyphRecord slot)
    {
        FontFamily? loaded = AtlasProcessor.LoadTtfFont(fontName);
        if (loaded is null) return;
        FontFamily family = loaded.Value;

        using var image = Image.Load<Rgba32>(srcPng);

        var refUpper = analysis.GetById(1040) ?? analysis.GetById(72);
        var refLower = analysis.GetById(1072) ?? analysis.GetById(97);
        if (refUpper is null || refLower is null) return;

        float upperBodyPx = analysis.BaselineConstant - refUpper.YOffset;
        float lowerBodyPx = analysis.BaselineConstant - refLower.YOffset;

        var testFont = family.CreateFont(100f, FontStyle.Bold);
        var opts100 = new TextOptions(testFont);
        float capHRatio = Math.Max(0.01f, TextMeasurer.MeasureBounds("А", opts100).Height / 100f);
        float xHRatio = Math.Max(0.01f, TextMeasurer.MeasureBounds("а", opts100).Height / 100f);

        bool isUpper = char.IsUpper((char)UaId);
        Font font = isUpper
            ? family.CreateFont(upperBodyPx / capHRatio, FontStyle.Bold)
            : family.CreateFont(lowerBodyPx / xHRatio, FontStyle.Bold);
        float ascenderPx = (font.FontMetrics.HorizontalMetrics.Ascender * font.Size) / font.FontMetrics.UnitsPerEm;

        var hRec = analysis.GetById(72);
        Color glyphColor = Color.White;
        if (hRec is not null && hRec.AtlasW > 0 && hRec.AtlasH > 0)
        {
            int cx = Math.Clamp((int)(hRec.AtlasX + hRec.AtlasW / 2f), 0, image.Width - 1);
            int cy = Math.Clamp((int)(hRec.AtlasY + hRec.AtlasH / 2f), 0, image.Height - 1);
            glyphColor = new Color(image[cx, cy]);
        }

        image.Mutate(ctx =>
        {
            char uaChar = (char)UaId;
            float absoluteBaselineY = slot.AtlasY + (analysis.BaselineConstant - slot.YOffset);
            float originY = absoluteBaselineY - ascenderPx;

            var textOptions = new TextOptions(font) { Origin = PointF.Empty, VerticalAlignment = VerticalAlignment.Top };
            var bounds = TextMeasurer.MeasureBounds(uaChar.ToString(), textOptions);
            float originX = slot.AtlasX + (slot.AtlasW / 2f) - (bounds.Width / 2f) - bounds.X;

            var richTextOptions = new RichTextOptions(font)
            {
                Origin = new PointF(originX, originY),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            ctx.DrawText(richTextOptions, uaChar.ToString(), Brushes.Solid(glyphColor));
        });

        Directory.CreateDirectory(outDir);
        image.Save(IOPath.Combine(outDir, fontName + ".png"));
    }
}

// =============================================================================
// SWH.FontTool.Analyzer — PngCanvasExperiment.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Контрольований, ізольований експеримент: чи толерантний рушій
//     SteamWorld Heist до ЗБІЛЬШЕННЯ розміру PNG-текстури шрифту.
//
//     Навіщо це потрібно: зараз генератор (FontGenerator/AtlasProcessor)
//     дає повністю нову, розраховану з реальних метрик TTF геометрію
//     (AtlasX/Y/W/H, XOffset/YOffset/XAdvance) лише 8 UA-унікальним літерам
//     (Є,І,Ї,Ґ і малі) — їм є куди рости, бо вони пишуться у ще вільне місце
//     PNG. Для решти 58 "спільних" кириличних літер (у т.ч. у, р, й, б —
//     літери з нижніми виступами, діакритикою чи високими штрихами) поле
//     геометрії з .fnt НЕ переписується (лишається від оригінального
//     растрового шрифту), перемальовуються лише пікселі — тому
//     специфічні літери іноді доводиться "підганяти" в PNG вручну, замість
//     чесно переписати їхню рамку в .fnt. Розширення цього самого підходу
//     (нова геометрія в свіжому місці) на всі 66 літер вирішило б це, але
//     вимагає значно БІЛЬШЕ вільного місця в PNG, ніж зараз є знизу під
//     існуючим вмістом.
//
//     Цей клас перевіряє ЛИШЕ ОДНЕ, найдешевше і найризикованіше питання
//     окремо від усього іншого: чи можна взагалі збільшити висоту PNG-
//     полотна (додати прозорий простір знизу, не зсуваючи наявний вміст) —
//     без жодних змін у .fnt — і чи рушій після цього нормально завантажить
//     шрифт. Заголовок .fnt (наскільки відомо з аналізу) не зберігає розмір
//     текстури як окреме поле, тому теоретично рушій має довіряти самому
//     PNG-файлу — але це припущення варто перевірити емпірично ПЕРЕД тим,
//     як переписувати весь генератор під повне перепакування атласу.
//
//     original-fonts/ НЕ чіпається: результат пишеться в окрему теку
//     Config.PngResizeExperimentDir біля .exe.
// EN: A controlled, isolated experiment: is the SteamWorld Heist engine
//     tolerant of the font's PNG texture GROWING in size.
//
//     Why this is needed: today the generator (FontGenerator/AtlasProcessor)
//     only gives fully new geometry — computed from real TTF metrics
//     (AtlasX/Y/W/H, XOffset/YOffset/XAdvance) — to the 8 UA-unique letters
//     (Є,І,Ї,Ґ and lowercase); they have room to grow because they're
//     written into still-free PNG space. For the other 58 "shared" Cyrillic
//     letters (including у, р, й, б — letters with descenders, diacritics,
//     or tall strokes) the .fnt geometry fields are NOT rewritten (they stay
//     from the original bitmap font), only the pixels are repainted
//     — so specific letters sometimes have to be "nudged" in the PNG by
//     hand instead of honestly rewriting their box in the .fnt. Extending
//     the same approach (fresh geometry in free space) to all 66 letters
//     would fix this, but it needs considerably MORE free PNG space than
//     currently exists below the existing content.
//
//     This class tests EXACTLY ONE, cheapest and riskiest question,
//     separately from everything else: can the PNG canvas height even be
//     increased at all (adding transparent space at the bottom, without
//     shifting existing content) — with zero changes to the .fnt — and does
//     the engine still load the font normally afterward? As far as the
//     analysis shows, the .fnt header doesn't store the texture size as a
//     separate field, so in theory the engine should trust the PNG file
//     itself — but that assumption is worth verifying empirically BEFORE
//     rewriting the entire generator around a full atlas repack.
//
//     original-fonts/ is NOT touched: the result is written into a separate
//     Config.PngResizeExperimentDir folder next to the .exe.
// =============================================================================

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SWH.FontTool.Analyzer;

public static class PngCanvasExperiment
{
    // UA: Наскільки збільшується висота для тесту. Число довільне — тут
    //     важлива не точна цифра (реальний бюджет місця під 66 літер
    //     буде пораховано окремо, коли/якщо цей тест пройде), а сам факт: чи
    //     рушій взагалі толерантний до зміни розміру текстури.
    // EN: How much the height grows for the test. The number is
    //     arbitrary — what matters here isn't the exact figure (the real
    //     space budget for 66 letters will be computed separately, if/when
    //     this test passes), but the fact itself: is the engine tolerant of
    //     the texture size changing at all.
    public const int TestExtraHeightPx = 256;

    // UA: Застосовує тест до УСІХ пар .png/.fnt у sourceDir (зазвичай
    //     Config.OriginalFontsDir, завжди read-only) і пише результат у
    //     outDir (Config.PngResizeExperimentDir). .fnt копіюється БЕЗ ЖОДНОЇ
    //     зміни — це важливо: тест ізольовано перевіряє тільки одну змінну
    //     (розмір текстури), не змішуючи її з питанням про геометрію гліфів.
    // EN: Applies the test to ALL .png/.fnt pairs in sourceDir (typically
    //     Config.OriginalFontsDir, always read-only) and writes the result
    //     into outDir (Config.PngResizeExperimentDir). The .fnt is copied
    //     WITHOUT ANY changes — this matters: the test isolates exactly one
    //     variable (texture size), without mixing in the separate question
    //     of glyph geometry.
    public static void RunResizeTest(string sourceDir, string outDir, TextWriter log)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Папка не знайдена / Folder not found: {sourceDir}");

        Directory.CreateDirectory(outDir);

        var pngFiles = Directory.GetFiles(sourceDir, "*.png");
        if (pngFiles.Length == 0)
        {
            log.WriteLine("У папці немає .png файлів. / No .png files in this folder.");
            return;
        }

        log.WriteLine($"Джерело (не змінюється) / Source (never modified): {sourceDir}");
        log.WriteLine($"Результат тесту / Test output: {outDir}");
        log.WriteLine($"Додаткова висота на файл / Extra height per file: +{TestExtraHeightPx}px");
        log.WriteLine();

        foreach (var pngPath in pngFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(pngPath);
            string destPng = Path.Combine(outDir, baseName + ".png");
            string srcFnt = Path.Combine(sourceDir, baseName + ".fnt");
            string destFnt = Path.Combine(outDir, baseName + ".fnt");

            try
            {
                using var original = Image.Load<Rgba32>(pngPath);
                int newWidth = original.Width;
                int newHeight = original.Height + TestExtraHeightPx;

                // UA: Нове полотно — прозоре, старий вміст копіюється у
                //     верхній лівий кут (0,0) БЕЗ зсуву, щоб усі наявні
                //     AtlasX/Y з .fnt лишались коректними без жодних змін.
                // EN: The new canvas is transparent; the old content is
                //     copied into the top-left corner (0,0) WITHOUT any
                //     shift, so every existing AtlasX/Y from the .fnt stays
                //     correct with zero changes needed.
                using var enlarged = new Image<Rgba32>(newWidth, newHeight);
                enlarged.Mutate(ctx => ctx.DrawImage(original, new Point(0, 0), 1f));
                enlarged.Save(destPng);

                // UA: .fnt копіюється 1-в-1, жодного байта не чіпаємо.
                // EN: The .fnt is copied 1-to-1, not a single byte touched.
                if (File.Exists(srcFnt))
                    File.Copy(srcFnt, destFnt, overwrite: true);

                log.WriteLine($"{baseName,-24} {original.Width}x{original.Height} -> {newWidth}x{newHeight}");
            }
            catch (Exception ex)
            {
                log.WriteLine($"{baseName,-24} ПОМИЛКА / error: {ex.Message}");
            }
        }

        log.WriteLine();
        log.WriteLine($"Далі вручну / Next, manually: скопіюй файли з {outDir} у гру, запусти її, пройдись меню + місію з укр. текстом.");
        log.WriteLine("  UA: .fnt тут НЕ змінено — усі AtlasX/Y лишились коректними, бо новий");
        log.WriteLine("      простір лише ДОДАНО знизу, нічого не зсунуто й не перезаписано.");
        log.WriteLine("  EN: The .fnt here is UNCHANGED — every AtlasX/Y stays correct, because");
        log.WriteLine("      the new space was only ADDED at the bottom, nothing shifted or rewritten.");
        log.WriteLine();
        log.WriteLine("  UA: Краш/спотворений або відсутній текст -> рушій чутливий до розміру");
        log.WriteLine("      текстури; повне перепакування атласу під усі 66 літер РИЗИКОВАНЕ");
        log.WriteLine("      без додаткового дослідження (можливо, розмір десь захардкожений поза .fnt).");
        log.WriteLine("  EN: Crash/corrupted or missing text -> the engine is sensitive to texture");
        log.WriteLine("      size; fully repacking the atlas for all 66 letters is RISKY without");
        log.WriteLine("      further investigation (the size may be hardcoded somewhere outside .fnt).");
        log.WriteLine();
        log.WriteLine("  UA: Усе виглядає так само, як з оригіналами -> рушій байдужий до розміру");
        log.WriteLine("      текстури; можна проєктувати повний перепак атласу для всіх 66 літер.");
        log.WriteLine("  EN: Everything looks the same as with the originals -> the engine doesn't");
        log.WriteLine("      care about texture size; a full atlas repack for all 66 letters can be");
        log.WriteLine("      designed.");
    }
}

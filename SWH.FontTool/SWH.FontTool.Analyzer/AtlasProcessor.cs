// =============================================================================
// SWH.FontTool.Analyzer — AtlasProcessor.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Рендерер атласів.
//
//     ПЕРЕХІД НА ПОВНЕ ПЕРЕПАКУВАННЯ (2026-07-21): раніше цей клас стирав
//     ВЕСЬ кириличний блок на СТАРИХ позиціях і перемальовував там же —
//     тобто навіть 58 "спільних" літер (а,б,в,у,р,й...) отримували нові
//     TTF-пікселі, втиснуті в стару, невідповідну рамку з оригінального
//     шрифту. Тепер (після FontGenerator.PlanAllUaSlots і
//     емпіричного підтвердження PngCanvasExperiment, що рушій толерантний
//     до вищої текстури) полотно PNG збільшується, а КОЖНА з 66 українських
//     літер малюється у своєму власному, щойно розрахованому місці в
//     новому просторі знизу. Стара ділянка кожного донора лишається
//     недоторканою (мертві, ніким не реферовані пікселі) — це не проблема:
//     жоден активний запис на них більше не вказує.
// EN: The atlas renderer.
//
//     SWITCH TO A FULL REPACK (2026-07-21): this class used to erase the
//     ENTIRE Cyrillic block at its OLD positions and repaint there — meaning
//     even the 58 "shared" letters (а,б,в,у,р,й...) got new TTF pixels
//     squeezed into an old, ill-fitting box from the original font.
//     Now (after FontGenerator.PlanAllUaSlots, and PngCanvasExperiment's
//     empirical confirmation that the engine tolerates a taller texture) the
//     PNG canvas is grown, and EVERY one of the 66 Ukrainian letters is
//     drawn in its own, freshly computed spot in the new space below. Each
//     donor's old area is left untouched (dead pixels nobody references
//     anymore) — that's not a problem: no active record points at them
//     anymore.
// =============================================================================

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SWH.FontTool.Core;
using IOPath = System.IO.Path;

namespace SWH.FontTool.Analyzer;

public static class AtlasProcessor
{
    // UA: Має збігатись з FontGenerator.GlyphBoxPaddingTop — та сама умовність
    //     "верх чорнила лежить на PadTop нижче AtlasY", закладена під час
    //     планування (PlanAllUaSlots), тепер відтворюється тут під час
    //     фінального рендеру.
    // EN: Must match FontGenerator.GlyphBoxPaddingTop — the same "ink top sits
    //     PadTop below AtlasY" convention established during planning
    //     (PlanAllUaSlots) is reproduced here during the final render.
    private const int GlyphBoxPaddingTop = 2;

    /// <summary>
    /// UA: Генерує україномовний PNG-атлас для одного шрифту: збільшує
    /// полотно до newImageHeight (старий вміст копіюється 1-в-1 у верхній
    /// лівий кут, нічого не зсувається — точно як у перевіреному
    /// PngCanvasExperiment), і малює всі 66 UA-літер з allSlots у їхніх
    /// нових місцях. Додатково зберігає дебаг-версію з кольоровими рамками
    /// для візуальної перевірки.
    /// EN: Generates the Ukrainian PNG atlas for a single font: grows the
    /// canvas to newImageHeight (the old content is copied 1-to-1 into the
    /// top-left corner, nothing shifted — exactly like the validated
    /// PngCanvasExperiment), and draws all 66 UA letters from allSlots at
    /// their new positions. Additionally saves a debug version with
    /// color-coded boxes for visual verification.
    /// </summary>
    public static void GenerateUaPng(string fontName, FontAnalysisResult analysis, Dictionary<int, int> plan, Dictionary<int, GlyphRecord> allSlots, int newImageHeight)
    {
        string srcPng = IOPath.Combine(Config.OriginalFontsDir, fontName + ".png");
        string outPng = IOPath.Combine(Config.OutputDir, fontName + ".png");
        string dbgPng = IOPath.Combine(Config.DebugDir, fontName + "_debug.png");

        if (!File.Exists(srcPng)) return;

        FontFamily? loaded = LoadTtfFont(fontName);
        if (loaded is null) return;
        FontFamily family = loaded.Value;

        using var original = Image.Load<Rgba32>(srcPng);

        // UA: ВИПРАВЛЕНО (2026-07-24, синхронізовано з FontGenerator.
        //     PlanAllUaSlots — докладний коментар там): референс — ЛАТИНКА
        //     (гарантовано коректна), не оригінальна кирилиця.
        //     Пряма причина: у шрифтах на Oswald-Bold оригінальна 'а'(1072)
        //     виявилась на 13-17% нижчою за 'a'(97) в ТОМУ Ж файлі —
        //     калібрування по ній успадковувало чужий дефект розміру.
        // EN: FIXED (2026-07-24, synced with FontGenerator.PlanAllUaSlots —
        //     see the detailed comment there): the reference is LATIN
        //     (guaranteed correct), not the original Cyrillic. Direct
        //     cause: in the Oswald-Bold fonts, the original 'а'(1072) turned
        //     out 13-17% shorter than 'a'(97) in the SAME file — calibrating
        //     off it meant inheriting someone else's sizing defect.
        var refUpper = analysis.GetById(72) ?? analysis.GetById(1040); // 'H' (or 'А' as fallback)
        var refLower = analysis.GetById(97) ?? analysis.GetById(1072); // 'a' (or 'а' as fallback)
        if (refUpper is null || refLower is null) return;

        // UA: ВИПРАВЛЕНО (2026-07-21..24): та сама послідовність фіксів, що й
        //     у FontGenerator.PlanAllUaSlots (докладний коментар там) —
        //     піксельна базова лінія замість BaselineConstant, растрове
        //     само-калібрування розміру замість векторного MeasureBounds.
        //     Обидва файли рахують ЦЮ математику незалежно (за усталеною в
        //     проєкті конвенцією — FontGenerator і AtlasProcessor завжди мали
        //     власні копії "математики розмірів"), тому важливо, щоб формули
        //     лишались синхронізованими між файлами.
        // EN: FIXED (2026-07-21..24): the same fix sequence as in
        //     FontGenerator.PlanAllUaSlots (see the detailed comment there) —
        //     a pixel baseline instead of BaselineConstant, raster
        //     self-calibration instead of vector MeasureBounds. Both files
        //     compute THIS math independently (per the project's established
        //     convention — FontGenerator and AtlasProcessor have always kept
        //     their own copies of the "size math"), so it's important the
        //     formulas stay in sync between the two files.
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
        float baseTrue = luRef.Bottom >= 0
            ? refLower.YOffset + (luRef.Bottom - (int)refLower.AtlasY)
            : analysis.BaselineConstant;

        // UA: ВИПРАВЛЕНО (2026-07-24) — та сама помилка калібрування розміру,
        //     що й у FontGenerator.PlanAllUaSlots (докладний коментар там):
        //     ціль мала бути ТІСНОЮ висотою чорнила референс-літери
        //     (inkBottom - inkTop + 1), а не (inkBottom - AtlasY), яка
        //     помилково додавала верхній запас оригінального боксу (~3px) до
        //     "висоти тіла" — це й роздувало кожну нову літеру на ~10-17%.
        // EN: FIXED (2026-07-24) — the same size-calibration bug as in
        //     FontGenerator.PlanAllUaSlots (see the detailed comment there):
        //     the target had to be the TIGHT ink height of the reference
        //     letter (inkBottom - inkTop + 1), not (inkBottom - AtlasY),
        //     which wrongly added the original box's top margin (~3px) into
        //     the "body height" — that's what inflated every new letter by
        //     ~10-17%.
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

        Font lowerFont = family.CreateFont(rasterLowerH100 > 0 ? 100f * lowerBodyPx / rasterLowerH100 : lowerBodyPx, FontStyle.Bold);
        Font upperFont = family.CreateFont(rasterUpperH100 > 0 ? 100f * upperBodyPx / rasterUpperH100 : upperBodyPx, FontStyle.Bold);

        // UA: Стеля висоти ЛИШЕ для ї/Ї = висота великих літер (синхронно з
        //     FontGenerator.PlanAllUaSlots — докладний коментар там). і лишається
        //     природним; ї/Ї у Comfortaa мають зависоку діакритику (ї/cap до 1.27).
        // EN: Height ceiling for ї/Ї ONLY = cap height (in sync with
        //     FontGenerator.PlanAllUaSlots — see the detailed comment there). і
        //     stays natural; ї/Ї in Comfortaa have a too-tall diacritic (ї/cap
        //     up to 1.27).
        Font GlyphFont(int uaId, bool isUpper)
        {
            Font bf = isUpper ? upperFont : lowerFont;
            if (uaId is 1111 or 1031) // ї Ї
            {
                int ceil = (int)Math.Round(upperBodyPx);
                int natural = RasterHeight(((char)uaId).ToString(), bf);
                if (ceil > 0 && natural > ceil)
                    return family.CreateFont(bf.Size * ceil / natural, FontStyle.Bold);
            }
            return bf;
        }

        // UA: Нове, вище полотно — прозоре; старий вміст копіюється в (0,0)
        //     БЕЗ зсуву (так само, як у PngCanvasExperiment, чию безпечність
        //     уже підтверджено запуском у грі).
        // EN: A new, taller canvas — transparent; the old content is copied
        //     into (0,0) WITHOUT any shift (exactly like PngCanvasExperiment,
        //     whose safety has already been confirmed by launching the game).
        int canvasHeight = Math.Max(newImageHeight, original.Height);
        using var image = new Image<Rgba32>(original.Width, canvasHeight);
        image.Mutate(ctx => ctx.DrawImage(original, new Point(0, 0), 1f));

        foreach (var uaId in plan.Values.Where(AlphabetProcessor.IsUa).Distinct())
        {
            if (!allSlots.TryGetValue(uaId, out var slot)) continue;
            if (slot.AtlasW <= 0 || slot.AtlasH <= 0) continue;

            bool isUpper = char.IsUpper((char)uaId);
            Font font = GlyphFont(uaId, isUpper);

            RenderGlyphSolo(image, slot, (char)uaId, font, fontName);
        }

        using var debugImg = image.Clone();
        debugImg.Mutate(ctx =>
        {
            // UA: Синім — недоторкана ASCII-латиниця (для орієнтиру),
            //     лаймовим — усі нові UA-слоти.
            // EN: Blue — the untouched ASCII Latin block (for reference),
            //     lime — every new UA slot.
            foreach (var r in analysis.Records.Where(r => r.IsAsciiPrintable))
            {
                if (r.AtlasW <= 0 || r.AtlasH <= 0) continue;
                ctx.Draw(Color.Blue, 1f, new RectangleF(r.AtlasX, r.AtlasY, r.AtlasW, r.AtlasH));
            }
            foreach (var s in allSlots.Values)
                ctx.Draw(Color.Lime, 1.5f, new RectangleF(s.AtlasX, s.AtlasY, s.AtlasW, s.AtlasH));
        });

        Directory.CreateDirectory(IOPath.GetDirectoryName(outPng)!);
        image.Save(outPng);
        debugImg.Save(dbgPng);
    }

    /// <summary>
    /// UA: ВИПРАВЛЕНО (2026-07-21..24): малює символ у своєму власному,
    /// ІЗОЛЬОВАНОМУ скретч-полотні (рівно так само, як під час вимірювання в
    /// PlanAllUaSlots — та сама пара DrawText+скан альфи), знаходить ТІСНІ
    /// межі власного чорнила (top/bottom/left/right), і вставляє готовий
    /// растр у фінальний атлас так, щоб верх чорнила ліг РІВНО на
    /// AtlasY+GlyphBoxPaddingTop (та сама умовність, яку заклав
    /// PlanAllUaSlots під час пакування боксів) — по горизонталі чорнило
    /// центрується в боксі. Це замінює стару "один DrawText напряму в атлас
    /// за формулою Ascender" — формула ламалась і для дот-літер (і,ї), і для
    /// виносних (р,у,ф,ц,щ), бо Ascender — загальна константа шрифту, а не
    /// властивість конкретного гліфа.
    ///     Колір — СУЦІЛЬНИЙ непрозорий білий (не семпл із пікселя 'H'):
    ///     семплінг одного пікселя виявився ненадійним (у header_small
    ///     конкретний піксель центру 'H' мав alpha=42 — літера виходила
    ///     "прозорою"). Чорнило гри — біле RGB зі змінним альфа-каналом
    ///     (антиаліасинг рушій формує сам через альфу шрифту).
    /// EN: FIXED (2026-07-21..24): draws the character into its own,
    /// ISOLATED scratch canvas (exactly like during measurement in
    /// PlanAllUaSlots — the same DrawText+alpha-scan pair), finds the TIGHT
    /// bounds of its own ink (top/bottom/left/right), and blits the finished
    /// raster into the final atlas so the ink top lands EXACTLY at
    /// AtlasY+GlyphBoxPaddingTop (the same convention PlanAllUaSlots
    /// established while packing the boxes) — horizontally the ink is
    /// centered within the box. This replaces the old "one DrawText straight
    /// into the atlas via the Ascender formula" — that formula broke for both
    /// dotted letters (і,ї) and descenders (р,у,ф,ц,щ), because Ascender is a
    /// font-wide constant, not a property of the specific glyph.
    ///     Color — SOLID opaque white (not a sample from an 'H' pixel):
    ///     single-pixel sampling turned out unreliable (in header_small the
    ///     specific center pixel of 'H' had alpha=42 — the letter came out
    ///     "transparent"). The game's ink is white RGB with a variable alpha
    ///     channel (the engine forms antialiasing itself via the font alpha).
    /// </summary>
    // UA: ПОТОВЩЕННЯ (faux-bold) ВИДАЛЕНО (2026-07-24). Спочатку його додали на
    //     прохання зробити тонку 'і' помітнішою, але воно лише додавало розміру
    //     й заважало точності (значно товще перо саме й спотворювало і/ї).
    //     Тепер літери малюються рівно тим
    //     кеглем, який калібрований під оригінал, без жодних додаткових проходів.
    // EN: EMBOLDENING (faux-bold) REMOVED (2026-07-24). It was first added to
    //     make the thin 'і' more prominent, but it only added size and hurt
    //     accuracy (a much thicker pen was exactly what was distorting і/ї).
    //     Letters are now drawn at exactly the size
    //     calibrated to the original, with no extra passes.
    // UA: 2026-07-25 — Контурний прохід ТІЛЬКИ для "factions" (див. коментар
    //     у Config.GetFontTtfPath — прямий перегляд пікселів H/I/o з
    //     factions.png підтвердив чорну обводку навколо білого заповнення в
    //     оригіналі). Товщина обведення виміряна напряму зі зрізу пікселів
    //     'I' (горизонтальний рядок посеред штриха: ~2px чорного з кожного
    //     боку між прозорим тлом і білим заповненням) — тому FactionsOutlinePx
    //     = 2, а не вигадане число. Реалізація — "бідняцький контур": кілька
    //     чорних проходів DrawText зі зсувом по колу під основним білим —
    //     той самий технічний прийом, що й у видаленому потовщенні (faux-
    //     bold) для і/ї, але тут це НЕ хак для виправлення дефекту, а
    //     свідоме відтворення реальної, підтвердженої прямим піксельним
    //     оглядом деталі дизайну оригіналу, і застосовується виключно до
    //     одного шрифту.
    // EN: 2026-07-25 — Outline pass ONLY for "factions" (see the comment in
    //     Config.GetFontTtfPath — direct pixel inspection of H/I/o from
    //     factions.png confirmed a black outline around the white fill in
    //     the original). The outline thickness was measured directly from an
    //     'I' pixel cross-section (a horizontal row through the mid-stroke:
    //     ~2px of black on each side between the transparent background and
    //     the white fill) — hence FactionsOutlinePx = 2, not a made-up
    //     number. Implementation is a "poor man's outline": several black
    //     DrawText passes offset in a ring, drawn under the main white pass —
    //     the same technique family as the removed faux-bold for і/ї, but
    //     here it is NOT a hack to fix a defect — it deliberately reproduces
    //     a real original design detail confirmed by direct pixel inspection,
    //     and is scoped to exactly one font.
    private const int FactionsOutlinePx = 2;

    private static void RenderGlyphSolo(Image<Rgba32> image, GlyphRecord slot, char uaChar, Font font, string fontName)
    {
        int outlinePx = fontName.Equals("factions", StringComparison.OrdinalIgnoreCase) ? FactionsOutlinePx : 0;
        int scratchW = Math.Max(64, (int)Math.Ceiling(slot.AtlasW) + 48 + outlinePx * 2);
        const int scratchH = 200;
        const float penX = 12f, penY = 24f;

        void DrawGlyphColor(IImageProcessingContext c, float ox, float oy, Color color)
        {
            var rto = new RichTextOptions(font)
            {
                Origin = new PointF(ox, oy),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            c.DrawText(rto, uaChar.ToString(), Brushes.Solid(color));
        }

        void DrawGlyph(IImageProcessingContext c, float ox, float oy)
        {
            if (outlinePx > 0)
            {
                // UA: 8-напрямний контур (кільце зсувів) — чорний прохід під білим.
                // EN: 8-direction outline (a ring of offsets) — a black pass under the white one.
                foreach (var (dx, dy) in new (float, float)[]
                {
                    (-outlinePx, 0), (outlinePx, 0), (0, -outlinePx), (0, outlinePx),
                    (-outlinePx, -outlinePx), (outlinePx, -outlinePx), (-outlinePx, outlinePx), (outlinePx, outlinePx)
                })
                    DrawGlyphColor(c, ox + dx, oy + dy, Color.Black);
            }
            DrawGlyphColor(c, ox, oy, Color.White);
        }

        using var scr = new Image<Rgba32>(scratchW, scratchH);
        scr.Mutate(c => DrawGlyph(c, penX, penY));

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
        if (top < 0) return; // UA: нічого не намальовано / EN: nothing rendered

        int inkW = right - left + 1;
        float penXAtlas = slot.AtlasX + (slot.AtlasW / 2f) - (inkW / 2f) - (left - penX);
        float penYAtlas = (slot.AtlasY + GlyphBoxPaddingTop) - (top - penY);

        image.Mutate(ctx => DrawGlyph(ctx, penXAtlas, penYAtlas));
    }

    /// <summary>UA: Завантажує TTF-шрифт для заданого імені шрифту гри (Config.GetFontTtfPath). / EN: Loads the TTF font for the given game font name (Config.GetFontTtfPath).</summary>
    public static FontFamily? LoadTtfFont(string fontName)
    {
        string path = Config.GetFontTtfPath(fontName);
        if (!File.Exists(path)) return null;
        return new FontCollection().Add(path);
    }
}

// =============================================================================
// SWH.LocEditor.GUI — ThemeManager.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Менеджер теми (світла/темна) та розміру шрифту таблиці. Налаштування
//     зберігаються у JSON поруч із .exe й застосовуються через
//     DynamicResource — жодних перезапусків для зміни вигляду не потрібно.
//
//     ШРИФТИ:
//     • FontBase / FontSm — шрифт ДАНИХ у таблиці (масштабується A-/A+)
//     • FontUI — шрифт ХРОМУ програми (тулбар, фільтри) — ФІКСОВАНИЙ, не
//       залежить від масштабування вмісту (стандартна UX-практика).
//
// EN: Theme (light/dark) and table font-size manager. Settings are
//     persisted to JSON next to the .exe and applied via DynamicResource —
//     no restart needed to change the look.
//
//     FONTS:
//     • FontBase / FontSm — table DATA font (scales with A-/A+)
//     • FontUI — program CHROME font (toolbar, filters) — FIXED, does not
//       change with content scaling (standard UX practice).
// =============================================================================

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SWH.LocEditor.GUI;

public static class ThemeManager
{
    public static bool IsDark { get; private set; } = true;
    public static double FontSize { get; private set; } = 13.0;
    public static int AutoSaveIntervalMinutes { get; private set; } = 5;

    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "locEditor_ui_settings.json");

    // UA: Спільна кольорова палітра бренду EMP_UA (та сама, що й в інших
    //     інструментах локалізації) — фіолетовий акцент продукту плюс
    //     жовто-синій прапор як підпис автора. Не вигадується наново під
    //     кожну гру, щоб усі інструменти виглядали як один набір.
    // EN: Shared EMP_UA brand palette (the same one used across the other
    //     localization tools) — a purple product accent plus the
    //     yellow/blue flag as the author's signature. Not reinvented per
    //     game, so all tools look like one coherent set.
    private static readonly (string Key, Color Dark, Color Light)[] Palette =
    [
        ("BgBase",       Hex("#0D0A14"), Hex("#F8F5FF")),
        // UA: BgSurface ("верх" — шапка, тулбари) — було майже чорним
        //     (#130F1E, ледь відрізнявся від BgBase). За фідбеком зроблено
        //     помітно світлішим і чіткіше фіолетовим, а не чорним.
        // EN: BgSurface (the "top" — header, toolbars) — used to be almost
        //     black (#130F1E, barely different from BgBase). Per feedback,
        //     made noticeably lighter and clearly purple, not black.
        ("BgSurface",    Hex("#221C3A"), Hex("#FFFFFF")),
        ("BgCard",       Hex("#1C1530"), Hex("#EDE5FF")),
        ("BgCardHov",    Hex("#25203F"), Hex("#E0D4FF")),
        ("BdNorm",       Hex("#2E1F5E"), Hex("#C8AAFF")),
        ("BdAcc",        Hex("#4A2F8A"), Hex("#9B70F0")),
        ("TextPrim",     Hex("#EAE0FF"), Hex("#1A0A3D")),
        ("TextSec",      Hex("#B0A0D0"), Hex("#4A3070")),
        ("TextDim",      Hex("#7A60A8"), Hex("#7A60A0")),
        ("Accent",       Hex("#8B5CF6"), Hex("#6B2FD4")),
        ("AccentDark",   Hex("#6D3AD9"), Hex("#5520B0")),
        ("FlagYellow",   Hex("#F5C518"), Hex("#C4960A")),
        ("FlagBlue",     Hex("#0057B8"), Hex("#004A99")),
        ("StatusGreen",  Hex("#2ECC71"), Hex("#1A8A4A")),
        ("StatusRed",    Hex("#E04444"), Hex("#C0392B")),
        ("StatusAmber",  Hex("#E8A020"), Hex("#B07800")),
        ("RowBg",        Hex("#130F1E"), Hex("#FFFFFF")),
        ("RowBgAlt",     Hex("#170D26"), Hex("#F5F0FF")),
        ("RowHov",       Hex("#1F1737"), Hex("#EDE5FF")),
        ("RowSel",       Hex("#372378"), Hex("#D4C0FF")),
        // UA: RowIssue підсилено (було #2E1414 — темний, легко плутався з
        //     RowDupWarn і навіть з RowUntranslatedBg). Проблемні рядки —
        //     ГОЛОВНІШИЙ маркер, ніж дублікат-попередження, тому колір
        //     зроблено помітно світлішим/червонішим ("світло-червоним").
        // EN: RowIssue strengthened (was #2E1414 — dark, easily confused
        //     with RowDupWarn and even RowUntranslatedBg). Issues are a MORE
        //     IMPORTANT marker than the duplicate-inconsistency warning, so
        //     the color was made noticeably lighter/redder ("light red").
        ("RowIssue",     Hex("#5C1B28"), Hex("#FBD0D0")),
        ("RowDupWarn",   Hex("#2E2814"), Hex("#F5EBCB")),
        // UA: Технічні рядки — світло-блакитний (замість "токсичного"
        //     темно-бірюзового #001818, який виглядав як попередження про
        //     небезпеку через поєднання з рамкою). Рамки більше немає —
        //     лише м'яка заливка.
        // EN: Technical rows — light sky-blue (instead of the "toxic" dark
        //     teal #001818, which looked like a hazard warning combined
        //     with the border). No more border — just a soft fill.
        ("RowTechnicalBg", Hex("#15304A"), Hex("#E3F2FB")),
        // UA: Колір ТЕКСТУ примітки "Технічний — не перекладати!" та
        //     легенди. Мусить залежати від теми: світло-блакитний #4FC3F7
        //     читабельний на темній заливці, але на СВІТЛІЙ (#E3F2FB) він
        //     майже зливається — тому для світлої теми береться насичений
        //     темно-бірюзовий. Той самий підхід, що й TechnicalBrush у
        //     BF1LocalizationTool.GUI (dark #4FD9E8 / light #0A6E7D).
        // EN: Color of the "Technical — do not translate!" note TEXT and of
        //     the legend dot. Must be theme-dependent: light blue #4FC3F7
        //     reads fine on the dark fill, but on the LIGHT one (#E3F2FB)
        //     it nearly disappears — so the light theme uses a saturated
        //     dark teal. Same approach as TechnicalBrush in
        //     BF1LocalizationTool.GUI (dark #4FD9E8 / light #0A6E7D).
        ("TechnicalFg",  Hex("#4FC3F7"), Hex("#0A6E8A")),
        // UA: Заливка ВСЬОГО рядка за статусом (не лише лівої рамки, як
        //     було раніше). Перша спроба (дуже малонасичені відтінки) була
        //     ЗАНАДТО слабкою — "Без перекладу" й "Перекладено" виглядали
        //     ОДНАКОВО (фідбек). Тепер відтінки помітно насиченіші й чітко
        //     різні за ВІДТІНКОМ (червоно-винний / зелений / бурштиновий),
        //     але й досі темніші за акцентні кольори кнопок, щоб текст
        //     TextPrim зверху лишався контрастним і читабельним.
        // EN: Fills the WHOLE row by status (not just a left border, as
        //     before). The first attempt (very low-saturation tints) was
        //     TOO weak — "Untranslated" and "Translated" looked THE SAME
        //     (feedback). Now the tints are noticeably more saturated and
        //     clearly different in HUE (wine-red / green / amber), while
        //     still darker/lighter than button accent colors so the
        //     TextPrim text on top stays contrasty and legible.
        ("RowUntranslatedBg", Hex("#3A1620"), Hex("#FBDEDE")),
        ("RowTranslatedBg",   Hex("#123A22"), Hex("#DCF5E3")),
        ("RowModifiedBg",     Hex("#3A2E0C"), Hex("#FBF0C8")),
        ("HdrBg",        Hex("#0F0B18"), Hex("#EDE5FF")),
        ("GridLine",     Hex("#211641"), Hex("#D8CCEE")),
        ("InputBg",      Hex("#0D0A14"), Hex("#FFFFFF")),
        ("InputBd",      Hex("#3A2A72"), Hex("#B090E8")),
    ];

    public static void Toggle() => Apply(!IsDark);
    public static void IncreaseFontSize() => SetFontSize(FontSize + 1.0);
    public static void DecreaseFontSize() => SetFontSize(FontSize - 1.0);

    public static void SetFontSize(double size)
    {
        FontSize = Math.Clamp(size, 10.0, 20.0);
        ApplyFontSizes();
    }

    public static void SetAutoSaveInterval(int minutes) =>
        AutoSaveIntervalMinutes = Math.Max(0, minutes);

    public static void LoadAndApply()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s is not null)
                {
                    IsDark = s.IsDark;
                    FontSize = Math.Clamp(s.FontSize, 10.0, 20.0);
                    AutoSaveIntervalMinutes = Math.Max(0, s.AutoSaveIntervalMinutes);
                }
            }
        }
        catch { /* UA: некоректний файл налаштувань — просто застосовуємо типові / EN: bad settings file — just apply defaults */ }
        Apply(IsDark);
    }

    public static void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new Settings { IsDark = IsDark, FontSize = FontSize, AutoSaveIntervalMinutes = AutoSaveIntervalMinutes },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* UA: не критично, якщо не вдалось зберегти вподобання / EN: not critical if saving preferences fails */ }
    }

    private static void Apply(bool dark)
    {
        IsDark = dark;
        var res = Application.Current.Resources;
        foreach (var (key, darkColor, lightColor) in Palette)
        {
            var brush = new SolidColorBrush(dark ? darkColor : lightColor);
            brush.Freeze();
            res[key] = brush;
        }
        ApplyFontSizes();
    }

    private static void ApplyFontSizes()
    {
        var res = Application.Current.Resources;

        // UA: Шрифт ДАНИХ (масштабується A-/A+)
        // EN: DATA font (scales with A-/A+)
        res["FontBase"] = FontSize;
        res["FontSm"] = Math.Max(FontSize - 1.5, 9.0);
        res["FontTiny"] = Math.Max(FontSize - 2.0, 8.0);

        // UA: Шрифт ХРОМУ інтерфейсу — ФІКСОВАНИЙ, не залежить від масштабу
        //     вмісту (кнопки, фільтри, шапка). Було 12.0 — за фідбеком
        //     ("текст шапки... досі малуватий", "фільтри взагалі
        //     нечитабельні") збільшено до 14.0, і кнопки/шапка отримали
        //     більший Padding (див. App.xaml/MainWindow.xaml) — разом це
        //     дає помітно "щільніший", менш "мілкуватий" інтерфейс.
        // EN: Interface CHROME font — FIXED, independent of content scale
        //     (buttons, filters, header). Was 12.0 — per feedback ("header
        //     text... still too small", "filters are altogether
        //     unreadable") bumped to 14.0, and buttons/header got more
        //     Padding (see App.xaml/MainWindow.xaml) — together this makes
        //     the interface noticeably less "shallow".
        res["FontUI"] = 14.0;
    }

    private static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private sealed class Settings
    {
        public bool IsDark { get; set; } = true;
        public double FontSize { get; set; } = 13.0;
        public int AutoSaveIntervalMinutes { get; set; } = 5;
    }
}

// =============================================================================
// SWH.LocEditor.GUI — App.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================

using System.Windows;
using System.Windows.Threading;
using SWH.LocEditor.GUI.Services;

namespace SWH.LocEditor.GUI;

/// <summary>
/// UA: Точка входу GUI. Завантажує збережену тему/розмір шрифту при
///     старті, зберігає при виході. Логує старт/вихід і будь-який
///     необроблений виняток — щоб у разі проблеми лишався слід у
///     logs/, а не тихий крах без пояснення (запит користувача: "варто
///     також додати логування на випадок проблем").
/// EN: GUI entry point. Loads the saved theme/font size on startup,
///     saves them on exit. Logs startup/exit and any unhandled
///     exception — so a problem leaves a trace in logs/ instead of a
///     silent crash with no explanation (user request: "also worth
///     adding logging for troubleshooting").
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        SimpleLogger.Info("=== SWH.LocEditor starting ===");
        // UA: ВАЖЛИВО — тема застосовується ДО base.OnStartup(e), бо саме
        //     base.OnStartup(e) створює вікно через StartupUri. MainWindow
        //     тепер читає Application.Current.Resources["AccentDark"/"BgCard"]
        //     напряму в конструкторі (SetActiveFilter) — якщо запустити це
        //     ПІСЛЯ створення вікна, перший виклик отримає null (ресурс ще не
        //     доданий) і кнопка фільтра лишиться без кольору до першого кліку.
        // EN: IMPORTANT — the theme is applied BEFORE base.OnStartup(e),
        //     since base.OnStartup(e) is what creates the window via
        //     StartupUri. MainWindow now reads
        //     Application.Current.Resources["AccentDark"/"BgCard"] directly
        //     in its constructor (SetActiveFilter) — running this AFTER the
        //     window is created would make the first call see null (resource
        //     not added yet) and leave the filter button uncolored until the
        //     first click.
        ThemeManager.LoadAndApply();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.SaveSettings();
        SimpleLogger.Info("=== SWH.LocEditor exiting ===");
        base.OnExit(e);
    }

    // UA: Ловить будь-який необроблений виняток з UI-потоку — записує його в
    //     лог з повним стеком і НЕ дає застосунку тихо впасти без сліду.
    //     e.Handled = true, бо для програми-редактора локалізації втратити
    //     сесію без пояснення гірше, ніж показати повідомлення про помилку.
    // EN: Catches any unhandled exception on the UI thread — records it in
    //     the log with the full stack, so the app doesn't silently die
    //     without a trace. e.Handled = true, since for a localization editor
    //     losing the session with no explanation is worse than showing an
    //     error message.
    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SimpleLogger.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            $"UA: Сталася непередбачена помилка (записано в logs/) / EN: An unexpected error occurred (logged to logs/):\n\n{e.Exception.Message}",
            "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

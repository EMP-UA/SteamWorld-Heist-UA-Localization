// =============================================================================
// SWH.LocEditor.GUI — SettingsWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Мінімальне вікно налаштувань — наразі лише інтервал автозбереження.
//     Значення читається/пишеться через ThemeManager, який також зберігає
//     тему й розмір шрифту в одному JSON поруч із .exe.
// EN: Minimal settings window — currently just the autosave interval. The
//     value is read/written through ThemeManager, which also persists the
//     theme and font size in one JSON next to the .exe.
// =============================================================================

using System.Windows;

namespace SWH.LocEditor.GUI;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        IntervalBox.Text = ThemeManager.AutoSaveIntervalMinutes.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text, out int minutes) || minutes < 0)
        {
            MessageBox.Show(
                "UA: Введіть ціле число ≥ 0 / EN: Enter a whole number ≥ 0",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ThemeManager.SetAutoSaveInterval(minutes);
        ThemeManager.SaveSettings();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

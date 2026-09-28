// =============================================================================
// SWH.LocEditor.GUI — ReviewMarkPromptWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Мінімальне вікно-запит одного рядка тексту — потрібне лише для
//     масового проставлення ДОВІЛЬНОГО (не готового +/-/+-) тексту вичитки
//     кільком виділеним рядкам одразу з контекстного меню MainWindow.
// EN: A minimal single-line text prompt window — needed only for bulk-
//     setting an ARBITRARY (not a ready-made +/-/+-) review text on several
//     selected rows at once from MainWindow's context menu.
// =============================================================================

using System.Windows;

namespace SWH.LocEditor.GUI;

public partial class ReviewMarkPromptWindow : Window
{
    /// <summary>
    /// UA: Введений текст — заповнюється лише якщо DialogResult == true.
    /// EN: The entered text — populated only if DialogResult == true.
    /// </summary>
    public string ReviewText { get; private set; } = "";

    public ReviewMarkPromptWindow()
    {
        InitializeComponent();
        TextBox.Focus();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        ReviewText = TextBox.Text ?? "";
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

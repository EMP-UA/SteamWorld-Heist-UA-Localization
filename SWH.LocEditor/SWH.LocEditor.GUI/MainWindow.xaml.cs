// =============================================================================
// SWH.LocEditor.GUI — MainWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Головне вікно редактора локалізації SteamWorld Heist. Читає мовний
//     CSV гри напряму — з .csv.z (без QuickBMS, через SWH.LocEditor.Core) або
//     зі звичайного .csv, зливає review-переклад (TSV), дозволяє редагувати
//     переклад по клітинці з живою валідацією й одним кліком розповсюджувати
//     переклад на всі рядки з ідентичним оригіналом (дублікати).
// EN: Main window of the SteamWorld Heist localization editor. Reads the
//     game's language CSV directly — from .csv.z (no QuickBMS, via
//     SWH.LocEditor.Core) or from a plain .csv, merges review translations
//     (TSV), allows per-cell editing with live validation, and propagates
//     a translation to all rows sharing an identical original (duplicates)
//     with a single click.
// =============================================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SWH.LocEditor.Core;
using SWH.LocEditor.GUI.Models;
using SWH.LocEditor.GUI.Services;

namespace SWH.LocEditor.GUI;

public partial class MainWindow : Window
{
    // ── Теки поруч із .exe / Folders next to the .exe ────────────────────
    //
    // UA: Створюються одразу при старті — так само, як original/review/output
    //     у TextValidator і original-dlc/localized/output у ImpakRepacker.
    //     Користувач може або покласти файли сюди заздалегідь (original/ —
    //     .csv чи .csv.z, review/ — TSV з Google Таблиць), або просто
    //     обрати файл вручну через діалог відкриття, де б він не лежав —
    //     теки лише пропонують зручне місце за замовчуванням, нічого не
    //     вимагають примусово.
    // EN: Created right at startup — same as original/review/output in
    //     TextValidator and original-dlc/localized/output in ImpakRepacker.
    //     The user can either drop files here ahead of time (original/ —
    //     .csv or .csv.z, review/ — TSV from Google Sheets), or just pick a
    //     file manually via the open dialog from wherever it happens to be
    //     — the folders only offer a convenient default location, nothing
    //     is forced.
    private const string OriginalDir = "original";
    private const string ReviewDir = "review";
    private const string OutputDir = "output";

    // ── Стан / State ──────────────────────────────────────────────────────
    private readonly ObservableCollection<LocEntryViewModel> _entries = new();
    private ICollectionView _view;
    private CsvLocDocument? _document;
    private string _originalPath = "";
    private string _filterMode = "all";
    private string _searchText = "";

    // UA: Таймер автозбереження — тихо зберігає *_AUTOSAVE.csv.z (або
    //     *_AUTOSAVE.csv, якщо оригінал не був стиснутим — обидва формати
    //     підтримуються, не лише .csv.z) у output/, якщо є незбережені
    //     зміни. Не чіпає файл, обраний через "Зберегти". Інтервал
    //     редагується через кнопку "⚙ Налаштування" (SettingsWindow).
    // EN: Autosave timer — silently saves *_AUTOSAVE.csv.z (or
    //     *_AUTOSAVE.csv if the original wasn't compressed — both formats
    //     are supported, not just .csv.z) into output/ if there are unsaved
    //     changes. Never touches the file chosen via "Save". The interval
    //     is editable via the "⚙ Settings" button (SettingsWindow).
    private readonly DispatcherTimer _autoSaveTimer = new();

    public MainWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_entries);
        _view.Filter = FilterEntry;
        MainGrid.ItemsSource = _view;
        SetActiveFilter("all");
        UpdateThemeButton();
        UpdateFontLabel();

        Directory.CreateDirectory(OriginalDir);
        Directory.CreateDirectory(ReviewDir);
        Directory.CreateDirectory(OutputDir);

        _autoSaveTimer.Tick += (_, _) => PerformAutoSave();
        ApplyAutoSaveSettings();

        ShowStatus($"UA: Поклади .csv/.csv.z у «{OriginalDir}/», review TSV — у «{ReviewDir}/», " +
                   $"результат з'явиться в «{OutputDir}/». Або обери файл вручну кнопкою вище. / " +
                   $"EN: Drop .csv/.csv.z into “{OriginalDir}/”, the review TSV into “{ReviewDir}/”, " +
                   $"the result will appear in “{OutputDir}/”. Or pick a file manually with the button above.");

        SimpleLogger.Info("MainWindow initialized");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ТЕМА ТА ШРИФТ / THEME AND FONT
    // ══════════════════════════════════════════════════════════════════════

    // UA: БАГ (фідбек: "на світлій темі одні кнопки чорні, інші білі"):
    //     SetActiveFilter встановлює Background/BorderBrush/Foreground
    //     кнопок фільтрів як ЛОКАЛЬНІ значення в коді (Brush-об'єкти,
    //     зняті з ресурсів на момент виклику) — а НЕ через DynamicResource
    //     у XAML. DynamicResource сам оновлюється при зміні теми,
    //     локально встановлений Brush — НІ: він лишається "заморожений"
    //     зі старої теми, доки хтось знову не викличе SetActiveFilter.
    //     Тому після перемикання теми кнопка фільтра, яку не клікали після
    //     цього, показувала колір ПОПЕРЕДНЬОЇ теми (звідси "деякі чорні,
    //     інші білі" — залежно від того, яка кнопка востаннє оновлювалась).
    //     Виправлення: перезастосовувати SetActiveFilter одразу після
    //     перемикання теми, щоб усі кнопки одразу отримали кольори НОВОЇ
    //     теми, а не лише активна/востаннє клікнута.
    // EN: BUG (feedback: "in light theme some buttons are black, others
    //     white"): SetActiveFilter sets filter buttons' Background/
    //     BorderBrush/Foreground as LOCAL values in code (Brush objects
    //     grabbed from resources at call time) — NOT via DynamicResource in
    //     XAML. DynamicResource auto-updates on a theme change, a locally
    //     assigned Brush does NOT: it stays "frozen" from the old theme
    //     until something calls SetActiveFilter again. So after toggling
    //     the theme, any filter button not clicked since then kept showing
    //     the PREVIOUS theme's color (hence "some black, some white" —
    //     depending on which button was last refreshed). Fix: reapply
    //     SetActiveFilter right after the theme toggle, so ALL buttons
    //     immediately get the NEW theme's colors, not just the
    //     active/last-clicked one.
    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateThemeButton();
        SetActiveFilter(_filterMode);
    }

    private void FontIncrease_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.IncreaseFontSize();
        UpdateFontLabel();
    }

    private void FontDecrease_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.DecreaseFontSize();
        UpdateFontLabel();
    }

    private void UpdateThemeButton() =>
        ThemeToggleBtn.Content = ThemeManager.IsDark
            ? "☀ Світла / Light"
            : "🌙 Темна / Dark";

    private void UpdateFontLabel() =>
        FontSizeLabel.Text = ThemeManager.FontSize.ToString("F0");

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
        // UA: Інтервал міг змінитися у вікні налаштувань — перезастосовуємо.
        // EN: The interval may have changed in the settings window — reapply it.
        ApplyAutoSaveSettings();
    }

    // ══════════════════════════════════════════════════════════════════════
    // АВТОЗБЕРЕЖЕННЯ / AUTOSAVE
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Читає поточний інтервал з ThemeManager (0 = вимкнено) і
    ///     запускає/зупиняє таймер відповідно.
    /// EN: Reads the current interval from ThemeManager (0 = disabled) and
    ///     starts/stops the timer accordingly.
    /// </summary>
    private void ApplyAutoSaveSettings()
    {
        int minutes = ThemeManager.AutoSaveIntervalMinutes;
        if (minutes > 0)
        {
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(minutes);
            if (!_autoSaveTimer.IsEnabled) _autoSaveTimer.Start();
        }
        else
        {
            _autoSaveTimer.Stop();
        }
    }

    /// <summary>
    /// UA: Тихо зберігає РОБОЧИЙ файл — TSV з усіма 7 колонками вичитки —
    ///     у review/&lt;назва&gt;_AUTOSAVE.tsv, якщо є незбережена робота.
    ///
    ///     Саме TSV, а не .csv.z: .csv.z — це КІНЦЕВИЙ артефакт для гри, він
    ///     фізично не здатен зберегти жодну колонку вичитки (примітку,
    ///     побажання, версію) — у нього просто немає для них місця. Робочим
    ///     файлом проєкту є review-таблиця, тож саме її має страхувати
    ///     автозбереження, інакше при збої втрачається вся робота вичитки.
    ///     Пишеться в review/ поруч із рештою робочих таблиць, а не в
    ///     output/ (там лежать готові файли для гри).
    ///
    ///     Умова — HasUnsavedWork, а не IsModified: правки полів вичитки не
    ///     торкаються тексту перекладу, тому за IsModified автозбереження
    ///     їх би просто не помітило.
    ///
    ///     Ніколи не питає підтвердження і не показує діалог — це фонова
    ///     страховка, а не заміна «Зберегти»/«Експорт TSV».
    /// EN: Silently saves the WORKING file — a TSV with all 7 proofreading
    ///     columns — into review/&lt;name&gt;_AUTOSAVE.tsv, if there is
    ///     unsaved work.
    ///
    ///     TSV specifically, not .csv.z: the .csv.z is the FINAL artifact
    ///     for the game and physically cannot hold any proofreading column
    ///     (note, suggestion, version) — there's simply no room for them in
    ///     it. The project's working file is the review table, so that's
    ///     what autosave must protect; otherwise a crash loses all
    ///     proofreading work. Written into review/ alongside the other
    ///     working tables, not output/ (which holds finished game files).
    ///
    ///     The condition is HasUnsavedWork, not IsModified: proofreading
    ///     edits don't touch the translation text, so IsModified alone
    ///     would make autosave miss them entirely.
    ///
    ///     Never prompts or shows a dialog — it's a background safety net,
    ///     not a replacement for "Save"/"Export TSV".
    /// </summary>
    private void PerformAutoSave()
    {
        if (_document == null || _entries.Count == 0) return;
        if (!_entries.Any(x => x.HasUnsavedWork)) return;

        try
        {
            MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            // UA: Знімаємо розширення двічі — "en.csv.z" має ДВІ частини
            //     розширення (.z і .csv), а "en.csv" лише одну (другий виклик
            //     тоді просто нічого не робить).
            // EN: Strip the extension twice — "en.csv.z" has TWO extension
            //     parts (.z and .csv), while a plain "en.csv" has only one
            //     (the second call is then a no-op).
            string baseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(_originalPath));
            string autoSaveName = baseName + "_AUTOSAVE.tsv";

            Directory.CreateDirectory(ReviewDir);
            string autoSavePath = Path.Combine(ReviewDir, autoSaveName);

            File.WriteAllBytes(autoSavePath, _document.ToReviewTsvBytes());
            ShowStatus($"💾 UA: Автозбереження робочого TSV / EN: Working TSV autosaved · " +
                       $"{DateTime.Now:HH:mm} · «{autoSaveName}»");
            SimpleLogger.Info($"Autosaved OK: {autoSavePath}");
        }
        catch (Exception ex)
        {
            // UA: Автозбереження — це страховка, а не критична дія; тиха
            //     невдача не повинна переривати роботу користувача — але
            //     все одно записується в лог, інакше причина зникнення
            //     автозбереження лишиться нез'ясованою.
            // EN: Autosave is a safety net, not a critical action; a silent
            //     failure shouldn't interrupt the user's work — but it's
            //     still logged, otherwise the reason autosave stopped
            //     working would remain a mystery.
            SimpleLogger.Error("Autosave failed", ex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ВІДКРИТТЯ ОРИГІНАЛУ / OPENING THE ORIGINAL
    // ══════════════════════════════════════════════════════════════════════

    private void OpenOriginalButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            InitialDirectory = Path.GetFullPath(OriginalDir),
            Filter = "UA: Мовні файли гри / EN: Game language files|*.csv;*.csv.z|" +
                     "Стиснуті / Compressed (*.csv.z)|*.csv.z|" +
                     "Звичайний CSV / Plain CSV (*.csv)|*.csv|" +
                     "Усі файли / All files|*.*",
            Title = "① UA: Відкрити оригінал (.csv або .csv.z) / EN: Open original (.csv or .csv.z)"
        };
        if (dlg.ShowDialog() != true) return;

        SimpleLogger.Info($"Opening original: {dlg.FileName}");
        try
        {
            byte[] rawCsv = LanguageArchive.LoadRaw(dlg.FileName);
            _document = CsvLocDocument.Parse(rawCsv);
            _originalPath = dlg.FileName;

            _entries.Clear();
            foreach (var entry in _document.Entries.Where(x => !x.IsStructural))
                _entries.Add(new LocEntryViewModel(entry));

            MergeReviewButton.IsEnabled = true;
            ExportTsvButton.IsEnabled = true;
            SaveButton.IsEnabled = true;

            _filterMode = "all";
            _searchText = "";
            SearchBox.Text = "";
            SetActiveFilter("all");

            RefreshView();
            ShowStatus($"✓ {Path.GetFileName(dlg.FileName)} · " +
                       $"{_entries.Count} UA: рядків / EN: rows " +
                       (LanguageArchive.IsCompressedArchive(dlg.FileName)
                           ? "· .csv.z UA: розпаковано напряму / EN: decompressed directly"
                           : ""));
            SimpleLogger.Info($"Original loaded OK: {dlg.FileName} · {_entries.Count} rows");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error($"Load original failed: {dlg.FileName}", ex);
            MessageBox.Show(
                $"UA: Помилка читання файлу / EN: File read error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ЗЛИТТЯ REVIEW / MERGING REVIEW
    // ══════════════════════════════════════════════════════════════════════

    private void MergeReviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) return;

        var dlg = new OpenFileDialog
        {
            InitialDirectory = Path.GetFullPath(ReviewDir),
            Filter = "UA: Файли перекладу (review) / EN: Review translation files|*.tsv;*.txt|Усі файли / All files|*.*",
            Title = "② UA: Злити review TSV / EN: Merge review TSV"
        };
        if (dlg.ShowDialog() != true) return;

        SimpleLogger.Info($"Merging review: {dlg.FileName}");
        try
        {
            byte[] tsvBytes = File.ReadAllBytes(dlg.FileName);
            int merged = _document.MergeReview(tsvBytes);
            int missing = _document.FindMissingTranslations().Count;

            foreach (var vm in _entries) vm.RefreshFromCore();

            SaveMissingKeysReport(dlg.FileName);

            RefreshView();
            ShowStatus($"✓ UA: Зіставлено {merged} перекладів, {missing} пропущено / " +
                       $"EN: Matched {merged} translations, {missing} missing " +
                       $"· «{Path.GetFileName(dlg.FileName)}»");
            SimpleLogger.Info($"Review merged OK: {dlg.FileName} · matched={merged} missing={missing}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error($"Merge review failed: {dlg.FileName}", ex);
            MessageBox.Show(
                $"UA: Помилка читання review / EN: Review read error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ЕКСПОРТ TSV / EXPORT TSV
    // UA: Вивантажує поточний документ у TSV (з табуляцією) для ручного
    //     редагування поза програмою (Excel/Google Таблиці) — напр. коли
    //     потрібно погратись із перекладом без запуску GUI, або передати
    //     файл перекладачеві без доступу до .exe. Формат сумісний з "Злити
    //     review" — відредагований файл можна одразу завантажити назад.
    // EN: Exports the current document to TSV (tab-separated) for manual
    //     editing outside the app (Excel/Google Sheets) — e.g. when you need
    //     to work on the translation without running the GUI, or hand the
    //     file to a translator with no access to the .exe. The format is
    //     compatible with "Merge Review" — the edited file can be loaded
    //     straight back in.
    // ══════════════════════════════════════════════════════════════════════

    private void ExportTsvButton_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) return;

        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        Directory.CreateDirectory(ReviewDir);
        string baseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(_originalPath));

        var dlg = new SaveFileDialog
        {
            InitialDirectory = Path.GetFullPath(ReviewDir),
            FileName = baseName + "_export.tsv",
            Filter = "TSV (*.tsv)|*.tsv|Усі файли / All files|*.*",
            Title = "📤 UA: Експорт TSV для ручного редагування / EN: Export TSV for manual editing"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            File.WriteAllBytes(dlg.FileName, _document.ToReviewTsvBytes());
            ShowStatus($"✓ UA: Експортовано в TSV / EN: Exported to TSV · «{Path.GetFileName(dlg.FileName)}»");
            SimpleLogger.Info($"Exported TSV OK: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error($"Export TSV failed: {dlg.FileName}", ex);
            MessageBox.Show(
                $"UA: Помилка експорту / EN: Export error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// UA: Зберігає повний список пропущених ключів у reports/ поруч із .exe
    ///     — той самий формат, що й у консольному TextValidator.
    /// EN: Saves the full list of missing keys into reports/ next to the
    ///     .exe — the same format as the console TextValidator.
    /// </summary>
    private void SaveMissingKeysReport(string reviewFileName)
    {
        if (_document == null) return;
        var missing = _document.FindMissingTranslations();
        if (missing.Count == 0) return;

        const string reportsDir = "reports";
        Directory.CreateDirectory(reportsDir);
        string safeName = string.Join("_", Path.GetFileNameWithoutExtension(_originalPath).Split(Path.GetInvalidFileNameChars()));
        string reportPath = Path.Combine(reportsDir, $"MissingKeys_{safeName}.txt");

        using var writer = new StreamWriter(reportPath, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine($"# Missing in review / Відсутні в review — {Path.GetFileName(_originalPath)}");
        writer.WriteLine($"# {missing.Count}");
        writer.WriteLine("# Ключ/Key\tОригінал/Original");
        foreach (var entry in missing)
            writer.WriteLine($"{entry.Key}\t{entry.Original}");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ЗБЕРЕЖЕННЯ / SAVING
    // ══════════════════════════════════════════════════════════════════════

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) return;

        MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var withIssues = _entries.Where(x => x.HasValidationIssue).ToList();
        if (withIssues.Count > 0)
        {
            var warn = MessageBox.Show(
                $"UA: {withIssues.Count} рядків мають проблеми валідації (див. фільтр «Проблемні») — " +
                "це може зламати відображення у грі!\n\n" +
                $"EN: {withIssues.Count} rows have validation issues (see the «Issues» filter) — " +
                "this may break in-game display!\n\n" +
                "UA: Продовжити все одно? / EN: Continue anyway?",
                "⚠ UA: Попередження / EN: Warning",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (warn != MessageBoxResult.Yes) return;
        }

        // UA: За замовчуванням пропонуємо теку output/ поруч із .exe — НІКОЛИ
        //     не туди, звідки відкрито оригінал, щоб випадково не
        //     перезаписати вихідні файли гри.
        // EN: Default to an output/ folder next to the .exe — NEVER the
        //     folder the original was opened from, so the game's source
        //     files are never accidentally overwritten.
        Directory.CreateDirectory(OutputDir);

        var dlg = new SaveFileDialog
        {
            InitialDirectory = Path.GetFullPath(OutputDir),
            FileName = Path.GetFileName(_originalPath),
            Filter = LanguageArchive.IsCompressedArchive(_originalPath)
                ? "Стиснутий архів гри / Compressed game archive (*.csv.z)|*.csv.z"
                : "CSV (*.csv)|*.csv",
            Title = "③ UA: Зберегти / EN: Save"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            byte[] rawCsv = _document.ToCsvBytes();
            LanguageArchive.SaveRaw(dlg.FileName, rawCsv);

            ShowStatus($"✓ UA: Збережено (з самоперевіркою) / EN: Saved (self-verified) · " +
                       $"«{Path.GetFileName(dlg.FileName)}»");
            SimpleLogger.Info($"Saved OK: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error($"Save failed: {dlg.FileName}", ex);
            MessageBox.Show(
                $"UA: Помилка збереження / EN: Save error:\n\n{ex.Message}",
                "Помилка / Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ДУБЛІКАТИ ОРИГІНАЛУ / DUPLICATE ORIGINALS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Явна дія користувача (лише за цим кліком, ніколи автоматично):
    ///     узгоджує переклад вибраного рядка з рештою рядків, де оригінал
    ///     повторюється дослівно — щоб не шукати й не вставляти вручну
    ///     кожен дублікат окремо.
    /// EN: An explicit user action (only on this click, never automatic):
    ///     synchronizes the selected row's translation with the rest of
    ///     the rows where the original repeats verbatim — so duplicates
    ///     don't need to be found and pasted in one by one.
    /// </summary>
    private void CtxApplyToDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) return;
        if (MainGrid.SelectedItem is not LocEntryViewModel vm) return;

        if (!vm.IsDuplicateOriginal)
        {
            ShowStatus("ℹ UA: Цей рядок не має дублікатів оригіналу / EN: This row has no duplicate originals");
            return;
        }

        int changed = _document.ApplyTranslationToDuplicates(vm.Core);
        _document.RecomputeDuplicates();
        foreach (var entry in _entries) entry.RefreshFromCore();

        RefreshView();
        ShowStatus($"✓ UA: Переклад застосовано до {changed} дублікатів / " +
                   $"EN: Translation applied to {changed} duplicates · «{vm.Key}»");
    }

    private void CtxCopyKey_Click(object sender, RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is LocEntryViewModel vm && !string.IsNullOrEmpty(vm.Key))
            Clipboard.SetText(vm.Key);
    }

    private void CtxCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is LocEntryViewModel vm && !string.IsNullOrEmpty(vm.Original))
            Clipboard.SetText(vm.Original);
    }

    // UA: Не всі, хто вичитує переклад, вільно читають англійську — окреме
    //     копіювання коментаря розробника (а не лише оригіналу) дозволяє
    //     вставити САМЕ його в перекладач, не змішуючи з текстом рядка.
    // EN: Not everyone proofreading the translation reads English fluently —
    //     copying the developer comment separately (not just the original)
    //     lets it be pasted straight into a translator, without mixing it
    //     with the row's own text.
    private void CtxCopyDevComment_Click(object sender, RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is LocEntryViewModel vm && !string.IsNullOrEmpty(vm.Comment))
            Clipboard.SetText(vm.Comment);
    }

    private void CtxPasteOriginalAsTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is LocEntryViewModel vm)
            vm.Translated = vm.Original;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ЗАВЕРШЕННЯ РЕДАГУВАННЯ КЛІТИНКИ / CELL EDIT COMPLETION
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UA: Після редагування клітинки перераховує лічильники й фільтр —
    ///     потрібно, бо правка «Вичитки» змінює WasReviewed, а отже й
    ///     розподіл між фільтрами «Пройшли вичитку»/«Без вичитки»
    ///     (правка перекладу так само впливає на «Перекладено»/«Змінено»).
    ///
    ///     Оновлення відкладається через Dispatcher: на момент
    ///     CellEditEnding транзакція редагування ЩЕ НЕ завершена, і прямий
    ///     виклик _view.Refresh() кинув би InvalidOperationException
    ///     («'Refresh' is not allowed during an AddNew or EditItem
    ///     transaction») — та сама відома пастка, що вже задокументована в
    ///     BF1LocalizationTool.GUI.RefreshView.
    /// EN: Recomputes counters and the filter after a cell edit — needed
    ///     because editing "Review" changes WasReviewed, and therefore how
    ///     rows split between the "Reviewed"/"Not reviewed" filters
    ///     (editing a translation likewise affects "Translated"/"Modified").
    ///
    ///     The refresh is deferred via the Dispatcher: at CellEditEnding
    ///     time the edit transaction is NOT yet finished, and calling
    ///     _view.Refresh() directly would throw InvalidOperationException
    ///     ("'Refresh' is not allowed during an AddNew or EditItem
    ///     transaction") — the same known gotcha already documented in
    ///     BF1LocalizationTool.GUI.RefreshView.
    /// </summary>
    private void MainGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            MainGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            RefreshView();
        }), DispatcherPriority.Background);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ФІЛЬТРАЦІЯ ТА ПОШУК / FILTERING AND SEARCH
    // ══════════════════════════════════════════════════════════════════════

    private bool FilterEntry(object obj)
    {
        if (obj is not LocEntryViewModel e) return false;

        // UA: "un"/"tr" явно виключають технічні рядки — три статуси
        //     (технічний/перекладено/без перекладу) взаємовиключні, як і в
        //     BF1LocalizationTool.GUI (FilterRow: "Перекладено" ховає технічні").
        // EN: "un"/"tr" explicitly exclude technical rows — the three
        //     statuses (technical/translated/untranslated) are mutually
        //     exclusive, same as in BF1LocalizationTool.GUI (FilterRow:
        //     "Translated" hides technical).
        bool passes = _filterMode switch
        {
            "un" => !e.IsTranslated && !e.IsTechnical,
            "tr" => e.IsTranslated && !e.IsTechnical,
            "technical" => e.IsTechnical,
            "dup" => e.IsDuplicateOriginal,
            "issues" => e.HasValidationIssue,
            "mod" => e.IsModified,
            // UA: ВИПРАВЛЕНО (баг "Без вичитки: -266"): CsvLocDocument.
            //     MergeReview зіставляє ReviewNote за КЛЮЧЕМ для БУДЬ-ЯКОГО
            //     нетехнічного... ні, насправді для БУДЬ-ЯКОГО нехструктур-
            //     ного рядка — включно з технічними (він не перевіряє
            //     IsTechnical). Тому технічний рядок, який просто присутній
            //     у review-файлі з непорожньою 5-ю колонкою, теж отримує
            //     WasReviewed=true. Явне "!e.IsTechnical" тут — і на
            //     "reviewed", і на "notreviewed" — гарантує, що технічні
            //     рахуються РІВНО один раз (у власному фільтрі "Технічні"),
            //     а не одночасно в "Пройшли вичитку" ще й спотворюють
            //     віднімання в UpdateStats (total - reviewed - technical),
            //     яке й ставало від'ємним.
            // EN: FIXED (the "Not reviewed: -266" bug): CsvLocDocument.
            //     MergeReview matches ReviewNote by KEY for ANY non-
            //     structural row — including technical ones (it never
            //     checks IsTechnical). So a technical row that simply
            //     appears in the review file with a non-empty 5th column
            //     also gets WasReviewed=true. The explicit "!e.IsTechnical"
            //     here — on BOTH "reviewed" and "notreviewed" — guarantees
            //     technical rows are counted EXACTLY once (in their own
            //     "Technical" filter), instead of also landing in
            //     "Reviewed" and throwing off the subtraction in
            //     UpdateStats (total - reviewed - technical), which is what
            //     was going negative.
            "reviewed" => e.WasReviewed && !e.IsTechnical,
            "notreviewed" => !e.WasReviewed && !e.IsTechnical,
            _ => true
        };
        if (!passes) return false;
        if (string.IsNullOrEmpty(_searchText)) return true;

        string q = _searchText.ToLowerInvariant();
        return e.Key.ToLowerInvariant().Contains(q)
            || e.Original.ToLowerInvariant().Contains(q)
            || e.Translated.ToLowerInvariant().Contains(q);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        RefreshView();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _filterMode = tag;
            SetActiveFilter(tag);
            RefreshView();
        }
    }

    // UA: Активний фільтр підсвічується акцентним фіолетовим (Background/
    //     BorderBrush) — той самий підхід, що й у EaWLocalizationTool.GUI
    //     (SetActiveFilter: Res("AccentDark")/Res("Accent") на активній
    //     кнопці, Res("BgCard")/Res("BdAcc") на решті). Раніше тут була лише
    //     зміна товщини шрифту (FontWeight) — саме це й малось на увазі під
    //     "немає кольорів на фільтрах".
    // EN: The active filter is highlighted with the accent purple
    //     (Background/BorderBrush) — the same approach as in
    //     EaWLocalizationTool.GUI (SetActiveFilter: Res("AccentDark")/
    //     Res("Accent") on the active button, Res("BgCard")/Res("BdAcc") on
    //     the rest). Previously this only toggled FontWeight — that's
    //     exactly what "no colors on the filters" was pointing at.
    private void SetActiveFilter(string active)
    {
        var buttons = new[] { FilterAll, FilterUn, FilterTr, FilterTechnical, FilterDup, FilterIssues, FilterMod, FilterReviewed, FilterNotReviewed };
        var tags = new[] { "all", "un", "tr", "technical", "dup", "issues", "mod", "reviewed", "notreviewed" };
        var res = Application.Current.Resources;
        for (int i = 0; i < buttons.Length; i++)
        {
            bool on = tags[i] == active;
            buttons[i].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            buttons[i].Background = on ? (Brush)res["AccentDark"] : (Brush)res["BgCard"];
            buttons[i].BorderBrush = on ? (Brush)res["Accent"] : (Brush)res["BdAcc"];
            // UA: Явно задаємо Foreground (а не покладаємось лише на Style
            //     Setter) — захисний крок проти нечитабельних фільтрів:
            //     білий текст на активній (яскраво-фіолетовій) кнопці,
            //     звичайний TextPrim на решті.
            // EN: Explicitly set Foreground (not just relying on the Style
            //     Setter) — a defensive step against unreadable filters:
            //     white text on the active (bright purple) button, regular
            //     TextPrim on the rest.
            buttons[i].Foreground = on ? Brushes.White : (Brush)res["TextPrim"];
        }
    }

    // UA: КЕШ ScrollViewer-а таблиці — DataGrid будує його один раз у своєму
    //     шаблоні при першому layout-проході; той самий об'єкт лишається
    //     дійсним усе життя вікна, тож достатньо знайти його один раз через
    //     VisualTreeHelper, а не проходити дерево на кожен виклик RefreshView.
    // EN: CACHE of the grid's ScrollViewer — the DataGrid builds it once in
    //     its template on the first layout pass; the same object stays valid
    //     for the window's whole lifetime, so it only needs to be found once
    //     via VisualTreeHelper, not re-walked on every RefreshView call.
    private ScrollViewer? _mainGridScrollViewer;

    private ScrollViewer? GetMainGridScrollViewer() =>
        _mainGridScrollViewer ??= FindVisualChild<ScrollViewer>(MainGrid);

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    // UA: БАГ (фідбек: "чому при закінченню редагування рядка мене програма
    //     автоматично підіймає до верху таблиці?"): ListCollectionView.
    //     Refresh() перебудовує ItemsControl-презентер зсередини і при
    //     цьому скидає позицію прокрутки DataGrid-а на самий верх — відомий
    //     побічний ефект WPF, а не щось навмисне тут. Викликається якраз із
    //     MainGrid_CellEditEnding після КОЖНОГО редагування клітинки, тому
    //     стрибок трапляється саме "при закінченні редагування рядка".
    //     Виправлення: запам'ятати VerticalOffset ScrollViewer-а ДО Refresh(),
    //     і повернути його ПІСЛЯ — Refresh() лишається (він потрібен, бо
    //     фільтр/лічильники можуть залежати від щойно зміненого поля), але
    //     користувач більше не "телепортується" вгору списку.
    // EN: BUG (feedback: "why does the app auto-scroll the table to the top
    //     when I finish editing a row?"): ListCollectionView.Refresh()
    //     rebuilds the ItemsControl presenter internally and, as a side
    //     effect, resets the DataGrid's scroll position to the very top — a
    //     known WPF quirk, not anything intentional here. It's called from
    //     MainGrid_CellEditEnding after EVERY cell edit, which is exactly
    //     why the jump happens "when finishing editing a row". Fix: capture
    //     the ScrollViewer's VerticalOffset BEFORE Refresh(), and restore it
    //     AFTER — Refresh() itself stays (it's still needed, since the
    //     filter/counters may depend on the field that was just edited), but
    //     the user is no longer "teleported" to the top of the list.
    private void RefreshView()
    {
        var scrollViewer = GetMainGridScrollViewer();
        double savedOffset = scrollViewer?.VerticalOffset ?? -1;

        _view.Refresh();
        UpdateStats();

        if (scrollViewer != null && savedOffset >= 0)
        {
            // UA: Відкладено — Refresh() ще завершує layout, і встановлення
            //     офсету одразу може бути перебите наступним layout-проходом.
            // EN: Deferred — Refresh() is still finishing layout, and setting
            //     the offset immediately could be overridden by the next
            //     layout pass.
            Dispatcher.BeginInvoke(new Action(() =>
                scrollViewer.ScrollToVerticalOffset(savedOffset)), DispatcherPriority.ContextIdle);
        }
    }

    private void UpdateStats()
    {
        int total = _entries.Count;
        int technical = _entries.Count(e => e.IsTechnical);
        int translated = _entries.Count(e => e.IsTranslated);
        // UA: "Без перекладу" рахує лише РЕАЛЬНО текстові рядки — технічні
        //     виключені (падають у власний фільтр, див. п.4 фідбеку).
        // EN: "Untranslated" only counts ACTUAL text rows — technical rows
        //     are excluded (they fall into their own filter, see feedback pt.4).
        int untranslated = total - translated - technical;
        int duplicates = _entries.Count(e => e.IsDuplicateOriginal);
        int issues = _entries.Count(e => e.HasValidationIssue);
        // UA: ВИПРАВЛЕНО (баг "Без вичитки: -266") — MergeReview проставляє
        //     ReviewNote за ключем БЕЗ перевірки IsTechnical, тому технічний
        //     рядок, присутній у review-файлі з непорожньою 5-ю колонкою,
        //     теж отримував WasReviewed=true. Якщо порахувати "reviewed" по
        //     ВСІХ рядках (включно з технічними), total - reviewed -
        //     technical віднімає технічні ДВІЧІ (один раз як "technical",
        //     ще раз як частину "reviewed") — і йде в мінус, якщо майже всі
        //     рядки (тех. і нетех.) мають непорожню примітку review. Тепер
        //     "reviewed" рахується ЛИШЕ по нетехнічних рядках — сумісно з
        //     "notreviewed", яке також виключає технічні.
        // EN: FIXED (the "Not reviewed: -266" bug) — MergeReview stamps
        //     ReviewNote by key WITHOUT checking IsTechnical, so a technical
        //     row present in the review file with a non-empty 5th column
        //     also got WasReviewed=true. Counting "reviewed" over ALL rows
        //     (including technical) made total - reviewed - technical
        //     subtract technical rows TWICE (once as "technical", again as
        //     part of "reviewed") — going negative whenever nearly all rows
        //     (technical and not) have a non-empty review note. "reviewed"
        //     is now counted over non-technical rows ONLY — consistent with
        //     "notreviewed", which also excludes technical.
        int reviewed = _entries.Count(e => e.WasReviewed && !e.IsTechnical);
        int visible = _entries.Count(FilterEntry);

        FilterAll.Content = $"Усі / All · {total}";
        FilterUn.Content = $"Без перекладу / Untranslated · {untranslated}";
        FilterTr.Content = $"Перекладено / Translated · {translated}";
        FilterTechnical.Content = $"⚙ Технічні / Technical · {technical}";
        FilterDup.Content = $"⧉ Дублікати / Duplicates · {duplicates}";
        FilterIssues.Content = $"⚠ Проблемні / Issues · {issues}";
        FilterMod.Content = $"✎ Змінено / Modified · {_entries.Count(e => e.IsModified)}";
        FilterReviewed.Content = $"✓ Пройшли вичитку / Reviewed · {reviewed}";
        // UA: total - technical - reviewed тепер завжди >= 0, бо і reviewed,
        //     і technical рахуються з ОДНІЄЇ й тієї ж виключної (nontech)
        //     множини — перетину більше немає.
        // EN: total - technical - reviewed is now always >= 0, since both
        //     reviewed and technical are counted from the same exclusive
        //     (non-overlapping) sets — no more double-counting.
        FilterNotReviewed.Content = $"✗ Без вичитки / Not reviewed · {total - technical - reviewed}";

        if (total > 0)
            ShowStatus($"{visible} UA: з / EN: of {total} · {translated}/{total - technical} " +
                       $"UA: перекладено / EN: translated · {issues} ⚠ · {duplicates} ⧉ · {technical} ⚙");
    }

    private void ShowStatus(string message) => StatusText.Text = message;
}

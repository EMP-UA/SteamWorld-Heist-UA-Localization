// =============================================================================
// SWH.LocEditor.GUI — Models/LocEntryViewModel.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ViewModel рядка CSV для GUI. Обгортає LocEntry з Core і додає
//     GUI-логіку: INotifyPropertyChanged, позначку зміни (Modified),
//     живу валідацію при редагуванні клітинки.
// EN: CSV row ViewModel for the GUI. Wraps LocEntry from Core and adds
//     GUI logic: INotifyPropertyChanged, a Modified flag, live validation
//     while editing a cell.
// =============================================================================

using System.ComponentModel;
using SWH.LocEditor.Core;

namespace SWH.LocEditor.GUI.Models;

public class LocEntryViewModel : INotifyPropertyChanged
{
    private bool _modified;
    // UA: Окремо від _modified — правки полів вичитки не пишуться у CSV
    //     гри, тому не повинні позначати рядок як змінений для збереження
    //     CSV, але МАЮТЬ тригерити автозбереження робочого TSV.
    // EN: Separate from _modified — proofreading-field edits aren't written
    //     into the game's CSV, so they must not mark the row as modified
    //     for the CSV save, but they MUST trigger the working-TSV autosave.
    private bool _reviewModified;
    private bool _hasValidationIssue;
    private string _validationWarning = "";

    public LocEntry Core { get; }

    public string Key => Core.Key;
    public string Original => Core.Original;
    public string Comment => Core.Comment;
    public bool IsStructural => Core.IsStructural;

    /// <summary>
    /// UA: Рядок без тексту для перекладу (заголовок розділу "#-----" чи
    ///     оригінал без жодної літери) — окремий фільтр "Технічні", НЕ
    ///     "Без перекладу". Взаємовиключно з IsTranslated (див. там).
    /// EN: A row with no text to translate (a "#-----" section header or an
    ///     original with no letters at all) — a separate "Technical" filter,
    ///     NOT "Untranslated". Mutually exclusive with IsTranslated (see there).
    /// </summary>
    public bool IsTechnical => Core.IsTechnical;

    /// <summary>
    /// UA: Позначка/коментар вичитки (є лише в review-файлі, не в оригіналі
    ///     гри). РЕДАГОВАНА — це робочий параметр, який проставляється саме
    ///     під час роботи з програмою, а не лише імпортується. Приймає як
    ///     готові позначки (+, -, +/-), так і довільний текст коментаря до
    ///     конкретного рядка.
    ///
    ///     ВАЖЛИВО: не позначає рядок як IsModified — IsModified стосується
    ///     ЛИШЕ тексту перекладу, який пишеться у CSV гри. Примітка вичитки
    ///     у CSV гри не зберігається взагалі; вона потрапляє у вихід через
    ///     «Експорт TSV» (5-та колонка).
    /// EN: The proofreading marker/comment (exists only in the review file,
    ///     not in the game's original). EDITABLE — this is a working field
    ///     filled in while actually using the app, not merely imported. It
    ///     accepts both ready-made markers (+, -, +/-) and free-form comment
    ///     text for a specific row.
    ///
    ///     IMPORTANT: does not flag the row as IsModified — IsModified
    ///     concerns ONLY the translation text written into the game's CSV.
    ///     The review note is never stored in the game CSV at all; it
    ///     leaves the app via "Export TSV" (5th column).
    /// </summary>
    public string ReviewNote
    {
        get => Core.ReviewNote;
        set
        {
            string newValue = value ?? "";
            if (Core.ReviewNote == newValue) return;
            Core.ReviewNote = newValue;
            _reviewModified = true;
            OnPropertyChanged(nameof(ReviewNote));
            // UA: WasReviewed є похідним від ReviewNote і керує фільтрами
            //     «Пройшли вичитку»/«Без вичитки» — тому теж сповіщаємо.
            // EN: WasReviewed derives from ReviewNote and drives the
            //     "Reviewed"/"Not reviewed" filters — so notify it too.
            OnPropertyChanged(nameof(WasReviewed));
            OnPropertyChanged(nameof(HasUnsavedWork));
        }
    }

    public bool WasReviewed => Core.WasReviewed;

    /// <summary>
    /// UA: Чи є в рядку НЕЗБЕРЕЖЕНА робота будь-якого роду — правка
    ///     перекладу (IsModified) АБО правка полів вичитки. Саме за цим
    ///     прапорцем автозбереження вирішує, чи писати робочий TSV: інакше
    ///     правки самих лише приміток вичитки не тригерили б збереження й
    ///     могли б загубитись.
    /// EN: Whether the row has ANY kind of UNSAVED work — a translation
    ///     edit (IsModified) OR an edit to the proofreading fields.
    ///     Autosave uses this flag to decide whether to write the working
    ///     TSV: otherwise review-note-only edits wouldn't trigger a save
    ///     and could be lost.
    /// </summary>
    public bool HasUnsavedWork => _modified || _reviewModified;

    public LocEntryViewModel(LocEntry core)
    {
        Core = core;
        Revalidate();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ПЕРЕКЛАД / TRANSLATION
    // ══════════════════════════════════════════════════════════════════════

    public string Translated
    {
        get => Core.Translated;
        set
        {
            if (Core.Translated == value) return;
            Core.Translated = value;
            _modified = true;
            OnPropertyChanged(nameof(Translated));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(IsTranslated));
            Revalidate();
        }
    }

    public bool IsModified => _modified;

    // UA: !Core.IsTechnical — три статуси (технічний/перекладено/без
    //     перекладу) взаємовиключні, як і в BF1LocalizationTool.GUI
    //     (EntryRow.IsTranslated/IsTechnical).
    // EN: !Core.IsTechnical — the three statuses (technical/translated/
    //     untranslated) are mutually exclusive, same as in
    //     BF1LocalizationTool.GUI (EntryRow.IsTranslated/IsTechnical).
    public bool IsTranslated => !Core.IsTechnical &&
                                 !string.IsNullOrEmpty(Core.Translated) &&
                                 !string.Equals(Core.Translated, Core.Original, StringComparison.Ordinal);

    /// <summary>
    /// UA: Встановлює переклад без позначки Modified (після завантаження /
    ///     злиття review, чи після масового застосування до дублікатів).
    /// EN: Sets the translation without the Modified flag (after loading /
    ///     merging review, or after bulk-applying to duplicates).
    /// </summary>
    public void RefreshFromCore()
    {
        OnPropertyChanged(nameof(Translated));
        OnPropertyChanged(nameof(IsTranslated));
        OnPropertyChanged(nameof(IsDuplicateOriginal));
        OnPropertyChanged(nameof(HasInconsistentDuplicateTranslation));
        OnPropertyChanged(nameof(ReviewNote));
        OnPropertyChanged(nameof(WasReviewed));
        Revalidate();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ДУБЛІКАТИ ОРИГІНАЛУ / DUPLICATE ORIGINALS
    // ══════════════════════════════════════════════════════════════════════

    public bool IsDuplicateOriginal => Core.IsDuplicateOriginal;
    public int DuplicateGroupSize => Core.DuplicateGroupSize;
    public bool HasInconsistentDuplicateTranslation => Core.HasInconsistentDuplicateTranslation;

    // ══════════════════════════════════════════════════════════════════════
    // ВАЛІДАЦІЯ / VALIDATION
    // ══════════════════════════════════════════════════════════════════════

    public bool HasValidationIssue
    {
        get => _hasValidationIssue;
        private set
        {
            if (_hasValidationIssue == value) return;
            _hasValidationIssue = value;
            OnPropertyChanged(nameof(HasValidationIssue));
        }
    }

    public string ValidationWarning
    {
        get => _validationWarning;
        private set
        {
            if (_validationWarning == value) return;
            _validationWarning = value;
            OnPropertyChanged(nameof(ValidationWarning));
        }
    }

    /// <summary>
    /// UA: Числовий ключ сортування для колонки "Попередження" — відносна
    ///     різниця довжини перекладу у %, зі знаком (+довший/-коротший).
    ///     Прив'язується через SortMemberPath у XAML, щоб клік по заголовку
    ///     колонки сортував за СЕРЙОЗНІСТЮ проблеми довжини, а не за
    ///     алфавітом тексту попередження.
    /// EN: Numeric sort key for the "Warning" column — the relative length
    ///     difference of the translation in %, signed (+longer/-shorter).
    ///     Bound via SortMemberPath in XAML so clicking the column header
    ///     sorts by the SEVERITY of the length issue, not alphabetically by
    ///     the warning text.
    /// </summary>
    public double LengthDeltaPercent { get; private set; }

    private void Revalidate()
    {
        LengthDeltaPercent = LocValidationService.LengthDeltaPercent(Core.Original, Core.Translated);
        OnPropertyChanged(nameof(LengthDeltaPercent));

        if (IsStructural || !IsTranslated)
        {
            HasValidationIssue = false;
            ValidationWarning = "";
            return;
        }
        var (hasIssue, desc) = LocValidationService.Check(Core.Original, Core.Translated);
        HasValidationIssue = hasIssue;
        ValidationWarning = desc;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

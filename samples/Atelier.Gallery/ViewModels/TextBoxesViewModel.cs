using System.ComponentModel.DataAnnotations;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>
/// A sign-up form validated with data annotations. Text fields bound to it show the validation errors
/// (INotifyDataErrorInfo) in place of their supporting text.
/// </summary>
public partial class SignUpForm : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Enter your name")]
    private string _fullName = "Alex Morgan";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Enter your email address")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Enter an address like name@example.com")]
    private string _email = "alex@atelier";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MinLength(8, ErrorMessage = "Use at least 8 characters")]
    private string _password = "secret";

    public SignUpForm() => ValidateAllProperties();

    public void Validate() => ValidateAllProperties();

    public void Reset()
    {
        FullName = "Alex Morgan";
        Email = "alex@atelier";
        Password = "secret";
    }
}

public partial class TextBoxesViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "TextFields";

    public const int BioMaxLength = 80;

    [ObservableProperty]
    private bool _controlsEnabled = true;

    [ObservableProperty]
    private bool _useFilledVariant;

    // Two-way binding: updates on every keystroke vs. when the field loses focus.
    [ObservableProperty]
    private string _liveText = "Type here";

    [ObservableProperty]
    private string _lostFocusText = "Commits on focus loss";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BioCounter))]
    private string _bio = "UI framework enthusiast.";

    [ObservableProperty]
    private string _lastTextChanged = "Type in the field to see TextChanged";

    [ObservableProperty]
    private string _submitResult = "Fix the errors and submit";

    public TextBoxesViewModel()
    {
        PageIcon = MaterialIconKind.Edit;
        PageTitle = "Text Fields";
        CommandGroup = Group;
        Keywords = "textbox text field input password label placeholder validation form";
    }

    public SignUpForm Form { get; } = new();

    public string BioCounter => $"{Bio.Length} / {BioMaxLength}";

    public const string DefaultNotes =
        "Multi-line fields wrap long lines at spaces, like this one, which is long enough to wrap in a narrow column.\n" +
        "Press Enter for a new line.\n\nEdit the notes and close the window: it asks whether to save them first.";

    private string _savedNotes = DefaultNotes;

    // The notes are "unsaved" until SaveNotes runs; the gallery window asks about them when it is closed.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedNotes), nameof(NotesStatus))]
    [NotifyCanExecuteChangedFor(nameof(SaveNotesCommand), nameof(RevertNotesCommand))]
    private string _notes = DefaultNotes;

    public bool HasUnsavedNotes => Notes != _savedNotes;

    public string NotesStatus => HasUnsavedNotes ? "Unsaved changes — closing the window asks to save" : "Saved";

    [RelayCommand(CanExecute = nameof(HasUnsavedNotes))]
    [property: Command("SaveNotes", Group, Label = "Save notes", Icon = MaterialIcons.Save, Description = "Mark the notes on the Text Fields page as saved")]
    public void SaveNotes()
    {
        _savedNotes = Notes;
        OnPropertyChanged(nameof(HasUnsavedNotes));
        OnPropertyChanged(nameof(NotesStatus));
        SaveNotesCommand.NotifyCanExecuteChanged();
        RevertNotesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasUnsavedNotes))]
    [property: Command("RevertNotes", Group, Label = "Revert", Icon = MaterialIcons.Undo, Description = "Discard the unsaved changes to the notes")]
    private void RevertNotes() => Notes = _savedNotes;

    public const string LogText =
        "[09:41:02] Loading project 'Atelier.slnx'\n[09:41:03] Restoring packages…\n[09:41:05] Building Atelier.Core\n" +
        "[09:41:07] Building Atelier.Controls — lines don't wrap here, so this long one scrolls sideways\n" +
        "[09:41:09] Building Atelier.Gallery\n[09:41:10] Running 1,490 tests\n[09:41:14] All tests passed\n[09:41:14] Done";

    [RelayCommand]
    [property: Command("Submit", Group, Label = "Create account", Description = "Validate the form and show the result")]
    private void Submit()
    {
        Form.Validate();
        SubmitResult = Form.HasErrors ? "Please fix the fields marked in red" : $"Submitted: {Form.FullName} <{Form.Email}>";
    }

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        ControlsEnabled = true;
        UseFilledVariant = false;
        LiveText = "Type here";
        LostFocusText = "Commits on focus loss";
        Bio = "UI framework enthusiast.";
        LastTextChanged = "Type in the field to see TextChanged";
        SubmitResult = "Fix the errors and submit";
        Form.Reset();
        Notes = DefaultNotes;
        SaveNotes();
    }
}

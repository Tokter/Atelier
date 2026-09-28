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
        Keywords = "textbox text field input password label placeholder validation form";
    }

    public SignUpForm Form { get; } = new();

    public string BioCounter => $"{Bio.Length} / {BioMaxLength}";

    [RelayCommand]
    [property: Command("Submit", "TextFields", Label = "Create account", Description = "Validate the form and show the result")]
    private void Submit()
    {
        Form.Validate();
        SubmitResult = Form.HasErrors ? "Please fix the fields marked in red" : $"Submitted: {Form.FullName} <{Form.Email}>";
    }

    [RelayCommand]
    [property: Command("Reset", "TextFields", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
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
    }
}

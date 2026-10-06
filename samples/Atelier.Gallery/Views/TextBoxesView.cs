using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class TextBoxesView : GalleryPage
{
    private readonly TextBoxesViewModel _vm;

    public TextBoxesView(TextBoxesViewModel viewModel)
        : base(MaterialIconKind.Edit, "Text Fields",
            "Text fields let users enter and edit text. They come in outlined and filled variants, with a floating label, " +
            "placeholder, leading icon, supporting text and validation errors.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Switch("Filled variant").BindIsChecked(_vm, v => v.UseFilledVariant, (v, on) => v.UseFilledVariant = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(AnatomySection(), InputOptionsSection(), MultilineSection(), BindingSection(), FormSection());
    }

    private UIElement AnatomySection() => Ui.Section("Anatomy and states",
        "The label rests inside the empty field and floats above it on focus or when there is text. The placeholder " +
        "appears once the label has floated. Use the header switch to see the filled variant.",
        Ui.Columns(260,
            Field().Label("Label only"),
            Field().Label("With placeholder").Placeholder("e.g. Acme Corp"),
            Field("Pre-filled value").Label("With text"),
            Field().Label("Search").LeadingIconKind(MaterialIconKind.Search).Placeholder("Type a keyword"),
            Field().Label("Phone").LeadingIconKind(MaterialIconKind.Phone).SupportingText("Include the country code"),
            Field().Placeholder("No label, placeholder only")),
        Ui.Demo("Read-only and disabled",
            Ui.Columns(260,
                Field("Can be selected and copied").Label("Read-only").IsReadOnly(),
                Field().Label("Disabled").Placeholder("Can't be edited").IsEnabled(false),
                Field("Protected value").Label("Disabled with text").IsEnabled(false))));

    private UIElement InputOptionsSection() => Ui.Section("Input options",
        "Limit the length, mask passwords and align the text. Ctrl+Z and Ctrl+Y undo and redo; UndoLimit sets how many " +
        "steps are kept (100 by default).",
        Ui.Columns(260,
            Ui.Demo("Maximum length",
                Field().Label("Short bio")
                    .MaxLength(TextBoxesViewModel.BioMaxLength)
                    .BindText(_vm, v => v.Bio, (v, text) => v.Bio = text)
                    .BindSupportingText(_vm, v => v.BioCounter)),
            Ui.Demo("Password",
                Field("hunter2hunter2").Label("Password").LeadingIconKind(MaterialIconKind.Lock).PasswordChar()
                    .SupportingText("Copy and cut are disabled")),
            Ui.Demo("Text alignment",
                Field("Centered").Label("Center").TextAlignment(TextAlignment.Center),
                Field("1,234.56").Label("Amount (right)").TextAlignment(TextAlignment.Right)),
            Ui.Demo("Undo and caret",
                Field("Edit me, then press Ctrl+Z").Label("Undo limit 3").UndoLimit(3),
                Field("A wider caret").Label("Caret width 3").CaretWidth(3))));

    private UIElement MultilineSection() => Ui.Section("Multi-line",
        "AcceptsReturn lets Enter insert line breaks and TextWrapping wraps long lines. The field grows from MinLines to " +
        "MaxLines and then scrolls with the caret or the mouse wheel. Up/Down move between lines, Home/End go to the ends " +
        "of the line and Ctrl+Home/End to the ends of the text.",
        Ui.Columns(300,
            Ui.Demo("Notes: 3 to 8 lines, wrapping",
                Field().Label("Notes").Multiline(minLines: 3, maxLines: 8)
                    .BindText(_vm, v => v.Notes, (v, text) => v.Notes = text)
                    .BindSupportingText(_vm, v => v.NotesStatus),
                Ui.Row(
                    new Button().Variant(ButtonVariant.Tonal).Command(_vm.SaveNotesCommand),
                    new Button().Variant(ButtonVariant.Text).Command(_vm.RevertNotesCommand))),
            Ui.Demo("Fixed height, no wrapping",
                Field(TextBoxesViewModel.LogText).Label("Build log").AcceptsReturn().Height(180)),
            Ui.Demo("Wrapping only (Enter is left to the form)",
                Field("Pasted line breaks are cut, as in a single-line field, but long text wraps onto more lines.")
                    .Label("Summary").TextWrapping().MaxLines(4))),
        Ui.Code("new TextBox().Label(\"Notes\").Multiline(minLines: 3, maxLines: 8)\n" +
                "    .BindText(vm, v => v.Notes, (v, text) => v.Notes = text)\n\n" +
                "// Ask before closing with unsaved changes (Alt+F4, taskbar, close button):\n" +
                "window.Closing += (s, e) =>\n{\n    if (vm.HasUnsavedNotes) e.Defer(AskToSaveAsync());\n};"));

    private UIElement BindingSection() => Ui.Section("Binding and events",
        "Text binds two-way. By default the source is updated on every change; with UpdateSourceTrigger.LostFocus only " +
        "when the field loses focus. TextChanged fires on every change.",
        Ui.Columns(300,
            Ui.Demo("On every change",
                Field().Label("Live").BindText(_vm, v => v.LiveText, (v, text) => v.LiveText = text),
                Ui.Readout(_vm, v => $"LiveText = \"{v.LiveText}\"")),
            Ui.Demo("On focus loss",
                Field().Label("Deferred").BindText(_vm, v => v.LostFocusText, (v, text) => v.LostFocusText = text, UpdateSourceTrigger.LostFocus),
                Ui.Readout(_vm, v => $"LostFocusText = \"{v.LostFocusText}\"")),
            Ui.Demo("TextChanged event",
                Field().Label("Events").OnTextChanged(text => _vm.LastTextChanged = $"TextChanged → \"{text}\" ({text.Length} chars)"),
                Ui.Readout(_vm, v => v.LastTextChanged))),
        Ui.Code("new TextBox().Label(\"Deferred\")\n" +
                "    .BindText(vm, v => v.Name, (v, text) => v.Name = text, UpdateSourceTrigger.LostFocus)"));

    private UIElement FormSection() => Ui.Section("Validation",
        "When the bound object implements INotifyDataErrorInfo (here an ObservableValidator with data annotations), the " +
        "field shows its error in place of the supporting text.",
        Ui.Columns(260,
            Field().Label("Full name").LeadingIconKind(MaterialIconKind.Person)
                .BindText(_vm.Form, f => f.FullName, (f, text) => f.FullName = text),
            Field().Label("Email").LeadingIconKind(MaterialIconKind.Mail).SupportingText("We never share your email")
                .BindText(_vm.Form, f => f.Email, (f, text) => f.Email = text),
            Field().Label("Password").LeadingIconKind(MaterialIconKind.Key).PasswordChar().SupportingText("At least 8 characters")
                .BindText(_vm.Form, f => f.Password, (f, text) => f.Password = text)),
        Ui.Row(
            new Button().Command(_vm.SubmitCommand),
            Ui.Readout(_vm, v => v.SubmitResult)),
        Ui.Code("[ObservableProperty, NotifyDataErrorInfo]\n[MinLength(8, ErrorMessage = \"Use at least 8 characters\")]\n" +
                "private string _password;\n\n" +
                "new TextBox().Label(\"Password\").PasswordChar()\n    .BindText(form, f => f.Password, (f, text) => f.Password = text)"));

    // Every field on the page follows the header's "Filled variant" switch. Fields are top-aligned: a stretched text
    // field grows its container to the height of its column row.
    private TextBox Field(string? text = null) =>
        new TextBox()
            .Text(text)
            .VerticalAlignment(VerticalAlignment.Top)
            .Bind(TextBox.VariantProperty, _vm, v => v.UseFilledVariant ? TextBoxVariant.Filled : TextBoxVariant.Outlined);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>
/// The Material Design 3 container style of a <see cref="TextBox"/>.
/// </summary>
public enum TextBoxVariant
{
    /// <summary>A transparent field surrounded by an outline; the floating label cuts a notch into the outline.</summary>
    Outlined,
    /// <summary>A tinted field with rounded top corners and an underline.</summary>
    Filled
}

/// <summary>
/// A text input with a Material Design 3 look: optional floating label, leading icon, placeholder and supporting text
/// (replaced by the first validation error while a binding reports one). Single-line by default; set
/// <see cref="AcceptsReturn"/> and/or <see cref="TextWrapping"/> for a multi-line text area.
/// </summary>
/// <remarks>
/// <para>
/// Editing: typing, Backspace/Delete (Ctrl: whole words), arrow keys, Home/End (Shift extends the selection, Ctrl moves
/// by words), Ctrl+A, clipboard (Ctrl+C/X/V, Ctrl+Insert, Shift+Delete, Shift+Insert), undo (Ctrl+Z) and redo (Ctrl+Y,
/// Ctrl+Shift+Z). Pointer: click places the caret, Shift+click extends the selection, drag selects, double-click selects
/// a word and triple-click selects everything (a line in a multi-line box).
/// </para>
/// <para>
/// Multi-line (see <see cref="IsMultiline"/>): Enter inserts a line break when <see cref="AcceptsReturn"/> is set,
/// Up/Down and Page Up/Page Down move between lines (keeping the caret's column), Home/End go to the start or end of the
/// line and Ctrl+Home/Ctrl+End to the start or end of the text. The first line sits where the text of a single-line
/// field does; the field grows by one line height per line, between <see cref="MinLines"/> and <see cref="MaxLines"/>,
/// and scrolls (mouse wheel, or following the caret) beyond that or when its height is set.
/// </para>
/// <para>
/// The caret and selection never split a text element: surrogate pairs (such as emoji) and grapheme clusters (such as a
/// letter plus combining accent) are edited and navigated as one character. Without <see cref="AcceptsReturn"/>, pasted
/// or typed text is cut at its first line break; with it, line breaks are inserted as '\n'.
/// </para>
/// <para>
/// Character offsets and line layout are computed once per text, font and width and cached, so hit testing, scrolling
/// the caret into view and rendering the caret and selection don't re-measure text.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new TextBox().Label("Notes").AcceptsReturn().TextWrapping(TextWrapping.Wrap).MinLines(3).MaxLines(8)
///     .BindText(vm, v => v.Notes, (v, text) => v.Notes = text)
/// </code>
/// </example>
public class TextBox : Control
{
    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly BindableProperty<string> TextProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(Text),
            string.Empty,
            (s, o, n) => ((TextBox)s).OnTextChanged(n),
            coerceValue: CoerceString
        );

    /// <summary>Identifies the <see cref="Placeholder"/> property.</summary>
    public static readonly BindableProperty<string> PlaceholderProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(Placeholder),
            string.Empty,
            coerceValue: CoerceString,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="IsReadOnly"/> property.</summary>
    public static readonly BindableProperty<bool> IsReadOnlyProperty =
        BindableProperty.Register<TextBox, bool>(nameof(IsReadOnly), false);

    /// <summary>Identifies the <see cref="CaretWidth"/> property.</summary>
    public static readonly BindableProperty<float> CaretWidthProperty =
        BindableProperty.Register<TextBox, float>(nameof(CaretWidth), 2f, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="FieldHeight"/> property.</summary>
    public static readonly BindableProperty<float> FieldHeightProperty =
        BindableProperty.Register<TextBox, float>(nameof(FieldHeight), 56f, options: PropertyOptions.AffectsMeasure,
            validateValue: static value => float.IsFinite(value) && value >= 0);

    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly BindableProperty<TextBoxVariant> VariantProperty =
        BindableProperty.Register<TextBox, TextBoxVariant>(
            nameof(Variant),
            TextBoxVariant.Outlined,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Label"/> property.</summary>
    public static readonly BindableProperty<string> LabelProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(Label),
            string.Empty,
            (s, o, n) => ((TextBox)s).OnLabelChanged(),
            coerceValue: CoerceString,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="LeadingIconKind"/> property.</summary>
    public static readonly BindableProperty<MaterialIconKind> LeadingIconKindProperty =
        BindableProperty.Register<TextBox, MaterialIconKind>(
            nameof(LeadingIconKind),
            MaterialIconKind.None,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="TrailingIconKind"/> property.</summary>
    public static readonly BindableProperty<MaterialIconKind> TrailingIconKindProperty =
        BindableProperty.Register<TextBox, MaterialIconKind>(
            nameof(TrailingIconKind),
            MaterialIconKind.None,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="SupportingText"/> property.</summary>
    public static readonly BindableProperty<string> SupportingTextProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(SupportingText),
            string.Empty,
            coerceValue: CoerceString,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="MaxLength"/> property.</summary>
    public static readonly BindableProperty<int> MaxLengthProperty =
        BindableProperty.Register<TextBox, int>(
            nameof(MaxLength),
            0,
            validateValue: static value => value >= 0
        );

    /// <summary>Identifies the <see cref="PasswordChar"/> property.</summary>
    public static readonly BindableProperty<char> PasswordCharProperty =
        BindableProperty.Register<TextBox, char>(
            nameof(PasswordChar),
            '\0',
            (s, o, n) => ((TextBox)s).EnsureCaretVisible(),
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="TextAlignment"/> property.</summary>
    public static readonly BindableProperty<TextAlignment> TextAlignmentProperty =
        BindableProperty.Register<TextBox, TextAlignment>(
            nameof(TextAlignment),
            TextAlignment.Left,
            (s, o, n) => ((TextBox)s).EnsureCaretVisible(),
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="UndoLimit"/> property.</summary>
    public static readonly BindableProperty<int> UndoLimitProperty =
        BindableProperty.Register<TextBox, int>(
            nameof(UndoLimit),
            100,
            (s, o, n) => ((TextBox)s).TrimUndoHistory(),
            validateValue: static value => value >= 0
        );

    /// <summary>Identifies the <see cref="AcceptsReturn"/> property.</summary>
    public static readonly BindableProperty<bool> AcceptsReturnProperty =
        BindableProperty.Register<TextBox, bool>(
            nameof(AcceptsReturn),
            false,
            (s, o, n) => ((TextBox)s).OnLineModeChanged(),
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="TextWrapping"/> property.</summary>
    public static readonly BindableProperty<TextWrapping> TextWrappingProperty =
        BindableProperty.Register<TextBox, TextWrapping>(
            nameof(TextWrapping),
            TextWrapping.NoWrap,
            (s, o, n) => ((TextBox)s).OnLineModeChanged(),
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="MinLines"/> property.</summary>
    public static readonly BindableProperty<int> MinLinesProperty =
        BindableProperty.Register<TextBox, int>(nameof(MinLines), 1, options: PropertyOptions.AffectsMeasure,
            validateValue: static value => value >= 1);

    /// <summary>Identifies the <see cref="MaxLines"/> property.</summary>
    public static readonly BindableProperty<int> MaxLinesProperty =
        BindableProperty.Register<TextBox, int>(nameof(MaxLines), 0, options: PropertyOptions.AffectsMeasure,
            validateValue: static value => value >= 0);

    private static string CoerceString(BindableObject sender, string value) => value ?? string.Empty;

    /// <summary>
    /// Gets or sets the text. <c>null</c> (for example from a binding) is stored as an empty string. Setting it from
    /// code moves the caret into range, clears the undo history and raises <see cref="TextChanged"/>; it is not limited
    /// by <see cref="MaxLength"/>.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the hint shown while the text is empty (below a resting label, once it floats). <c>null</c> is stored as empty.</summary>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the user can't change the text. Navigation, selection and copying still work. The default is
    /// <c>false</c>.
    /// </summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the field container, excluding supporting text. The default is the Material Design 3
    /// height, 56 px; themes use less for dense (desktop) layouts. The field still grows to fit its text, and a filled
    /// field with a label is at least 52 px so the floating label and the text don't overlap.
    /// </summary>
    public float FieldHeight
    {
        get => GetValue(FieldHeightProperty);
        set => SetValue(FieldHeightProperty, value);
    }

    /// <summary>Gets or sets the caret width in pixels (rounded, at least 1). The default is 2.</summary>
    public float CaretWidth
    {
        get => GetValue(CaretWidthProperty);
        set => SetValue(CaretWidthProperty, value);
    }

    /// <summary>Gets or sets the container style. The default is <see cref="TextBoxVariant.Outlined"/>.</summary>
    public TextBoxVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Gets or sets the label, shown inside the empty field and floating above the text while focused or filled.
    /// <c>null</c> is stored as empty (no label).
    /// </summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Gets or sets the icon shown before the text, or <see cref="MaterialIconKind.None"/> (the default) for none.</summary>
    public MaterialIconKind LeadingIconKind
    {
        get => GetValue(LeadingIconKindProperty);
        set => SetValue(LeadingIconKindProperty, value);
    }

    /// <summary>
    /// Gets or sets the helper text shown below the field; a validation error replaces it (see
    /// <see cref="DisplayedSupportingText"/>). <c>null</c> is stored as empty.
    /// </summary>
    public string SupportingText
    {
        get => GetValue(SupportingTextProperty);
        set => SetValue(SupportingTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of characters (UTF-16 code units) the user can enter by typing or pasting; longer
    /// input is truncated (never inside a surrogate pair). 0, the default, means no limit. Text set from code is not limited.
    /// </summary>
    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the character shown instead of each character of the text, turning the box into a password field;
    /// '\0' (the default) shows the text. While set, copying and cutting are disabled and word navigation jumps to the
    /// start or end.
    /// </summary>
    public char PasswordChar
    {
        get => GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal alignment of the text within the field while it fits; longer text scrolls and starts
    /// at the left. The default is <see cref="TextAlignment.Left"/>.
    /// </summary>
    public TextAlignment TextAlignment
    {
        get => GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of undo steps kept; the oldest are dropped. 0 disables undo. The default is 100.
    /// Consecutive typing is grouped into word-sized steps, consecutive Backspace or Delete presses into one step.
    /// </summary>
    public int UndoLimit
    {
        get => GetValue(UndoLimitProperty);
        set => SetValue(UndoLimitProperty, value);
    }

    /// <summary>
    /// Gets or sets whether Enter inserts a line break, making the box multi-line (see <see cref="IsMultiline"/>). Pasted
    /// text then keeps its line breaks (as '\n'). Ctrl+Enter is left to the surrounding UI, for example a dialog's
    /// default button. The default is <c>false</c>.
    /// </summary>
    public bool AcceptsReturn
    {
        get => GetValue(AcceptsReturnProperty);
        set => SetValue(AcceptsReturnProperty, value);
    }

    /// <summary>
    /// Gets or sets whether lines wrap at the width of the field (at spaces; words that are too long break between
    /// characters). <see cref="TextWrapping.Wrap"/> makes the box multi-line (see <see cref="IsMultiline"/>) even without
    /// <see cref="AcceptsReturn"/>. The default is <see cref="TextWrapping.NoWrap"/>: long lines scroll horizontally.
    /// </summary>
    public TextWrapping TextWrapping
    {
        get => GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of lines a multi-line box is at least tall enough for. At least 1; the default is 1. An
    /// explicit <c>Height</c> takes precedence.
    /// </summary>
    public int MinLines
    {
        get => GetValue(MinLinesProperty);
        set => SetValue(MinLinesProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of lines a multi-line box grows to before it scrolls. 0, the default, means it grows with
    /// its text. <see cref="MinLines"/> wins if it is larger; an explicit <c>Height</c> takes precedence.
    /// </summary>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>
    /// Gets whether the box shows several lines: <see cref="AcceptsReturn"/> is set or <see cref="TextWrapping"/> is
    /// <see cref="TextWrapping.Wrap"/>. A password field (see <see cref="PasswordChar"/>) is always single-line.
    /// </summary>
    public bool IsMultiline => (AcceptsReturn || TextWrapping == TextWrapping.Wrap) && PasswordChar == '\0';

    // Line breaks the user types or pastes are kept only in a multi-line box that accepts Enter.
    private bool AcceptsLineBreaks => AcceptsReturn && IsMultiline;

    /// <summary>Gets whether a label is set.</summary>
    public bool HasLabel => !string.IsNullOrEmpty(Label);

    /// <summary>Gets whether a leading icon is set.</summary>
    public bool HasLeadingIcon => LeadingIconKind != MaterialIconKind.None;

    /// <summary>
    /// Gets or sets the icon button shown after the text, or <see cref="MaterialIconKind.None"/> (the default) for none.
    /// Clicking it raises <see cref="TrailingIconClick"/> (for example to open a picker or clear the text) and doesn't
    /// move the caret.
    /// </summary>
    public MaterialIconKind TrailingIconKind
    {
        get => GetValue(TrailingIconKindProperty);
        set => SetValue(TrailingIconKindProperty, value);
    }

    /// <summary>Gets whether a trailing icon is set.</summary>
    public bool HasTrailingIcon => TrailingIconKind != MaterialIconKind.None;

    /// <summary>Gets whether the pointer is over the trailing icon.</summary>
    public bool IsTrailingIconHovered { get; private set; }

    /// <summary>Gets whether the trailing icon is being pressed.</summary>
    public bool IsTrailingIconPressed { get; private set; }

    /// <summary>Occurs when the trailing icon is clicked.</summary>
    public event EventHandler? TrailingIconClick;

    /// <summary>The size of the trailing icon's touch target and state layer, centered 24 px from the right edge.</summary>
    public const float TrailingIconTargetSize = 40f;

    /// <summary>
    /// Gets the bounds of the trailing icon's 40×40 target in the element's coordinates: centered vertically in the field
    /// row (see <see cref="GetFieldRowHeight"/>) with its center 24 px from the right edge. Empty without a trailing icon.
    /// </summary>
    public Rect GetTrailingIconBounds()
    {
        if (!HasTrailingIcon) return Rect.Zero;
        float rowHeight = GetFieldRowHeight();
        float half = TrailingIconTargetSize * 0.5f;
        return new Rect(Bounds.Width - 24f - half, rowHeight * 0.5f - half, TrailingIconTargetSize, TrailingIconTargetSize);
    }

    /// <summary>
    /// Gets the height of the row the icons and the resting label are centered in: the field container (the bounds
    /// above the supporting text) of a single-line box, or the height of a one-line field for a multi-line box, so they
    /// stay beside the first line.
    /// </summary>
    public float GetFieldRowHeight() =>
        IsMultiline ? GetOneLineFieldHeight() : Math.Max(0f, Bounds.Height - (HasSupportingText ? 20f : 0f));

    // The container height of a one-line field, as MeasureOverride computes it for a single line.
    private float GetOneLineFieldHeight()
    {
        float fieldHeight = HasLabel && Variant == TextBoxVariant.Filled ? Math.Max(FieldHeight, 52f) : FieldHeight;
        return Math.Max(fieldHeight, LineHeight + Padding.Vertical);
    }

    /// <summary>Raises <see cref="TrailingIconClick"/>, as a click on the trailing icon does.</summary>
    protected virtual void OnTrailingIconClick() => TrailingIconClick?.Invoke(this, EventArgs.Empty);

    private void SetTrailingIconHovered(bool hovered)
    {
        if (IsTrailingIconHovered == hovered) return;
        IsTrailingIconHovered = hovered;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    /// <remarks>The arrow over the trailing icon, otherwise the I-beam (or the set cursor).</remarks>
    protected override CursorType GetCursor() => IsTrailingIconHovered ? CursorType.Arrow : base.GetCursor();

    /// <summary>
    /// Gets whether a supporting line is shown below the field: the <see cref="SupportingText"/>, or the first validation
    /// error while a binding reports one (see <see cref="Validation"/>).
    /// </summary>
    public bool HasSupportingText => !string.IsNullOrEmpty(SupportingText) || HasValidationError;

    /// <summary>Gets whether a binding of this text box currently reports a validation error.</summary>
    public bool HasValidationError => Validation.GetHasError(this);

    private IReadOnlyList<object>? _errorsForText;
    private string _errorText = string.Empty;

    /// <summary>
    /// Gets the text shown in the supporting line: the first validation error if there is one, otherwise <see cref="SupportingText"/>.
    /// The error's string is cached until the errors change.
    /// </summary>
    public string DisplayedSupportingText
    {
        get
        {
            var errors = Validation.GetErrors(this);
            if (errors.Count == 0)
            {
                _errorsForText = null;
                return SupportingText;
            }

            if (!ReferenceEquals(errors, _errorsForText))
            {
                _errorsForText = errors;
                _errorText = errors[0]?.ToString() ?? string.Empty;
            }
            return _errorText;
        }
    }

    /// <summary>Gets the floating label's animation progress: 0 = resting inside the field, 1 = floating above the text.</summary>
    public float LabelAnimationProgress { get; private set; } = 0f;

    private float _scrollX;
    private float _scrollY;
    // Set by ArrangeOverride, which runs before the new bounds are assigned: the scroll offsets are fitted to the new
    // size when they are next read.
    private bool _scrollFitPending;
    // Set when the mouse wheel scrolled a multi-line box; layout passes then keep the offset instead of jumping back to
    // the caret.
    private bool _userScrolled;

    /// <summary>Gets how far the text is scrolled left, in pixels, to keep the caret visible.</summary>
    public float ScrollOffset
    {
        get
        {
            ApplyPendingScrollFit();
            return _scrollX;
        }
    }

    /// <summary>
    /// Gets how far the lines of a multi-line box are scrolled up, in pixels, to keep the caret visible or as scrolled
    /// with the mouse wheel. Always 0 for a single-line box.
    /// </summary>
    public float VerticalScrollOffset
    {
        get
        {
            ApplyPendingScrollFit();
            return _scrollY;
        }
    }

    /// <summary>Gets the x coordinate where the text viewport starts: the left padding plus room for a leading icon.</summary>
    public float GetTextContentStartX() => Padding.Left + (HasLeadingIcon ? 36f : 0f);

    /// <summary>Gets the width of the area the text is shown in (the bounds minus padding and icons).</summary>
    public float GetViewportWidth()
    {
        float right = Bounds.Width - Padding.Right - (HasTrailingIcon ? 36f : 0f);
        float left = GetTextContentStartX();
        return Math.Max(0f, right - left);
    }

    private int _caretIndex = 0;

    /// <summary>
    /// Gets or sets the caret position as an index into <see cref="Text"/> (0 = before the first character). Setting it
    /// clears the selection; the value is clamped to the text and moved back to the start of a text element if needed.
    /// </summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set => SetCaretIndex(value, keepSelection: false);
    }

    /// <summary>Gets the fixed end of the selection; the caret is the moving end.</summary>
    public int SelectionAnchor { get; private set; } = 0;

    /// <summary>Gets the index of the first selected character.</summary>
    public int SelectionStart => Math.Min(SelectionAnchor, _caretIndex);

    /// <summary>Gets the number of selected characters (UTF-16 code units).</summary>
    public int SelectionLength => Math.Abs(_caretIndex - SelectionAnchor);

    /// <summary>Gets whether any text is selected.</summary>
    public bool HasSelection => SelectionLength > 0;

    /// <summary>Gets the selected text, or an empty string.</summary>
    public string SelectedText => HasSelection && Text.Length >= SelectionStart + SelectionLength
        ? Text.Substring(SelectionStart, SelectionLength)
        : string.Empty;

    /// <summary>
    /// Gets whether the caret is currently drawn: the box is focused and the caret is in the visible phase of its blink.
    /// </summary>
    public bool CaretVisible => IsFocused && _caretBlinkOn;

    /// <summary>
    /// Occurs after the text changed, with the new text. For user edits it is raised after the caret and selection have
    /// been updated, so handlers see the final <see cref="CaretIndex"/>.
    /// </summary>
    public event EventHandler<string>? TextChanged;

    /// <summary>
    /// Gets or sets the time the caret stays visible or hidden while blinking, for all text boxes.
    /// <see cref="TimeSpan.Zero"/> disables blinking. The default is 530 ms.
    /// </summary>
    /// <remarks>The blink timer only runs while a text box is focused and displayed in a window.</remarks>
    public static TimeSpan CaretBlinkInterval { get; set; } = TimeSpan.FromMilliseconds(530);

    private static AnimationClock? _clock;

    /// <summary>Sets the clock that drives the floating-label animation of all text boxes; without one the label jumps.</summary>
    /// <param name="clock">The animation clock.</param>
    public static void SetGlobalAnimationClock(AnimationClock clock) => _clock = clock;

    private FloatAnimation? _labelAnimation;

    // Fallback click counting for pointer events without a ClickCount.
    private ulong _lastClickTime = 0;
    private Point _lastClickPos = Point.Zero;
    private int _clickCount;

    // Caret blinking.
    private DispatcherTimer? _caretTimer;
    private bool _caretBlinkOn = true;

    // Editing state: TextChanged of an edit is deferred until the caret has moved.
    private bool _isEditing;
    private bool _textChangedPending;

    // Undo/redo.
    private readonly List<UndoEntry> _undoStack = new();
    private readonly List<UndoEntry> _redoStack = new();
    private bool _breakUndoCoalescing = true;

    // Cached text metrics: caret offsets per UTF-16 index, text-element boundaries and the displayed (masked) text.
    private string? _metricsText;
    private float _metricsFontSize = float.NaN;
    private string? _metricsFamily;
    private char _metricsMask;
    private float[] _offsets = new float[1];
    private bool[] _boundaries = new bool[] { true };
    private string _displayText = string.Empty;
    private bool _metricsMultiline;
    private int _metricsVersion;

    // Cached line layout (see EnsureLines): one entry per visual line, plus the line strings, created when first drawn.
    private readonly List<LineSpan> _lines = new();
    private readonly List<string?> _lineTexts = new();
    private int _linesVersion = -1;
    private float _linesWrapWidth = float.NaN;
    private float _maxLineWidth;
    private List<LineSpan>? _measureLines;

    // The x position (in unscrolled content coordinates) Up/Down aim for, kept across consecutive vertical moves.
    private float _preferredCaretX = float.NaN;

    static TextBox()
    {
        PaddingProperty.OverrideDefaultValue<TextBox>(new Thickness(16, 8));
        CornerRadiusProperty.OverrideDefaultValue<TextBox>(new CornerRadius(4));
        CursorProperty.OverrideDefaultValue<TextBox>(CursorType.IBeam);
    }

    /// <summary>Initializes an empty, focusable text box.</summary>
    public TextBox()
    {
        IsFocusable = true;
    }

    /// <summary>Initializes a text box with <paramref name="text"/>, the caret at its end and the label floating.</summary>
    /// <param name="text">The initial text.</param>
    public TextBox(string text) : this()
    {
        Text = text;
        _caretIndex = Text.Length;
        SelectionAnchor = _caretIndex;
        if (!string.IsNullOrEmpty(Text))
        {
            LabelAnimationProgress = 1f;
        }
        EnsureCaretVisible();
    }

    #region Text metrics

    /// <summary>
    /// Gets the text as drawn: <see cref="Text"/>, or one <see cref="PasswordChar"/> per text element in a password field.
    /// </summary>
    public string DisplayText
    {
        get
        {
            EnsureTextMetrics();
            return _displayText;
        }
    }

    /// <summary>
    /// Gets the horizontal distance in pixels from the start of the text to caret position <paramref name="index"/>
    /// (clamped to the text), as if all text were on one line (line breaks have no width in a multi-line box). Uses
    /// cached measurements. See <see cref="GetCharacterX"/> for where a position is drawn.
    /// </summary>
    /// <param name="index">A caret position, 0 to <c>Text.Length</c>.</param>
    public float GetCharacterOffset(int index)
    {
        EnsureTextMetrics();
        return _offsets[Math.Clamp(index, 0, Text.Length)];
    }

    /// <summary>Gets the width of the displayed text in pixels, as if it were on one line.</summary>
    public float GetTextWidth() => GetCharacterOffset(Text.Length);

    /// <summary>
    /// Gets the x coordinate at which the first character is drawn: the viewport start, plus the alignment offset while
    /// the text fits, minus <see cref="ScrollOffset"/>. For a multi-line box, see <see cref="GetLineOriginX"/>.
    /// </summary>
    public float GetTextOriginX() => IsMultiline ? GetLineOriginX(0) : GetTextContentStartX() + GetAlignmentOffset(GetTextWidth()) - ScrollOffset;

    /// <summary>
    /// Gets the x coordinate, in the element's coordinates, at which caret position <paramref name="index"/> is drawn on
    /// its line (scrolling and alignment included).
    /// </summary>
    /// <param name="index">A caret position, 0 to <c>Text.Length</c>.</param>
    public float GetCharacterX(int index)
    {
        if (!IsMultiline) return GetTextOriginX() + GetCharacterOffset(index);

        EnsureLines();
        index = Math.Clamp(index, 0, Text.Length);
        int line = GetLineIndexFromCharacterIndex(index);
        return GetLineOriginX(line) + _offsets[index] - _offsets[_lines[line].Start];
    }

    private float GetAlignmentOffset(float width)
    {
        var alignment = TextAlignment;
        if (alignment == TextAlignment.Left) return 0f;

        float viewport = GetViewportWidth();
        float caretWidth = GetRoundedCaretWidth();
        if (width + caretWidth > viewport) return 0f;

        return alignment == TextAlignment.Center
            ? MathF.Round((viewport - width) * 0.5f)
            : viewport - width - caretWidth;
    }

    private float GetRoundedCaretWidth() => Math.Max(1f, MathF.Round(CaretWidth));

    private void EnsureTextMetrics()
    {
        string text = Text;
        float fontSize = FontSize;
        string? family = FontFamily;
        char mask = PasswordChar;
        bool multiline = IsMultiline;
        if (ReferenceEquals(text, _metricsText) && fontSize == _metricsFontSize && mask == _metricsMask &&
            multiline == _metricsMultiline && string.Equals(family, _metricsFamily))
        {
            return;
        }

        _metricsText = text;
        _metricsFontSize = fontSize;
        _metricsFamily = family;
        _metricsMask = mask;
        _metricsMultiline = multiline;
        _metricsVersion++;

        int n = text.Length;
        if (_offsets.Length < n + 1)
        {
            int capacity = Math.Max(n + 1, _offsets.Length * 2);
            _offsets = new float[capacity];
            _boundaries = new bool[capacity];
        }

        // Text-element boundaries (grapheme clusters, which includes surrogate pairs).
        Array.Clear(_boundaries, 0, n + 1);
        int elementCount = 0;
        for (int i = 0; i < n;)
        {
            _boundaries[i] = true;
            elementCount++;
            i += Math.Max(1, StringInfo.GetNextTextElementLength(text.AsSpan(i)));
        }
        _boundaries[n] = true;

        // Offsets: each index inside a text element maps to the element's start.
        var font = fontSize > 0 ? TextMeasurer.GetFont(fontSize, family) : null;
        float maskWidth = 0f;
        if (mask != '\0' && font != null)
        {
            ReadOnlySpan<char> maskSpan = [mask];
            maskWidth = font.MeasureText(maskSpan);
        }

        float x = 0f;
        float elementX = 0f;
        int elementStart = 0;
        for (int i = 0; i <= n; i++)
        {
            if (_boundaries[i])
            {
                if (i > 0 && font != null)
                {
                    // Line breaks end a line in a multi-line box and take no room.
                    x += mask != '\0' ? maskWidth
                        : multiline && IsLineBreak(text[elementStart]) ? 0f
                        : font.MeasureText(text.AsSpan(elementStart, i - elementStart));
                }
                elementX = x;
                elementStart = i;
            }
            _offsets[i] = elementX;
        }

        _displayText = mask != '\0' ? new string(mask, elementCount) : text;
    }

    private bool IsBoundary(int index)
    {
        EnsureTextMetrics();
        return _boundaries[index];
    }

    /// <summary>Returns the text-element boundary at or before <paramref name="index"/>.</summary>
    private int SnapBackward(int index)
    {
        index = Math.Clamp(index, 0, Text.Length);
        while (index > 0 && !IsBoundary(index)) index--;
        return index;
    }

    /// <summary>Returns the text-element boundary at or after <paramref name="index"/>.</summary>
    private int SnapForward(int index)
    {
        int length = Text.Length;
        index = Math.Clamp(index, 0, length);
        while (index < length && !IsBoundary(index)) index++;
        return index;
    }

    /// <summary>Returns the start of the text element before caret position <paramref name="index"/>.</summary>
    private int PreviousBoundary(int index) => index <= 0 ? 0 : SnapBackward(index - 1);

    /// <summary>Returns the end of the text element after caret position <paramref name="index"/>.</summary>
    private int NextBoundary(int index) => index >= Text.Length ? Text.Length : SnapForward(index + 1);

    private static bool IsLineBreak(char c) => c is '\r' or '\n';

    #endregion

    #region Lines

    /// <summary>
    /// Gets the distance between the baselines of consecutive lines: the font's line spacing, rounded up to whole pixels.
    /// </summary>
    public float LineHeight => MathF.Ceiling(TextMeasurer.GetFontSpacing(FontSize, FontFamily));

    /// <summary>
    /// Gets the number of visual lines: 1 for a single-line box; otherwise one per line of the text, plus the extra lines
    /// created by wrapping (see <see cref="TextWrapping"/>). Text ending in a line break has an empty last line.
    /// </summary>
    public int LineCount
    {
        get
        {
            EnsureLines();
            return _lines.Count;
        }
    }

    /// <summary>Gets the index in <see cref="Text"/> of the first character of visual line <paramref name="line"/>.</summary>
    /// <param name="line">The line index, clamped to 0 to <see cref="LineCount"/> − 1.</param>
    public int GetLineStart(int line) => GetLine(line).Start;

    /// <summary>Gets the number of characters on visual line <paramref name="line"/>, excluding its line break.</summary>
    /// <param name="line">The line index, clamped to 0 to <see cref="LineCount"/> − 1.</param>
    public int GetLineLength(int line) => GetLine(line).Length;

    /// <summary>Gets the width in pixels of visual line <paramref name="line"/>.</summary>
    /// <param name="line">The line index, clamped to 0 to <see cref="LineCount"/> − 1.</param>
    public float GetLineWidth(int line) => GetLine(line).Width;

    /// <summary>
    /// Gets the characters of visual line <paramref name="line"/> as drawn, without the line break (the
    /// <see cref="DisplayText"/> for a single-line box). The strings are created once per layout and cached.
    /// </summary>
    /// <param name="line">The line index, clamped to 0 to <see cref="LineCount"/> − 1.</param>
    public string GetLineText(int line)
    {
        if (!IsMultiline) return DisplayText;

        EnsureLines();
        line = Math.Clamp(line, 0, _lines.Count - 1);
        var span = _lines[line];
        return _lineTexts[line] ??= span.Length == 0 ? string.Empty : Text.Substring(span.Start, span.Length);
    }

    /// <summary>
    /// Gets the visual line caret position <paramref name="index"/> is on. A position where a line wraps belongs to the
    /// line it starts.
    /// </summary>
    /// <param name="index">A caret position, clamped to 0 to <c>Text.Length</c>.</param>
    public int GetLineIndexFromCharacterIndex(int index)
    {
        EnsureLines();
        index = Math.Clamp(index, 0, Text.Length);

        // Last line whose start is at or before index.
        int lo = 0;
        int hi = _lines.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (_lines[mid].Start <= index) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>
    /// Gets the x coordinate at which visual line <paramref name="line"/> starts: the viewport start, plus the alignment
    /// offset while the line fits, minus <see cref="ScrollOffset"/>.
    /// </summary>
    /// <param name="line">The line index, clamped to 0 to <see cref="LineCount"/> − 1.</param>
    public float GetLineOriginX(int line) =>
        GetTextContentStartX() + GetAlignmentOffset(IsMultiline ? GetLine(line).Width : GetTextWidth()) - ScrollOffset;

    /// <summary>
    /// Gets the y coordinate of the baseline of the first line of a multi-line box when it isn't scrolled: where the text
    /// of a single-line field of <see cref="FieldHeight"/> sits.
    /// </summary>
    public float GetFirstBaselineY()
    {
        float rowHeight = GetOneLineFieldHeight();
        return Variant == TextBoxVariant.Filled && HasLabel ? rowHeight - 14f : (rowHeight + FontSize) * 0.5f - 2f;
    }

    /// <summary>Gets the y coordinate of the baseline of visual line <paramref name="line"/> of a multi-line box, scrolling included.</summary>
    /// <param name="line">The line index (not clamped).</param>
    public float GetLineBaselineY(int line) => GetFirstBaselineY() + line * LineHeight - VerticalScrollOffset;

    /// <summary>
    /// Gets the y coordinate of the top of visual line <paramref name="line"/> of a multi-line box, scrolling included;
    /// each line is <see cref="LineHeight"/> tall.
    /// </summary>
    /// <param name="line">The line index (not clamped).</param>
    public float GetLineTop(int line) => GetViewportTop() + line * LineHeight - VerticalScrollOffset;

    /// <summary>Gets the y coordinate where the lines of a multi-line box are shown from (0 for a single-line box).</summary>
    public float GetViewportTop() => IsMultiline ? GetFirstBaselineY() - GetAscent() : 0f;

    /// <summary>
    /// Gets the height of the area the lines of a multi-line box are shown in, at least one line: the field container
    /// minus the space above the first line and below the last. For a single-line box, the field row height.
    /// </summary>
    public float GetViewportHeight()
    {
        if (!IsMultiline) return GetFieldRowHeight();

        float container = Math.Max(0f, Bounds.Height - (HasSupportingText ? 20f : 0f));
        float lineHeight = LineHeight;
        float top = GetViewportTop();
        float bottomInset = Math.Max(0f, GetOneLineFieldHeight() - top - lineHeight);
        return Math.Max(lineHeight, container - top - bottomInset);
    }

    /// <summary>Gets the height of all lines of a multi-line box: <see cref="LineCount"/> × <see cref="LineHeight"/>.</summary>
    public float GetContentHeight() => LineCount * LineHeight;

    private float GetMaxVerticalScroll() => IsMultiline ? Math.Max(0f, GetContentHeight() - GetViewportHeight()) : 0f;

    private float GetMaxHorizontalScroll()
    {
        if (IsMultiline)
        {
            if (TextWrapping == TextWrapping.Wrap) return 0f;
            EnsureLines();
            return Math.Max(0f, _maxLineWidth + GetRoundedCaretWidth() - GetViewportWidth());
        }
        return Math.Max(0f, GetTextWidth() + GetRoundedCaretWidth() - GetViewportWidth());
    }

    private float GetAscent()
    {
        if (FontSize <= 0) return 0f;
        TextMeasurer.GetFont(FontSize, FontFamily).GetFontMetrics(out var metrics);
        return -metrics.Ascent;
    }

    private LineSpan GetLine(int line)
    {
        EnsureLines();
        return _lines[Math.Clamp(line, 0, _lines.Count - 1)];
    }

    // The last caret position on a line (for End, clicks past the end and Up/Down): a line wrapped at a space ends
    // where the next line starts, which would show the caret there, so the caret stops before that space.
    private int GetLineCaretEnd(int line)
    {
        var span = GetLine(line);
        int end = span.Start + span.Length;
        bool wrapped = span.End == end && line < _lines.Count - 1;
        return wrapped && end > span.Start && char.IsWhiteSpace(Text[end - 1]) ? PreviousBoundary(end) : end;
    }

    private void EnsureLines()
    {
        EnsureTextMetrics();
        float wrapWidth = GetWrapWidth(GetViewportWidth());
        if (_linesVersion == _metricsVersion && wrapWidth.Equals(_linesWrapWidth))
        {
            return;
        }

        _linesVersion = _metricsVersion;
        _linesWrapWidth = wrapWidth;
        BuildLines(_lines, wrapWidth);

        _maxLineWidth = 0f;
        _lineTexts.Clear();
        foreach (var line in _lines)
        {
            _maxLineWidth = Math.Max(_maxLineWidth, line.Width);
            _lineTexts.Add(null);
        }
    }

    // The width lines wrap at for a viewport (leaving room for the caret), or infinity when they don't wrap.
    private float GetWrapWidth(float viewportWidth)
    {
        if (!IsMultiline || TextWrapping != TextWrapping.Wrap) return float.PositiveInfinity;
        float width = viewportWidth - GetRoundedCaretWidth();
        return width > 0 ? width : float.PositiveInfinity;
    }

    // Splits the text into visual lines: at '\n', "\r\n" and '\r', and with a finite wrap width after the last space
    // that fits (or between characters for a word wider than the line). Trailing spaces may overhang the wrap width.
    private void BuildLines(List<LineSpan> lines, float wrapWidth)
    {
        EnsureTextMetrics();
        lines.Clear();
        string text = Text;
        int n = text.Length;
        if (!IsMultiline)
        {
            AddLine(lines, 0, n, n);
            return;
        }

        bool wrap = float.IsFinite(wrapWidth);
        int paragraphStart = 0;
        while (true)
        {
            int paragraphEnd = text.IndexOfAny(s_lineBreaks, paragraphStart);
            if (paragraphEnd < 0) paragraphEnd = n;

            int lineStart = paragraphStart;
            if (wrap)
            {
                int breakAfterSpace = -1;
                int i = paragraphStart;
                while (i < paragraphEnd)
                {
                    int next = Math.Min(paragraphEnd, NextBoundary(i));
                    bool space = char.IsWhiteSpace(text[i]);
                    if (!space && i > lineStart && _offsets[next] - _offsets[lineStart] > wrapWidth)
                    {
                        int breakAt = breakAfterSpace > lineStart ? breakAfterSpace : i;
                        AddLine(lines, lineStart, breakAt - lineStart, breakAt);
                        lineStart = breakAt;
                        breakAfterSpace = -1;
                        continue; // re-check the current character against the new line
                    }

                    if (space) breakAfterSpace = next;
                    i = next;
                }
            }

            if (paragraphEnd >= n)
            {
                AddLine(lines, lineStart, n - lineStart, n);
                return;
            }

            int nextParagraph = paragraphEnd + (text[paragraphEnd] == '\r' && paragraphEnd + 1 < n && text[paragraphEnd + 1] == '\n' ? 2 : 1);
            AddLine(lines, lineStart, paragraphEnd - lineStart, nextParagraph);
            paragraphStart = nextParagraph;
        }
    }

    private void AddLine(List<LineSpan> lines, int start, int length, int end) =>
        lines.Add(new LineSpan(start, length, end, _offsets[start + length] - _offsets[start]));

    /// <summary>A visual line: its characters (without the line break), where the next line starts and its width.</summary>
    private readonly record struct LineSpan(int Start, int Length, int End, float Width);

    #endregion

    #region Caret and selection

    /// <summary>
    /// Moves the caret to <paramref name="index"/> (clamped, and moved back to the start of a text element if it would
    /// split one), scrolling it into view.
    /// </summary>
    /// <param name="index">The new caret position.</param>
    /// <param name="keepSelection"><c>true</c> to extend the selection from its anchor; <c>false</c> to clear it.</param>
    public void SetCaretIndex(int index, bool keepSelection = false)
    {
        _caretIndex = SnapBackward(index);
        if (!keepSelection)
        {
            SelectionAnchor = _caretIndex;
        }
        _preferredCaretX = float.NaN;
        _breakUndoCoalescing = true;
        EnsureCaretVisible();
        ResetCaretBlink();
        InvalidateVisual();
    }

    /// <summary>
    /// Selects <paramref name="length"/> characters from <paramref name="start"/> (clamped and widened to whole text
    /// elements); the caret ends up at the end of the selection.
    /// </summary>
    /// <param name="start">The index of the first character to select.</param>
    /// <param name="length">The number of characters to select.</param>
    public void Select(int start, int length)
    {
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        SelectionAnchor = SnapBackward(start);
        _caretIndex = SnapForward(start + length);
        _breakUndoCoalescing = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    /// <summary>Selects the whole text, with the caret at the end.</summary>
    public void SelectAll()
    {
        SelectionAnchor = 0;
        _caretIndex = Text.Length;
        _breakUndoCoalescing = true;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    /// <summary>Collapses the selection to the caret.</summary>
    public void ClearSelection()
    {
        SelectionAnchor = _caretIndex;
        InvalidateVisual();
    }

    /// <summary>
    /// Scrolls the text (see <see cref="ScrollOffset"/> and, for a multi-line box, <see cref="VerticalScrollOffset"/>) so
    /// the caret is inside the viewport.
    /// </summary>
    public void EnsureCaretVisible()
    {
        _scrollFitPending = false;
        _userScrolled = false;

        float viewportWidth = GetViewportWidth();
        if (viewportWidth <= 0)
        {
            SetScroll(0f, 0f);
            return;
        }

        bool multiline = IsMultiline;
        float caretWidth = GetRoundedCaretWidth();
        int line = multiline ? GetLineIndexFromCharacterIndex(_caretIndex) : 0;
        float caretTextX = GetCharacterOffset(_caretIndex);
        if (multiline)
        {
            caretTextX -= _offsets[_lines[line].Start];
        }

        float newX = _scrollX;
        if (caretTextX < newX)
        {
            newX = caretTextX;
        }
        else if (caretTextX + caretWidth > newX + viewportWidth)
        {
            newX = caretTextX + caretWidth - viewportWidth;
        }

        float newY = 0f;
        if (multiline)
        {
            float lineHeight = LineHeight;
            float viewportHeight = GetViewportHeight();
            float top = line * lineHeight;
            newY = _scrollY;
            if (top < newY)
            {
                newY = top;
            }
            else if (top + lineHeight > newY + viewportHeight)
            {
                newY = top + lineHeight - viewportHeight;
            }
            newY = Math.Clamp(newY, 0f, GetMaxVerticalScroll());
        }

        SetScroll(Math.Clamp(newX, 0f, GetMaxHorizontalScroll()), newY);
    }

    // Keeps the scroll offsets in range without following the caret (after the wheel scrolled a multi-line box).
    private void ClampScroll() =>
        SetScroll(Math.Clamp(_scrollX, 0f, GetMaxHorizontalScroll()), Math.Clamp(_scrollY, 0f, GetMaxVerticalScroll()));

    private void SetScroll(float x, float y)
    {
        if (MathF.Abs(x - _scrollX) > 0.001f || MathF.Abs(y - _scrollY) > 0.001f)
        {
            _scrollX = x;
            _scrollY = y;
            InvalidateVisual();
        }
    }

    private void ApplyPendingScrollFit()
    {
        if (!_scrollFitPending) return;

        _scrollFitPending = false;
        if (_userScrolled && IsMultiline)
        {
            ClampScroll();
        }
        else
        {
            EnsureCaretVisible();
        }
    }

    /// <summary>
    /// Selects the word (or run of whitespace or punctuation) at <paramref name="charIndex"/>. In a password field it
    /// selects everything.
    /// </summary>
    /// <param name="charIndex">A caret position inside or at the end of the word.</param>
    public void SelectWord(int charIndex)
    {
        string text = Text;
        if (string.IsNullOrEmpty(text)) return;
        if (PasswordChar != '\0')
        {
            SelectAll();
            return;
        }

        charIndex = Math.Clamp(charIndex, 0, text.Length);

        int targetIndex = charIndex;
        if (targetIndex >= text.Length && targetIndex > 0)
        {
            targetIndex = targetIndex - 1;
        }
        else if (targetIndex < text.Length && char.IsWhiteSpace(text[targetIndex]) && targetIndex > 0 && !char.IsWhiteSpace(text[targetIndex - 1]))
        {
            targetIndex = targetIndex - 1;
        }

        int cat = GetCharCategory(text[targetIndex]);

        int start = targetIndex;
        while (start > 0 && GetCharCategory(text[start - 1]) == cat)
        {
            start--;
        }

        int end = targetIndex + 1;
        while (end < text.Length && GetCharCategory(text[end]) == cat)
        {
            end++;
        }

        if (start < end)
        {
            Select(start, end - start);
        }
        else
        {
            SetCaretIndex(charIndex, keepSelection: false);
        }
    }

    private static int GetCharCategory(char c)
    {
        if (IsLineBreak(c)) return 4; // a double-click at a line's end selects the word before it, not the next line
        if (char.IsLetterOrDigit(c) || c == '_') return 1;
        if (char.IsWhiteSpace(c)) return 2;
        return 3;
    }

    /// <summary>Gets whether <paramref name="c"/> is part of a word for word navigation (letters, digits and '_').</summary>
    /// <param name="c">The character.</param>
    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Returns the start of the word before <paramref name="startIndex"/> (skipping whitespace first), as used by
    /// Ctrl+Left and Ctrl+Backspace.
    /// </summary>
    /// <param name="startIndex">The position to search backwards from.</param>
    public int FindPreviousWordBoundary(int startIndex)
    {
        string text = Text;
        if (string.IsNullOrEmpty(text) || startIndex <= 0)
            return 0;

        int i = Math.Clamp(startIndex, 0, text.Length);

        // 1. Skip whitespace backwards
        while (i > 0 && char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        if (i <= 0)
            return 0;

        // 2. Skip word characters or non-word/non-whitespace characters backwards
        if (IsWordChar(text[i - 1]))
        {
            while (i > 0 && IsWordChar(text[i - 1]))
            {
                i--;
            }
        }
        else
        {
            while (i > 0 && !char.IsWhiteSpace(text[i - 1]) && !IsWordChar(text[i - 1]))
            {
                i--;
            }
        }

        return SnapBackward(i);
    }

    /// <summary>
    /// Returns the start of the next word after <paramref name="startIndex"/> (skipping the current word and the
    /// whitespace after it), as used by Ctrl+Right and Ctrl+Delete.
    /// </summary>
    /// <param name="startIndex">The position to search forwards from.</param>
    public int FindNextWordBoundary(int startIndex)
    {
        string text = Text;
        if (string.IsNullOrEmpty(text) || startIndex >= text.Length)
            return string.IsNullOrEmpty(text) ? 0 : text.Length;

        int i = Math.Clamp(startIndex, 0, text.Length);

        if (char.IsWhiteSpace(text[i]))
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
        }
        else if (IsWordChar(text[i]))
        {
            while (i < text.Length && IsWordChar(text[i]))
            {
                i++;
            }
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
        }
        else
        {
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsWordChar(text[i]))
            {
                i++;
            }
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
        }

        return SnapForward(i);
    }

    // Previous/next word positions; a password field has no visible words, so they jump to the ends.
    private int PreviousWordPosition(int index) => PasswordChar != '\0' ? 0 : FindPreviousWordBoundary(index);

    private int NextWordPosition(int index) => PasswordChar != '\0' ? Text.Length : FindNextWordBoundary(index);

    /// <summary>
    /// Returns the caret position closest to <paramref name="localX"/>, a distance in pixels from the start of the text
    /// (see <see cref="GetTextOriginX"/>). Uses a binary search over the cached character offsets.
    /// </summary>
    internal int EstimateCaretIndex(float localX) => Text.Length == 0 || localX <= 0 ? 0 : HitTest(0, Text.Length, localX);

    /// <summary>
    /// Returns the caret position closest to <paramref name="point"/>, in the element's coordinates: on the line under
    /// the point (the first or last line when above or below them), at the character boundary nearest to its x.
    /// </summary>
    /// <param name="point">The point, for example a pointer position.</param>
    public int GetCharacterIndexFromPoint(Point point)
    {
        if (!IsMultiline) return EstimateCaretIndex(point.X - GetTextOriginX());

        EnsureLines();
        int line = (int)MathF.Floor((point.Y - GetLineTop(0)) / LineHeight);
        line = Math.Clamp(line, 0, _lines.Count - 1);
        return GetIndexInLine(line, point.X - GetLineOriginX(line));
    }

    // The caret position on a line closest to localX, measured from the line's origin.
    private int GetIndexInLine(int line, float localX)
    {
        int start = GetLine(line).Start;
        return HitTest(start, GetLineCaretEnd(line), _offsets[start] + localX);
    }

    // The text-element boundary between start and end whose offset is closest to x. Binary search over the offsets.
    private int HitTest(int start, int end, float x)
    {
        EnsureTextMetrics();
        if (end <= start || x <= _offsets[start]) return start;
        if (x >= _offsets[end]) return end;

        // Largest index whose offset is <= x.
        int lo = start;
        int hi = end;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (_offsets[mid] <= x) lo = mid;
            else hi = mid - 1;
        }

        int previous = SnapBackward(lo);
        int next = Math.Min(end, NextBoundary(previous));
        return x - _offsets[previous] < _offsets[next] - x ? previous : next;
    }

    /// <summary>
    /// Selects visual line <paramref name="line"/> without its line break, with the caret at its end, as a triple-click
    /// does in a multi-line box.
    /// </summary>
    /// <param name="line">The line index, clamped to 0 to <see cref="LineCount"/> − 1.</param>
    public void SelectLine(int line)
    {
        line = Math.Clamp(line, 0, LineCount - 1);
        Select(GetLineStart(line), GetLineLength(line));
    }

    #endregion

    #region Editing

    /// <summary>
    /// Replaces the selection (or inserts at the caret) with <paramref name="replacement"/> as if the user typed it: the
    /// text is cut at its first line break (unless <see cref="AcceptsReturn"/> is set) and limited by
    /// <see cref="MaxLength"/>. The caret ends up after the inserted text. Works even when <see cref="IsReadOnly"/> is set.
    /// </summary>
    /// <param name="replacement">The text to insert; <c>null</c> or empty deletes the selection.</param>
    public void ReplaceSelection(string replacement) => InsertText(replacement ?? string.Empty, EditKind.Other);

    /// <summary>Copies the selection to the clipboard. Does nothing without a selection or in a password field.</summary>
    public void Copy()
    {
        if (HasSelection && PasswordChar == '\0')
        {
            Clipboard.SetText(SelectedText);
        }
    }

    /// <summary>
    /// Moves the selection to the clipboard. Does nothing without a selection, when read-only or in a password field.
    /// </summary>
    public void Cut()
    {
        if (HasSelection && !IsReadOnly && PasswordChar == '\0')
        {
            Clipboard.SetText(SelectedText);
            ReplaceRange(SelectionStart, SelectionLength, string.Empty, EditKind.Other);
        }
    }

    /// <summary>
    /// Replaces the selection with the clipboard text, cut at its first line break (unless <see cref="AcceptsReturn"/> is
    /// set) and limited by <see cref="MaxLength"/>. Does nothing when read-only.
    /// </summary>
    public void Paste()
    {
        if (!IsReadOnly)
        {
            string? clip = Clipboard.GetText();
            if (!string.IsNullOrEmpty(clip))
            {
                InsertText(clip, EditKind.Other);
            }
        }
    }

    /// <summary>Gets whether <see cref="Undo"/> has an edit to revert.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether <see cref="Redo"/> has an undone edit to reapply.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Reverts the last edit (or group of typed characters) and restores the caret and selection.</summary>
    /// <returns><c>true</c> if an edit was undone; <c>false</c> if there was none or the box is read-only.</returns>
    public bool Undo()
    {
        if (IsReadOnly || _undoStack.Count == 0) return false;

        var entry = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(entry);
        ApplyText(Splice(Text, entry.Start, entry.Inserted.Length, entry.Removed), entry.CaretBefore, entry.AnchorBefore);
        _breakUndoCoalescing = true;
        return true;
    }

    /// <summary>Reapplies the last undone edit.</summary>
    /// <returns><c>true</c> if an edit was redone; <c>false</c> if there was none or the box is read-only.</returns>
    public bool Redo()
    {
        if (IsReadOnly || _redoStack.Count == 0) return false;

        var entry = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(entry);
        int caret = entry.Start + entry.Inserted.Length;
        ApplyText(Splice(Text, entry.Start, entry.Removed.Length, entry.Inserted), caret, caret);
        _breakUndoCoalescing = true;
        return true;
    }

    private static readonly char[] s_lineBreaks = ['\r', '\n'];

    private void InsertText(string text, EditKind kind)
    {
        int lineBreak = text.IndexOfAny(s_lineBreaks);
        if (lineBreak >= 0)
        {
            text = !AcceptsLineBreaks ? text[..lineBreak]
                : text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n')
                : text;
        }

        text = LimitToMaxLength(text);
        if (text.Length == 0 && !HasSelection)
        {
            return;
        }

        ReplaceRange(SelectionStart, SelectionLength, text, kind);
    }

    // Truncates text to insert so that the result stays within MaxLength, without splitting a text element.
    private string LimitToMaxLength(string text)
    {
        int maxLength = MaxLength;
        if (maxLength <= 0) return text;

        int available = maxLength - (Text.Length - SelectionLength);
        if (available <= 0) return string.Empty;
        if (text.Length <= available) return text;

        var span = text.AsSpan();
        int cut = 0;
        while (cut < span.Length)
        {
            int next = cut + Math.Max(1, StringInfo.GetNextTextElementLength(span[cut..]));
            if (next > available) break;
            cut = next;
        }
        return text[..cut];
    }

    private void DeleteBackward(bool word)
    {
        if (HasSelection)
        {
            ReplaceRange(SelectionStart, SelectionLength, string.Empty, EditKind.Other);
        }
        else if (_caretIndex > 0)
        {
            int start = word ? PreviousWordPosition(_caretIndex) : PreviousBoundary(_caretIndex);
            ReplaceRange(start, _caretIndex - start, string.Empty, word ? EditKind.Other : EditKind.Backspace);
        }
    }

    private void DeleteForward(bool word)
    {
        if (HasSelection)
        {
            ReplaceRange(SelectionStart, SelectionLength, string.Empty, EditKind.Other);
        }
        else if (_caretIndex < Text.Length)
        {
            int end = word ? NextWordPosition(_caretIndex) : NextBoundary(_caretIndex);
            ReplaceRange(_caretIndex, end - _caretIndex, string.Empty, word ? EditKind.Other : EditKind.Delete);
        }
    }

    private static string Splice(string text, int start, int removeLength, string insert) =>
        string.Concat(text.AsSpan(0, start), insert.AsSpan(), text.AsSpan(start + removeLength));

    // Every user edit goes through here: records undo, sets the text, moves the caret, then raises TextChanged.
    private void ReplaceRange(int start, int length, string insert, EditKind kind)
    {
        if (length == 0 && insert.Length == 0) return;

        string text = Text;
        string removed = length > 0 ? text.Substring(start, length) : string.Empty;
        RecordUndo(start, removed, insert, kind);

        int caret = start + insert.Length;
        ApplyText(Splice(text, start, length, insert), caret, caret);
    }

    private void ApplyText(string newText, int caret, int anchor)
    {
        _isEditing = true;
        try
        {
            Text = newText;
        }
        finally
        {
            _isEditing = false;
        }

        _caretIndex = SnapBackward(caret);
        SelectionAnchor = SnapBackward(anchor);
        _preferredCaretX = float.NaN;
        EnsureCaretVisible();
        ResetCaretBlink();
        InvalidateVisual();

        if (_textChangedPending)
        {
            _textChangedPending = false;
            TextChanged?.Invoke(this, Text);
        }
    }

    private void RecordUndo(int start, string removed, string inserted, EditKind kind)
    {
        _redoStack.Clear();
        int limit = UndoLimit;
        if (limit == 0)
        {
            _undoStack.Clear();
            return;
        }

        bool breakCoalescing = _breakUndoCoalescing;
        _breakUndoCoalescing = false;

        if (!breakCoalescing && _undoStack.Count > 0)
        {
            var last = _undoStack[^1];
            if (kind == EditKind.Typing && last.Kind == EditKind.Typing && removed.Length == 0 &&
                last.Start + last.Inserted.Length == start && !StartsNewWord(last.Inserted, inserted))
            {
                last.Inserted += inserted;
                return;
            }

            if (kind == EditKind.Backspace && last.Kind == EditKind.Backspace && inserted.Length == 0 && start + removed.Length == last.Start)
            {
                last.Start = start;
                last.Removed = removed + last.Removed;
                return;
            }

            if (kind == EditKind.Delete && last.Kind == EditKind.Delete && inserted.Length == 0 && start == last.Start)
            {
                last.Removed += removed;
                return;
            }
        }

        _undoStack.Add(new UndoEntry
        {
            Start = start,
            Removed = removed,
            Inserted = inserted,
            CaretBefore = _caretIndex,
            AnchorBefore = SelectionAnchor,
            Kind = kind,
        });
        TrimUndoHistory();
    }

    // Typing is grouped per word: a new group starts with the first non-space character after a space.
    private static bool StartsNewWord(string previous, string next) =>
        previous.Length > 0 && next.Length > 0 && char.IsWhiteSpace(previous[^1]) && !char.IsWhiteSpace(next[0]);

    private void TrimUndoHistory()
    {
        int limit = UndoLimit;
        if (_undoStack.Count > limit)
        {
            _undoStack.RemoveRange(0, _undoStack.Count - limit);
        }
        if (limit == 0)
        {
            _redoStack.Clear();
        }
    }

    private void OnTextChanged(string newText)
    {
        UpdateLabelAnimation(animate: true);
        InvalidateMeasure();
        InvalidateVisual();

        if (_isEditing)
        {
            // ApplyText moves the caret and then raises TextChanged.
            _textChangedPending = true;
            return;
        }

        // Set from code or a binding: the undo history no longer matches the text.
        _undoStack.Clear();
        _redoStack.Clear();
        _breakUndoCoalescing = true;
        _caretIndex = SnapBackward(_caretIndex);
        SelectionAnchor = SnapBackward(SelectionAnchor);
        EnsureCaretVisible();
        TextChanged?.Invoke(this, newText);
    }

    #endregion

    #region Focus, caret blinking and label

    /// <inheritdoc/>
    public override void OnGotFocus()
    {
        base.OnGotFocus();
        UpdateLabelAnimation(animate: true);
        UpdateCaretTimer();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        UpdateLabelAnimation(animate: true);
        UpdateCaretTimer();
        ClearSelection();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        UpdateCaretTimer();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        UpdateCaretTimer();
    }

    // The blink timer runs only while focused and displayed, so an idle or removed text box holds no timer.
    private void UpdateCaretTimer()
    {
        _caretBlinkOn = true;
        var interval = CaretBlinkInterval;
        if (IsFocused && IsAttachedToVisualTree && interval > TimeSpan.Zero)
        {
            if (_caretTimer == null)
            {
                _caretTimer = new DispatcherTimer();
                _caretTimer.Tick += OnCaretTimerTick;
            }
            _caretTimer.Interval = interval;
            _caretTimer.Start();
        }
        else
        {
            _caretTimer?.Stop();
        }
    }

    // Shows the caret and restarts the blink phase, so the caret doesn't blink while typing or moving it.
    private void ResetCaretBlink()
    {
        _caretBlinkOn = true;
        if (_caretTimer is { IsEnabled: true })
        {
            var interval = CaretBlinkInterval;
            if (interval > TimeSpan.Zero)
            {
                _caretTimer.Interval = interval;
            }
        }
    }

    private void OnCaretTimerTick(object? sender, EventArgs e)
    {
        if (!IsFocused || !IsAttachedToVisualTree)
        {
            UpdateCaretTimer();
            return;
        }

        _caretBlinkOn = !_caretBlinkOn;
        InvalidateVisual();
    }

    private void OnLabelChanged()
    {
        UpdateLabelAnimation(animate: false);
    }

    // AcceptsReturn or TextWrapping changed: the lines and the scrolling start over.
    private void OnLineModeChanged()
    {
        _preferredCaretX = float.NaN;
        EnsureCaretVisible();
    }

    private void UpdateLabelAnimation(bool animate)
    {
        bool shouldFloat = HasLabel && (IsFocused || !string.IsNullOrEmpty(Text));
        float target = shouldFloat ? 1f : 0f;

        if (MathF.Abs(LabelAnimationProgress - target) < 0.001f)
            return;

        // A newer animation takes over the value.
        _labelAnimation?.Stop();
        _labelAnimation = null;

        if (!animate || _clock == null)
        {
            LabelAnimationProgress = target;
            InvalidateVisual();
            return;
        }

        _labelAnimation = new FloatAnimation(
            LabelAnimationProgress,
            target,
            TimeSpan.FromMilliseconds(180),
            p =>
            {
                LabelAnimationProgress = p;
                InvalidateVisual();
            },
            Easing.EmphasizedDecelerate
        );

        _clock.Add(_labelAnimation);
    }

    #endregion

    #region Input

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        base.OnPointerPressed(e);
        e.Handled = true;

        if (e.Button == PointerButtons.Left && HasTrailingIcon && GetTrailingIconBounds().Contains(e.Position))
        {
            IsTrailingIconPressed = true;
            CapturePointer();
            InvalidateVisual();
            return;
        }

        Focus();
        CapturePointer();

        int idx = GetCharacterIndexFromPoint(e.Position);
        int clicks = e.ClickCount > 0 ? e.ClickCount : CountClicks(e);

        if (clicks >= 3)
        {
            if (IsMultiline)
            {
                SelectLine(GetLineIndexFromCharacterIndex(idx));
            }
            else
            {
                SelectAll();
            }
        }
        else if (clicks == 2)
        {
            SelectWord(idx);
        }
        else
        {
            SetCaretIndex(idx, keepSelection: e.Modifiers.HasFlag(ModifierKeys.Shift));
        }

        ResetCaretBlink();
        InvalidateVisual();
    }

    // Counts consecutive clicks for pointer events that don't carry a ClickCount (500 ms, 10 px).
    private int CountClicks(PointerEventArgs e)
    {
        ulong clickTime = e.TimestampMs > 0 ? e.TimestampMs : (ulong)Environment.TickCount64;
        ulong timeDelta = clickTime >= _lastClickTime ? clickTime - _lastClickTime : ulong.MaxValue;
        bool continues = _clickCount > 0 && timeDelta < 500 &&
                         MathF.Abs(e.Position.X - _lastClickPos.X) < 10f && MathF.Abs(e.Position.Y - _lastClickPos.Y) < 10f;

        _clickCount = continues ? _clickCount + 1 : 1;
        _lastClickTime = clickTime;
        _lastClickPos = e.Position;
        return _clickCount;
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (HasTrailingIcon)
        {
            var origin = PointToScreen(Point.Zero);
            var local = new Point(e.ScreenPosition.X - origin.X, e.ScreenPosition.Y - origin.Y);
            SetTrailingIconHovered(IsEnabled && GetTrailingIconBounds().Contains(local));
        }

        if (IsTrailingIconPressed)
        {
            return;
        }

        if (IsPointerCaptured)
        {
            int idx = GetCharacterIndexFromPoint(e.Position);
            if (idx != _caretIndex)
            {
                SetCaretIndex(idx, keepSelection: true);
            }
        }
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        bool iconClicked = IsTrailingIconPressed && IsTrailingIconHovered;
        if (IsTrailingIconPressed)
        {
            IsTrailingIconPressed = false;
            InvalidateVisual();
        }

        if (IsPointerCaptured)
        {
            ReleasePointerCapture();
            InvalidateVisual();
        }

        if (iconClicked)
        {
            e.Handled = true;
            OnTrailingIconClick();
        }
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsTrailingIconPressed)
        {
            SetTrailingIconHovered(false);
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        if (IsTrailingIconPressed)
        {
            IsTrailingIconPressed = false;
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A multi-line box scrolls three lines per notch (horizontally with a horizontal wheel when lines don't wrap) and
    /// handles the event while it can scroll that way, so an enclosing scroll viewer takes over at the ends.
    /// </remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled || !IsMultiline) return;

        ApplyPendingScrollFit();
        float step = 3f * LineHeight;
        float x = _scrollX;
        float y = _scrollY;
        if (e.DeltaY != 0)
        {
            y = Math.Clamp(y - e.DeltaY * step, 0f, GetMaxVerticalScroll());
        }
        if (e.DeltaX != 0)
        {
            x = Math.Clamp(x - e.DeltaX * step, 0f, GetMaxHorizontalScroll());
        }

        if (x != _scrollX || y != _scrollY)
        {
            SetScroll(x, y);
            _userScrolled = true;
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    public override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (IsReadOnly || !IsEnabled || !IsFocused || string.IsNullOrEmpty(e.Text))
            return;

        InsertText(e.Text, EditKind.Typing);
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || !IsFocused)
            return;

        bool shift = e.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = e.Modifiers.HasFlag(ModifierKeys.Control);

        var key = e.Key;
        if (key == Key.None)
        {
            // Unmapped keys: fall back to the Windows virtual-key codes for Backspace and Delete.
            key = e.KeyCode switch
            {
                8 => Key.Backspace,
                46 => Key.Delete,
                _ => Key.None
            };
        }

        bool readOnly = IsReadOnly;
        bool multiline = IsMultiline;
        bool handled = true;
        switch (key)
        {
            case Key.Enter when AcceptsLineBreaks && !readOnly && (e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0:
                InsertText("\n", EditKind.Typing);
                break;
            case Key.Up when multiline && !ctrl:
            case Key.Down when multiline && !ctrl:
            case Key.PageUp when multiline && !ctrl:
            case Key.PageDown when multiline && !ctrl:
                HandleVerticalNavigationKey(key, shift);
                break;
            case Key.A when ctrl:
                SelectAll();
                break;
            case Key.C when ctrl:
            case Key.Insert when ctrl && !shift:
                Copy();
                break;
            case Key.X when ctrl:
            case Key.Delete when shift && !ctrl:
                Cut();
                break;
            case Key.V when ctrl:
            case Key.Insert when shift && !ctrl:
                Paste();
                break;
            case Key.Z when ctrl && !shift:
                Undo();
                break;
            case Key.Z when ctrl && shift:
            case Key.Y when ctrl:
                Redo();
                break;
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
                HandleNavigationKey(key, shift, ctrl);
                break;
            case Key.Backspace when !readOnly:
                DeleteBackward(ctrl);
                break;
            case Key.Delete when !readOnly:
                DeleteForward(ctrl);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            ResetCaretBlink();
            e.Handled = true;
            InvalidateVisual();
        }
        else if (!readOnly && IsCharacterKey(key) && (e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
        {
            // The key types a character (it arrives as text input): keep it from running single-key shortcuts such as
            // a node editor's "X".
            e.Handled = true;
        }
    }

    private static bool IsCharacterKey(Key key) => key is
        (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9) or (>= Key.NumPad0 and <= Key.NumPad9) or
        Key.Space or Key.Minus or Key.Equal or Key.Comma or Key.Period or Key.Slash or Key.Semicolon or Key.Apostrophe or
        Key.LeftBracket or Key.RightBracket or Key.Backslash or Key.GraveAccent or
        Key.NumPadAdd or Key.NumPadSubtract or Key.NumPadMultiply or Key.NumPadDivide or Key.NumPadDecimal;

    private void HandleNavigationKey(Key key, bool shift, bool ctrl)
    {
        switch (key)
        {
            case Key.Left:
                if (ctrl)
                {
                    int target = PreviousWordPosition(shift ? _caretIndex : (HasSelection ? SelectionStart : _caretIndex));
                    SetCaretIndex(target, keepSelection: shift);
                }
                else if (shift)
                {
                    if (_caretIndex > 0) SetCaretIndex(PreviousBoundary(_caretIndex), keepSelection: true);
                }
                else if (HasSelection)
                {
                    SetCaretIndex(SelectionStart, keepSelection: false);
                }
                else if (_caretIndex > 0)
                {
                    SetCaretIndex(PreviousBoundary(_caretIndex), keepSelection: false);
                }
                break;

            case Key.Right:
                if (ctrl)
                {
                    int target = NextWordPosition(shift ? _caretIndex : (HasSelection ? SelectionStart + SelectionLength : _caretIndex));
                    SetCaretIndex(target, keepSelection: shift);
                }
                else if (shift)
                {
                    if (_caretIndex < Text.Length) SetCaretIndex(NextBoundary(_caretIndex), keepSelection: true);
                }
                else if (HasSelection)
                {
                    SetCaretIndex(SelectionStart + SelectionLength, keepSelection: false);
                }
                else if (_caretIndex < Text.Length)
                {
                    SetCaretIndex(NextBoundary(_caretIndex), keepSelection: false);
                }
                break;

            // A multi-line box goes to the start or end of the line, Ctrl+Home/End to the start or end of the text.
            case Key.Home:
                SetCaretIndex(IsMultiline && !ctrl ? GetLineStart(GetLineIndexFromCharacterIndex(_caretIndex)) : 0, keepSelection: shift);
                break;

            case Key.End:
                SetCaretIndex(IsMultiline && !ctrl ? GetLineCaretEnd(GetLineIndexFromCharacterIndex(_caretIndex)) : Text.Length, keepSelection: shift);
                break;
        }
    }

    // Up/Down move one line, Page Up/Page Down one viewport of lines; the caret keeps aiming for the column it started
    // in. Moving above the first line goes to the start of the text, below the last line to its end.
    private void HandleVerticalNavigationKey(Key key, bool shift)
    {
        int pageLines = Math.Max(1, (int)(GetViewportHeight() / LineHeight));
        int delta = key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            Key.PageUp => -pageLines,
            _ => pageLines,
        };

        int line = GetLineIndexFromCharacterIndex(_caretIndex);
        float scroll = ScrollOffset;
        float preferredX = float.IsNaN(_preferredCaretX) ? GetCharacterX(_caretIndex) + scroll : _preferredCaretX;

        int target = line + delta;
        int index = target < 0 ? 0
            : target >= LineCount ? Text.Length
            : GetIndexInLine(target, preferredX - scroll - GetLineOriginX(target));

        SetCaretIndex(index, keepSelection: shift);
        _preferredCaretX = preferredX;
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    /// <remarks>
    /// A multi-line box is as tall as a one-line field plus one <see cref="LineHeight"/> per further line, counting the
    /// lines (wrapped at the available width) between <see cref="MinLines"/> and <see cref="MaxLines"/>.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsMultiline)
        {
            return MeasureMultiline(availableSize);
        }

        var padding = Padding;
        string displayText = Text.Length > 0 ? DisplayText
            : !string.IsNullOrEmpty(Placeholder) ? Placeholder
            : !string.IsNullOrEmpty(Label) ? Label
            : " ";
        var textSize = TextMeasurer.Measure(displayText, FontSize, FontFamily);
        float contentW = GetTextContentStartX() + textSize.Width + padding.Right + 4 + (HasTrailingIcon ? 36f : 0f);
        bool hasSupportingText = HasSupportingText;
        if (hasSupportingText)
        {
            // Measure what is drawn: the validation error replaces the supporting text.
            var supportSize = TextMeasurer.Measure(DisplayedSupportingText, 12f, FontFamily);
            contentW = Math.Max(contentW, padding.Left + supportSize.Width + padding.Right);
        }

        float fieldHeight = HasLabel && Variant == TextBoxVariant.Filled ? Math.Max(FieldHeight, 52f) : FieldHeight;
        float containerH = Math.Max(fieldHeight, textSize.Height + padding.Vertical);
        float totalH = containerH + (hasSupportingText ? 20f : 0f);

        // The text's width is what the field would like; it can get narrower (down to 140) when there is less room, so
        // layouts such as a star grid column can tell what it needs.
        float width = float.IsFinite(availableSize.Width) ? Math.Min(contentW, availableSize.Width) : contentW;
        return new Size(
            Math.Max(140f, width),
            totalH
        );
    }

    private Size MeasureMultiline(Size availableSize)
    {
        var padding = Padding;
        float chromeWidth = GetTextContentStartX() + padding.Right + (HasTrailingIcon ? 36f : 0f);
        float availableTextWidth = float.IsFinite(availableSize.Width) ? availableSize.Width - chromeWidth : float.PositiveInfinity;

        _measureLines ??= new List<LineSpan>();
        BuildLines(_measureLines, GetWrapWidth(availableTextWidth));

        float textWidth = 0f;
        foreach (var line in _measureLines)
        {
            textWidth = Math.Max(textWidth, line.Width);
        }
        if (Text.Length == 0)
        {
            string hint = !string.IsNullOrEmpty(Placeholder) ? Placeholder : Label;
            textWidth = TextMeasurer.Measure(hint, FontSize, FontFamily).Width;
        }

        float contentW = chromeWidth + textWidth + 4;
        bool hasSupportingText = HasSupportingText;
        if (hasSupportingText)
        {
            var supportSize = TextMeasurer.Measure(DisplayedSupportingText, 12f, FontFamily);
            contentW = Math.Max(contentW, padding.Left + supportSize.Width + padding.Right);
        }

        int maxLines = MaxLines;
        int lines = Math.Max(MinLines, maxLines > 0 ? Math.Min(_measureLines.Count, maxLines) : _measureLines.Count);
        float containerH = GetOneLineFieldHeight() + (lines - 1) * LineHeight;
        float totalH = containerH + (hasSupportingText ? 20f : 0f);

        float width = float.IsFinite(availableSize.Width) ? Math.Min(contentW, availableSize.Width) : contentW;
        return new Size(Math.Max(140f, width), totalH);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // The bounds are assigned after this returns; the scroll offsets are fitted to them when next read.
        _scrollFitPending = true;
        return size;
    }

    #endregion

    private enum EditKind
    {
        Other,
        Typing,
        Backspace,
        Delete,
    }

    private sealed class UndoEntry
    {
        public int Start;
        public string Removed = string.Empty;
        public string Inserted = string.Empty;
        public int CaretBefore;
        public int AnchorBefore;
        public EditKind Kind;
    }
}

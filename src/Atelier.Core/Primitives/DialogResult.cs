namespace Atelier.Core.Primitives;

/// <summary>
/// Specifies identifiers to indicate the return value of a dialog.
/// </summary>
public enum DialogResult
{
    /// <summary>The dialog was closed without choosing a result (for example dismissed).</summary>
    None = 0,
    /// <summary>The OK button was chosen.</summary>
    Ok,
    /// <summary>The Cancel button was chosen.</summary>
    Cancel,
    /// <summary>The Yes button was chosen.</summary>
    Yes,
    /// <summary>The No button was chosen.</summary>
    No,
    /// <summary>The Abort button was chosen.</summary>
    Abort,
    /// <summary>The Retry button was chosen.</summary>
    Retry,
    /// <summary>The Ignore button was chosen.</summary>
    Ignore,
    /// <summary>A custom, application-defined button was chosen.</summary>
    Custom
}

/// <summary>
/// Predefined button sets that can be displayed on a standard <c>Dialog</c> (in Atelier.Controls).
/// </summary>
public enum DialogButtons
{
    /// <summary>No predefined buttons.</summary>
    None = 0,
    /// <summary>An OK button.</summary>
    Ok,
    /// <summary>OK and Cancel buttons.</summary>
    OkCancel,
    /// <summary>Yes and No buttons.</summary>
    YesNo,
    /// <summary>Yes, No and Cancel buttons.</summary>
    YesNoCancel
}

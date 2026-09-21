namespace Atelier.Core.Primitives;

/// <summary>
/// Specifies identifiers to indicate the return value of a dialog.
/// </summary>
public enum DialogResult
{
    None = 0,
    Ok,
    Cancel,
    Yes,
    No,
    Abort,
    Retry,
    Ignore,
    Custom
}

/// <summary>
/// Predefined button sets that can be displayed on a standard <see cref="Dialog"/>.
/// </summary>
public enum DialogButtons
{
    None = 0,
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel
}

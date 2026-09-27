using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="Popup"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class PopupMarkup
{
    /// <summary>Sets the content shown in the popup.</summary>
    public static T Child<T>(this T popup, UIElement? child) where T : Popup => popup.Set(Popup.ChildProperty, child);

    /// <summary>Sets the element the popup is positioned relative to.</summary>
    public static T PlacementTarget<T>(this T popup, UIElement? target) where T : Popup => popup.Set(Popup.PlacementTargetProperty, target);

    /// <summary>Sets where the popup opens relative to its target, e.g. below it. It flips to the other side when there isn't enough room.</summary>
    public static T Placement<T>(this T popup, PlacementMode placement) where T : Popup => popup.Set(Popup.PlacementProperty, placement);

    /// <summary>Shifts the popup horizontally by <paramref name="offset"/> pixels (positive is to the right) from its computed position.</summary>
    public static T HorizontalOffset<T>(this T popup, float offset) where T : Popup => popup.Set(Popup.HorizontalOffsetProperty, offset);

    /// <summary>Shifts the popup vertically by <paramref name="offset"/> pixels (positive is down) from its computed position.</summary>
    public static T VerticalOffset<T>(this T popup, float offset) where T : Popup => popup.Set(Popup.VerticalOffsetProperty, offset);

    /// <summary>Shifts the popup from its computed position by (<paramref name="horizontal"/>, <paramref name="vertical"/>) pixels.</summary>
    public static T Offset<T>(this T popup, float horizontal, float vertical) where T : Popup => popup.HorizontalOffset(horizontal).VerticalOffset(vertical);

    /// <summary>
    /// Keeps the popup open on clicks outside it and on Escape. By default (<c>false</c>) those close it ("light
    /// dismiss").
    /// </summary>
    public static T StaysOpen<T>(this T popup, bool staysOpen = true) where T : Popup => popup.Set(Popup.StaysOpenProperty, staysOpen);

    /// <summary>Opens or closes the popup.</summary>
    public static T IsOpen<T>(this T popup, bool isOpen = true) where T : Popup => popup.Set(Popup.IsOpenProperty, isOpen);

    /// <summary>Makes the popup at least as wide as its target, like a drop-down list.</summary>
    public static T MatchTargetWidth<T>(this T popup, bool matchTargetWidth = true) where T : Popup => popup.Set(Popup.MatchTargetWidthProperty, matchTargetWidth);

    /// <summary>Sets the shadow depth. 0 draws no shadow; the default is 6.</summary>
    public static T Elevation<T>(this T popup, float elevation) where T : Popup => popup.Set(Popup.ElevationProperty, elevation);

    /// <summary>Sets the color of the popup's outline. It is drawn only where <c>BorderThickness</c> is non-zero.</summary>
    public static T BorderBrush<T>(this T popup, Color color) where T : Popup => popup.Set(Popup.BorderBrushProperty, color);

    /// <summary>Sets the same outline width on all sides of the popup.</summary>
    public static T BorderThickness<T>(this T popup, float uniform) where T : Popup => popup.Set(Popup.BorderThicknessProperty, new Thickness(uniform));

    /// <summary>Sets the outline width of each side of the popup.</summary>
    public static T BorderThickness<T>(this T popup, Thickness thickness) where T : Popup => popup.Set(Popup.BorderThicknessProperty, thickness);

    /// <summary>Handles <see cref="Popup.Opened"/>, raised when the popup opens.</summary>
    public static T OnOpened<T>(this T popup, EventHandler handler) where T : Popup
    {
        popup.Opened += handler;
        return popup;
    }

    /// <summary>Runs <paramref name="action"/> when the popup opens (<see cref="Popup.Opened"/>).</summary>
    public static T OnOpened<T>(this T popup, Action action) where T : Popup => popup.OnOpened(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="Popup.Closed"/>, raised when the popup closes, including by light dismiss.</summary>
    public static T OnClosed<T>(this T popup, EventHandler handler) where T : Popup
    {
        popup.Closed += handler;
        return popup;
    }

    /// <summary>Runs <paramref name="action"/> when the popup closes (<see cref="Popup.Closed"/>).</summary>
    public static T OnClosed<T>(this T popup, Action action) where T : Popup => popup.OnClosed(MarkupExtensions.ToHandler(action));

    /// <summary>Binds whether the popup is open to a value of <paramref name="source"/>. With a <paramref name="setter"/>, light dismiss writes <c>false</c> back.</summary>
    public static T BindIsOpen<T, TSource>(this T popup, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Popup where TSource : class =>
        popup.BindToSource(Popup.IsOpenProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds whether the popup is open to a value of the DataContext. With a <paramref name="setter"/>, light dismiss writes <c>false</c> back.</summary>
    public static T BindIsOpen<T, TDataContext>(this T popup, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Popup where TDataContext : class =>
        popup.BindToDataContext(Popup.IsOpenProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>
/// Fluent methods for <see cref="Dialog"/>. Add buttons with <see cref="Dialog.AddButton"/>, which chains as well. See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class DialogMarkup
{
    /// <summary>Sets the headline shown at the top. <c>null</c> or empty hides it.</summary>
    public static T Title<T>(this T dialog, string? title) where T : Dialog => dialog.Set(Dialog.TitleProperty, title);

    /// <summary>Sets the supporting text below the title. It is shown only while there is no <c>Content</c>.</summary>
    public static T Message<T>(this T dialog, string? message) where T : Dialog => dialog.Set(Dialog.MessageProperty, message);

    /// <summary>Sets custom content shown below the title, in place of the message.</summary>
    public static T Content<T>(this T dialog, UIElement? content) where T : Dialog => dialog.Set(Dialog.ContentProperty, content);

    /// <summary>Replaces the buttons with a standard set, such as OK and Cancel.</summary>
    public static T ButtonsPreset<T>(this T dialog, DialogButtons preset) where T : Dialog => dialog.Set(Dialog.ButtonsPresetProperty, preset);

    /// <summary>Sets whether Escape closes the dialog with the cancel button's result (the default).</summary>
    public static T CloseOnEscape<T>(this T dialog, bool closeOnEscape = true) where T : Dialog => dialog.Set(Dialog.CloseOnEscapeProperty, closeOnEscape);

    /// <summary>Sets the shadow depth of the dialog. 0 draws no shadow.</summary>
    public static T Elevation<T>(this T dialog, float elevation) where T : Dialog => dialog.Set(Dialog.ElevationProperty, elevation);

    /// <summary>Sets the color of the dialog's outline. It is drawn only where <c>BorderThickness</c> is non-zero.</summary>
    public static T BorderBrush<T>(this T dialog, Color color) where T : Dialog => dialog.Set(Dialog.BorderBrushProperty, color);

    /// <summary>Sets the same outline width on all sides of the dialog.</summary>
    public static T BorderThickness<T>(this T dialog, float uniform) where T : Dialog => dialog.Set(Dialog.BorderThicknessProperty, new Thickness(uniform));

    /// <summary>Sets the outline width of each side of the dialog.</summary>
    public static T BorderThickness<T>(this T dialog, Thickness thickness) where T : Dialog => dialog.Set(Dialog.BorderThicknessProperty, thickness);

    /// <summary>Handles <see cref="Dialog.Opened"/>, raised when the dialog is shown.</summary>
    public static T OnOpened<T>(this T dialog, EventHandler handler) where T : Dialog
    {
        dialog.Opened += handler;
        return dialog;
    }

    /// <summary>Runs <paramref name="action"/> when the dialog is shown (<see cref="Dialog.Opened"/>).</summary>
    public static T OnOpened<T>(this T dialog, Action action) where T : Dialog => dialog.OnOpened(MarkupExtensions.ToHandler(action));

    /// <summary>Handles <see cref="Dialog.Closing"/>, raised before the dialog closes. Set <c>Cancel</c> to keep it open, e.g. to validate input.</summary>
    public static T OnClosing<T>(this T dialog, EventHandler<DialogClosingEventArgs> handler) where T : Dialog
    {
        dialog.Closing += handler;
        return dialog;
    }

    /// <summary>Handles <see cref="Dialog.Closed"/>, raised with the result after the dialog closed.</summary>
    public static T OnClosed<T>(this T dialog, EventHandler<DialogClosedEventArgs> handler) where T : Dialog
    {
        dialog.Closed += handler;
        return dialog;
    }

    /// <summary>Binds the title to a value of <paramref name="source"/>.</summary>
    public static T BindTitle<T, TSource>(this T dialog, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Dialog where TSource : class =>
        dialog.BindToSource(Dialog.TitleProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the title to a value of the DataContext.</summary>
    public static T BindTitle<T, TDataContext>(this T dialog, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Dialog where TDataContext : class =>
        dialog.BindToDataContext(Dialog.TitleProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the message to a value of <paramref name="source"/>.</summary>
    public static T BindMessage<T, TSource>(this T dialog, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Dialog where TSource : class =>
        dialog.BindToSource(Dialog.MessageProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the message to a value of the DataContext.</summary>
    public static T BindMessage<T, TDataContext>(this T dialog, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Dialog where TDataContext : class =>
        dialog.BindToDataContext(Dialog.MessageProperty, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="DialogHost"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class DialogHostMarkup
{
    /// <summary>Sets the regular content, shown underneath any open dialog.</summary>
    public static T Content<T>(this T host, UIElement? content) where T : DialogHost => host.Set(DialogHost.ContentProperty, content);

    /// <summary>
    /// Shows <paramref name="dialog"/> over the content, replacing any open dialogs; <c>null</c> closes them all. To
    /// stack a dialog on top of the open ones, use <see cref="Dialog.ShowAsync(DialogHost)"/>.
    /// </summary>
    public static T Dialog<T>(this T host, UIElement? dialog) where T : DialogHost => host.Set(DialogHost.DialogProperty, dialog);

    /// <summary>Closes all open dialogs when <paramref name="isOpen"/> is <c>false</c>. Setting <c>true</c> has no effect unless a dialog is open.</summary>
    public static T IsOpen<T>(this T host, bool isOpen = true) where T : DialogHost => host.Set(DialogHost.IsOpenProperty, isOpen);

    /// <summary>Lets a click on the darkened overlay dismiss the topmost dialog; a <see cref="Controls.Dialog"/> closes with Cancel.</summary>
    public static T CloseOnClickAway<T>(this T host, bool closeOnClickAway = true) where T : DialogHost => host.Set(DialogHost.CloseOnClickAwayProperty, closeOnClickAway);

    /// <summary>Sets the color of the scrim that darkens the content behind a dialog. The default is 50% black.</summary>
    public static T OverlayColor<T>(this T host, Color color) where T : DialogHost => host.Set(DialogHost.OverlayColorProperty, color);

    /// <summary>Sets an identifier, so code can find this host when a window has several.</summary>
    public static T Identifier<T>(this T host, string? identifier) where T : DialogHost => host.Set(DialogHost.IdentifierProperty, identifier);

    /// <summary>Handles <see cref="DialogHost.DialogOpened"/>, raised when a dialog is shown in this host.</summary>
    public static T OnDialogOpened<T>(this T host, EventHandler<DialogHostEventArgs> handler) where T : DialogHost
    {
        host.DialogOpened += handler;
        return host;
    }

    /// <summary>Handles <see cref="DialogHost.DialogClosed"/>, raised when a dialog of this host closed.</summary>
    public static T OnDialogClosed<T>(this T host, EventHandler<DialogHostEventArgs> handler) where T : DialogHost
    {
        host.DialogClosed += handler;
        return host;
    }

    /// <summary>Binds whether a dialog is open to a value of <paramref name="source"/>. With a <paramref name="setter"/>, closing writes <c>false</c> back.</summary>
    public static T BindIsOpen<T, TSource>(this T host, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : DialogHost where TSource : class =>
        host.BindToSource(DialogHost.IsOpenProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds whether a dialog is open to a value of the DataContext. With a <paramref name="setter"/>, closing writes <c>false</c> back.</summary>
    public static T BindIsOpen<T, TDataContext>(this T host, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : DialogHost where TDataContext : class =>
        host.BindToDataContext(DialogHost.IsOpenProperty, getter, setter, updateSourceTrigger, getterExpression);
}

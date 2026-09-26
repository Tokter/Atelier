using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Core.Tree;

namespace Atelier.Theming.Material;

/// <summary>
/// The Material Design 3 default styles of the controls: sizes, shapes, content alignment and typography from the MD3
/// component specs, so apps don't have to set them on every control.
/// </summary>
/// <remarks>
/// <para>
/// These are implicit theme styles (see <see cref="StyleManager.ThemeStyles"/>): they apply underneath the app's own
/// styles and local values, so anything set in the app still wins, and an app style only needs to set what it changes.
/// Inheritable text properties are only set on controls (a button's font size and weight flow to its label), never on
/// <see cref="TextBlock"/>, so text keeps inheriting from its container.
/// </para>
/// <para>
/// Implicit styles apply to derived types too; types with their own look (such as <see cref="TitleBarButton"/> or the
/// selection controls deriving from <see cref="ToggleButton"/>) get their own style so the base type's style doesn't
/// apply to them.
/// </para>
/// <para>
/// Heights follow <see cref="MaterialSizing"/>: with the default desktop sizing, buttons are 32 px, text fields and
/// selects 48 px and list rows 40 px, and switches use a 40×24 track; with <see cref="MaterialSizing.Touch"/> they have
/// the MD3 default sizes (40, 56, 48 px and 52×32).
/// </para>
/// </remarks>
public static class MaterialStyles
{
    /// <summary>
    /// Creates the default control styles for <paramref name="colors"/>, sized per <paramref name="sizing"/>
    /// (<see cref="MaterialSizing.Desktop"/> when <c>null</c>).
    /// </summary>
    public static List<Style> CreateStyles(MaterialColorScheme colors, MaterialSizing? sizing = null)
    {
        ArgumentNullException.ThrowIfNull(colors);
        sizing ??= MaterialSizing.Desktop;
        var labelLarge = MaterialTypescale.LabelLarge;
        var bodyLarge = MaterialTypescale.BodyLarge;

        // Heights from the MD3 specs (at density 0), reduced by the density; vertical padding centers the 20 px label line.
        float buttonHeight = sizing.Height(40f);        // 40 → 32 at density -2
        float buttonPaddingY = (buttonHeight - labelLarge.LineHeight) * 0.5f;
        float fieldHeight = sizing.Height(56f);         // text fields and selects: 56 → 48
        float listItemHeight = sizing.Height(48f);      // menu and list rows: 48 → 40
        float listItemPaddingY = (listItemHeight - MaterialTypescale.BodyMedium.LineHeight) * 0.5f;

        return
        [
            // Common buttons: fully rounded, 24 px side padding, medium-weight label centered (the 14 px label-large
            // size is the default font size, which stays inheritable). Applies to RepeatButton too.
            new Style(typeof(Button))
                .Set(Control.PaddingProperty, new Thickness(24, buttonPaddingY))
                .Set(UIElement.MinHeightProperty, buttonHeight)
                .Set(UIElement.MinWidthProperty, 48f)
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.Full))
                .Set(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)
                .Set(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
                .Set(Control.FontWeightProperty, labelLarge.Weight),

            // Window caption buttons keep their fixed size; only center their glyph.
            new Style(typeof(TitleBarButton))
                .Set(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)
                .Set(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center),

            // Standalone toggle buttons look like segmented buttons: button height, fully rounded, label centered.
            new Style(typeof(ToggleButton))
                .Set(Control.PaddingProperty, new Thickness(16, buttonPaddingY))
                .Set(UIElement.MinHeightProperty, buttonHeight)
                .Set(UIElement.MinWidthProperty, 48f)
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.Full))
                .Set(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)
                .Set(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
                .Set(Control.FontWeightProperty, labelLarge.Weight),

            // Selection controls (the RadioButton style falls back to this one) lay out their own indicator; their
            // labels use the regular body text of their container.
            new Style(typeof(CheckBox)),

            // Switches: the MD3 52×32 track, or the desktop 40×24 one.
            new Style(typeof(Switch))
                .Set(Switch.TrackWidthProperty, sizing.SwitchTrackWidth)
                .Set(Switch.TrackHeightProperty, sizing.SwitchTrackHeight),

            // Text fields: body-large input text, extra-small (4 px) corners, density-adjusted height.
            new Style(typeof(TextBox))
                .Set(Control.FontSizeProperty, bodyLarge.Size)
                .Set(TextBox.FieldHeightProperty, fieldHeight)
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.ExtraSmall)),

            // Selects (outlined): like text fields.
            new Style(typeof(ComboBox))
                .Set(Control.FontSizeProperty, bodyLarge.Size)
                .Set(Control.PaddingProperty, new Thickness(16, 8))
                .Set(UIElement.MinHeightProperty, fieldHeight)
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.ExtraSmall)),

            // List and menu items: density-adjusted rows (12 px side padding), square corners.
            new Style(typeof(ListBoxItem))
                .Set(Control.PaddingProperty, new Thickness(12, listItemPaddingY))
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.None)),

            // Menus (popups): extra-small corners, surface-container at elevation level 2, no outline.
            new Style(typeof(Popup))
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.ExtraSmall))
                .Set(Popup.ElevationProperty, MaterialElevation.Level2)
                .Set(Popup.BorderThicknessProperty, Thickness.Zero),

            // Dialogs: extra-large (28 px) corners, 24 px padding, elevation level 3, no outline.
            new Style(typeof(Dialog))
                .Set(Control.CornerRadiusProperty, new CornerRadius(MaterialShape.ExtraLarge))
                .Set(Control.PaddingProperty, new Thickness(24))
                .Set(Dialog.ElevationProperty, MaterialElevation.Level3)
                .Set(Dialog.BorderThicknessProperty, Thickness.Zero),

            // The scrim behind modal dialogs: the scrim color at 32%.
            new Style(typeof(DialogHost))
                .Set(DialogHost.OverlayColorProperty, colors.Scrim.WithAlpha(0.32f)),

            // Cards: medium (12 px) corners.
            new Style(typeof(Card))
                .Set(Atelier.Layout.Border.CornerRadiusProperty, new CornerRadius(MaterialShape.Medium)),
        ];
    }
}

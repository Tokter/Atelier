using System;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum TransformOriginPreset
{
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public partial class TransformationViewModel : PageViewModel
{
    // Playground
    [ObservableProperty]
    private bool _useLayoutTransform;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _rotation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _scaleX = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _scaleY = 1;

    [ObservableProperty]
    private bool _uniformScale = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _skewX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _skewY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _translateX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Matrix), nameof(MatrixText))]
    private float _translateY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Origin))]
    private TransformOriginPreset _originPreset = TransformOriginPreset.Center;

    // Interaction with the transformed card
    [ObservableProperty]
    private int _clickCount;

    [ObservableProperty]
    private float _cardSliderValue = 50;

    [ObservableProperty]
    private bool _cardSwitch = true;

    // Window zoom
    [ObservableProperty]
    private float _zoom = 1;

    [ObservableProperty]
    private bool _zoomWholeWindow;

    public TransformationViewModel()
    {
        PageTitle = "Transform & Zoom";
        PageIcon = MaterialIconKind.CropRotate;
        Keywords = "transform rendertransform rotate scale skew translate origin matrix zoom hit testing";
    }

    /// <summary>The playground transform: scale, then skew, rotate and translate (degrees and pixels).</summary>
    public Matrix3x2 Matrix =>
        Matrix3x2.CreateScale(ScaleX, ScaleY)
        * Matrix3x2.CreateSkew(ToRadians(SkewX), ToRadians(SkewY))
        * Matrix3x2.CreateRotation(ToRadians(Rotation))
        * Matrix3x2.CreateTranslation(TranslateX, TranslateY);

    public string MatrixText
    {
        get
        {
            var m = Matrix;
            return $"[{m.M11:0.00} {m.M12:0.00}; {m.M21:0.00} {m.M22:0.00}; {m.M31:0} {m.M32:0}]";
        }
    }

    public Point Origin => OriginPreset switch
    {
        TransformOriginPreset.TopLeft => new Point(0, 0),
        TransformOriginPreset.TopRight => new Point(1, 0),
        TransformOriginPreset.BottomLeft => new Point(0, 1),
        TransformOriginPreset.BottomRight => new Point(1, 1),
        _ => new Point(0.5f, 0.5f),
    };

    partial void OnScaleXChanged(float value)
    {
        if (UniformScale)
        {
            ScaleY = value;
        }
    }

    partial void OnScaleYChanged(float value)
    {
        if (UniformScale)
        {
            ScaleX = value;
        }
    }

    [RelayCommand]
    private void CardClicked() => ClickCount++;

    [RelayCommand]
    private void Preset(string name)
    {
        Reset();
        switch (name)
        {
            case "Tilt":
                Rotation = -12;
                SkewX = 8;
                break;
            case "Shear":
                SkewX = 25;
                break;
            case "Stamp":
                Rotation = 18;
                UniformScale = true;
                ScaleX = 1.15f;
                break;
            case "Stretch":
                UniformScale = false;
                ScaleX = 1.5f;
                ScaleY = 0.75f;
                break;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        UniformScale = true;
        Rotation = 0;
        ScaleX = 1;
        ScaleY = 1;
        SkewX = 0;
        SkewY = 0;
        TranslateX = 0;
        TranslateY = 0;
        OriginPreset = TransformOriginPreset.Center;
    }

    [RelayCommand]
    private void SetZoom(string percent) => Zoom = float.Parse(percent, System.Globalization.CultureInfo.InvariantCulture) / 100f;

    private static float ToRadians(float degrees) => degrees * MathF.PI / 180f;
}

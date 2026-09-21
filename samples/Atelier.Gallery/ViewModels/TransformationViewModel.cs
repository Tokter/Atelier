using System;
using System.Numerics;
using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class TransformationViewModel : PageViewModel
{
    // ==========================================
    // 1. Accessibility Zoom State
    // ==========================================
    [ObservableProperty]
    private float _windowZoom = 1.0f;

    [ObservableProperty]
    private bool _scaleWholeApp = false;

    public Action<float, bool>? RequestZoomUpdate { get; set; }

    // ==========================================
    // 2. 2D Affine Transformation Matrix State
    // ==========================================
    [ObservableProperty]
    private float _rotationDegrees = 0f;

    [ObservableProperty]
    private float _scaleX = 1.0f;

    [ObservableProperty]
    private float _scaleY = 1.0f;

    [ObservableProperty]
    private bool _uniformScaling = true;

    [ObservableProperty]
    private float _skewXDegrees = 0f;

    [ObservableProperty]
    private float _skewYDegrees = 0f;

    [ObservableProperty]
    private float _translationX = 0f;

    [ObservableProperty]
    private float _translationY = 0f;

    [ObservableProperty]
    private float _originX = 0.5f;

    [ObservableProperty]
    private float _originY = 0.5f;

    [ObservableProperty]
    private bool _isLayoutTransformMode = false;

    public Action? RequestTransformUpdate { get; set; }

    // ==========================================
    // 3. Transformed Surface Interactive State
    // ==========================================
    [ObservableProperty]
    private int _targetClickCount = 0;

    [ObservableProperty]
    private float _targetSliderValue = 50f;

    [ObservableProperty]
    private string _targetInputText = "Interactive while transformed!";

    [ObservableProperty]
    private bool _targetSwitchState = true;

    [ObservableProperty]
    private string _interactionLog = "Interactivity verified: Click button or drag slider to test transformed hit-testing.";

    // ==========================================
    // 4. Mathematical Matrix Computed Properties
    // ==========================================
    public const float TargetWidth = 240f;
    public const float TargetHeight = 180f;

    public Matrix3x2 BaseMatrix
    {
        get
        {
            float rad = RotationDegrees * (MathF.PI / 180f);
            float skewXRad = SkewXDegrees * (MathF.PI / 180f);
            float skewYRad = SkewYDegrees * (MathF.PI / 180f);

            return Matrix3x2.CreateScale(ScaleX, ScaleY)
                 * Matrix3x2.CreateSkew(skewXRad, skewYRad)
                 * Matrix3x2.CreateRotation(rad)
                 * Matrix3x2.CreateTranslation(TranslationX, TranslationY);
        }
    }

    public Matrix3x2 CurrentMatrix
    {
        get
        {
            float ox = TargetWidth * OriginX;
            float oy = TargetHeight * OriginY;

            return Matrix3x2.CreateTranslation(-ox, -oy)
                 * BaseMatrix
                 * Matrix3x2.CreateTranslation(ox, oy);
        }
    }

    public Point CurrentOrigin => new(OriginX, OriginY);

    public bool IsCenterOrigin => MathF.Abs(OriginX - 0.5f) < 0.01f && MathF.Abs(OriginY - 0.5f) < 0.01f;
    public bool IsTopLeftOrigin => MathF.Abs(OriginX - 0.0f) < 0.01f && MathF.Abs(OriginY - 0.0f) < 0.01f;
    public bool IsTopRightOrigin => MathF.Abs(OriginX - 1.0f) < 0.01f && MathF.Abs(OriginY - 0.0f) < 0.01f;
    public bool IsBottomLeftOrigin => MathF.Abs(OriginX - 0.0f) < 0.01f && MathF.Abs(OriginY - 1.0f) < 0.01f;
    public bool IsBottomRightOrigin => MathF.Abs(OriginX - 1.0f) < 0.01f && MathF.Abs(OriginY - 1.0f) < 0.01f;

    public string ActiveOriginName
    {
        get
        {
            if (IsCenterOrigin) return "Center";
            if (IsTopLeftOrigin) return "Top-Left";
            if (IsTopRightOrigin) return "Top-Right";
            if (IsBottomLeftOrigin) return "Bottom-Left";
            if (IsBottomRightOrigin) return "Bottom-Right";
            return $"{OriginX * 100:F0}%, {OriginY * 100:F0}%";
        }
    }

    public string MatrixM11 => $"{CurrentMatrix.M11:F3}";
    public string MatrixM12 => $"{CurrentMatrix.M12:F3}";
    public string MatrixM21 => $"{CurrentMatrix.M21:F3}";
    public string MatrixM22 => $"{CurrentMatrix.M22:F3}";
    public string MatrixM31 => $"{CurrentMatrix.M31:F1} px";
    public string MatrixM32 => $"{CurrentMatrix.M32:F1} px";

    public string DeterminantText
    {
        get
        {
            float det = CurrentMatrix.GetDeterminant();
            return $"det(M) = {det:F4}";
        }
    }

    public string InvertibleStatus
    {
        get
        {
            bool inv = Matrix3x2.Invert(CurrentMatrix, out _);
            return inv ? "Invertible (Hit-test active)" : "Singular (Non-invertible)";
        }
    }

    public TransformationViewModel()
    {
        PageTitle = "Transform & Zoom";
        PageIcon = MaterialIconKind.CropRotate;
    }

    partial void OnWindowZoomChanged(float value)
    {
        RequestZoomUpdate?.Invoke(value, ScaleWholeApp);
    }

    partial void OnScaleWholeAppChanged(bool value)
    {
        RequestZoomUpdate?.Invoke(WindowZoom, value);
    }

    partial void OnRotationDegreesChanged(float value) => OnMatrixUpdated();

    partial void OnScaleXChanged(float value)
    {
        if (UniformScaling && MathF.Abs(ScaleY - value) > 0.001f)
        {
            ScaleY = value;
        }
        OnMatrixUpdated();
    }

    partial void OnScaleYChanged(float value)
    {
        if (UniformScaling && MathF.Abs(ScaleX - value) > 0.001f)
        {
            ScaleX = value;
        }
        OnMatrixUpdated();
    }

    partial void OnSkewXDegreesChanged(float value) => OnMatrixUpdated();
    partial void OnSkewYDegreesChanged(float value) => OnMatrixUpdated();
    partial void OnTranslationXChanged(float value) => OnMatrixUpdated();
    partial void OnTranslationYChanged(float value) => OnMatrixUpdated();

    partial void OnOriginXChanged(float value)
    {
        OnPropertyChanged(nameof(IsCenterOrigin));
        OnPropertyChanged(nameof(IsTopLeftOrigin));
        OnPropertyChanged(nameof(IsTopRightOrigin));
        OnPropertyChanged(nameof(IsBottomLeftOrigin));
        OnPropertyChanged(nameof(IsBottomRightOrigin));
        OnPropertyChanged(nameof(ActiveOriginName));
        OnMatrixUpdated();
    }

    partial void OnOriginYChanged(float value)
    {
        OnPropertyChanged(nameof(IsCenterOrigin));
        OnPropertyChanged(nameof(IsTopLeftOrigin));
        OnPropertyChanged(nameof(IsTopRightOrigin));
        OnPropertyChanged(nameof(IsBottomLeftOrigin));
        OnPropertyChanged(nameof(IsBottomRightOrigin));
        OnPropertyChanged(nameof(ActiveOriginName));
        OnMatrixUpdated();
    }

    partial void OnTargetSliderValueChanged(float value)
    {
        InteractionLog = $"Transformed Slider dragged to {value:F0}%!";
    }

    partial void OnTargetSwitchStateChanged(bool value)
    {
        InteractionLog = $"Transformed Switch toggled to {(value ? "ON" : "OFF")}!";
    }

    partial void OnIsLayoutTransformModeChanged(bool value)
    {
        InteractionLog = value
            ? "Pipeline switched to LayoutTransform: Element participates in Measure/Arrange, expanding layout space."
            : "Pipeline switched to RenderTransform: Element applies post-layout at draw time; hinges around pivot pin.";
        OnMatrixUpdated();
    }

    private void OnMatrixUpdated()
    {
        OnPropertyChanged(nameof(BaseMatrix));
        OnPropertyChanged(nameof(CurrentMatrix));
        OnPropertyChanged(nameof(CurrentOrigin));
        OnPropertyChanged(nameof(MatrixM11));
        OnPropertyChanged(nameof(MatrixM12));
        OnPropertyChanged(nameof(MatrixM21));
        OnPropertyChanged(nameof(MatrixM22));
        OnPropertyChanged(nameof(MatrixM31));
        OnPropertyChanged(nameof(MatrixM32));
        OnPropertyChanged(nameof(DeterminantText));
        OnPropertyChanged(nameof(InvertibleStatus));
        RequestTransformUpdate?.Invoke();
    }

    [RelayCommand]
    private void SetZoom(float factor)
    {
        WindowZoom = factor;
    }

    [RelayCommand]
    private void ResetZoom()
    {
        WindowZoom = 1.0f;
        ScaleWholeApp = false;
        InteractionLog = "Accessibility Zoom reset to 100% (Normal).";
    }

    [RelayCommand]
    private void ResetTransforms()
    {
        RotationDegrees = 0f;
        ScaleX = 1.0f;
        ScaleY = 1.0f;
        SkewXDegrees = 0f;
        SkewYDegrees = 0f;
        TranslationX = 0f;
        TranslationY = 0f;
        OriginX = 0.5f;
        OriginY = 0.5f;
        InteractionLog = "All 2D matrix transforms reset to Identity.";
    }

    [RelayCommand]
    private void ApplyIsometricTilt()
    {
        RotationDegrees = -15f;
        SkewXDegrees = 12f;
        SkewYDegrees = 0f;
        ScaleX = 1.0f;
        ScaleY = 1.0f;
        TranslationX = 0f;
        TranslationY = 0f;
        InteractionLog = "Applied Isometric Tilt (-15° Rot, 12° SkewX).";
    }

    [RelayCommand]
    private void ApplyCardTilt()
    {
        UniformScaling = false;
        RotationDegrees = 20f;
        ScaleX = 0.85f;
        ScaleY = 1.05f;
        SkewXDegrees = -8f;
        SkewYDegrees = 0f;
        TranslationX = 0f;
        TranslationY = 0f;
        InteractionLog = "Applied Dynamic Card Tilt (20° Rot, non-uniform scale, -8° SkewX).";
    }

    [RelayCommand]
    private void ApplyBadgeStamp()
    {
        RotationDegrees = -12f;
        ScaleX = 1.15f;
        ScaleY = 1.15f;
        SkewXDegrees = 0f;
        SkewYDegrees = 0f;
        TranslationX = 0f;
        TranslationY = 0f;
        InteractionLog = "Applied Angled Badge Stamp (-12° Rot, 1.15x Scale).";
    }

    [RelayCommand]
    private void ApplyShear()
    {
        RotationDegrees = 0f;
        SkewXDegrees = 25f;
        SkewYDegrees = 0f;
        ScaleX = 1.0f;
        ScaleY = 1.0f;
        TranslationX = 0f;
        TranslationY = 0f;
        InteractionLog = "Applied Horizontal Shear (25° SkewX).";
    }

    [RelayCommand]
    public void SetOrigin(string preset)
    {
        switch (preset.ToLowerInvariant())
        {
            case "topleft":
            case "top-left":
                OriginX = 0f;
                OriginY = 0f;
                break;
            case "topright":
            case "top-right":
                OriginX = 1f;
                OriginY = 0f;
                break;
            case "bottomleft":
            case "bottom-left":
                OriginX = 0f;
                OriginY = 1f;
                break;
            case "bottomright":
            case "bottom-right":
                OriginX = 1f;
                OriginY = 1f;
                break;
            case "center":
            default:
                OriginX = 0.5f;
                OriginY = 0.5f;
                break;
        }
        InteractionLog = $"Pivot origin changed to {ActiveOriginName}. Card hinges around ({OriginX * TargetWidth:F0}px, {OriginY * TargetHeight:F0}px).";
    }

    [RelayCommand]
    private void TargetButtonClicked()
    {
        TargetClickCount++;
        InteractionLog = $"Target button clicked {TargetClickCount} time{(TargetClickCount == 1 ? "" : "s")}! (Pivot: {ActiveOriginName}, Rot={RotationDegrees:F0}°, Scale={ScaleX:F2}x)";
    }
}

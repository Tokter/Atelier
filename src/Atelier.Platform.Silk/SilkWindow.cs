using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;
using SkiaSharp;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.HotReload;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Core.Platform;
using Atelier.Core.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SilkKey = global::Silk.NET.Input.Key;

namespace Atelier.Platform.Silk;

public class SilkWindow : IDisposable
{
    private readonly IWindow _window;
    private IInputContext? _inputContext;
    private GRGlInterface? _glInterface;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private PaintRegistry? _paintRegistry;
    private readonly AnimationClock _animationClock = new();

    private readonly ConcurrentQueue<Action> _dispatchQueue = new();
    private Func<UIElement>? _contentFactory;

    private UIElement? _rootElement;
    private UIElement? _hoveredElement;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    static SilkWindow()
    {
        InitializeSingleFileNativeProbing();
    }

    private static void InitializeSingleFileNativeProbing()
    {
        string appName = AppDomain.CurrentDomain.FriendlyName;
        if (string.IsNullOrEmpty(appName) || appName == "DefaultDomain")
        {
            appName = "Atelier";
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                string netTemp = Path.Combine(Path.GetTempPath(), ".net", appName);
                if (Directory.Exists(netTemp))
                {
                    var files = Directory.GetFiles(netTemp, "glfw3.dll", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                        string dir = Path.GetDirectoryName(files[0])!;
                        SetDllDirectory(dir);
                        try
                        {
                            NativeLibrary.Load(files[0]);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(global::Silk.NET.GLFW.Glfw).Assembly, (name, asm, searchPath) =>
            {
                if (name.Contains("glfw", StringComparison.OrdinalIgnoreCase))
                {
                    string netTemp = Path.Combine(Path.GetTempPath(), ".net", appName);
                    if (Directory.Exists(netTemp))
                    {
                        var files = Directory.GetFiles(netTemp, "glfw3.dll", SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                            if (NativeLibrary.TryLoad(files[0], out var handle))
                            {
                                return handle;
                            }
                        }
                    }
                }
                return IntPtr.Zero;
            });
        }
        catch { }

        try
        {
            GlfwWindowing.RegisterPlatform();
            GlfwInput.RegisterPlatform();
        }
        catch { }
    }

    // Threading & Dispatch
    private int _mainThreadId;
    public int MainThreadId => _mainThreadId;

    // Performance & Frame Stats
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _frameCount = 0;
    private double _fpsSampleStart = 0;
    private string _fpsText = string.Empty;
    private double _fpsTextValue = double.NaN;
    private int _fpsTextAnimations = -1;

    /// <summary>
    /// Gets the number of frames actually rendered per second. With render-on-demand this drops to zero while
    /// nothing changes on screen.
    /// </summary>
    public double CurrentFps { get; private set; }

    private bool _showFpsOverlay = true;

    /// <summary>
    /// Gets or sets whether the frame-rate overlay is drawn in the bottom-left corner.
    /// </summary>
    public bool ShowFpsOverlay
    {
        get => _showFpsOverlay;
        set { _showFpsOverlay = value; InvalidateRender(); }
    }

    // Render-on-demand. A frame is drawn only when something changed. When nothing is animating, repeating or queued,
    // the loop switches Silk to event-driven mode and sleeps until OS input arrives or InvalidateRender() wakes it,
    // instead of redrawing an unchanged screen at the display refresh rate.
    private volatile bool _needsRender = true;
    private volatile bool _isLoaded;
    private bool _resumingFromIdle;

    /// <summary>
    /// Requests a new frame, waking the render loop if it is idle. Safe to call from any thread.
    /// </summary>
    /// <remarks>
    /// Changes inside the element tree request frames automatically (through <see cref="VisualNode.InvalidateVisual"/>
    /// and layout invalidation). Call this when something outside the tree affects what is drawn.
    /// </remarks>
    public void InvalidateRender()
    {
        _needsRender = true;
        if (_isLoaded && !_isClosing && !_isDisposed && _window.IsEventDriven)
        {
            _window.ContinueEvents();
        }
    }

    // Also wakes the loop, so changes made from a background thread (e.g. a timer updating a bound view model) show up
    // even while the window is idle. During a frame the loop is not event-driven, so this is just a flag write.
    private void OnTreeInvalidated() => InvalidateRender();

    private void OnPopupChanged(Popup popup) => InvalidateRender();

    private void OnThemeChanged(Theme theme) => InvalidateRender();

    // Global style changes are coalesced: a theme may add many styles, but the tree is restyled once per frame.
    private volatile bool _globalStylesChanged;

    private void OnGlobalStylesChanged()
    {
        _globalStylesChanged = true;
        InvalidateRender();
    }

    private void OnWindowStateChanged(WindowState state) => InvalidateRender();

    private void OnWindowFocusChanged(bool focused) => InvalidateRender();

    /// <summary>
    /// Gets or sets the root element displayed in the window.
    /// </summary>
    public UIElement? Content
    {
        get => _rootElement;
        set
        {
            if (value == _rootElement)
            {
                return;
            }

            if (_rootElement != null)
            {
                _rootElement.NeedsVisualUpdate -= OnTreeInvalidated;
                _rootElement.NeedsLayoutUpdate -= OnTreeInvalidated;
                _rootElement.DetachFromHost();
            }

            _rootElement = value;
            _hoveredElement = null;
            _pressedElement = null;
            if (_rootElement != null)
            {
                _rootElement.NeedsVisualUpdate += OnTreeInvalidated;
                _rootElement.NeedsLayoutUpdate += OnTreeInvalidated;
                _rootElement.AttachToHost();
                _rootElement.InvalidateMeasure();
                _rootElement.InvalidateVisual();
            }
            InvalidateRender();
        }
    }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread at the start of the next frame, waking the loop if idle.
    /// Safe to call from any thread.
    /// </summary>
    public void Dispatch(Action action)
    {
        _dispatchQueue.Enqueue(action);
        InvalidateRender();
    }

    public void SetContent(Func<UIElement> contentFactory)
    {
        _contentFactory = contentFactory;
        Content = contentFactory();
    }

    public void ReloadContent()
    {
        if (_contentFactory != null)
        {
            Content = _contentFactory();
        }
        else if (_rootElement != null)
        {
            _rootElement.InvalidateMeasure();
            _rootElement.InvalidateVisual();
        }
    }

    private void OnHotReloadTriggered()
    {
        Dispatch(() =>
        {
            Console.WriteLine("[HotReload] UI rebuild triggered by Hot Reload.");
            ReloadContent();
        });
    }

    public static SilkWindow? Current { get; private set; }

    public bool IsTitleLess { get; }
    public bool IsTransparent { get; }
    private float _windowOpacity = 1.0f;
    private Color? _windowBackground;

    /// <summary>Gets or sets the opacity applied to the window background (requires a transparent window).</summary>
    public float WindowOpacity
    {
        get => _windowOpacity;
        set { _windowOpacity = value; InvalidateRender(); }
    }

    /// <summary>Gets or sets the window background; <c>null</c> uses the theme's background color.</summary>
    public Color? WindowBackground
    {
        get => _windowBackground;
        set { _windowBackground = value; InvalidateRender(); }
    }
    public int ResizeBorderThickness { get; set; } = 4;
    public string? WindowIconPath { get; set; }

    public void SetWindowIcon(string pathOrResource)
    {
        try
        {
            using var bitmap = Atelier.Controls.Image.LoadBitmap(pathOrResource);
            if (bitmap != null)
            {
                SetWindowIcon(bitmap);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SilkWindow] Failed to load window icon from '{pathOrResource}': {ex.Message}");
        }
    }

    public void SetWindowIcon(SKBitmap bitmap)
    {
        if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0) return;

        try
        {
            using var rgbaBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(rgbaBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), null);
            }

            byte[] pixelBytes = rgbaBitmap.Bytes;
            var rawImage = new RawImage(rgbaBitmap.Width, rgbaBitmap.Height, pixelBytes);
            _window.SetWindowIcon(new ReadOnlySpan<RawImage>(new[] { rawImage }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SilkWindow] Failed to set window icon: {ex.Message}");
        }
    }

    public WindowState WindowState
    {
        get
        {
            if (TryGetHwnd(out var hwnd))
            {
                if (IsIconic(hwnd)) return WindowState.Minimized;
                if (IsZoomed(hwnd)) return WindowState.Maximized;
                return WindowState.Normal;
            }
            return _window.WindowState;
        }
        set
        {
            if (TryGetHwnd(out var hwnd))
            {
                switch (value)
                {
                    case WindowState.Minimized:
                        ShowWindow(hwnd, SW_MINIMIZE);
                        return;
                    case WindowState.Maximized:
                        ShowWindow(hwnd, SW_MAXIMIZE);
                        return;
                    case WindowState.Normal:
                        ShowWindow(hwnd, SW_RESTORE);
                        return;
                }
            }
            _window.WindowState = value;
        }
    }

    public Vector2D<int> Position
    {
        get => _window.Position;
        set => _window.Position = value;
    }

    public Vector2D<int> Size
    {
        get => _window.Size;
        set => _window.Size = value;
    }

    private bool _isClosing;
    private bool _isManualDragging;
    private Vector2D<int> _manualDragStartWinPos;
    private Vector2D<int> _manualDragStartMousePos;
    private SubclassProcDelegate? _wndProcDelegate;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SubclassProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProcDelegate pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProcDelegate pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    private const uint WM_CLOSE = 0x0010;
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const uint WM_NCCALCSIZE = 0x0083;
    private const uint WM_NCHITTEST = 0x0084;
    private const uint WM_NCDESTROY = 0x0082;
    private const uint WM_NCLBUTTONDOWN = 0x00A1;

    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NCCALCSIZE_PARAMS
    {
        public RECT rgrc0;
        public RECT rgrc1;
        public RECT rgrc2;
        public IntPtr lppos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private bool TryGetHwnd(out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var win32 = _window.Native?.Win32;
                if (win32.HasValue && win32.Value.Hwnd != IntPtr.Zero && IsWindow(win32.Value.Hwnd))
                {
                    hwnd = win32.Value.Hwnd;
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    public void DragMove()
    {
        if (TryGetHwnd(out var hwnd))
        {
            try
            {
                ReleaseCapture();
                SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                return;
            }
            catch
            {
                // Fallback to manual drag
            }
        }

        _isManualDragging = true;
        _manualDragStartWinPos = _window.Position;
        if (_inputContext?.Mice.Count > 0)
        {
            var m = _inputContext.Mice[0];
            _manualDragStartMousePos = new Vector2D<int>((int)m.Position.X, (int)m.Position.Y);
        }
    }

    public void Minimize()
    {
        if (TryGetHwnd(out var hwnd))
        {
            ShowWindow(hwnd, SW_MINIMIZE);
            return;
        }
        _window.WindowState = WindowState.Minimized;
    }

    public void Maximize()
    {
        if (TryGetHwnd(out var hwnd))
        {
            ShowWindow(hwnd, SW_MAXIMIZE);
            return;
        }
        _window.WindowState = WindowState.Maximized;
    }

    public void Restore()
    {
        if (TryGetHwnd(out var hwnd))
        {
            ShowWindow(hwnd, SW_RESTORE);
            return;
        }
        _window.WindowState = WindowState.Normal;
    }

    public void ToggleMaximize()
    {
        if (TryGetHwnd(out var hwnd))
        {
            if (IsZoomed(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(hwnd, SW_MAXIMIZE);
            }
            return;
        }
        _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    public void Close()
    {
        if (TryGetHwnd(out var hwnd))
        {
            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        Dispatch(() => _window.Close());
    }

    public SilkWindow(
        string title = "Atelier Application",
        int width = 1024,
        int height = 768,
        bool isTitleLess = false,
        bool isTransparent = false,
        float windowOpacity = 1.0f,
        Color? windowBackground = null,
        int resizeBorderThickness = 4,
        string? iconPath = "Assets/Icons/Atelier.png")
    {
        Current = this;
        IsTitleLess = isTitleLess;
        IsTransparent = isTransparent || windowOpacity < 1.0f;
        WindowOpacity = windowOpacity;
        WindowBackground = windowBackground;
        ResizeBorderThickness = resizeBorderThickness;
        WindowIconPath = iconPath;

        var options = WindowOptions.Default;
        options.Title = title;
        options.Size = new Vector2D<int>(width, height);
        options.VSync = true;
        // Buffers are swapped manually, only for frames that were actually rendered (see OnRender).
        options.ShouldSwapAutomatically = false;
        options.PreferredDepthBufferBits = 24;
        options.PreferredStencilBufferBits = 8;
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));

        if (isTitleLess && !OperatingSystem.IsWindows())
        {
            options.WindowBorder = WindowBorder.Hidden;
        }

        if (IsTransparent)
        {
            options.TransparentFramebuffer = true;
        }

        // Explicitly register GLFW windowing and input platforms to support Single-File publishing & Native AOT
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClosing;
        _window.StateChanged += OnWindowStateChanged;
        _window.FocusChanged += OnWindowFocusChanged;

        ThemeManager.ThemeChanged += OnThemeChanged;
        StyleManager.GlobalStyles.StylesChanged += OnGlobalStylesChanged;
        PopupManager.PopupOpened += OnPopupChanged;
        PopupManager.PopupClosed += OnPopupChanged;

        Atelier.Controls.Button.SetGlobalAnimationClock(_animationClock);
        CheckBox.SetGlobalAnimationClock(_animationClock);
        Atelier.Controls.Switch.SetGlobalAnimationClock(_animationClock);
        TextBox.SetGlobalAnimationClock(_animationClock);
        Slider.SetGlobalAnimationClock(_animationClock);
        ScrollViewer.SetGlobalAnimationClock(_animationClock);
        TransitioningContentControl.SetGlobalAnimationClock(_animationClock);
        DialogHost.RootVisualProvider = () => Current?.Content;

        HotReloadManager.HotReloadTriggered += OnHotReloadTriggered;
    }

    public void Run()
    {
        Current = this;
        _mainThreadId = Environment.CurrentManagedThreadId;
        Dispatcher.UIThread = new SilkDispatcher(this);
        var oldContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SilkSynchronizationContext(this));
        try
        {
            _window.Run();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(oldContext);
            Dispatcher.UIThread = Dispatcher.ImmediateDispatcher.Instance;
            Dispose();
        }
    }

    private void SetupWindowsTitlelessFrame()
    {
        if (!TryGetHwnd(out var hwnd)) return;

        _wndProcDelegate = SubclassProc;
        SetWindowSubclass(hwnd, _wndProcDelegate, (UIntPtr)1001, UIntPtr.Zero);

        // Extend frame into client area for DWM drop shadow
        var margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Notify DWM of frame change so WM_NCCALCSIZE is dispatched immediately
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }

    private IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        try
        {
            switch (uMsg)
            {
                case WM_NCCALCSIZE:
                    if (wParam != IntPtr.Zero)
                    {
                        if (IsZoomed(hWnd))
                        {
                            IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                            if (GetMonitorInfo(hMonitor, ref monitorInfo))
                            {
                                var csp = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);
                                csp.rgrc0 = monitorInfo.rcWork;
                                Marshal.StructureToPtr(csp, lParam, false);
                            }
                        }
                        return IntPtr.Zero;
                    }
                    else
                    {
                        if (IsZoomed(hWnd))
                        {
                            IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                            if (GetMonitorInfo(hMonitor, ref monitorInfo))
                            {
                                Marshal.StructureToPtr(monitorInfo.rcWork, lParam, false);
                            }
                        }
                        return IntPtr.Zero;
                    }

                case WM_GETMINMAXINFO:
                {
                    IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                    if (hMonitor != IntPtr.Zero)
                    {
                        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                        if (GetMonitorInfo(hMonitor, ref monitorInfo))
                        {
                            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                            mmi.ptMaxPosition.X = monitorInfo.rcWork.Left - monitorInfo.rcMonitor.Left;
                            mmi.ptMaxPosition.Y = monitorInfo.rcWork.Top - monitorInfo.rcMonitor.Top;
                            mmi.ptMaxSize.X = monitorInfo.rcWork.Right - monitorInfo.rcWork.Left;
                            mmi.ptMaxSize.Y = monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top;
                            Marshal.StructureToPtr(mmi, lParam, false);
                            return IntPtr.Zero;
                        }
                    }
                    break;
                }

                case WM_NCHITTEST:
                    if (!IsZoomed(hWnd))
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        if (GetWindowRect(hWnd, out RECT rect))
                        {
                            int resizeBorder = Math.Max(1, ResizeBorderThickness);
                            int cornerBorder = Math.Max(resizeBorder * 2, 8);

                            bool isTop = y >= rect.Top && y < rect.Top + resizeBorder;
                            bool isBottom = y >= rect.Bottom - resizeBorder && y < rect.Bottom;
                            bool isLeft = x >= rect.Left && x < rect.Left + resizeBorder;
                            bool isRight = x >= rect.Right - resizeBorder && x < rect.Right;

                            bool isCornerTop = y >= rect.Top && y < rect.Top + cornerBorder;
                            bool isCornerBottom = y >= rect.Bottom - cornerBorder && y < rect.Bottom;
                            bool isCornerLeft = x >= rect.Left && x < rect.Left + cornerBorder;
                            bool isCornerRight = x >= rect.Right - cornerBorder && x < rect.Right;

                            if (isCornerTop && isCornerLeft) return (IntPtr)HTTOPLEFT;
                            if (isCornerTop && isCornerRight) return (IntPtr)HTTOPRIGHT;
                            if (isCornerBottom && isCornerLeft) return (IntPtr)HTBOTTOMLEFT;
                            if (isCornerBottom && isCornerRight) return (IntPtr)HTBOTTOMRIGHT;

                            if (isTop) return (IntPtr)HTTOP;
                            if (isBottom) return (IntPtr)HTBOTTOM;
                            if (isLeft) return (IntPtr)HTLEFT;
                            if (isRight) return (IntPtr)HTRIGHT;
                        }
                    }
                    break;

                case WM_NCDESTROY:
                    if (_wndProcDelegate != null)
                    {
                        RemoveWindowSubclass(hWnd, _wndProcDelegate, uIdSubclass);
                        _wndProcDelegate = null;
                    }
                    break;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SilkWindow] SubclassProc error: {ex}");
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }

    private void OnLoad()
    {
        // 0. Setup Windows borderless frame, shadows, and resizing hooks
        if (IsTitleLess && OperatingSystem.IsWindows())
        {
            SetupWindowsTitlelessFrame();
        }

        // 1. Initialize Silk.NET Input
        _inputContext = _window.CreateInput();
        HookInputEvents();

        // 2. Initialize SkiaSharp OpenGL context
        _glInterface = GRGlInterface.Create(name =>
            _window.GLContext!.TryGetProcAddress(name, out var addr) ? addr : IntPtr.Zero);

        _grContext = GRContext.CreateGl(_glInterface);
        _paintRegistry = new PaintRegistry();

        // 3. Create initial hardware render target
        RecreateSurface(_window.FramebufferSize);

        // 4. Ensure a default theme exists if none is set
        if (!ThemeManager.HasTheme)
        {
            ThemeManager.Current = MaterialTheme.CreateLight();
        }

        // 5. Register native clipboard
        Clipboard.Current = new SilkClipboard(_window);

        // 6. Set window/application icon
        if (!string.IsNullOrEmpty(WindowIconPath))
        {
            SetWindowIcon(WindowIconPath);
        }

        _isLoaded = true;
        _needsRender = true;
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        if (_isDisposed || _isClosing || _grContext == null) return;
        RecreateSurface(size);
    }

    private void RecreateSurface(Vector2D<int> size)
    {
        if (_isDisposed || _isClosing || _grContext == null || size.X <= 0 || size.Y <= 0) return;

        _surface?.Dispose();
        _renderTarget?.Dispose();

        var fbInfo = new GRGlFramebufferInfo(0, 0x8058 /* GL_RGBA8 */);
        _renderTarget = new GRBackendRenderTarget(size.X, size.Y, 0, 8, fbInfo);
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

        _rootElement?.InvalidateMeasure();
        _needsRender = true;
    }

    private void HookInputEvents()
    {
        if (_inputContext == null) return;

        foreach (var mouse in _inputContext.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseScroll;
        }

        foreach (var keyboard in _inputContext.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }
    }

    private void UnhookInputEvents()
    {
        if (_inputContext == null) return;

        try
        {
            foreach (var mouse in _inputContext.Mice)
            {
                mouse.MouseDown -= OnMouseDown;
                mouse.MouseUp -= OnMouseUp;
                mouse.MouseMove -= OnMouseMove;
                mouse.Scroll -= OnMouseScroll;
            }

            foreach (var keyboard in _inputContext.Keyboards)
            {
                keyboard.KeyDown -= OnKeyDown;
                keyboard.KeyUp -= OnKeyUp;
                keyboard.KeyChar -= OnKeyChar;
            }
        }
        catch { }
    }

    #region Input Event Bridges

    private UIElement? _pressedElement;
    private UIElement? _hoveredPopupElement;

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        _needsRender = true; // discrete input usually changes something on screen
        if (_rootElement == null) return;

        var screenPos = new Point(mouse.Position.X, mouse.Position.Y);
        var btn = button switch
        {
            MouseButton.Left => PointerButtons.Left,
            MouseButton.Right => PointerButtons.Right,
            MouseButton.Middle => PointerButtons.Middle,
            _ => PointerButtons.None
        };

        int clickCount = RegisterClick(mouse, btn, screenPos);
        var modifiers = CurrentModifiers();

        if (PopupManager.HandleMouseDown(screenPos, btn, modifiers, clickCount))
        {
            _pressedElement = null;
            return;
        }

        var hit = _rootElement.HitTest(screenPos);

        if (hit != null)
        {
            hit.Focus();

            var e = new PointerEventArgs(screenPos, screenPos, btn, (ulong)Environment.TickCount64, modifiers, clickCount);
            hit.DispatchBubblePointerEvent(e, (el, localE) => el.OnPointerPressed(localE));
            _pressedElement = hit;
        }
        else
        {
            FocusManager.SetFocus(null);
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        _needsRender = true; // discrete input usually changes something on screen
        if (_rootElement == null) return;

        var screenPos = new Point(mouse.Position.X, mouse.Position.Y);
        var btn = button switch
        {
            MouseButton.Left => PointerButtons.Left,
            MouseButton.Right => PointerButtons.Right,
            MouseButton.Middle => PointerButtons.Middle,
            _ => PointerButtons.None
        };

        // A release reports the click count of the press it ends.
        int clickCount = _clickCounter.GetReleaseCount(btn);
        var modifiers = CurrentModifiers();

        if (PopupManager.HandleMouseUp(screenPos, btn, modifiers, clickCount))
        {
            _pressedElement = null;
            return;
        }

        var target = UIElement.CapturedElement ?? _pressedElement ?? _rootElement.HitTest(screenPos);

        if (target != null)
        {
            var e = new PointerEventArgs(screenPos, screenPos, btn, (ulong)Environment.TickCount64, modifiers, clickCount);
            target.DispatchBubblePointerEvent(e, (el, localE) => el.OnPointerReleased(localE));
        }

        if (button == MouseButton.Left && UIElement.CapturedElement != null)
        {
            UIElement.CapturedElement.ReleasePointerCapture();
        }

        _isManualDragging = false;
        _pressedElement = null;
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (_isManualDragging)
        {
            if (mouse.IsButtonPressed(MouseButton.Left))
            {
                int dx = (int)position.X - _manualDragStartMousePos.X;
                int dy = (int)position.Y - _manualDragStartMousePos.Y;
                _window.Position = new Vector2D<int>(_window.Position.X + dx, _window.Position.Y + dy);
                return;
            }
            else
            {
                _isManualDragging = false;
            }
        }

        if (_rootElement == null) return;

        var screenPos = new Point(position.X, position.Y);
        var modifiers = CurrentModifiers();

        // If pointer is captured by an element, deliver move directly to it
        if (UIElement.CapturedElement != null)
        {
            var moveE = new PointerEventArgs(screenPos, screenPos, modifiers: modifiers);
            UIElement.CapturedElement.DispatchBubblePointerEvent(moveE, (el, localE) => el.OnPointerMoved(localE));
            return;
        }

        if (PopupManager.HandleMouseMove(screenPos, ref _hoveredPopupElement, modifiers))
        {
            if (_hoveredElement != null)
            {
                var exitE = new PointerEventArgs(screenPos, screenPos, modifiers: modifiers);
                _hoveredElement.DispatchBubblePointerEvent(exitE, (el, localE) => el.OnPointerExited(localE));
                _hoveredElement = null;
            }
            return;
        }
        else if (_hoveredPopupElement != null)
        {
            var exitE = new PointerEventArgs(screenPos, screenPos, modifiers: modifiers);
            _hoveredPopupElement.DispatchBubblePointerEvent(exitE, (el, localE) => el.OnPointerExited(localE));
            _hoveredPopupElement = null;
        }

        var hit = _rootElement.HitTest(screenPos);

        if (hit != _hoveredElement)
        {
            if (_hoveredElement != null)
            {
                var exitE = new PointerEventArgs(screenPos, screenPos, modifiers: modifiers);
                _hoveredElement.DispatchBubblePointerEvent(exitE, (el, localE) => el.OnPointerExited(localE));
            }

            _hoveredElement = hit;

            if (_hoveredElement != null)
            {
                var enterE = new PointerEventArgs(screenPos, screenPos, modifiers: modifiers);
                _hoveredElement.DispatchBubblePointerEvent(enterE, (el, localE) => el.OnPointerEntered(localE));
            }
        }

        if (hit != null)
        {
            var moveE = new PointerEventArgs(screenPos, screenPos, modifiers: modifiers);
            hit.DispatchBubblePointerEvent(moveE, (el, localE) => el.OnPointerMoved(localE));
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        _needsRender = true; // discrete input usually changes something on screen
        if (_rootElement == null) return;

        var screenPos = new Point(mouse.Position.X, mouse.Position.Y);
        var scrollModifiers = CurrentModifiers();
        if (PopupManager.HandleMouseScroll(screenPos, scroll.X, scroll.Y, scrollModifiers))
        {
            return;
        }

        var target = _hoveredElement ?? _rootElement.HitTest(screenPos);

        if (target != null)
        {
            var wheelE = new PointerWheelEventArgs(screenPos, screenPos, scroll.X, scroll.Y, (ulong)Environment.TickCount64, scrollModifiers);
            target.DispatchBubblePointerEvent(wheelE, (el, localE) => el.OnPointerWheel(localE));
        }
    }

    private SilkKey _repeatingKey = SilkKey.Unknown;
    private int _repeatingKeyCode;
    private IKeyboard? _repeatingKeyboard;
    private ulong _nextRepeatTime;
    private const int InitialKeyRepeatDelayMs = 450;
    private const int KeyRepeatIntervalMs = 35;

    private static bool IsRepeatingKey(SilkKey key) => key switch
    {
        SilkKey.Left => true,
        SilkKey.Right => true,
        SilkKey.Up => true,
        SilkKey.Down => true,
        SilkKey.Backspace => true,
        SilkKey.Delete => true,
        SilkKey.Home => true,
        SilkKey.End => true,
        SilkKey.PageUp => true,
        SilkKey.PageDown => true,
        _ => false
    };

    private void ProcessKeyRepeat()
    {
        if (_repeatingKey == SilkKey.Unknown || _repeatingKeyboard == null)
            return;

        if (!_repeatingKeyboard.IsKeyPressed(_repeatingKey))
        {
            _repeatingKey = SilkKey.Unknown;
            _repeatingKeyboard = null;
            return;
        }

        ulong now = (ulong)Environment.TickCount64;
        if (now >= _nextRepeatTime)
        {
            _nextRepeatTime = now + KeyRepeatIntervalMs;

            var atelierKey = MapKey(_repeatingKey);
            if (atelierKey != Core.Events.Key.None)
            {
                var keyEventArgs = new KeyEventArgs(atelierKey, _repeatingKeyCode, GetModifiers(_repeatingKeyboard), isDown: true, isRepeat: true);
                if (!PopupManager.HandleKeyDown(keyEventArgs))
                {
                    FocusManager.DispatchKeyDown(keyEventArgs, _rootElement);
                }
            }
        }
    }

    private void OnKeyDown(IKeyboard keyboard, SilkKey key, int keyCode)
    {
        _needsRender = true; // discrete input usually changes something on screen
        // Hot Reload manual trigger: F5 or Ctrl+R
        if (key == SilkKey.F5 || (key == SilkKey.R && (keyboard.IsKeyPressed(SilkKey.ControlLeft) || keyboard.IsKeyPressed(SilkKey.ControlRight))))
        {
            Console.WriteLine("[HotReload] Manual hot reload key triggered (F5 / Ctrl+R).");
            ReloadContent();
            return;
        }

        var atelierKey = MapKey(key);

        // Tab focus cycling
        if (atelierKey == Core.Events.Key.Tab && _rootElement != null)
        {
            bool shift = keyboard.IsKeyPressed(SilkKey.ShiftLeft) || keyboard.IsKeyPressed(SilkKey.ShiftRight);
            if (shift)
            {
                FocusManager.FocusPrevious(_rootElement);
            }
            else
            {
                FocusManager.FocusNext(_rootElement);
            }
            return;
        }

        if (IsRepeatingKey(key))
        {
            _repeatingKey = key;
            _repeatingKeyCode = keyCode;
            _repeatingKeyboard = keyboard;
            _nextRepeatTime = (ulong)Environment.TickCount64 + InitialKeyRepeatDelayMs;
        }

        var keyEventArgs = new KeyEventArgs(atelierKey, keyCode, GetModifiers(keyboard), true);
        if (PopupManager.HandleKeyDown(keyEventArgs))
        {
            return;
        }

        FocusManager.DispatchKeyDown(keyEventArgs, _rootElement);
    }

    private void OnKeyUp(IKeyboard keyboard, SilkKey key, int keyCode)
    {
        _needsRender = true; // discrete input usually changes something on screen
        if (_repeatingKey == key)
        {
            _repeatingKey = SilkKey.Unknown;
            _repeatingKeyboard = null;
        }

        var atelierKey = MapKey(key);
        var keyEventArgs = new KeyEventArgs(atelierKey, keyCode, GetModifiers(keyboard), false);
        FocusManager.DispatchKeyUp(keyEventArgs, _rootElement);
    }

    // Double/triple click detection, using the platform's double-click time and distance.
    private readonly ClickCounter _clickCounter = new();

    private int RegisterClick(IMouse mouse, PointerButtons button, Point position) =>
        _clickCounter.RegisterPress(button, position, Environment.TickCount64, mouse.DoubleClickTime, Math.Max(1, mouse.DoubleClickRange));

    private ModifierKeys CurrentModifiers()
    {
        var keyboards = _inputContext?.Keyboards;
        return keyboards != null && keyboards.Count > 0 ? GetModifiers(keyboards[0]) : ModifierKeys.None;
    }

    private static ModifierKeys GetModifiers(IKeyboard keyboard)
    {
        var mods = ModifierKeys.None;
        if (keyboard.IsKeyPressed(SilkKey.ShiftLeft) || keyboard.IsKeyPressed(SilkKey.ShiftRight))
            mods |= ModifierKeys.Shift;
        if (keyboard.IsKeyPressed(SilkKey.ControlLeft) || keyboard.IsKeyPressed(SilkKey.ControlRight))
            mods |= ModifierKeys.Control;
        if (keyboard.IsKeyPressed(SilkKey.AltLeft) || keyboard.IsKeyPressed(SilkKey.AltRight))
            mods |= ModifierKeys.Alt;
        if (keyboard.IsKeyPressed(SilkKey.SuperLeft) || keyboard.IsKeyPressed(SilkKey.SuperRight))
            mods |= ModifierKeys.Windows;
        return mods;
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        _needsRender = true; // discrete input usually changes something on screen
        var textArgs = new TextInputEventArgs(c.ToString());
        FocusManager.DispatchTextInput(textArgs, _rootElement);
    }

    private static Core.Events.Key MapKey(SilkKey silkKey) => silkKey switch
    {
        SilkKey.Backspace => Core.Events.Key.Backspace,
        SilkKey.Tab => Core.Events.Key.Tab,
        SilkKey.Enter => Core.Events.Key.Enter,
        SilkKey.KeypadEnter => Core.Events.Key.Enter,
        SilkKey.Escape => Core.Events.Key.Escape,
        SilkKey.Space => Core.Events.Key.Space,
        SilkKey.PageUp => Core.Events.Key.PageUp,
        SilkKey.PageDown => Core.Events.Key.PageDown,
        SilkKey.End => Core.Events.Key.End,
        SilkKey.Home => Core.Events.Key.Home,
        SilkKey.Left => Core.Events.Key.Left,
        SilkKey.Up => Core.Events.Key.Up,
        SilkKey.Right => Core.Events.Key.Right,
        SilkKey.Down => Core.Events.Key.Down,
        SilkKey.Delete => Core.Events.Key.Delete,
        SilkKey.A => Core.Events.Key.A,
        SilkKey.B => Core.Events.Key.B,
        SilkKey.C => Core.Events.Key.C,
        SilkKey.D => Core.Events.Key.D,
        SilkKey.E => Core.Events.Key.E,
        SilkKey.F => Core.Events.Key.F,
        SilkKey.G => Core.Events.Key.G,
        SilkKey.H => Core.Events.Key.H,
        SilkKey.I => Core.Events.Key.I,
        SilkKey.J => Core.Events.Key.J,
        SilkKey.K => Core.Events.Key.K,
        SilkKey.L => Core.Events.Key.L,
        SilkKey.M => Core.Events.Key.M,
        SilkKey.N => Core.Events.Key.N,
        SilkKey.O => Core.Events.Key.O,
        SilkKey.P => Core.Events.Key.P,
        SilkKey.Q => Core.Events.Key.Q,
        SilkKey.R => Core.Events.Key.R,
        SilkKey.S => Core.Events.Key.S,
        SilkKey.T => Core.Events.Key.T,
        SilkKey.U => Core.Events.Key.U,
        SilkKey.V => Core.Events.Key.V,
        SilkKey.W => Core.Events.Key.W,
        SilkKey.X => Core.Events.Key.X,
        SilkKey.Y => Core.Events.Key.Y,
        SilkKey.Z => Core.Events.Key.Z,
        SilkKey.Number0 => Core.Events.Key.D0,
        SilkKey.Number1 => Core.Events.Key.D1,
        SilkKey.Number2 => Core.Events.Key.D2,
        SilkKey.Number3 => Core.Events.Key.D3,
        SilkKey.Number4 => Core.Events.Key.D4,
        SilkKey.Number5 => Core.Events.Key.D5,
        SilkKey.Number6 => Core.Events.Key.D6,
        SilkKey.Number7 => Core.Events.Key.D7,
        SilkKey.Number8 => Core.Events.Key.D8,
        SilkKey.Number9 => Core.Events.Key.D9,
        SilkKey.F1 => Core.Events.Key.F1,
        SilkKey.F2 => Core.Events.Key.F2,
        SilkKey.F3 => Core.Events.Key.F3,
        SilkKey.F4 => Core.Events.Key.F4,
        SilkKey.F5 => Core.Events.Key.F5,
        SilkKey.F6 => Core.Events.Key.F6,
        SilkKey.F7 => Core.Events.Key.F7,
        SilkKey.F8 => Core.Events.Key.F8,
        SilkKey.F9 => Core.Events.Key.F9,
        SilkKey.F10 => Core.Events.Key.F10,
        SilkKey.F11 => Core.Events.Key.F11,
        SilkKey.F12 => Core.Events.Key.F12,
        SilkKey.Keypad0 => Core.Events.Key.NumPad0,
        SilkKey.Keypad1 => Core.Events.Key.NumPad1,
        SilkKey.Keypad2 => Core.Events.Key.NumPad2,
        SilkKey.Keypad3 => Core.Events.Key.NumPad3,
        SilkKey.Keypad4 => Core.Events.Key.NumPad4,
        SilkKey.Keypad5 => Core.Events.Key.NumPad5,
        SilkKey.Keypad6 => Core.Events.Key.NumPad6,
        SilkKey.Keypad7 => Core.Events.Key.NumPad7,
        SilkKey.Keypad8 => Core.Events.Key.NumPad8,
        SilkKey.Keypad9 => Core.Events.Key.NumPad9,
        SilkKey.KeypadDecimal => Core.Events.Key.NumPadDecimal,
        SilkKey.KeypadDivide => Core.Events.Key.NumPadDivide,
        SilkKey.KeypadMultiply => Core.Events.Key.NumPadMultiply,
        SilkKey.KeypadSubtract => Core.Events.Key.NumPadSubtract,
        SilkKey.KeypadAdd => Core.Events.Key.NumPadAdd,
        SilkKey.KeypadEqual => Core.Events.Key.NumPadEqual,
        SilkKey.Insert => Core.Events.Key.Insert,
        SilkKey.CapsLock => Core.Events.Key.CapsLock,
        SilkKey.NumLock => Core.Events.Key.NumLock,
        SilkKey.ScrollLock => Core.Events.Key.ScrollLock,
        SilkKey.PrintScreen => Core.Events.Key.PrintScreen,
        SilkKey.Pause => Core.Events.Key.Pause,
        SilkKey.Menu => Core.Events.Key.Menu,
        SilkKey.Minus => Core.Events.Key.Minus,
        SilkKey.Equal => Core.Events.Key.Equal,
        SilkKey.Comma => Core.Events.Key.Comma,
        SilkKey.Period => Core.Events.Key.Period,
        SilkKey.Slash => Core.Events.Key.Slash,
        SilkKey.Semicolon => Core.Events.Key.Semicolon,
        SilkKey.Apostrophe => Core.Events.Key.Apostrophe,
        SilkKey.LeftBracket => Core.Events.Key.LeftBracket,
        SilkKey.RightBracket => Core.Events.Key.RightBracket,
        SilkKey.BackSlash => Core.Events.Key.Backslash,
        SilkKey.GraveAccent => Core.Events.Key.GraveAccent,
        SilkKey.F13 => Core.Events.Key.F13,
        SilkKey.F14 => Core.Events.Key.F14,
        SilkKey.F15 => Core.Events.Key.F15,
        SilkKey.F16 => Core.Events.Key.F16,
        SilkKey.F17 => Core.Events.Key.F17,
        SilkKey.F18 => Core.Events.Key.F18,
        SilkKey.F19 => Core.Events.Key.F19,
        SilkKey.F20 => Core.Events.Key.F20,
        SilkKey.F21 => Core.Events.Key.F21,
        SilkKey.F22 => Core.Events.Key.F22,
        SilkKey.F23 => Core.Events.Key.F23,
        SilkKey.F24 => Core.Events.Key.F24,
        SilkKey.ShiftLeft => Core.Events.Key.LeftShift,
        SilkKey.ShiftRight => Core.Events.Key.RightShift,
        SilkKey.ControlLeft => Core.Events.Key.LeftCtrl,
        SilkKey.ControlRight => Core.Events.Key.RightCtrl,
        SilkKey.AltLeft => Core.Events.Key.LeftAlt,
        SilkKey.AltRight => Core.Events.Key.RightAlt,
        SilkKey.SuperLeft => Core.Events.Key.LeftWindows,
        SilkKey.SuperRight => Core.Events.Key.RightWindows,
        _ => Core.Events.Key.None
    };

    #endregion

    private void OnUpdate(double deltaTime)
    {
        if (_isDisposed || _isClosing) return;

        // Process key repeat for held navigation & editing keys
        ProcessKeyRepeat();

        // 0. Process thread-safe dispatch queue (e.g. Hot Reload notifications)
        while (_dispatchQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Atelier.Dispatch] Error: {ex}");
            }
        }

        // 1. Advance Animation Clock (sub-pixel spring & tween updates).
        // After an idle period deltaTime spans the whole sleep; animations started by the input that woke the loop
        // must start from zero instead of jumping to their end, so the first update after idling advances nothing.
        double animationDelta = _resumingFromIdle ? 0 : Math.Min(deltaTime, MaxAnimationStepSeconds);
        _resumingFromIdle = false;
        _animationClock.Update(animationDelta);

        // 2. Re-apply styles once if the global styles changed since the last frame
        if (_globalStylesChanged)
        {
            _globalStylesChanged = false;
            _rootElement?.ApplyStylesToTree();
        }

        // 3. Measure & Arrange Passes
        if (_rootElement != null && _window.Size.X > 0 && _window.Size.Y > 0)
        {
            var winSize = new Size(_window.Size.X, _window.Size.Y);
            var rootTransform = _rootElement.GetEffectiveTransform();

            Size availableSize = winSize;
            if (!rootTransform.IsIdentity && Matrix3x2.Invert(rootTransform, out var inv))
            {
                float localW = MathF.Abs(winSize.Width * inv.M11 + winSize.Height * inv.M21);
                float localH = MathF.Abs(winSize.Width * inv.M12 + winSize.Height * inv.M22);
                if (localW > 0 && localH > 0)
                {
                    availableSize = new Size(localW, localH);
                }
            }

            _rootElement.Measure(availableSize);
            _rootElement.Arrange(new Rect(Point.Zero, availableSize));

            // Measure & Arrange active popups
            PopupManager.UpdatePopups(winSize);
        }
    }

    // Longest animation step per frame, so a stalled frame (e.g. while the OS drags the window) doesn't skip animations.
    private const double MaxAnimationStepSeconds = 0.1;

    // Work that needs the loop to keep ticking even when nothing has been invalidated yet.
    private bool HasContinuousWork =>
        _animationClock.ActiveAnimationCount > 0 || _repeatingKey != SilkKey.Unknown || !_dispatchQueue.IsEmpty;

    private void OnRender(double deltaTime)
    {
        if (_isDisposed || _isClosing || _surface == null || _paintRegistry == null || _grContext == null) return;

        bool continuous = HasContinuousWork;
        if (!_needsRender && !continuous)
        {
            // Nothing changed: skip drawing and swapping, and sleep until input or InvalidateRender() wakes the loop.
            EnterIdle();
            return;
        }

        _needsRender = false;
        _window.IsEventDriven = false;

        var canvas = _surface.Canvas;

        // 1. Clear with background (supporting transparency and opacity)
        Color bg = WindowBackground ?? (ThemeManager.HasTheme && ThemeManager.Current is MaterialTheme mt
            ? mt.Colors.Background
            : Color.White);

        if (IsTransparent || WindowOpacity < 1.0f)
        {
            byte alpha = (byte)Math.Clamp((int)(bg.A * WindowOpacity), 0, 255);
            canvas.Clear(new SKColor(bg.R, bg.G, bg.B, alpha));
        }
        else
        {
            canvas.Clear(new SKColor(bg.R, bg.G, bg.B, bg.A));
        }

        // 2. Render visual tree using stack-allocated DrawingContext (Zero allocations!)
        var drawingContext = new DrawingContext(canvas, _paintRegistry);

        if (_rootElement != null)
        {
            VisualTreeRenderer.Render(_rootElement, ref drawingContext, ThemeVisualPresenter.Instance);
        }

        // 2b. Render active popups above the visual tree
        PopupManager.RenderPopups(ref drawingContext, ThemeVisualPresenter.Instance);

        // 3. FPS & Performance overlay (Bottom-Left)
        TrackRenderedFrame();
        if (ShowFpsOverlay && _window.Size.Y > 40)
        {
            string fpsText = GetFpsText();
            float boxWidth = 170f;
            float boxHeight = 24f;
            float boxX = 8f;
            float boxY = _window.Size.Y - boxHeight - 8f;

            drawingContext.DrawRoundedRect(new Rect(boxX, boxY, boxWidth, boxHeight), new CornerRadius(4), Color.Black.WithAlpha(0.65f));
            drawingContext.DrawText(fpsText, new Point(boxX + 6f, boxY + 16f), Color.FromHex("#00E676"), 12f, bold: true);
        }

        // 4. Flush GPU commands and present (VSync throttles the loop here while frames are being rendered)
        canvas.Flush();
        _grContext.Flush();
        _window.GLContext?.SwapBuffers();
    }

    private void EnterIdle()
    {
        if (_window.IsEventDriven)
        {
            return;
        }

        _window.IsEventDriven = true;
        _resumingFromIdle = true;

        // Restart the frame-rate sample so the next measurement doesn't include the idle period.
        _frameCount = 0;
        _fpsSampleStart = _stopwatch.Elapsed.TotalSeconds;
        CurrentFps = 0;
    }

    private void TrackRenderedFrame()
    {
        _frameCount++;
        double now = _stopwatch.Elapsed.TotalSeconds;
        double elapsed = now - _fpsSampleStart;
        if (elapsed >= 0.5)
        {
            CurrentFps = _frameCount / elapsed;
            _frameCount = 0;
            _fpsSampleStart = now;
        }
    }

    // Rebuilt only when the displayed numbers change, so the overlay doesn't allocate a string every frame.
    private string GetFpsText()
    {
        double fps = Math.Round(CurrentFps);
        int animations = _animationClock.ActiveAnimationCount;
        if (fps != _fpsTextValue || animations != _fpsTextAnimations)
        {
            _fpsTextValue = fps;
            _fpsTextAnimations = animations;
            _fpsText = $"FPS: {fps:F0} | Animations: {animations}";
        }
        return _fpsText;
    }

    private void CleanupGraphicsResources()
    {
        try
        {
            if (_grContext != null)
            {
                _grContext.AbandonContext();
            }
        }
        catch { }

        try
        {
            _surface?.Dispose();
        }
        catch { }
        finally
        {
            _surface = null;
        }

        try
        {
            _renderTarget?.Dispose();
        }
        catch { }
        finally
        {
            _renderTarget = null;
        }

        try
        {
            _paintRegistry?.Dispose();
        }
        catch { }
        finally
        {
            _paintRegistry = null;
        }

        try
        {
            _grContext?.Dispose();
        }
        catch { }
        finally
        {
            _grContext = null;
        }

        try
        {
            _glInterface?.Dispose();
        }
        catch { }
        finally
        {
            _glInterface = null;
        }
    }

    private void OnClosing()
    {
        _isClosing = true;

        UnhookInputEvents();
        CleanupGraphicsResources();

        if (_wndProcDelegate != null && TryGetHwnd(out var hwnd))
        {
            try
            {
                RemoveWindowSubclass(hwnd, _wndProcDelegate, (UIntPtr)1001);
            }
            catch { }
            _wndProcDelegate = null;
        }
    }

    private bool _isDisposed;
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        OnClosing();

        HotReloadManager.HotReloadTriggered -= OnHotReloadTriggered;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        StyleManager.GlobalStyles.StylesChanged -= OnGlobalStylesChanged;
        PopupManager.PopupOpened -= OnPopupChanged;
        PopupManager.PopupClosed -= OnPopupChanged;

        if (_rootElement != null)
        {
            _rootElement.NeedsVisualUpdate -= OnTreeInvalidated;
            _rootElement.NeedsLayoutUpdate -= OnTreeInvalidated;
            _rootElement.DetachFromHost();
        }

        _window.StateChanged -= OnWindowStateChanged;
        _window.FocusChanged -= OnWindowFocusChanged;
        _window.Load -= OnLoad;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Update -= OnUpdate;
        _window.Render -= OnRender;
        _window.Closing -= OnClosing;

        try
        {
            _inputContext?.Dispose();
        }
        catch { }
        _inputContext = null;

        if (Current == this)
        {
            Current = null;
        }

        if (Clipboard.Current is SilkClipboard)
        {
            Clipboard.Current = null!;
        }

        Atelier.Controls.Button.SetGlobalAnimationClock(null!);
        CheckBox.SetGlobalAnimationClock(null!);
        Atelier.Controls.Switch.SetGlobalAnimationClock(null!);
        TextBox.SetGlobalAnimationClock(null!);
        Slider.SetGlobalAnimationClock(null!);
        ScrollViewer.SetGlobalAnimationClock(null!);
        TransitioningContentControl.SetGlobalAnimationClock(null!);
        DialogHost.RootVisualProvider = null;

        try
        {
            _window.Dispose();
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}

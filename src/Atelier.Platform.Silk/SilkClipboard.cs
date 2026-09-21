using System;
using System.Runtime.InteropServices;
using Atelier.Core.Platform;
using Silk.NET.Windowing;

namespace Atelier.Platform.Silk;

public class SilkClipboard : IClipboard
{
    private readonly IWindow _window;
    private string? _fallbackBuffer;

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    public SilkClipboard(IWindow window)
    {
        _window = window;
    }

    private IntPtr GetHwnd()
    {
        if (OperatingSystem.IsWindows())
        {
            var win32 = _window.Native?.Win32;
            if (win32.HasValue && win32.Value.Hwnd != IntPtr.Zero)
            {
                return win32.Value.Hwnd;
            }
        }
        return IntPtr.Zero;
    }

    public string? GetText()
    {
        if (OperatingSystem.IsWindows())
        {
            IntPtr hwnd = GetHwnd();
            if (!OpenClipboard(hwnd))
            {
                return _fallbackBuffer;
            }

            try
            {
                if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                {
                    return null;
                }

                IntPtr handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(pointer);
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        return _fallbackBuffer;
    }

    public void SetText(string? text)
    {
        _fallbackBuffer = text;

        if (OperatingSystem.IsWindows() && text != null)
        {
            IntPtr hwnd = GetHwnd();
            if (!OpenClipboard(hwnd))
            {
                return;
            }

            try
            {
                EmptyClipboard();

                int charCount = text.Length + 1; // including null terminator
                int bytesCount = charCount * sizeof(char);

                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytesCount);
                if (hGlobal == IntPtr.Zero)
                {
                    return;
                }

                IntPtr target = GlobalLock(hGlobal);
                if (target == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                    Marshal.WriteInt16(target, text.Length * sizeof(char), 0); // null terminator
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }

                SetClipboardData(CF_UNICODETEXT, hGlobal);
            }
            finally
            {
                CloseClipboard();
            }
        }
    }

    public bool ContainsText()
    {
        if (OperatingSystem.IsWindows())
        {
            return IsClipboardFormatAvailable(CF_UNICODETEXT);
        }

        return !string.IsNullOrEmpty(_fallbackBuffer);
    }
}

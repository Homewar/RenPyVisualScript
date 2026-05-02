using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace RenPyVisualScriptMVVM.Core.Native;

public static class WindowsDwmShadow
{
    private const int GWL_STYLE = -16;

    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int DWMWA_NCRENDERING_POLICY = 2;
    private const int DWMNCRP_ENABLED = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(
        IntPtr hwnd,
        ref Margins margins);

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var platformHandle = window.TryGetPlatformHandle();

        if (platformHandle?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
            return;

        var style = GetWindowStyle(hwnd);

        style |= WS_THICKFRAME;
        style |= WS_SYSMENU;
        style |= WS_MINIMIZEBOX;
        style |= WS_MAXIMIZEBOX;

        /*
         * WS_CAPTION иногда помогает DWM дать настоящую тень,
         * но может вернуть системный caption behavior на некоторых конфигурациях.
         * Если появятся системные кнопки/полоса — закомментируй эту строку.
         */
        //style |= WS_CAPTION;

        SetWindowStyle(hwnd, style);

        var policy = DWMNCRP_ENABLED;
        DwmSetWindowAttribute(
            hwnd,
            DWMWA_NCRENDERING_POLICY,
            ref policy,
            sizeof(int));

        /*
         * Маленькая DWM-рамка внутри client area.
         * DwmExtendFrameIntoClientArea официально расширяет frame в client area.
         */
        var margins = new Margins
        {
            Left = 1,
            Right = 1,
            Top = 1,
            Bottom = 1
        };

        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private static int GetWindowStyle(IntPtr hwnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GWL_STYLE).ToInt32()
            : GetWindowLong32(hwnd, GWL_STYLE);
    }

    private static void SetWindowStyle(IntPtr hwnd, int style)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, GWL_STYLE, new IntPtr(style));
        else
            SetWindowLong32(hwnd, GWL_STYLE, style);
    }
}
using Avalonia.Media.Imaging;
using ImageMagick;
using ImageMagick.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RenPyVisualScriptMVVM.Modules.Editors.Services.Preview;

internal static class CrossPlatformPreviewProvider
{
    private static readonly Dictionary<string, Bitmap?> VideoCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Bitmap?> FontCache = new(StringComparer.OrdinalIgnoreCase);

    [SupportedOSPlatform("windows6.1")]
    public static Bitmap? GetVideoThumbnail(string path, int width, int height)
    {
        var cacheKey = $"{path}|{width}|{height}";
        if (VideoCache.TryGetValue(cacheKey, out var cached))
            return cached;

        if (OperatingSystem.IsWindows())
        {
            var shellPreview = WindowsShellThumbnailProvider.GetThumbnail(path, width, height);
            VideoCache[cacheKey] = shellPreview;
            return shellPreview;
        }

        try
        {
            var settings = new MagickReadSettings
            {
                FrameIndex = 0,
                FrameCount = 1
            };

            using var collection = new MagickImageCollection();
            collection.Read(path, settings);
            if (collection.Count == 0)
            {
                VideoCache[cacheKey] = null;
                return null;
            }

            using var frame = (MagickImage)collection[0].Clone();
            frame.Resize(new MagickGeometry((uint)width, (uint)height)
            {
                IgnoreAspectRatio = false,
                Greater = false
            });

            var bitmap = ToAvaloniaBitmap(frame);
            VideoCache[cacheKey] = bitmap;
            return bitmap;
        }
        catch
        {
            VideoCache[cacheKey] = null;
            return null;
        }
    }

    public static Bitmap? GetFontPreview(string path, string previewText, int width, int height)
    {
        var cacheKey = $"{path}|{previewText}|{width}|{height}";
        if (FontCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            using var image = new MagickImage(new MagickColor("#202022"), (uint)width, (uint)height);
            var drawables = new Drawables()
                .Font(path)
                .FontPointSize(18)
                .FillColor(new MagickColor("#E6E6E6"))
                .TextAlignment(TextAlignment.Left)
                .Text(10, (height / 2.0) + 6, previewText);

            drawables.Draw(image);

            var bitmap = ToAvaloniaBitmap(image);
            FontCache[cacheKey] = bitmap;
            return bitmap;
        }
        catch
        {
            FontCache[cacheKey] = null;
            return null;
        }
    }

    private static Bitmap ToAvaloniaBitmap(IMagickImage<byte> image)
    {
        using var stream = new MemoryStream();
        image.Write(stream, MagickFormat.Png32);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}

internal static class WindowsShellThumbnailProvider
{
    [SupportedOSPlatform("windows6.1")]
    public static Bitmap? GetThumbnail(string path, int width, int height)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        IntPtr hBitmap = IntPtr.Zero;
        IShellItemImageFactory? factory = null;

        try
        {
            var factoryGuid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref factoryGuid, out factory);
            factory.GetImage(
                new NativeSize(width, height),
                ShellItemImageFlags.BiggerSizeOk | ShellItemImageFlags.ThumbnailOnly,
                out hBitmap);

            if (hBitmap == IntPtr.Zero)
                return null;

            using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);

            if (factory is not null && Marshal.IsComObject(factory))
                Marshal.ReleaseComObject(factory);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, ShellItemImageFlags flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width;
        public int Height;
    }

    [Flags]
    private enum ShellItemImageFlags
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8
    }
}

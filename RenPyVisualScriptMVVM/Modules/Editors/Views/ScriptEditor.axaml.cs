using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using System.Runtime.InteropServices;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class ScriptEditor : Window
{
    private readonly AudioPreviewPlayer _audioPreviewPlayer = new();

    public ScriptEditor()
    {
        InitializeComponent();
        Closing += (_, _) => _audioPreviewPlayer.Dispose();
    }

    private ScriptEditorViewModel? ViewModel => DataContext as ScriptEditorViewModel;

    private void CharacterList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Character character })
            ViewModel?.NavigateToFile(character.FilePath, character.Line);
    }

    private void LabelList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: LabelOutlineItem label })
            ViewModel?.NavigateToFile(label.FilePath, label.Line);
    }

    private void TransitionItems_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: TransitionPanelItem transition })
            ViewModel?.NavigateToFile(transition.FileName, transition.Line);
    }

    private void TransitionChoice_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is StructureLinkItem choice)
            ViewModel?.NavigateToFile(choice.FileName, choice.Line);
    }

    private async void AudioPlay_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not ResourceFileItem resource)
            return;

        try
        {
            _audioPreviewPlayer.Toggle(resource.FullPath);
        }
        catch (Exception ex)
        {
            await MessageBox("Audio preview error", ex.Message);
        }
    }

    private void AudioStop_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _audioPreviewPlayer.Stop();
    }

    private async System.Threading.Tasks.Task MessageBox(string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Width = 80
                    }
                }
            }
        };

        if (window.Content is StackPanel panel && panel.Children[1] is Button button)
            button.Click += (_, _) => window.Close();

        await window.ShowDialog(this);
    }
}

public sealed class ImagePreviewConverter : IValueConverter
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private const int PreviewWidth = 360;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            using var stream = File.OpenRead(path);
            var extension = Path.GetExtension(path);
            var bitmap = extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                ? Bitmap.DecodeToWidth(stream, PreviewWidth)
                : Bitmap.DecodeToWidth(stream, PreviewWidth);
            Cache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            Cache[path] = null;
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class AnimatedImagePreview : Image
{
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<AnimatedImagePreview, string?>(nameof(FilePath));

    private static readonly Dictionary<string, PreviewImageSet?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private const int PreviewWidth = 360;
    private DispatcherTimer? _animationTimer;
    private PreviewImageSet? _previewSet;
    private int _frameIndex;

    static AnimatedImagePreview()
    {
        FilePathProperty.Changed.AddClassHandler<AnimatedImagePreview>((control, _) => control.LoadPreview());
    }

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadPreview();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAnimation();
        base.OnDetachedFromVisualTree(e);
    }

    private void LoadPreview()
    {
        StopAnimation();
        _frameIndex = 0;
        Source = null;

        var path = FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (!Cache.TryGetValue(path, out _previewSet))
        {
            _previewSet = CreatePreviewSet(path);
            Cache[path] = _previewSet;
        }

        if (_previewSet is null || _previewSet.Frames.Count == 0)
            return;

        Source = _previewSet.Frames[0];

        if (_previewSet.Frames.Count > 1)
            StartAnimation();
    }

    private void StartAnimation()
    {
        if (_previewSet is null || _previewSet.Frames.Count <= 1)
            return;

        _animationTimer = new DispatcherTimer();
        _animationTimer.Tick += AnimationTimerOnTick;
        _animationTimer.Interval = _previewSet.Delays[0];
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer is null)
            return;

        _animationTimer.Stop();
        _animationTimer.Tick -= AnimationTimerOnTick;
        _animationTimer = null;
    }

    private void AnimationTimerOnTick(object? sender, EventArgs e)
    {
        if (_previewSet is null || _previewSet.Frames.Count == 0 || _animationTimer is null)
            return;

        _frameIndex = (_frameIndex + 1) % _previewSet.Frames.Count;
        Source = _previewSet.Frames[_frameIndex];
        _animationTimer.Interval = _previewSet.Delays[_frameIndex];
    }

    private static PreviewImageSet? CreatePreviewSet(string path)
    {
        try
        {
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                var bitmap = Bitmap.DecodeToWidth(stream, PreviewWidth);
                return new PreviewImageSet(new[] { bitmap }, new[] { TimeSpan.MaxValue });
            }

            using var collection = new MagickImageCollection(path);
            collection.Coalesce();

            var frames = new List<Bitmap>(collection.Count);
            var delays = new List<TimeSpan>(collection.Count);

            foreach (var frame in collection)
            {
                using var clonedFrame = (MagickImage)frame.Clone();
                if (clonedFrame.Width > PreviewWidth)
                    clonedFrame.Resize(PreviewWidth, 0);

                using var stream = new MemoryStream();
                clonedFrame.Format = MagickFormat.Png32;
                clonedFrame.Write(stream);
                stream.Position = 0;

                frames.Add(new Bitmap(stream));

                var delay = Math.Max(50, clonedFrame.AnimationDelay * 10);
                delays.Add(TimeSpan.FromMilliseconds(delay));
            }

            return frames.Count == 0 ? null : new PreviewImageSet(frames, delays);
        }
        catch
        {
            return null;
        }
    }

    private sealed class PreviewImageSet
    {
        public PreviewImageSet(IReadOnlyList<Bitmap> frames, IReadOnlyList<TimeSpan> delays)
        {
            Frames = frames;
            Delays = delays;
        }

        public IReadOnlyList<Bitmap> Frames { get; }
        public IReadOnlyList<TimeSpan> Delays { get; }
    }
}

public sealed class VideoPreviewConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return ShellThumbnailProvider.GetThumbnail(path, 400, 225);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class VideoThumbnailPreview : Border
{
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<VideoThumbnailPreview, string?>(nameof(FilePath));

    private readonly Image _previewImage;
    private readonly TextBlock _fallbackText;

    static VideoThumbnailPreview()
    {
        FilePathProperty.Changed.AddClassHandler<VideoThumbnailPreview>((control, _) => control.LoadPreview());
    }

    public VideoThumbnailPreview()
    {
        Background = Brushes.Black;

        _previewImage = new Image
        {
            Stretch = Stretch.Uniform,
            IsVisible = false
        };

        _fallbackText = new TextBlock
        {
            Text = "Preview unavailable",
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        Child = new Grid
        {
            Children =
            {
                _previewImage,
                _fallbackText
            }
        };
    }

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadPreview();
    }

    private void LoadPreview()
    {
        var path = FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ShowFallback("File not found");
            return;
        }

        var preview = ShellThumbnailProvider.GetThumbnail(path, 400, 225);
        if (preview is null)
        {
            ShowFallback("Preview unavailable");
            return;
        }

        _previewImage.Source = preview;
        _previewImage.IsVisible = true;
        _fallbackText.IsVisible = false;
    }

    private void ShowFallback(string message)
    {
        _previewImage.Source = null;
        _previewImage.IsVisible = false;
        _fallbackText.Text = message;
        _fallbackText.IsVisible = true;
    }
}

internal static class ShellThumbnailProvider
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? GetThumbnail(string path, int width, int height)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var cacheKey = $"{path}|{width}|{height}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        Bitmap? result = null;
        IShellItemImageFactory? factory = null;
        IntPtr hBitmap = IntPtr.Zero;

        try
        {
            var factoryGuid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref factoryGuid, out factory);
            factory.GetImage(
                new NativeSize(width, height),
                ShellItemImageFlags.BiggerSizeOk | ShellItemImageFlags.ThumbnailOnly,
                out hBitmap);

            if (hBitmap != IntPtr.Zero)
                result = ConvertHBitmap(hBitmap);
        }
        catch
        {
            result = null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);

            if (factory is not null)
                Marshal.ReleaseComObject(factory);
        }

        Cache[cacheKey] = result;
        return result;
    }

    private static Bitmap? ConvertHBitmap(IntPtr hBitmap)
    {
        try
        {
            using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        catch
        {
            return null;
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Preview;
using System;
using System.IO;

namespace RenPyVisualScriptMVVM.Modules.Editors.Controls;

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

        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            ShowFallback("Preview unavailable");
            return;
        }

        var preview = CrossPlatformPreviewProvider.GetVideoThumbnail(path, 400, 225);
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

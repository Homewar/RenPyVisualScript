using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;

namespace RenPyVisualScriptMVVM.Modules.Editors.Controls;

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

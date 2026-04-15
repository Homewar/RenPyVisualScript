using System;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace RenPyVisualScriptMVVM.Modules.Editors.Services;

internal sealed class AudioPreviewPlayer : IDisposable
{
    private string? _currentFilePath;
    private WaveOutEvent? _outputDevice;
    private WaveStream? _audioStream;

    public string? CurrentFilePath => _currentFilePath;
    public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;

    public void Toggle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        if (string.Equals(_currentFilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            Stop();
            return;
        }

        Play(filePath);
    }

    public void Play(string filePath)
    {
        Stop();

        _audioStream = CreateReader(filePath);
        _outputDevice = new WaveOutEvent();
        _outputDevice.Init(_audioStream);
        _outputDevice.PlaybackStopped += OnPlaybackStopped;
        _outputDevice.Play();
        _currentFilePath = filePath;
    }

    public void Stop()
    {
        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OnPlaybackStopped;
            _outputDevice.Stop();
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        _audioStream?.Dispose();
        _audioStream = null;
        _currentFilePath = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private static WaveStream CreateReader(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            return new VorbisWaveReader(filePath);

        return new AudioFileReader(filePath);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Stop();
    }
}

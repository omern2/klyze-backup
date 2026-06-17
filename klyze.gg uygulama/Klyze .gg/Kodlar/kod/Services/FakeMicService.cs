using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ValorantAutoClicker.Services
{
    public class FakeMicService : IDisposable
    {
        private WasapiOut _playback;
        private AudioFileReader _currentFile;
        private MMDeviceEnumerator _deviceEnumerator;
        private float _volume = 0.8f;
        private bool _isPlaying;
        private bool _enumAvailable;
        private List<string> _playlist;
        private int _playlistIndex;
        private int _playlistDeviceIndex = -1;
        private bool _loopPlaylist;

        public bool IsPlaying => _isPlaying;
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_playback != null)
                    _playback.Volume = _volume;
            }
        }

        public bool LoopPlaylist
        {
            get => _loopPlaylist;
            set => _loopPlaylist = value;
        }

        public event Action<bool> PlayingStateChanged;
        public event Action<string> SongChanged;

        public FakeMicService()
        {
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _enumAvailable = true;
            }
            catch
            {
                _enumAvailable = false;
            }
        }

        public List<string> GetOutputDevices()
        {
            if (!_enumAvailable || _deviceEnumerator == null)
                return new List<string> { "Varsayılan Cihaz" };

            try
            {
                var devs = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                if (devs == null) return new List<string> { "Varsayılan Cihaz" };
                return devs.Select(d =>
                {
                    try { return d.FriendlyName ?? "Bilinmeyen Cihaz"; }
                    catch { return "Bilinmeyen Cihaz"; }
                }).ToList();
            }
            catch
            {
                return new List<string> { "Varsayılan Cihaz" };
            }
        }

        public bool IsVBCableInstalled()
        {
            if (!_enumAvailable || _deviceEnumerator == null) return false;
            try
            {
                var devs = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                return devs?.Any(d =>
                {
                    try { return (d.FriendlyName ?? "").IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0; }
                    catch { return false; }
                }) ?? false;
            }
            catch { return false; }
        }

        public int GetVBCableDeviceIndex()
        {
            if (!_enumAvailable || _deviceEnumerator == null) return -1;
            try
            {
                var devs = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                if (devs == null) return -1;
                for (int i = 0; i < devs.Count; i++)
                {
                    try
                    {
                        if ((devs[i].FriendlyName ?? "").IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0)
                            return i;
                    }
                    catch { }
                }
                return -1;
            }
            catch { return -1; }
        }

        public void PlayPlaylist(List<string> files, int deviceIndex = -1)
        {
            Stop();
            if (files == null || files.Count == 0) return;
            _playlist = new List<string>(files);
            _playlistIndex = 0;
            _playlistDeviceIndex = deviceIndex;
            PlayCurrent(deviceIndex);
        }

        public void PlayFile(string path, int deviceIndex = -1)
        {
            if (string.IsNullOrEmpty(path)) return;
            _playlist = new List<string> { path };
            _playlistIndex = 0;
            _playlistDeviceIndex = deviceIndex;
            PlayCurrent(deviceIndex);
        }

        private void PlayCurrent(int deviceIndex = -1)
        {
            if (_playlist == null || _playlistIndex >= _playlist.Count)
            {
                if (_loopPlaylist)
                {
                    _playlistIndex = 0;
                    PlayCurrent(deviceIndex);
                }
                else
                {
                    Stop();
                }
                return;
            }

            var filePath = _playlist[_playlistIndex];
            if (!File.Exists(filePath))
            {
                _playlistIndex++;
                PlayCurrent(deviceIndex);
                return;
            }

            try
            {
                CleanupPlayback();

                _currentFile = new AudioFileReader(filePath);
                _currentFile.Volume = _volume;

                if (deviceIndex >= 0 && _enumAvailable && _deviceEnumerator != null)
                {
                    var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                    if (deviceIndex < devices.Count)
                        _playback = new WasapiOut(devices[deviceIndex], AudioClientShareMode.Shared, false, 100);
                    else
                        _playback = new WasapiOut { Volume = _volume };
                }
                else
                {
                    _playback = new WasapiOut { Volume = _volume };
                }

                _playback.PlaybackStopped += OnPlaybackStopped;
                _playback.Init(_currentFile);
                _playback.Play();

                _isPlaying = true;
                PlayingStateChanged?.Invoke(true);
                try { SongChanged?.Invoke(filePath); } catch { }
            }
            catch
            {
                _playlistIndex++;
                PlayCurrent(deviceIndex);
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (_playlist != null)
            {
                _playlistIndex++;
                var devIdx = _playlistDeviceIndex;
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    PlayCurrent(devIdx);
                });
            }
        }

        public void Stop()
        {
            _playlist = null;
            _playlistIndex = 0;
            CleanupPlayback();
            if (_isPlaying)
            {
                _isPlaying = false;
                PlayingStateChanged?.Invoke(false);
            }
        }

        private void CleanupPlayback()
        {
            if (_playback != null)
            {
                _playback.PlaybackStopped -= OnPlaybackStopped;
                try { _playback.Stop(); } catch { }
                _playback.Dispose();
                _playback = null;
            }
            if (_currentFile != null)
            {
                _currentFile.Dispose();
                _currentFile = null;
            }
        }

        public void Dispose()
        {
            Stop();
            _deviceEnumerator?.Dispose();
        }
    }
}

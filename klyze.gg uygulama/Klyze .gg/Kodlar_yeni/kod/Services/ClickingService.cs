using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class ClickingService
    {
        private readonly Microsoft.Extensions.Logging.ILogger<ClickingService> _logger;
        private CancellationTokenSource _cts;
        private readonly object _stateLock = new object();
        private bool _isRunning;

        public bool IsRunning
        {
            get { lock (_stateLock) return _isRunning; }
            private set { lock (_stateLock) _isRunning = value; }
        }

        public event Action<string> StatusChanged;
        public event Action<bool> RunningStateChanged;

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private const uint ME_LEFTDOWN = 0x0002;
        private const uint ME_LEFTUP = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        public ClickingService()
        {
            _logger = LoggingService.CreateLogger<ClickingService>();
        }

        public async Task StartAsync(IEnumerable<Point> positions, int speedMs, int durationSec = 0)
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            RunningStateChanged?.Invoke(true);
            StatusChanged?.Invoke("Çalışıyor...");

            try
            {
                await Task.Run(() => RunClicking(positions, speedMs, durationSec, _cts.Token), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Clicking cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in clicking loop.");
                StatusChanged?.Invoke("Hata: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
                RunningStateChanged?.Invoke(false);
                StatusChanged?.Invoke("Tamamlandı.");
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                _cts?.Cancel();
            }
        }

        private void RunClicking(IEnumerable<Point> positions, int speedMs, int durationSec, CancellationToken token)
        {
            var localPositions = new List<Point>(positions);
            if (localPositions.Count == 0) return;

            _logger.LogInformation("Starting clicking at {Count} positions with interval {Speed}ms", localPositions.Count, speedMs);

            timeBeginPeriod(1);
            try
            {
                var startTime = DateTime.UtcNow;
                int index = 0;
                int speed = Math.Max(1, speedMs);

                while (!token.IsCancellationRequested)
                {
                    if (durationSec > 0 && (DateTime.UtcNow - startTime).TotalSeconds >= durationSec)
                        break;

                    var pos = localPositions[index];
                    SetCursorPos((int)pos.X, (int)pos.Y);
                    mouse_event(ME_LEFTDOWN, 0, 0, 0, 0);
                    mouse_event(ME_LEFTUP, 0, 0, 0, 0);

                    index = (index + 1) % localPositions.Count;
                    Thread.Sleep(speed);
                }
            }
            finally
            {
                timeEndPeriod(1);
            }
        }

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint period);
    }
}

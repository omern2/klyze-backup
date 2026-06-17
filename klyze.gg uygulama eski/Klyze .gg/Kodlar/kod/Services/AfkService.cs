using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ValorantAutoClicker.Services
{
    public class AfkService
    {
        private readonly ILogger<AfkService> _logger;
        private CancellationTokenSource _cts;
        private readonly object _stateLock = new object();
        private bool _isRunning;

        public bool IsRunning
        {
            get { lock (_stateLock) return _isRunning; }
            private set { lock (_stateLock) _isRunning = value; }
        }

        public event Action<bool> RunningStateChanged;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_W = 0x57, VK_A = 0x41, VK_S = 0x53, VK_D = 0x44;
        private const byte VK_SPACE = 0x20;
        private const uint ME_LEFTDOWN = 0x0002;
        private const uint ME_LEFTUP = 0x0004;

        public AfkService(ILogger<AfkService> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(int durationSec, int intervalSec, bool infinite,
            bool forward, bool backward, bool right, bool left, bool jump, bool shoot)
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            RunningStateChanged?.Invoke(true);

            try
            {
                await Task.Run(() => RunAfk(durationSec, intervalSec, infinite,
                    forward, backward, right, left, jump, shoot, _cts.Token), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("AFK mode cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AFK loop.");
            }
            finally
            {
                IsRunning = false;
                RunningStateChanged?.Invoke(false);
            }
        }

        public void Stop()
        {
            if (IsRunning) _cts?.Cancel();
        }

        private void RunAfk(int durationSec, int intervalSec, bool infinite,
            bool fw, bool bw, bool rt, bool lt, bool jp, bool sh, CancellationToken token)
        {
            var start = DateTime.Now;
            var rnd = new Random();
            var moves = new[] { VK_W, VK_A, VK_S, VK_D };

            _logger.LogInformation("AFK started. Infinite: {Infinite}, Interval: {Interval}s", infinite, intervalSec);

            while (!token.IsCancellationRequested)
            {
                if (!infinite && (DateTime.Now - start).TotalSeconds >= durationSec) break;

                var available = new List<byte>();
                if (fw) available.Add(VK_W);
                if (bw) available.Add(VK_S);
                if (rt) available.Add(VK_D);
                if (lt) available.Add(VK_A);

                if (available.Count > 0)
                {
                    var key = available[rnd.Next(available.Count)];
                    keybd_event(key, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(rnd.Next(500, 1500));
                    keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }

                if (jp && rnd.Next(100) < 30)
                {
                    keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(50);
                    keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }

                if (sh && rnd.Next(100) < 20)
                {
                    mouse_event(ME_LEFTDOWN, 0, 0, 0, 0);
                    Thread.Sleep(rnd.Next(100, 300));
                    mouse_event(ME_LEFTUP, 0, 0, 0, 0);
                }

                Thread.Sleep(intervalSec * 1000);
            }

            _logger.LogInformation("AFK mode finished.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace ValorantAutoClicker.Services
{
    public class SpamService
    {
        private readonly ILogger<SpamService> _logger;
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

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        public SpamService(ILogger<SpamService> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(string text, int count, int speedMs)
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            RunningStateChanged?.Invoke(true);

            try
            {
                await Task.Run(() => RunSpam(text, count, speedMs, _cts.Token), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Spam cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in spam loop.");
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

        private void RunSpam(string text, int count, int speedMs, CancellationToken token)
        {
            _logger.LogInformation("Spam started. Text: '{Text}', Count: {Count}, Speed: {Speed}ms", text, count, speedMs);

            for (int i = 0; i < count && !token.IsCancellationRequested; i++)
            {
                try
                {
                    // Enter down
                    keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(30);
                    keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(50);

                    // Copy to clipboard and paste
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try { System.Windows.Clipboard.SetText(text); }
                        catch { }
                    });
                    Thread.Sleep(30);

                    // Ctrl+V
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(20);
                    keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(20);
                    keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(20);
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(50);

                    // Enter to send
                    keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(20);
                    keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(speedMs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during spam iteration {Index}", i);
                }
            }

            _logger.LogInformation("Spam finished.");
        }
    }
}

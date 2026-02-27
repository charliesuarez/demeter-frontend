using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Main.Services
{
    internal class SensorDataStateContainer : IDisposable
    {
        private readonly ISensorDataService _sensorDataService;
        private readonly ILogger<SensorDataStateContainer> _logger;
        private readonly Timer _pollTimer;
        private readonly SemaphoreSlim _pollLock = new(1, 1);

        private SensorDataResponse? _latestData;
        private IReadOnlyList<SensorHealthStatus> _healthStatus = Array.Empty<SensorHealthStatus>();
        private bool _isPolling;
        private bool _isDisposed;

        public event Action? onDataUpdated;
        public event Action<string>? onError;
        public SensorDataResponse? LatestData => _latestData;
        public IReadOnlyList<SensorHealthStatus> HealthStatus => _healthStatus;
        public bool IsPolling => _isPolling;
        public DateTime? LastUpdated { get; private set; }
        public int PollingIntervalMs { get; set; } = 10_000;

        public SensorDataStateContainer(
            ISensorDataService sensorDataService,
            ILogger<SensorDataStateContainer> logger) 
        {
            _sensorDataService = sensorDataService;
            _logger = logger;
            _pollTimer = new Timer();
            _pollTimer.Elapsed += OnPollTimerElapsed;
        }

        public void StartPolling()
        {
            if (_isPolling) return;

            _pollTimer.Interval = PollingIntervalMs;
            _pollTimer.Start();
            _isPolling = true;
            _logger.LogInformation("Started sensor data polling at {Interval}ms interval",
                PollingIntervalMs);
            _ = RefreshDataAsync();
        }

        public void StopPolling() 
        {
            if (_isPolling) return;

            _pollTimer.Stop();
            _isPolling = false;
            _logger.LogInformation("Stopped sensor data polling");
        }

        public void Dispose() 
        { 
            if (_isDisposed) return; 
            
            _pollTimer.Stop();
            _pollTimer.Dispose();
            _pollLock.Dispose();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }

        public async Task RefreshDataAsync()
        {
            if (!await _pollLock.WaitAsync(0))
            {
                _logger.LogDebug("Refresh already in progress, skipping");
                return;
            }

            try
            {
                var dataTask = _sensorDataService.GetLatestReadingAsync();
                var healthTask = _sensorDataService.GetSensorHealthAsync();

                await Task.WhenAll(dataTask, healthTask);

                _latestData = await dataTask;
                _healthStatus = await healthTask;
                LastUpdated = DateTime.Now;

                onDataUpdated?.Invoke();
                _logger.LogDebug("Sensor data refreshed at {Time}", LastUpdated);
            }
            catch (SensorDataException ex)
            {
                _logger.LogWarning(ex, "Failed to refresh sensor data");
                onError?.Invoke(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error refreshing sensor data");
                onError?.Invoke("An unexpected error occurred");
            }
            finally
            {
                _pollLock.Release();
            }
        }

        private async void OnPollTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await RefreshDataAsync();
        }
    }
}

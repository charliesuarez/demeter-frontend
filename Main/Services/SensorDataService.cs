using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Main.Services
{
    public class SensorDataService : ISensorDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SensorDataService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public SensorDataService(
            HttpClient httpClient, 
            ILogger<SensorDataService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        async Task<SensorDataResponse?> ISensorDataService.GetLatestReadingAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("/latest", cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<SensorDataResponse>(_jsonOptions, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch latest sensor readings");
                throw new SensorDataException("Unable to retrieve latest sensor data", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Request to fetch latest readings timed out");
                throw new SensorDataException("Request timed out", ex);
            }
        }

        async Task<IReadOnlyList<SensorReading>> ISensorDataService.GetHistoryAsync(
            string deviceId, 
            int hours, 
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/history?hours={hours}",
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var readings = await response.Content.ReadFromJsonAsync<List<SensorReading>>(
                    _jsonOptions,
                    cancellationToken);

                return readings ?? new List<SensorReading>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch sensor history for device {deviceId}", deviceId);
                throw new SensorDataException($"Unable to retrieve history for device {deviceId}", ex);
            }
        }


        async Task<IReadOnlyList<SensorHealthStatus>> ISensorDataService.GetAggregatedDataAsync(
            string deviceId, 
            AggregationInterval interval, 
            DateTime startTime, 
            DateTime endTime, 
            CancellationToken cancellationToken)
        {
            try
            {
                var query = $"/aggregated/{deviceId}" +
                    $"?interval={interval}" +
                    $"&start={startTime:O}" +
                    $"&end={endTime:O}";

                var response = await _httpClient.GetAsync(query, cancellationToken);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<List<AggregatedReading>>(
                    _jsonOptions,
                    cancellationToken);

                return (IReadOnlyList<SensorHealthStatus>)(data ?? new List<AggregatedReading>());
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, 
                    "Failed to fetch aggregated data for device {deviceId}", deviceId);
                throw new SensorDataException(
                    $"Unable to retrieve aggregated data for device {deviceId}.", ex);
            }
        }

        async Task<IReadOnlyList<SensorHealthStatus>> ISensorDataService.GetSensorHealthAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    "/health",
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var health = await response.Content.ReadFromJsonAsync<List<SensorHealthStatus>>(
                    _jsonOptions,
                    cancellationToken);

                return health ?? new List<SensorHealthStatus>();
            }
            catch (HttpRequestException ex) {
                _logger.LogError(ex, "Failed to fetch sensor health status");
                throw new SensorDataException("Unable to retrieve sensor health status", ex);
            }
        }

        async Task<IReadOnlyList<DeviceInfo>> ISensorDataService.GetDevicesAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    "/devices",
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var devices = await response.Content.ReadFromJsonAsync<List<DeviceInfo>>(
                    _jsonOptions,
                    cancellationToken);

                return (IReadOnlyList<DeviceInfo>)(devices ?? new List<DeviceInfo>());
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch device list");
                throw new SensorDataException("Unable to retrieve device list", ex);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main.Services
{
    internal interface ISensorDataService
    {
        Task<SensorDataResponse?> GetLatestReadingAsync(
            CancellationToken cancellation = default);

        Task<IReadOnlyList<SensorReading>> GetHistoryAsync(
            string deviceId,
            int hours = 24,
            CancellationToken cancellation = default);

        Task<IReadOnlyList<SensorHealthStatus>> GetAggregatedDataAsync(
            string deviceId,
            AggregationInterval interval,
            DateTime startTIme,
            DateTime endTime,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SensorHealthStatus>> GetSensorHealthAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(
            CancellationToken cancellationToken = default);
    }
}

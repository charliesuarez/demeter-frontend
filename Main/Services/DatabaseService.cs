using SQLite;
using Main.Services;
using System.Text;

namespace Main.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, "demeter.db");

        private SystemSettings? _cachedSettings;

        public DatabaseService()
        {
            var options = new SQLiteConnectionString(DatabasePath, true);
            _database = new SQLiteAsyncConnection(options);
        }

        public async Task InitializeAsync()
        {
            await _database.ExecuteAsync("PRAGMA journal_mode = WAL;");
            await _database.ExecuteAsync("PRAGMA synchronous = NORMAL;");

            await _database.CreateTableAsync<Batch>();
            await _database.CreateTableAsync<Crop>();
            await _database.CreateTableAsync<SensorReading>();
            await _database.CreateTableAsync<SystemSettings>();

            var existing = await _database.Table<SystemSettings>().FirstOrDefaultAsync();
            if (existing == null)
                await _database.InsertAsync(new SystemSettings());

            await CleanupOldReadingsAsync();
        }

        // =====================
        // BATCH METHODS
        // =====================

        public async Task<Batch> CreateBatchAsync()
        {
            var batch = new Batch { StartDate = DateTime.Now };
            await _database.InsertAsync(batch);
            return batch;
        }

        public async Task<Batch?> GetActiveBatchAsync()
        {
            return await _database.Table<Batch>()
                .Where(b => b.EndDate == null)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Batch>> GetAllBatchesAsync()
        {
            return await _database.Table<Batch>().ToListAsync();
        }

        public async Task EndBatchAsync(int batchId)
        {
            var batch = await _database.Table<Batch>()
                .Where(b => b.Id == batchId)
                .FirstOrDefaultAsync();
            if (batch != null)
            {
                batch.EndDate = DateTime.Now;
                await _database.UpdateAsync(batch);
            }
        }

        // =====================
        // CROP METHODS
        // =====================

        public async Task<Crop> CreateCropAsync(int batchId, int slotNumber, string type)
        {
            var crop = new Crop
            {
                BatchId = batchId,
                SlotNumber = slotNumber,
                Type = type,
                StartDate = DateTime.Now
            };
            await _database.InsertAsync(crop);
            return crop;
        }

        public async Task<List<Crop>> GetCropsByBatchAsync(int batchId)
        {
            return await _database.Table<Crop>()
                .Where(c => c.BatchId == batchId)
                .ToListAsync();
        }

        public async Task<List<Crop>> GetAllCropsAsync()
        {
            return await _database.Table<Crop>().ToListAsync();
        }

        public async Task HarvestCropAsync(int cropId)
        {
            var crop = await _database.Table<Crop>()
                .Where(c => c.Id == cropId)
                .FirstOrDefaultAsync();
            if (crop != null)
            {
                crop.EndDate = DateTime.Now;
                await _database.UpdateAsync(crop);
            }
        }

        // =====================
        // SENSOR READING METHODS
        // =====================

        /// <summary>
        /// Saves every reading unconditionally.
        /// The ESP firmware controls the transmission rate.
        /// </summary>
        public async Task SaveReadingAsync(SensorReading reading)
        {
            await _database.InsertAsync(reading);
        }

        public async Task<List<SensorReading>> GetReadingsByBatchAsync(int batchId)
        {
            return await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();
        }

        public async Task<List<SensorReading>> GetLastNReadingsAsync(int batchId, int count)
        {
            var all = await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId)
                .OrderByDescending(r => r.Timestamp)
                .Take(count)
                .ToListAsync();
            all.Reverse();
            return all;
        }

        public async Task<List<SensorReading>> GetReadingsInRangeAsync(int batchId, DateTime from)
        {
            return await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId && r.Timestamp >= from)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();
        }

        public async Task<List<SensorReading>> GetHourlyAveragesAsync(int batchId, DateTime from)
        {
            var raw = await GetReadingsInRangeAsync(batchId, from);
            if (!raw.Any()) return raw;

            return raw
                .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0))
                .OrderBy(g => g.Key)
                .Select(g => new SensorReading
                {
                    BatchId = batchId,
                    Timestamp = g.Key,
                    Ph = g.Average(r => r.Ph),
                    WaterTemp = g.Average(r => r.WaterTemp),
                    AirTemp = g.Average(r => r.AirTemp),
                    Humidity = g.Average(r => r.Humidity),
                    Tds = g.Average(r => r.Tds),
                    Ec = g.Average(r => r.Ec),
                    WaterLevel = g.Average(r => r.WaterLevel),
                    ReservoirLevel = g.Average(r => r.ReservoirLevel),
                    LightIntensity = g.Average(r => r.LightIntensity),
                    DissolvedOxygen = g.Average(r => r.DissolvedOxygen)
                })
                .ToList();
        }

        public async Task<List<SensorReading>> GetDailyAveragesAsync(int batchId, DateTime from)
        {
            var raw = await GetReadingsInRangeAsync(batchId, from);
            if (!raw.Any()) return raw;

            return raw
                .GroupBy(r => r.Timestamp.Date)
                .OrderBy(g => g.Key)
                .Select(g => new SensorReading
                {
                    BatchId = batchId,
                    Timestamp = g.Key,
                    Ph = g.Average(r => r.Ph),
                    WaterTemp = g.Average(r => r.WaterTemp),
                    AirTemp = g.Average(r => r.AirTemp),
                    Humidity = g.Average(r => r.Humidity),
                    Tds = g.Average(r => r.Tds),
                    Ec = g.Average(r => r.Ec),
                    WaterLevel = g.Average(r => r.WaterLevel),
                    ReservoirLevel = g.Average(r => r.ReservoirLevel),
                    LightIntensity = g.Average(r => r.LightIntensity),
                    DissolvedOxygen = g.Average(r => r.DissolvedOxygen)
                })
                .ToList();
        }

        // =====================
        // SETTINGS METHODS
        // =====================

        public async Task<SystemSettings> GetSettingsAsync()
        {
            if (_cachedSettings != null) return _cachedSettings;
            _cachedSettings = await _database.Table<SystemSettings>().FirstOrDefaultAsync()
                              ?? new SystemSettings();
            return _cachedSettings;
        }

        public async Task SaveSettingsAsync(SystemSettings settings)
        {
            var existing = await _database.Table<SystemSettings>().FirstOrDefaultAsync();
            if (existing == null)
                await _database.InsertAsync(settings);
            else
                await _database.UpdateAsync(settings);

            _cachedSettings = settings;
        }

        // =====================
        // MAINTENANCE METHODS
        // =====================

        /// <summary>
        /// Deletes all sensor readings older than 90 days.
        /// Uses ticks since SQLite-NET stores DateTime as ticks (long).
        /// </summary>
        public async Task CleanupOldReadingsAsync()
        {
            var cutoffTicks = DateTime.Now.AddDays(-90).Ticks;
            await _database.ExecuteAsync(
                "DELETE FROM SensorReadings WHERE Timestamp < ?",
                cutoffTicks);
        }

        public async Task<int> GetReadingCountAsync()
        {
            return await _database.Table<SensorReading>().CountAsync();
        }

        public async Task<string> ExportToCsvAsync()
        {
            var readings = await _database.Table<SensorReading>()
                .OrderBy(r => r.Timestamp)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,BatchId,Timestamp,Ph,WaterTemp,AirTemp,Humidity,TDS,EC,WaterLevel,ReservoirLevel,LightIntensity,DissolvedOxygen");

            foreach (var r in readings)
            {
                sb.AppendLine($"{r.Id},{r.BatchId},{r.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                              $"{r.Ph:F2},{r.WaterTemp:F2},{r.AirTemp:F2},{r.Humidity:F2}," +
                              $"{r.Tds:F2},{r.Ec:F2},{r.WaterLevel:F2},{r.ReservoirLevel:F2}," +
                              $"{r.LightIntensity:F2},{r.DissolvedOxygen:F2}");
            }

            return sb.ToString();
        }

        public async Task ResetAsync()
        {
            await _database.DropTableAsync<SensorReading>();
            await _database.DropTableAsync<Crop>();
            await _database.DropTableAsync<Batch>();
            await _database.DropTableAsync<SystemSettings>();
            _cachedSettings = null;
            await InitializeAsync();
        }
    }
}

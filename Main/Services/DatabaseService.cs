using SQLite;
using Main.Services;

namespace Main.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        private static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, "demeter.db");

        public DatabaseService()
        {
            _database = new SQLiteAsyncConnection(DatabasePath);
        }

        public async Task InitializeAsync()
        {
            await _database.CreateTableAsync<Batch>();
            await _database.CreateTableAsync<Crop>();
            await _database.CreateTableAsync<SensorReading>();
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

        public async Task SaveReadingAsync(SensorReading reading)
        {
            await _database.InsertAsync(reading);
        }

        // All raw readings for a batch ordered by time
        public async Task<List<SensorReading>> GetReadingsByBatchAsync(int batchId)
        {
            return await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();
        }

        // Last N raw readings for a batch (for Live view)
        public async Task<List<SensorReading>> GetLastNReadingsAsync(int batchId, int count)
        {
            var all = await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId)
                .OrderByDescending(r => r.Timestamp)
                .Take(count)
                .ToListAsync();
            all.Reverse(); // oldest first for chart display
            return all;
        }

        // Readings within a time window for a batch (used by hourly/weekly views)
        public async Task<List<SensorReading>> GetReadingsInRangeAsync(int batchId, DateTime from)
        {
            return await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId && r.Timestamp >= from)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();
        }

        // Hourly averages — groups readings into 1-hour buckets and averages each sensor
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

        // Daily averages — groups readings into 24-hour buckets and averages each sensor
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

        public async Task ResetAsync()
        {
            await _database.DropTableAsync<SensorReading>();
            await _database.DropTableAsync<Crop>();
            await _database.DropTableAsync<Batch>();
            await InitializeAsync();
        }
    }
}

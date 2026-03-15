using SQLite;

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

        // Creates the tables if they don't exist yet
        public async Task InitializeAsync()
        {
            await _database.CreateTableAsync<Batch>();
            await _database.CreateTableAsync<Crop>();
            await _database.CreateTableAsync<SensorReading>();
        }

        // =====================
        // BATCH METHODS
        // =====================

        // Save a new batch and return it with its auto-assigned Id
        public async Task<Batch> CreateBatchAsync()
        {
            var batch = new Batch { StartDate = DateTime.Now };
            await _database.InsertAsync(batch);
            return batch;
        }

        // Get the currently active batch (one with no end date)
        public async Task<Batch?> GetActiveBatchAsync()
        {
            return await _database.Table<Batch>()
                .Where(b => b.EndDate == null)
                .FirstOrDefaultAsync();
        }

        // Get all batches ever
        public async Task<List<Batch>> GetAllBatchesAsync()
        {
            return await _database.Table<Batch>().ToListAsync();
        }

        // Mark a batch as harvested
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

        // Save a new crop
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

        // Get all crops for a specific batch
        public async Task<List<Crop>> GetCropsByBatchAsync(int batchId)
        {
            return await _database.Table<Crop>()
                .Where(c => c.BatchId == batchId)
                .ToListAsync();
        }

        // Get all crops ever (for stats)
        public async Task<List<Crop>> GetAllCropsAsync()
        {
            return await _database.Table<Crop>().ToListAsync();
        }

        // Mark a crop as harvested
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

        // Save a new sensor reading
        public async Task SaveReadingAsync(SensorReading reading)
        {
            await _database.InsertAsync(reading);
        }

        // Get all readings for a specific batch (for analytics charts)
        public async Task<List<SensorReading>> GetReadingsByBatchAsync(int batchId)
        {
            return await _database.Table<SensorReading>()
                .Where(r => r.BatchId == batchId)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();
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
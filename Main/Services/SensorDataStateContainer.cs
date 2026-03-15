namespace Main.Services
{
    public class SensorDataStateContainer
    {
        private SensorReading? _latestReading;

        public SensorReading? LatestReading => _latestReading;

        public event Action? OnChange;

        public void SetLatestReading(SensorReading reading)
        {
            _latestReading = reading;
            OnChange?.Invoke();
        }
    }
}
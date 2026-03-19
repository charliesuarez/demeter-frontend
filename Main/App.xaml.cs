using Main.Bluetooth;
using Microsoft.Extensions.DependencyInjection;

namespace Main
{
	public partial class App : Application
	{
		public App(IServiceProvider services)
		{
			InitializeComponent();

			// Grab the singleton instances from DI
			var ble = services.GetRequiredService<IBluetoothService>();
			var state = services.GetRequiredService<SensorState>();

			// Initialize BLE (hooks up adapter events, etc.)
			_ = ble.InitializeAsync();

			// Whenever BLE receives sensor data, store it in SensorState
			ble.SensorDataReceived += data => state.SetLatest(data);
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			return new Window(new MainPage()) { Title = "Main" };
		}
	}
}

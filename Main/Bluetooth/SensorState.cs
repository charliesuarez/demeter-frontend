using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main.Bluetooth
{
	public class SensorState
	{
		public SensorData latest { get; private set; } = new(); //gets sensors when updated

		public event Action? Changed; //updates ui

		public void SetLatest(SensorData data)
		{
			latest = data;
			Changed?.Invoke();
		}



	}
}

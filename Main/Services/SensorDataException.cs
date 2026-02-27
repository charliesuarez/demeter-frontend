using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main.Services
{
    internal class SensorDataException : Exception
    {
        public SensorDataException(string message) : base(message) { }

        public SensorDataException(
            string message,
            Exception innerException)
            : base(message, innerException)
        { }
    }
}

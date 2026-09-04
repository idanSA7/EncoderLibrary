using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IcdModelsLIbrary;

namespace TelemetrySimulator.DTOs
{
    public class TelemetrySimulationRequestDto
    {
        private const int MIN_PORT_RANGE = 1024;
        private const int MAX_PORT_RANGE = 65535;

        private const int MIN_INTERVAL_MS = 10;
        private const int MAX_INTERVAL_MS = 60000;
        private const int DEFAULT_INTERVAL_MS = 1000;

        [Range(MIN_PORT_RANGE, MAX_PORT_RANGE, ErrorMessage = "Port must be between 1024 and 65535.")]
        public int? DestinationNetworkPort { get; set; }

        [Required(ErrorMessage = "Transmission interval is required.")]
        [Range(MIN_INTERVAL_MS, MAX_INTERVAL_MS, ErrorMessage = "Interval must be between 10ms and 60000ms.")]
        public int TransmissionIntervalMilliseconds { get; set; } = DEFAULT_INTERVAL_MS;

        public IcdType? IcdType { get; set; }

        public Dictionary<string, string>? TelemetryInputs { get; set; } = new Dictionary<string, string>();
    }
}
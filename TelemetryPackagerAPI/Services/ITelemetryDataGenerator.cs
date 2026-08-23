using EncoderLIbrary;
using IcdModelsLIbrary;
using TelemetrySimulator.DTOs;

namespace TelemetrySimulator.Services
{
    public interface ITelemetryDataGenerator
    {
        Dictionary<string, string> PrepareTelemetryData(IcdModel icd, TelemetrySimulationRequestDto configuration);
    }
}

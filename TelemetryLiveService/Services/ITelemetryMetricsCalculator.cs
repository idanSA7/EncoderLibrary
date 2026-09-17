using System.Text.Json.Nodes;

namespace TelemetryLiveService.Services
{
    public interface ITelemetryMetricsCalculator
    {
        Dictionary<string, object> CalculateMetrics(JsonObject jsonObject);
        void Reset();
    }
}

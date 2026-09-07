using IcdModelsLIbrary;

namespace TelemetryDeviceAPI.Interfaces
{
    public interface IIcdLoaderService
    {
        Dictionary<IcdType, IcdModel> LoadDefinitions();
    }
}

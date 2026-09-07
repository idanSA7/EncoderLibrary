using System;
using System.Collections.Generic;
using System.IO;
using IcdModelsLIbrary;
using Microsoft.Extensions.Options;
using TelemetryDeviceAPI.Configuration;
using TelemetryDeviceAPI.Interfaces;
namespace TelemetryDeviceAPI.Services
{

    public class IcdLoaderService : IIcdLoaderService
    {
        private readonly IcdSettings _settings;

        public IcdLoaderService(IOptions<IcdSettings> settings)
        {
            _settings = settings.Value;
        }

        public Dictionary<IcdType, IcdModel> LoadDefinitions()
        {
            string folderPath = Path.Combine(AppContext.BaseDirectory, _settings.IcdDefinition);

            return new Dictionary<IcdType, IcdModel>
            {
                [IcdType.FlightBoxUp] = IcdModel.LoadFromJson(File.ReadAllText(Path.Combine(folderPath, "FlightBoxUpIcd.json"))),
                [IcdType.FlightBoxDown] = IcdModel.LoadFromJson(File.ReadAllText(Path.Combine(folderPath, "FlightBoxDownIcd.json")))
            };
        }
    }
}
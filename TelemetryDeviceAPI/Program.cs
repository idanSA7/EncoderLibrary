using System.IO;
using DecoderLibrary;
using IcdModelsLIbrary;
using KafkaIntegrationLibrary.Configuration;
using KafkaIntegrationLibrary.Interfaces;
using KafkaIntegrationLibrary.Services;
using TelemetryDeviceAPI.Interfaces;
using TelemetryDeviceAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string icdDirectory = Path.Combine(AppContext.BaseDirectory, "IcdDefinitions");
var icdDefinitions = new Dictionary<IcdType, IcdModel>
{
    [IcdType.FlightBoxUp] = IcdModel.LoadFromJson(File.ReadAllText(Path.Combine(icdDirectory, "FlightBoxUpIcd.json"))),
    [IcdType.FlightBoxDown] = IcdModel.LoadFromJson(File.ReadAllText(Path.Combine(icdDirectory, "FlightBoxDownIcd.json")))
};

builder.Services.AddSingleton(icdDefinitions);

builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddSingleton<DecoderFlow>();
builder.Services.AddSingleton<ISnifferService, SnifferService>();
builder.Services.AddSingleton<IPacketQueueService, TelemetryPipelineService>();

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection(nameof(KafkaSettings)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
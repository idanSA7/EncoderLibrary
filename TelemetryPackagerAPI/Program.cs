using TelemetrySimulator.Configuration;
using TelemetrySimulator.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<NetworkSettings>(
    builder.Configuration.GetSection(nameof(NetworkSettings)));

builder.Services.AddSingleton<ITelemetryDataGenerator, TelemetryDataGenerator>();
builder.Services.AddSingleton<ITelemetrySimulationService, TelemetrySimulationService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
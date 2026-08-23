using TelemetrySimulator.Configuration;
using TelemetrySimulator.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Options Pattern for NetworkSettings using nameof
builder.Services.Configure<NetworkSettings>(
    builder.Configuration.GetSection(nameof(NetworkSettings)));

// Register Data Generator and Telemetry Simulation Service
builder.Services.AddSingleton<ITelemetryDataGenerator, TelemetryDataGenerator>();
builder.Services.AddSingleton<ITelemetrySimulationService, TelemetrySimulationService>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
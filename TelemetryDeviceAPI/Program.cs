using DecoderLibrary;
using IcdModelsLIbrary;
using TelemetryDeviceAPI.Configuration;
using TelemetryDeviceAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddSingleton<DecoderFlow>();
builder.Services.AddSingleton<IcdModel>();
builder.Services.AddSingleton<IPacketQueueService, PacketQueueService>();
builder.Services.AddSingleton<ISnifferService, SnifferService>();
builder.Services.Configure<KafkaSettings>
    (builder.Configuration.GetSection(nameof(KafkaSettings)));

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
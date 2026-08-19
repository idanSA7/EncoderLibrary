using TelemetryDeviceAPI.Services;
using DecoderLibrary;
using IcdModelsLIbrary;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Kafka Producer Service registration
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

// Decoder Flow registration
builder.Services.AddSingleton<DecoderFlow>();

// ICD Model registration
builder.Services.AddSingleton<IcdModel>(sp =>
{
    // Replace with your actual ICD loading logic if loaded from JSON or file
    return new IcdModel();
});

// Packet Queue Pipeline Service registration
builder.Services.AddSingleton<IPacketQueueService, PacketQueueService>();

// Sniffer Service registration
builder.Services.AddSingleton<ISnifferService, SnifferService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
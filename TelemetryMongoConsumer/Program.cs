using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using TelemetryMongoConsumer.Configuration;
using TelemetryMongoConsumer.Interfaces;
using TelemetryMongoConsumer.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

MongoSettings mongoSettings = builder.Configuration
    .GetSection(nameof(MongoSettings))
    .Get<MongoSettings>() ?? new MongoSettings();

builder.Services.AddSingleton(mongoSettings);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection(nameof(KafkaSettings)));

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoSettings.ConnectionString));

builder.Services.AddSingleton<ITelemetryPacketProcessor, TelemetryPacketProcessor>();
builder.Services.AddSingleton<ITelemetryMongoRepository, TelemetryMongoRepository>();
builder.Services.AddSingleton<IKafkaConsumerManager, KafkaMongoConsumerWorker>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
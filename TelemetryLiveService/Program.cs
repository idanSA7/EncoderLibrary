using TelemetryLiveService.Configuration;
using TelemetryLiveService.Hubs;
using TelemetryLiveService.Services;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicyName = "AllowSignalRClients";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection(nameof(KafkaSettings)));

builder.Services.Configure<TelemetryFilterSettings>(
    builder.Configuration.GetSection(nameof(TelemetryFilterSettings)));

builder.Services.AddSingleton<LiveTelemetryConsumerService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(corsPolicyName);

app.UseAuthorization();

app.MapControllers();

app.MapHub<TelemetryHub>("/telemetryHub");

app.Run();
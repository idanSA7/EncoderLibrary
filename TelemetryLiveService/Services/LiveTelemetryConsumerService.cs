using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TelemetryLiveService.Configuration;
using TelemetryLiveService.Hubs;

namespace TelemetryLiveService.Services;

public class LiveTelemetryConsumerService
{
    private readonly ILogger<LiveTelemetryConsumerService> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly TelemetryFilterSettings _filterSettings;
    private readonly IHubContext<TelemetryHub> _hubContext;

    private CancellationTokenSource? _cts;
    private Task? _consumingTask;

    public bool IsRunning => _consumingTask != null && !_consumingTask.IsCompleted;

    public LiveTelemetryConsumerService(
        ILogger<LiveTelemetryConsumerService> logger,
        IOptions<KafkaSettings> kafkaOptions,
        IOptions<TelemetryFilterSettings> filterOptions,
        IHubContext<TelemetryHub> hubContext)
    {
        _logger = logger;
        _kafkaSettings = kafkaOptions.Value;
        _filterSettings = filterOptions.Value;
        _hubContext = hubContext;
    }

    public void StartConsuming()
    {
        if (IsRunning)
        {
            _logger.LogWarning("Live Telemetry Consumer is already running.");
            return;
        }

        _cts = new CancellationTokenSource();
        _consumingTask = Task.Run(async () => await ConsumeLoopAsync(_cts.Token));
        _logger.LogInformation("Live Telemetry Consumer started.");
    }

    public void StopConsuming()
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Live Telemetry Consumer is not running.");
            return;
        }

        _cts?.Cancel();
        _consumingTask?.Wait();
        _cts?.Dispose();
        _cts = null;
        _consumingTask = null;
        _logger.LogInformation("Live Telemetry Consumer stopped.");
    }

    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        using IConsumer<Ignore, string> consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_kafkaSettings.Topic);

        _logger.LogInformation("Live Telemetry Consumer subscribed to topic: {Topic}", _kafkaSettings.Topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<Ignore, string>? consumeResult = consumer.Consume(cancellationToken);
                    if (consumeResult?.Message?.Value == null)
                    {
                        continue;
                    }

                    string rawMessage = consumeResult.Message.Value;
                    _logger.LogInformation("Received raw message from Kafka: {Raw}", rawMessage);

                    string? filteredJson = FilterPayload(rawMessage);

                    if (filteredJson != null)
                    {
                        _logger.LogInformation("Broadcasting filtered data to SignalR: {Filtered}", filteredJson);
                        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", filteredJson, cancellationToken);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error occurred.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consume loop was cancelled.");
        }
        finally
        {
            consumer.Close();
        }
    }

    private string? FilterPayload(string rawJson)
    {
        try
        {
            JsonNode? jsonNode = JsonNode.Parse(rawJson);
            if (jsonNode is not JsonObject jsonObject)
            {
                return rawJson;
            }

            if (_filterSettings.IncludedFields == null || !_filterSettings.IncludedFields.Any())
            {
                return rawJson;
            }

            JsonObject filteredObject = new JsonObject();

            foreach (string field in _filterSettings.IncludedFields)
            {
                if (jsonObject.TryGetPropertyValue(field, out JsonNode? value))
                {
                    filteredObject[field] = value?.DeepClone();
                }
            }

            return filteredObject.ToJsonString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse or filter JSON payload");
            return null;
        }
    }
}
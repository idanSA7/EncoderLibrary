using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelemetryLiveService.Configuration;
using TelemetryLiveService.Hubs;

namespace TelemetryLiveService.Services;

public class LiveTelemetryConsumerService
{
    private readonly ILogger<LiveTelemetryConsumerService> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ITelemetryMetricsCalculator _metricsCalculator;

    private CancellationTokenSource? _liveTelemetryCancellationToken;
    private Task? _consumingTask;

    public bool IsRunning => _consumingTask != null && !_consumingTask.IsCompleted;

    public LiveTelemetryConsumerService(
        ILogger<LiveTelemetryConsumerService> logger,
        IOptions<KafkaSettings> kafkaOptions,
        IHubContext<TelemetryHub> hubContext,
        ITelemetryMetricsCalculator metricsCalculator)
    {
        _logger = logger;
        _kafkaSettings = kafkaOptions.Value;
        _hubContext = hubContext;
        _metricsCalculator = metricsCalculator;
    }

    public void StartConsuming()
    {
        if (IsRunning)
        {
            _logger.LogWarning("Live Telemetry Consumer is already running.");
            return;
        }

        _liveTelemetryCancellationToken = new CancellationTokenSource();
        _consumingTask = Task.Run(() => ConsumeLoopAsync(_liveTelemetryCancellationToken.Token));
        _logger.LogInformation("Live Telemetry Consumer started.");
    }

    public void StopConsuming()
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Live Telemetry Consumer is not running.");
            return;
        }

        _liveTelemetryCancellationToken?.Cancel();

        try
        {
            _consumingTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }

        _liveTelemetryCancellationToken?.Dispose();
        _liveTelemetryCancellationToken = null;
        _consumingTask = null;

        _metricsCalculator.Reset();
        _logger.LogInformation("Live Telemetry Consumer stopped.");
    }

    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        using IConsumer<Null, string> consumer = CreateConsumer();
        consumer.Subscribe(_kafkaSettings.Topic);
        _logger.LogInformation("Live Telemetry Consumer subscribed to topic: {Topic}", _kafkaSettings.Topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessSingleMessageAsync(consumer, cancellationToken);
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

    private async Task ProcessSingleMessageAsync(IConsumer<Null, string> consumer, CancellationToken cancellationToken)
    {
        try
        {
            ConsumeResult<Null, string>? consumeResult = consumer.Consume(cancellationToken);
            if (string.IsNullOrWhiteSpace(consumeResult?.Message?.Value))
            {
                return;
            }

            JsonNode? jsonNode = JsonNode.Parse(consumeResult.Message.Value);
            if (jsonNode is not JsonObject jsonObject)
            {
                return;
            }

            Dictionary<string, object> averages = _metricsCalculator.CalculateMetrics(jsonObject);

            if (averages.Count > 0)
            {
                string payload = JsonSerializer.Serialize(averages);
                await BroadcastTelemetryAsync(payload, cancellationToken);
            }
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Kafka consume error occurred.");
        }
    }

    private async Task BroadcastTelemetryAsync(string payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Broadcasting calculated averages to SignalR: {Payload}", payload);
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", payload, cancellationToken);
    }

    private IConsumer<Null, string> CreateConsumer()
    {
        ConsumerConfig config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        return new ConsumerBuilder<Null, string>(config).Build();
    }
}
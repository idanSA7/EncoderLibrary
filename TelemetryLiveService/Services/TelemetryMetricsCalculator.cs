using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TelemetryLiveService.Configuration;

namespace TelemetryLiveService.Services;

public class TelemetryMetricsCalculator : ITelemetryMetricsCalculator
{
    private record MetricDataPoint(DateTime Timestamp, double Value);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<MetricDataPoint>> _metricsHistory = new();
    private readonly TelemetryFilterSettings _filterSettings;

    public TelemetryMetricsCalculator(IOptions<TelemetryFilterSettings> filterOptions)
    {
        _filterSettings = filterOptions.Value;
    }

    public Dictionary<string, object> CalculateMetrics(JsonObject jsonObject)
    {
        DateTime now = DateTime.UtcNow;
        Dictionary<string, object> results = new Dictionary<string, object>();

        if (_filterSettings.IncludedFields == null)
        {
            return results;
        }

        foreach (string field in _filterSettings.IncludedFields)
        {
            if (TryExtractNumericValue(jsonObject, field, out double numericValue))
            {
                results[field] = ProcessFieldMetrics(field, numericValue, now);
            }
        }

        return results;
    }

    public void Reset()
    {
        _metricsHistory.Clear();
    }

    private bool TryExtractNumericValue(JsonObject jsonObject, string field, out double value)
    {
        value = 0.0;
        if (jsonObject.TryGetPropertyValue(field, out JsonNode? nodeValue) && nodeValue != null)
        {
            return double.TryParse(nodeValue.ToString(), out value);
        }
        return false;
    }

    private object ProcessFieldMetrics(string field, double currentValue, DateTime now)
    {
        ConcurrentQueue<MetricDataPoint> queue = _metricsHistory.GetOrAdd(field, _ => new ConcurrentQueue<MetricDataPoint>());

        queue.Enqueue(new MetricDataPoint(now, currentValue));
        PruneOldDataPoints(queue, now.AddMinutes(-15));

        List<MetricDataPoint> windowPoints = queue.ToList();

        return new
        {
            CurrentValue = currentValue,
            Avg1Min = Math.Round(CalculateWindowAverage(windowPoints, now.AddMinutes(-1)), 2),
            Avg5Min = Math.Round(CalculateWindowAverage(windowPoints, now.AddMinutes(-5)), 2),
            Avg15Min = Math.Round(CalculateWindowAverage(windowPoints, now.AddMinutes(-15)), 2),
            SamplesCount = windowPoints.Count
        };
    }

    private void PruneOldDataPoints(ConcurrentQueue<MetricDataPoint> queue, DateTime oldestAllowedTimestamp)
    {
        while (queue.TryPeek(out MetricDataPoint? oldestPoint) && oldestPoint != null && oldestPoint.Timestamp < oldestAllowedTimestamp)
        {
            queue.TryDequeue(out _);
        }
    }

    private double CalculateWindowAverage(List<MetricDataPoint> windowPoints, DateTime threshold)
    {
        List<MetricDataPoint> filteredPoints = windowPoints.Where(point => point.Timestamp >= threshold).ToList();
        return filteredPoints.Count > 0 ? filteredPoints.Average(point => point.Value) : 0.0;
    }
}
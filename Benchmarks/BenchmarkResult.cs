namespace Proxus_MQTT_Bench.Benchmarks;

public record BenchmarkResult
{
    public required string? BrokerName { get; init; }
    public required BenchmarkParameters Parameters { get; init; }
    public bool IsSuccess { get; init; } = true;
    public TimeSpan ConnectionTime { get; init; }
    public TimeSpan TotalElapsedTime { get; set; }
    public double MessageThroughput { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public TimeSpan DisconnectionTime { get; init; }
    public double MessageDeliverySuccessRate { get; init; }
    public double MessageLossRate { get; init; }
    public double TotalDataTransferred { get; init; }
    public string DataSizeUnit { get; init; } = "B";
    public double MessageReceptionRate { get; init; }
    public int TotalMessagesReceived { get; init; }
    public int TotalMessagesSent { get; init; }
    public double AverageCpuUtilization { get; init; }
    public double AverageMemoryConsumption { get; init; }
    public string LastError { get; init; } = "";
    public double PerformanceScore { get; set; }
    public string? ErrorMessage { get; init; }
    public int OutOfOrderMessages { get; set; }
    public int Reconnections { get; set; }

    public static BenchmarkResult CreateFailedResult(BenchmarkParameters parameters, string errorMessage)
    {
        return new BenchmarkResult
        {
            BrokerName = parameters.BrokerName,
            Parameters = parameters,
            IsSuccess = false,
            ConnectionTime = TimeSpan.Zero,
            TotalElapsedTime = TimeSpan.Zero,
            MessageThroughput = 0,
            AverageLatency = TimeSpan.Zero,
            DisconnectionTime = TimeSpan.Zero,
            MessageDeliverySuccessRate = 0,
            MessageLossRate = 0,
            TotalDataTransferred = 0,
            DataSizeUnit = "B",
            ErrorMessage = errorMessage,
        };
    }

    public string FormatMetric(string metricName)
    {
        return metricName switch
        {
            "Performance Score" => PerformanceScore.ToString("N0"),
            "Message Throughput (msg/s)" => MessageThroughput.ToString("N0"),
            "Message Reception Rate (msg/s)" => MessageReceptionRate.ToString("N0"),
            "Message Delivery Success Rate" => MessageDeliverySuccessRate.ToString("P0"),
            "Average Latency (ms)" => AverageLatency.TotalMilliseconds.ToString("N0"),
            "Total Data Transferred" => FormatDataSize(TotalDataTransferred),
            "Out of Order Messages" => OutOfOrderMessages.ToString("N0"),
            "Reconnections" => Reconnections.ToString("N0"),
            "Messages Sent" => TotalMessagesSent.ToString("N0"),
            "Messages Received" => TotalMessagesReceived.ToString("N0"),
            "Connection Time (s)" => ConnectionTime.TotalSeconds.ToString("N0"),
            "Disconnection Time (s)" => DisconnectionTime.TotalSeconds.ToString("N0"),
            "CPU Utilization (%)" => AverageCpuUtilization.ToString("N0"),
            "Memory Consumption (MB)" => AverageMemoryConsumption.ToString("N0"),
            "Message Loss Rate" => MessageLossRate.ToString("P0"),
            "Total Elapsed Time (s)" => TotalElapsedTime.TotalSeconds.ToString("N0"),
            "Subscribers" => Parameters.SubscriberCount.ToString("N0"),
            "Messages per Subscriber" => (TotalMessagesReceived / (double)Parameters.SubscriberCount).ToString("N0"),
            _ => "N/A"
        };
    }

    private string FormatDataSize(double size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:N2} {units[unitIndex]}";
    }
}
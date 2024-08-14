namespace Proxus_MQTT_Bench.Benchmarks;

public static class BenchmarkReporter
{
    public static void GenerateReport(IReadOnlyCollection<BenchmarkResult> results)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("No results to report.");
            return;
        }

        var sb = new StringBuilder();
        var groupedResults = results.GroupBy(r => new
        {
            ClientCount = r.Parameters.PublisherCount,
            SubscriberCount = r.Parameters.SubscriberCount,
            r.Parameters.MessageCount,
            r.Parameters.MessageSize,
            r.Parameters.Qos,
            r.Parameters.Retain,
            r.Parameters.MqttVersion
        }).ToList();

        sb.AppendLine("# MQTT Benchmark Results");

        // Machine Information
        sb.AppendLine("## Test Machine Information");
        sb.AppendLine(SystemInfo.GetSystemInfo());
        sb.AppendLine();
    
        // Rank brokers based on their performance scores
        var rankedBrokers = results
            .Where(r => r.IsSuccess)
            .GroupBy(r => r.BrokerName)
            .Select(g => new
            {
                BrokerName = g.Key,
                AverageScore = g.Average(r => r.PerformanceScore)
            })
            .OrderByDescending(x => x.AverageScore)
            .ToList();

        // Display ranked brokers in the report
        sb.AppendLine("## Broker Performance Ranking");
        sb.AppendLine("| Rank | Broker | Average Performance Score |");
        sb.AppendLine("|---|---|---|");
        for (int i = 0; i < rankedBrokers.Count; i++)
        {
            sb.AppendLine($"| {i + 1} | {rankedBrokers[i].BrokerName} | {rankedBrokers[i].AverageScore:N2} |");
        }
        sb.AppendLine();

        foreach (var group in groupedResults)
        {
            sb.AppendLine($"## Protocol Version: {group.Key.MqttVersion}," +
                          $" Publishers: {group.Key.ClientCount}," +
                          $" Subscribers: {group.Key.SubscriberCount}," +
                          $" Messages: {group.Key.MessageCount}," +
                          $" Size: {group.Key.MessageSize} bytes, " +
                          $"QoS: {group.Key.Qos}, Retain: {group.Key.Retain}");
            sb.AppendLine();

            var successfulBrokers = group.Where(r => r.IsSuccess).Select(r => r.BrokerName).Distinct().ToList();
            var failedBrokers = group.Where(r => !r.IsSuccess).Select(r => r.BrokerName).Distinct().ToList();

            if (successfulBrokers.Count > 0)
            {
                sb.AppendLine("| Metric | " + string.Join(" | ", successfulBrokers) + " |");
                sb.AppendLine("|--------|" + string.Join("|", successfulBrokers.Select(_ => "--------")) + "|");

                var metrics = new[]
                {
                    "Performance Score",
                    "Message Throughput (msg/s)",
                    "Message Reception Rate (msg/s)",
                    "Message Delivery Success Rate",
                    "Average Latency (ms)",
                    "Total Data Transferred",
                    "Out of Order Messages",
                    "Reconnections",
                    "Messages Sent",
                    "Messages Received",
                    "Connection Time (s)",
                    "Disconnection Time (s)",
                    "CPU Utilization (%)",
                    "Memory Consumption (MB)",
                    "Message Loss Rate",
                    "Total Elapsed Time (s)",
                    "Subscribers",
                    "Messages per Subscriber"
                };

                foreach (var metricName in metrics)
                {
                    var metricValues = new List<string> { metricName };
                    metricValues.AddRange(successfulBrokers.Select(broker => 
                        group.First(r => r.BrokerName == broker && r.IsSuccess).FormatMetric(metricName)));
                    sb.AppendLine("| " + string.Join(" | ", metricValues) + " |");
                }

                sb.AppendLine();
            }

            if (failedBrokers.Count > 0)
            {
                sb.AppendLine("<span style=\"color: red;\">**Benchmark failures were encountered for the following brokers:**</span>");

                foreach (var failedBroker in failedBrokers)
                {
                    var brokerResult = group.First(r => r.BrokerName == failedBroker && !r.IsSuccess);
                    sb.AppendLine($"- **{failedBroker}:** {brokerResult.ErrorMessage}");
                    if (!string.IsNullOrEmpty(brokerResult.LastError))
                    {
                        sb.AppendLine($"  ```");
                        sb.AppendLine($"  {brokerResult.LastError.Trim()}");
                        sb.AppendLine($"  ```");
                    }
                }

                sb.AppendLine();
            }
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var reportFilename = $"MQTT_Benchmark_Results_{timestamp}.md";
        var reportFullPath = Path.Combine(Directory.GetCurrentDirectory(), reportFilename);

        File.WriteAllText(reportFilename, sb.ToString());

        Console.WriteLine("Benchmark completed.");
        Console.WriteLine($"Report generated: {reportFullPath}");
    }
}
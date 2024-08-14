namespace Proxus_MQTT_Bench.Benchmarks
{
    public static class BenchmarkRunner
    {

        public static List<BenchmarkParameters> GenerateBenchmarkParameters(IEnumerable<BrokerInfo> brokers, string serverUri,
            bool[] cleanSessions, string[] mqttVersions, int[] publisherCounts, int[] subscriberCounts,
            int[] messageCounts, int[] messageSizes, int[] qosLevels, bool[] retainFlags,
            bool benchmarkMqttVersions, bool benchmarkPublishersCounts, bool benchmarkSubscriberCounts,
            bool benchmarkMessageCounts, bool benchmarkMessageSizes, bool benchmarkQosLevels,
            bool benchmarkRetainFlags, bool benchmarkCleanSession)
        {
            return (from broker in brokers
                from cleanSession in benchmarkCleanSession ? cleanSessions : [false]
                from mqttVersion in benchmarkMqttVersions ? mqttVersions : ["v311"]
                from publisherCount in benchmarkPublishersCounts ? publisherCounts : [10]
                from messageCount in benchmarkMessageCounts ? messageCounts : [1000]
                from messageSize in benchmarkMessageSizes ? messageSizes : [64]
                from qos in benchmarkQosLevels ? qosLevels : [1]
                from retain in benchmarkRetainFlags ? retainFlags : [false]
                from subscriberCount in benchmarkSubscriberCounts ? subscriberCounts : [0]
                select new BenchmarkParameters
                {
                    BrokerName = broker.Name,
                    ServerUri = serverUri,
                    Port = broker.Port,
                    CleanSession = cleanSession,
                    Username = null,
                    Password = null,
                    KeepAlivePeriod = TimeSpan.FromSeconds(60),
                    ConnectionTimeOut = TimeSpan.FromSeconds(60),
                    MqttVersion = mqttVersion.ToLowerInvariant() switch
                    {
                        "v310" => MqttProtocolVersion.V310,
                        "v311" => MqttProtocolVersion.V311,
                        "v500" => MqttProtocolVersion.V500,
                        _ => throw new ArgumentException($"Invalid MQTT version '{mqttVersion}'.")
                    },
                    PublisherCount = publisherCount,
                    MessageCount = messageCount,
                    MessageSize = messageSize,
                    Qos = qos,
                    Retain = retain,
                    SubscriberCount = subscriberCount
                }).ToList();
        }

        public static async Task<BenchmarkResult> RunBenchmarkAsync(BenchmarkParameters parameters)
        {
            try
            {
                await DockerServiceManager.StartMonitoringAsync(parameters.BrokerName);

                var clients = await InitializeClientsAsync(parameters);

                var elapsedConnect = await ConnectClientsAsync(clients, parameters);

                var messages = GenerateMessages(parameters);

                var (messagesSent, totalMessagesReceived, elapsed, processingTime, latencies, outOfOrderMessages,
                        reconnections) =
                    await SendAndReceiveMessagesAsync(clients, messages, parameters);

                var elapsedDisconnect = await DisconnectClientsAsync(clients);

                var dataSize = CalculateDataSize(parameters);

                var throughput = messagesSent / processingTime.TotalSeconds;
                var messageReceiveRate = totalMessagesReceived / processingTime.TotalSeconds;

                var averageLatency = latencies.Any() ? latencies.Average() : 0;

                var (cpuUsage, memoryUsage, lastError) = await DockerServiceManager.StopMonitoringAsync();

                return CreateBenchmarkResult(parameters, elapsedConnect, elapsed, messagesSent, totalMessagesReceived,
                    averageLatency, elapsedDisconnect, dataSize, throughput, messageReceiveRate, cpuUsage,
                    memoryUsage, lastError, outOfOrderMessages, reconnections);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Benchmark failed: {ex.Message}");
                return BenchmarkResult.CreateFailedResult(parameters, ex.Message);
            }
        }

        private static Task<List<IMqttClient>> InitializeClientsAsync(BenchmarkParameters parameters)
        {
            var factory = new MqttFactory();
            return Task.FromResult(Enumerable.Range(0, parameters.PublisherCount + parameters.SubscriberCount)
                .Select(_ => factory.CreateMqttClient()).ToList());
        }

        private static async Task<TimeSpan> ConnectClientsAsync(List<IMqttClient> clients,
            BenchmarkParameters parameters)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                var connectTasks = clients.Select(c =>
                    MqttClientHelper.ConnectWithRetryAsync(c, MqttClientHelper.CreateMqttClientOptions(parameters)));
                await Task.WhenAll(connectTasks);
            }
            catch (Exception ex)
            {
                throw new Exception($"Connection failed: {ex.Message}");
            }

            var endTimestamp = Stopwatch.GetTimestamp();
            return TimeSpan.FromSeconds((endTimestamp - startTimestamp) / (double)Stopwatch.Frequency);
        }

        private static List<MqttApplicationMessage> GenerateMessages(BenchmarkParameters parameters)
        {
            return Enumerable.Range(1, parameters.MessageCount)
                .Select(i => new MqttApplicationMessageBuilder()
                    .WithTopic($"test/{i}")
                    .WithPayload(new byte[parameters.MessageSize])
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)parameters.Qos)
                    .WithRetainFlag(parameters.Retain)
                    .Build())
                .ToList();
        }

        private static async Task<(int messagesSent, int totalMessagesReceived, TimeSpan elapsed, TimeSpan
                processingTime,
                List<double> latencies, int outOfOrderMessages, int reconnections)>
            SendAndReceiveMessagesAsync(List<IMqttClient> clients, List<MqttApplicationMessage> messages,
                BenchmarkParameters parameters)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            var totalMessagesSent = parameters.PublisherCount * parameters.MessageCount;
            var sentCounters = new ConcurrentDictionary<int, int>();
            var receivedCounters = new ConcurrentDictionary<int, int>();
            var progressReport = 0;
            var processingStarted = new ManualResetEventSlim(false);
            var allMessagesReceived = new ManualResetEventSlim(false);

            var receivedMessagesPerClient =
                new ConcurrentDictionary<int, ConcurrentBag<MqttApplicationMessageReceivedEventArgs>>();
            var latencies = new ConcurrentBag<double>();
            var outOfOrderMessages = 0;
            var reconnections = 0;

            // Set up subscribers
            for (int i = 0; i < parameters.SubscriberCount; i++)
            {
                var subscriberIndex = i;
                receivedMessagesPerClient[subscriberIndex] =
                    [];

                await clients[subscriberIndex].SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("test/#").Build());

                var lastMessageId = -1;
                clients[subscriberIndex].ApplicationMessageReceivedAsync += e =>
                {
                    receivedCounters.AddOrUpdate(subscriberIndex, 1, (_, count) => count + 1);
                    receivedMessagesPerClient[subscriberIndex].Add(e);

                    var messageData = e.ApplicationMessage.Payload;
                    var sendTimestamp = BitConverter.ToInt64(messageData, 0);
                    var messageId = BitConverter.ToInt32(messageData, 8);
                    var receiveTimestamp = Stopwatch.GetTimestamp();
                    var latency = (receiveTimestamp - sendTimestamp) / (double)Stopwatch.Frequency * 1000;
                    latencies.Add(latency);

                    if (messageId <= lastMessageId)
                    {
                        Interlocked.Increment(ref outOfOrderMessages);
                    }

                    lastMessageId = messageId;

                    if (receivedCounters.Values.Sum() >= totalMessagesSent)
                    {
                        allMessagesReceived.Set();
                    }

                    return Task.CompletedTask;
                };

                clients[subscriberIndex].DisconnectedAsync += _ =>
                {
                    Interlocked.Increment(ref reconnections);
                    return Task.CompletedTask;
                };
            }

            // Set up publishers and start sending messages
            var sendTasks = clients.Skip(parameters.SubscriberCount).Select((client, index) => Task.Run(async () =>
            {
                var results = new List<MqttClientPublishResult>();

                processingStarted.Wait();

                for (int i = 0; i < messages.Count; i++)
                {
                    if (!client.IsConnected)
                    {
                        await client.ConnectAsync(MqttClientHelper.CreateMqttClientOptions(parameters));
                        Interlocked.Increment(ref reconnections);
                    }

                    try
                    {
                        var messageWithMetadata = new MqttApplicationMessageBuilder()
                            .WithTopic(messages[i].Topic)
                            .WithPayload(BitConverter.GetBytes(Stopwatch.GetTimestamp())
                                .Concat(BitConverter.GetBytes(i))
                                .Concat(messages[i].Payload)
                                .ToArray())
                            .WithQualityOfServiceLevel(messages[i].QualityOfServiceLevel)
                            .WithRetainFlag(messages[i].Retain)
                            .Build();

                        var result = await client.PublishAsync(messageWithMetadata);
                        results.Add(result);
                        sentCounters.AddOrUpdate(index, 1, (_, count) => count + 1);

                        var newProgressReport = sentCounters.Values.Sum() * 100 / totalMessagesSent;
                        if (newProgressReport >= progressReport + 10)
                        {
                            progressReport = newProgressReport / 10 * 10;
                            Console.Write($"\rProgress: {progressReport}%");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"\nAn error occurred: {ex.Message}");
                        throw;
                    }
                }

                return results;
            })).ToList();

            processingStarted.Set();
            var processingStartTimestamp = Stopwatch.GetTimestamp();
            var sendResults = await Task.WhenAll(sendTasks);

            if (parameters.SubscriberCount > 0)
            {
                allMessagesReceived.Wait();
            }

            var processingEndTimestamp = Stopwatch.GetTimestamp();

            var endTimestamp = Stopwatch.GetTimestamp();
            var elapsed = TimeSpan.FromSeconds((endTimestamp - startTimestamp) / (double)Stopwatch.Frequency);

            var totalMessagesReceived = receivedCounters.Values.Sum();
            var messagesSent = sendResults.Sum(r => r.Count);

            var processingTime =
                TimeSpan.FromSeconds((processingEndTimestamp - processingStartTimestamp) / (double)Stopwatch.Frequency);

            Console.WriteLine("\rProgress: 100%");

            return (messagesSent, totalMessagesReceived, elapsed, processingTime, latencies.ToList(),
                outOfOrderMessages, reconnections);
        }

        private static async Task<TimeSpan> DisconnectClientsAsync(List<IMqttClient> clients)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                var disconnectTasks = clients.Select(c => c.DisconnectAsync());
                await Task.WhenAll(disconnectTasks);
            }
            catch (Exception ex)
            {
                throw new Exception($"Disconnection failed: {ex.Message}");
            }

            var endTimestamp = Stopwatch.GetTimestamp();
            return TimeSpan.FromSeconds((endTimestamp - startTimestamp) / (double)Stopwatch.Frequency);
        }

        private static double CalculateDataSize(BenchmarkParameters parameters)
        {
            return parameters.PublisherCount * parameters.MessageCount *
                   (parameters.MessageSize + 12); // Add 12 bytes for metadata
        }

        private static BenchmarkResult CreateBenchmarkResult(BenchmarkParameters parameters, TimeSpan elapsedConnect,
            TimeSpan elapsed, int messagesSent, int totalMessagesReceived, double averageLatency,
            TimeSpan elapsedDisconnect, double dataSize, double throughput,
            double messageReceiveRate, double cpuUsage, double memoryUsage, string lastError,
            int outOfOrderMessages, int reconnections)
        {
            double successRate, lossRate;
            if (parameters.Qos == 0)
            {
                lossRate = messagesSent > 0 ? Math.Max(0, 1.0 - (double)totalMessagesReceived / messagesSent) : 0;
                successRate = 1.0 - lossRate;
            }
            else
            {
                var expectedMessagesReceived = parameters.SubscriberCount * parameters.MessageCount * parameters.PublisherCount;
                lossRate = expectedMessagesReceived > 0
                    ? Math.Max(0, (double)(expectedMessagesReceived - totalMessagesReceived) / expectedMessagesReceived)
                    : 0;
                successRate = 1.0 - lossRate;
            }


            return new BenchmarkResult
            {
                BrokerName = parameters.BrokerName,
                Parameters = parameters,
                IsSuccess = true,
                ConnectionTime = elapsedConnect,
                TotalElapsedTime = elapsed,
                MessageThroughput = throughput,
                AverageLatency = TimeSpan.FromMilliseconds(averageLatency),
                DisconnectionTime = elapsedDisconnect,
                MessageDeliverySuccessRate = successRate,
                MessageLossRate =  lossRate,
                TotalDataTransferred = FormatDataSizeAndUnit(dataSize).Item1,
                DataSizeUnit = FormatDataSizeAndUnit(dataSize).Item2,
                MessageReceptionRate = messageReceiveRate,
                TotalMessagesReceived = totalMessagesReceived,
                TotalMessagesSent = messagesSent,
                AverageCpuUtilization = cpuUsage,
                AverageMemoryConsumption = memoryUsage,
                LastError = lastError,
                OutOfOrderMessages = outOfOrderMessages,
                Reconnections = reconnections,
                PerformanceScore = CalculatePerformanceScore(throughput, messageReceiveRate, averageLatency,successRate,  lossRate,
                    cpuUsage, memoryUsage, outOfOrderMessages, reconnections)
            };
        }

        private static double CalculatePerformanceScore(double throughput, double messageReceiveRate,
            double averageLatency, double successRate, double lossRate, double outOfOrderMessages, double reconnections,
            double cpuUsage, double memoryUsage)
        {
            // Normalize values to a 0-1 scale
            var normalizedThroughput = Math.Min(throughput / 10000, 1);
            var normalizedMessageReceiveRate = Math.Min(messageReceiveRate / 10000, 1);
            var normalizedLatency = Math.Max(0, 1 - (averageLatency / 1000));
            var normalizedSuccessRate = successRate;
            var normalizedLossRate = 1 - lossRate;
            var normalizedOutOfOrderMessages = Math.Max(0, 1 - (outOfOrderMessages / 1000.0));
            var normalizedReconnections = Math.Max(0, 1 - (reconnections / 100.0));
            var normalizedCpuUsage = 1 - (cpuUsage / 100);
            var normalizedMemoryUsage = 1 - (memoryUsage / 100);

            // Assign weights to each factor
            var weights = new Dictionary<string, double>
            {
                {"Throughput", 0.2},
                {"MessageReceiveRate", 0.2},
                {"Latency", 0.15},
                {"SuccessRate", 0.15},
                {"LossRate", 0.1},
                {"OutOfOrderMessages", 0.05},
                {"Reconnections", 0.05},
                // {"CpuUsage", 0.05},
                // {"MemoryUsage", 0.05}
            };

            // Calculate weighted sum
            var score = (normalizedThroughput * weights["Throughput"]) +
                        (normalizedMessageReceiveRate * weights["MessageReceiveRate"]) +
                        (normalizedLatency * weights["Latency"]) +
                        (normalizedSuccessRate * weights["SuccessRate"]) +
                        (normalizedLossRate * weights["LossRate"]) +
                        (normalizedOutOfOrderMessages * weights["OutOfOrderMessages"]) +
                        (normalizedReconnections * weights["Reconnections"]) 
                        /*+ (normalizedCpuUsage * weights["CpuUsage"]) +
                        (normalizedMemoryUsage * weights["MemoryUsage"])*/;

            return Math.Round(score * 100, 2);
        }

        private static (double, string) FormatDataSizeAndUnit(double bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
            int unitIndex = 0;
            while (bytes >= 1024 && unitIndex < units.Length - 1)
            {
                bytes /= 1024;
                unitIndex++;
            }

            return (Math.Round(bytes, 2), units[unitIndex]);
        }
    }
}
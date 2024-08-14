namespace Proxus_MQTT_Bench.Infrastructure
{
    /// <summary>
    /// Manages Docker services for the MQTT benchmark application.
    /// </summary>
    public static class DockerServiceManager
    {
        private static CancellationTokenSource? _cancellationTokenSource;
        private static List<double> _cpuUsageList = [];
        private static List<double> _memoryUsageList = [];
        private static string _lastError = "";
        private static readonly DockerClient DockerClient;
        private const string ProjectName = "proxus_mqtt_bench";

        static DockerServiceManager()
        {
            DockerClient = new DockerClientConfiguration().CreateClient();
        }

        /// <summary>
        /// Starts a Docker service asynchronously using docker-compose.
        /// </summary>
        /// <param name="serviceName">The name of the service to start.</param>
        /// <param name="dockerComposeFilePath">The path to the docker-compose file.</param>
        public static async Task StartServiceAsync(string serviceName, string dockerComposeFilePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = $"-p {ProjectName} -f {dockerComposeFilePath} up -d {serviceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"Failed to start service {serviceName}. Error: {await process.StandardError.ReadToEndAsync()}");
            }

            await StartMonitoringAsync(serviceName);
        }

        /// <summary>
        /// Stops a Docker service asynchronously using docker-compose.
        /// </summary>
        /// <param name="serviceName">The name of the service to stop.</param>
        /// <param name="dockerComposeFilePath">The path to the docker-compose file.</param>
        public static async Task StopServiceAsync(string serviceName, string dockerComposeFilePath)
        {
            await StopMonitoringAsync();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = $"-p {ProjectName} -f {dockerComposeFilePath} stop {serviceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"Failed to stop service {serviceName}. Error: {await process.StandardError.ReadToEndAsync()}");
            }
        }

        /// <summary>
        /// Starts monitoring a Docker service for resource usage.
        /// </summary>
        /// <param name="serviceName">The name of the service to monitor.</param>
        public static async Task StartMonitoringAsync(string serviceName)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cpuUsageList = [];
            _memoryUsageList = [];
            _lastError = "";

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await GetContainerResourceUsage(serviceName);
                        _lastError = await GetContainerLastError(serviceName);
                    }
                    catch (Exception ex)
                    {
                        _lastError = ex.Message;
                    }

                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Retrieves resource usage statistics for a Docker container.
        /// </summary>
        /// <param name="serviceName">The name of the service to monitor.</param>
        /// TODO: Investigate and fix issues with incorrect results for some containers (https://github.com/dotnet/Docker.DotNet/issues/607)
        private static async Task GetContainerResourceUsage(string serviceName)
        {
            var containerName = $"{ProjectName}-{serviceName}-1";
            await DockerClient.Containers.GetContainerStatsAsync(containerName,
                new ContainerStatsParameters() { Stream = false },
                new Progress<ContainerStatsResponse>(m =>
                {
                    if (m.MemoryStats is { Stats: not null })
                    {
                        ulong usedMemory;
                        if (m.MemoryStats.Stats.TryGetValue("cache", out ulong memusedstatscache))
                            usedMemory = m.MemoryStats.Usage - memusedstatscache;
                        else
                            usedMemory = m.MemoryStats.Usage;

                        // Convert bytes to megabytes
                        double usedMemoryMb = (double)usedMemory / (1024 * 1024);
                        _memoryUsageList.Add(usedMemoryMb);
                    }

                    if (m.CPUStats != null && m.PreCPUStats != null)
                    {
                        if (m.CPUStats.CPUUsage.TotalUsage != null && m.PreCPUStats.CPUUsage.TotalUsage != null &&
                            m.CPUStats.SystemUsage != null && m.PreCPUStats.SystemUsage != null &&
                            m.CPUStats.OnlineCPUs != null)
                        {
                            var cpuDelta = m.CPUStats.CPUUsage.TotalUsage - m.PreCPUStats.CPUUsage.TotalUsage;
                            var systemCpuDelta = m.CPUStats.SystemUsage - m.PreCPUStats.SystemUsage;
                            var numberCpus = m.CPUStats.OnlineCPUs;
                            var cpuUsagePerc = (cpuDelta / (double)systemCpuDelta) * numberCpus * 100.0f;

                            _cpuUsageList.Add(cpuUsagePerc);
                        }
                    }
                }),
                _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Retrieves the last error message from a Docker container's logs.
        /// </summary>
        /// <param name="serviceName">The name of the service to check.</param>
        /// <returns>The last error message from the container's logs.</returns>
        private static async Task<string> GetContainerLastError(string serviceName)
        {
            var containerName = $"{ProjectName}-{serviceName}-1";
            var logOptions = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = "100" // Retrieve only the last 100 lines of logs
            };

            await using var logStream =
                await DockerClient.Containers.GetContainerLogsAsync(containerName, logOptions,
                    _cancellationTokenSource.Token);
            using var reader = new StreamReader(logStream);

            var logs = await reader.ReadToEndAsync();
            var logLines = logs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Find the last error line in the logs
            for (int i = logLines.Length - 1; i >= 0; i--)
            {
                var line = logLines[i];
                if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("fail", StringComparison.OrdinalIgnoreCase))
                {
                    return line;
                }
            }

            return string.Empty; // Return empty string if no errors found
        }

        /// <summary>
        /// Stops monitoring a Docker service and returns the average resource usage.
        /// </summary>
        /// <returns>A tuple containing average CPU usage, average memory usage, and the last error message.</returns>
        public static async Task<(double cpuUsage, double memoryUsage, string lastError)> StopMonitoringAsync()
        {
            if (_cancellationTokenSource != null)
            {
                await _cancellationTokenSource.CancelAsync();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            var averageCpuUsage = _cpuUsageList.Any() ? _cpuUsageList.Average() : 0;
            var averageMemoryUsage = _memoryUsageList.Any() ? _memoryUsageList.Average() : 0;

            return (averageCpuUsage, averageMemoryUsage, _lastError);
        }
    }
}
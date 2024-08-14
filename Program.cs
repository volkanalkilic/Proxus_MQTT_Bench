namespace Proxus_MQTT_Bench;

internal abstract class Program
{
    // File path to the docker-compose.yml file
    private static readonly string DockerComposeFilePath = Path.Combine("config", "docker-compose.yml");
    // Server URI, set to localhost by default
    private static readonly string ServerUri = "localhost";
    // A list to hold the broker information
    private static List<BrokerInfo>? _brokers;

    // Clean session flags for benchmarking
    private static bool[] _cleanSessions = [true, false];
    // MQTT versions for benchmarking
    private static string[] _mqttVersions = ["v310", "v311", "v500"];
    // Publisher counts for benchmarking
    private static int[] _publisherCounts = [10, 100];
    // Subscriber counts for benchmarking
    private static int[] _subscriberCounts = [1, 5];
    // Message counts for benchmarking
    private static int[] _messageCounts = [1_000, 10_000, 100_000];
    // Message sizes for benchmarking
    private static int[] _messageSizes = [64, 256, 1024];
    // QoS levels for benchmarking
    private static int[] _qosLevels = [0, 1, 2];
    // Retain flags for benchmarking
    private static bool[] _retainFlags = [true, false];

    // Flags to indicate which parameters to benchmark
    private static bool _benchmarkMqttVersions = false;
    private static bool _benchmarkPublishersCounts = true;
    private static bool _benchmarkSubscriberCounts = true;
    private static bool _benchmarkMessageCounts = false;
    private static bool _benchmarkMessageSizes = false;
    private static bool _benchmarkQosLevels = false;
    private static bool _benchmarkRetainFlags = false;
    private static bool _benchmarkCleanSession = false;

    private static async Task Main()
    {
        Console.WriteLine("\n\n***************** Proxus MQTT Bench ******************\n\n");

        // Load broker information from the docker-compose file
        _brokers = LoadBrokerInfoFromComposeFile();
        // Display the available brokers to the user
        DisplayBrokers();

        // Get the selected brokers from the user
        var selectedBrokers = GetSelectedBrokers();
        // If no brokers are selected, exit the program
        if (selectedBrokers == null) return;

        // Display the default benchmark parameters to the user
        DisplayDefaultParameters();
        // Display the benchmark flags to the user
        DisplayBenchmarkFlags();

        // Ask the user if they want to change any of the default parameters
        if (ShouldChangeParameters())
        {
            // Update the benchmark flags based on user input
            UpdateBenchmarkFlags();
            // Update the parameter ranges based on user input
            UpdateParameterRanges();
        }

        // Generate the benchmark parameters based on the selected brokers and user input
        var benchmarks = GenerateBenchmarks(selectedBrokers);
        // Run the benchmarks and collect the results
        var results = await RunBenchmarks(selectedBrokers, benchmarks);

        // Generate a report of the benchmark results
        BenchmarkReporter.GenerateReport(results);
    }

    // Displays the available brokers to the user
    private static void DisplayBrokers()
    {
        Console.WriteLine("Available brokers:");
        for (int i = 0; i < _brokers.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {_brokers[i].Name}");
        }
    }

    // Gets the selected brokers from the user
    private static List<BrokerInfo>? GetSelectedBrokers()
    {
        Console.WriteLine(
            "Select brokers to test (enter numbers separated by commas, e.g., 1,2 or 'all' for all brokers):");
        string? input = Console.ReadLine();

        // If the input is invalid, display an error message and exit
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("Invalid input. Exiting.");
            return null;
        }

        // If the user enters "all", select all brokers
        if (input.ToLower() == "all") return _brokers;

        try
        {
            // Parse the user input and select the corresponding brokers
            var selectedIndices = input.Split(',').Select(x => int.Parse(x.Trim()) - 1).ToList();
            return selectedIndices.Where(i => i >= 0 && i < _brokers.Count).Select(i => _brokers[i]).ToList();
        }
        catch (FormatException)
        {
            // If the input is invalid, display an error message and exit
            Console.WriteLine("Invalid input. Please enter numbers separated by commas.");
            return null;
        }
    }

    // Displays the default benchmark parameters to the user
    private static void DisplayDefaultParameters()
    {
        Console.WriteLine("\nDefault benchmark parameters:");
        Console.WriteLine($"MQTT Versions: {string.Join(", ", _mqttVersions)}");
        Console.WriteLine($"Publisher Counts: {string.Join(", ", _publisherCounts)}");
        Console.WriteLine($"Subscriber Counts: {string.Join(", ", _subscriberCounts)}");
        Console.WriteLine($"Message Counts: {string.Join(", ", _messageCounts)}");
        Console.WriteLine($"Message Sizes: {string.Join(", ", _messageSizes)}");
        Console.WriteLine($"QoS Levels: {string.Join(", ", _qosLevels)}");
        Console.WriteLine($"Retain Flags: {string.Join(", ", _retainFlags)}");
        Console.WriteLine($"Clean Sessions: {string.Join(", ", _cleanSessions)}");
    }

    // Displays the benchmark flags to the user
    private static void DisplayBenchmarkFlags()
    {
        Console.WriteLine("\nBenchmark Flags:");
        Console.WriteLine($"Benchmark MQTT Versions: {_benchmarkMqttVersions} (y/n)");
        Console.WriteLine($"Benchmark Publisher Counts: {_benchmarkPublishersCounts} (y/n)");
        Console.WriteLine($"Benchmark Subscriber Counts: {_benchmarkSubscriberCounts} (y/n)");
        Console.WriteLine($"Benchmark Message Counts: {_benchmarkMessageCounts} (y/n)");
        Console.WriteLine($"Benchmark Message Sizes: {_benchmarkMessageSizes} (y/n)");
        Console.WriteLine($"Benchmark QoS Levels: {_benchmarkQosLevels} (y/n)");
        Console.WriteLine($"Benchmark Retain Flags: {_benchmarkRetainFlags} (y/n)");
        Console.WriteLine($"Benchmark Clean Session: {_benchmarkCleanSession} (y/n)");
    }

    // Asks the user if they want to change any of the default parameters
    private static bool ShouldChangeParameters()
    {
        Console.WriteLine("\nDo you want to change any of the default parameters? (y/n)");
        return Console.ReadLine()?.ToLower() == "y";
    }

    // Updates the benchmark flags based on user input
    private static void UpdateBenchmarkFlags()
    {
        _benchmarkMqttVersions = GetYesNoInput("Benchmark MQTT Versions");
        _benchmarkPublishersCounts = GetYesNoInput("Benchmark Publisher Counts");
        _benchmarkSubscriberCounts = GetYesNoInput("Benchmark Subscriber Counts");
        _benchmarkMessageCounts = GetYesNoInput("Benchmark Message Counts");
        _benchmarkMessageSizes = GetYesNoInput("Benchmark Message Sizes");
        _benchmarkQosLevels = GetYesNoInput("Benchmark QoS Levels");
        _benchmarkRetainFlags = GetYesNoInput("Benchmark Retain Flags");
        _benchmarkCleanSession = GetYesNoInput("Benchmark Clean Session");
    }

    // Gets a yes/no input from the user
    private static bool GetYesNoInput(string prompt)
    {
        Console.WriteLine($"{prompt} (y/n)?");
        return Console.ReadLine()?.ToLower() == "y";
    }

    // Updates the parameter ranges based on user input
    private static void UpdateParameterRanges()
    {
        if (_benchmarkMqttVersions)
            _mqttVersions = GetArrayInput("Enter MQTT versions to test (comma-separated, e.g., v310,v311,v500):",
                _mqttVersions);

        if (_benchmarkPublishersCounts)
            _publisherCounts = GetArrayInput("Enter publisher counts to test (comma-separated, e.g., 10,20,50):",
                _publisherCounts);

        if (_benchmarkSubscriberCounts)
            _subscriberCounts = GetArrayInput("Enter subscriber counts to test (comma-separated, e.g., 1,5,10):",
                _subscriberCounts);

        if (_benchmarkMessageCounts)
            _messageCounts = GetArrayInput("Enter message counts to test (comma-separated, e.g., 1000,10000,100000):",
                _messageCounts);

        if (_benchmarkMessageSizes)
            _messageSizes = GetArrayInput("Enter message sizes to test (comma-separated, e.g., 64,256,1024):",
                _messageSizes);

        if (_benchmarkQosLevels)
            _qosLevels = GetArrayInput("Enter QoS levels to test (comma-separated, e.g., 0,1,2):", _qosLevels);

        if (_benchmarkRetainFlags)
            _retainFlags = GetArrayInput("Enter retain flags to test (comma-separated, e.g., true,false):",
                _retainFlags);

        if (_benchmarkCleanSession)
            _cleanSessions = GetArrayInput("Enter clean session flags to test (comma-separated, e.g., true,false):",
                _cleanSessions);
    }

    // Gets an array of values from the user
    private static T[] GetArrayInput<T>(string prompt, T[] defaultValues)
    {
        Console.WriteLine($"{prompt} (Current values: {string.Join(", ", defaultValues)})");
        Console.WriteLine("Press Enter to keep current values, or enter new values:");
        string? input = Console.ReadLine();

        // If the user presses Enter, return the default values
        if (string.IsNullOrWhiteSpace(input))
            return defaultValues;

        try
        {
            // Parse the user input and return the array of values
            if (typeof(T) == typeof(string))
                return input.Split(',').Select(x => x.Trim()).Cast<T>().ToArray();
            else if (typeof(T) == typeof(int))
                return input.Split(',').Select(x => (T)(object)int.Parse(x.Trim())).ToArray();
            else if (typeof(T) == typeof(bool))
                return input.Split(',').Select(x => (T)(object)bool.Parse(x.Trim())).ToArray();
            else
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
        }
        catch (Exception ex)
        {
            // If the input is invalid, display an error message and return the default values
            Console.WriteLine($"Invalid input. Using default values. Error: {ex.Message}");
            return defaultValues;
        }
    }

    // Generates the benchmark parameters based on the selected brokers and user input
    private static List<BenchmarkParameters> GenerateBenchmarks(List<BrokerInfo> selectedBrokers)
    {
        return BenchmarkRunner.GenerateBenchmarkParameters(selectedBrokers, ServerUri,
            _cleanSessions, _mqttVersions, _publisherCounts, _subscriberCounts, _messageCounts, _messageSizes,
            _qosLevels,
            _retainFlags, _benchmarkMqttVersions, _benchmarkPublishersCounts, _benchmarkSubscriberCounts,
            _benchmarkMessageCounts, _benchmarkMessageSizes, _benchmarkQosLevels, _benchmarkRetainFlags,
            _benchmarkCleanSession);
    }

    // Runs the benchmarks and collects the results
    private static async Task<List<BenchmarkResult>> RunBenchmarks(List<BrokerInfo> selectedBrokers,
        List<BenchmarkParameters> benchmarks)
    {
        var results = new List<BenchmarkResult>();

        // Iterate over the selected brokers
        foreach (var broker in selectedBrokers)
        {
            try
            {
                // Start the broker service
                LogHelper.LogInformation($"Initializing benchmark for broker: {broker.Name}");
                await DockerServiceManager.StartServiceAsync(broker.Name, DockerComposeFilePath);

                // Get the benchmarks for the current broker
                var brokerBenchmarks = benchmarks.Where(b => b.BrokerName == broker.Name);
                // Iterate over the benchmarks for the current broker
                foreach (var benchmark in brokerBenchmarks)
                {
                    try
                    {
                        // Run the benchmark and collect the result
                        LogHelper.LogInformation(
                            $"Running benchmark for {benchmark.BrokerName} on {benchmark.ServerUri}:{benchmark.Port} with {benchmark.MqttVersion}, {benchmark.PublisherCount} publishers, {benchmark.SubscriberCount} subscribers, {benchmark.MessageCount} messages ({benchmark.MessageSize} bytes), QoS {benchmark.Qos}, Retain: {benchmark.Retain}");
                        var result = await BenchmarkRunner.RunBenchmarkAsync(benchmark);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        // If the benchmark fails, log the error and add a failed result to the list
                        LogHelper.LogError($"Benchmark failed for {benchmark.BrokerName}: {ex.Message}");
                        results.Add(BenchmarkResult.CreateFailedResult(benchmark, ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                // If an error occurs, log the error
                LogHelper.LogError($"An error occurred: {ex.Message}");
            }
            finally
            {
                // Stop the broker service
                await DockerServiceManager.StopServiceAsync(broker.Name, DockerComposeFilePath);
                Console.WriteLine();
            }
        }

        // Return the list of benchmark results
        return results;
    }

    // Loads the broker information from the docker-compose file
    private static List<BrokerInfo>? LoadBrokerInfoFromComposeFile()
    {
        var brokers = new List<BrokerInfo>();
        // Open the docker-compose file
        using var reader = new StreamReader(DockerComposeFilePath);
        var yaml = new YamlStream();
        yaml.Load(reader);

        // Parse the YAML file and extract the broker information
        var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
        var services = (YamlMappingNode)rootNode.Children[new YamlScalarNode("services")];

        // Iterate over the services in the docker-compose file
        foreach (var service in services.Children)
        {
            // Get the broker name and port
            var brokerName = service.Key.ToString();
            var serviceDetails = (YamlMappingNode)service.Value;
            var portsNode = (YamlSequenceNode)serviceDetails.Children[new YamlScalarNode("ports")];
            var portMapping = portsNode.Children[0].ToString();
            var hostPort = int.Parse(portMapping.Split(':')[0]);

            // Add the broker information to the list
            brokers.Add(new BrokerInfo { Name = brokerName, ServerUri = ServerUri, Port = hostPort });
        }

        // Return the list of broker information
        return brokers;
    }
}
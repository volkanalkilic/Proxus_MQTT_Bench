Proxus MQTT Bench: A Comprehensive MQTT Broker Benchmarking Tool
----------------------------------------------------------------

**Proxus MQTT Bench** is a powerful and versatile benchmarking tool designed to evaluate the performance of various MQTT brokers. It leverages Docker to run and manage a single broker instance at a time, allowing for a comprehensive and controlled testing environment.

**Story:**

This tool was initially developed to test the embedded MQTT broker used in the **Proxus IIoT Platform** ([https://www.proxus.io](https://www.proxus.io/)). Recognizing its potential for broader use, I decided to expand its capabilities to benchmark other popular MQTT brokers.

**Disclaimer:**

This tool is designed to provide a standardized and repeatable benchmarking process. However, it's important to note that I'm not an expert in optimizing broker performance for each specific implementation, nor am I a seasoned software developer. The benchmark results may not reflect the absolute maximum performance achievable, and the code itself might benefit from community contributions.

**Community Support:**

To ensure the most accurate and reliable results, I encourage community involvement in two key areas:

1. **Broker Configuration:** Share your expertise on best practices for configuring and optimizing the performance of each broker. Your contributions will significantly enhance the accuracy and value of this tool.
2. **Code Improvement:** If you have experience with .NET development, feel free to contribute to the codebase. Your insights can improve the tool's functionality, efficiency, and overall performance.

### Features

- **Multi-Broker Support:** Benchmarks multiple popular MQTT brokers including EMQX, HiveMQ, Mosquitto, VerneMQ, VolantMQ, ActiveMQ, and RabbitMQ.
- **Docker Integration:** Runs and manages a broker instance within a Docker container for a consistent and isolated testing environment.
- **Configurable Parameters:** Allows customization of benchmark parameters such as:
  - MQTT protocol version (v3.1.0, v3.1.1, v5.0.0)
  - Publisher count
  - Subscriber count
  - Message count
  - Message size
  - QoS level
  - Retain flag
  - Clean session flag
- **Performance Metrics:** Collects and reports detailed performance metrics:
  - Message throughput
  - Average latency
  - Message delivery success rate
  - Message loss rate
  - Total data transferred
  - CPU utilization
  - Memory consumption
  - Out-of-order messages
  - Reconnections
- **Performance Score:** Calculates a weighted performance score based on the collected metrics.
- **Detailed Report Generation:** Generates a comprehensive Markdown report summarizing the benchmark results, including:
  - Test machine information
  - Broker performance ranking
  - Detailed performance metrics for each broker
  - Benchmark failure details
- **User-Friendly Interface:** Provides a simple command-line interface for configuring and running benchmarks.

### Usage

1. **Prerequisites:**

   - .NET 8.0 SDK
   - Docker
2. **Clone the repository:**

   ```
   git clone https://github.com/volkanalkilic/Proxus_MQTT_Bench.git
   ```

   3. **Navigate to the project directory**
   4. **Build the project:**

      ```
      dotnet build
      ```
   5. **Run the benchmark:**

      ```
      dotnet run
      ```
   6. **Follow the on-screen prompts to select brokers and configure benchmark parameters.**
   7. **The benchmark will run and generate a Markdown report in the current directory.**

### Configuration

- **Docker-Compose File:** The `docker-compose.yml` file in the `config` directory defines the Docker services for each broker. You can modify this file to customize the broker configurations, ports, and environment variables.
- **Broker Configuration Files:** The `config` directory also includes configuration files for some brokers (e.g., `emqx.conf`, `mosquitto.conf`). You can modify these files to customize the broker settings.

### Contributing

Contributions are welcome! Please submit pull requests or issues on the GitHub repository. Your expertise on broker configuration, optimization, and .NET development is invaluable.

### License

Proxus MQTT Bench is licensed under the MIT License.

### Acknowledgements

This project is built upon the following libraries:

- **Docker.DotNet:** For interacting with the Docker API.
- **MQTTnet:** For MQTT client operations.
- **System.Management:** For retrieving system information on Windows.
- **YamlDotNet:** For parsing YAML files.

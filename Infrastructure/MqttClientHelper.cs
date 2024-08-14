namespace Proxus_MQTT_Bench.Infrastructure
{
    /// <summary>
    /// Helper class for MQTT client operations.
    /// </summary>
    public static class MqttClientHelper
    {
        /// <summary>
        /// Attempts to connect an MQTT client with retry logic, ensuring authentication is successful.
        /// </summary>
        /// <param name="client">The MQTT client to connect.</param>
        /// <param name="options">The MQTT client options.</param>
        /// <param name="maxRetries">Maximum number of connection attempts. Default is 10.</param>
        /// <param name="delayMilliseconds">Delay between retries in milliseconds. Default is 1000ms.</param>
        /// <returns>The result of the MQTT client connection.</returns>
        /// <exception cref="InvalidOperationException">Thrown if unable to connect after all retries.</exception>
        public static async Task<MqttClientConnectResult> ConnectWithRetryAsync(IMqttClient client,
            MqttClientOptions options, int maxRetries = 20, int delayMilliseconds = 1000)
        {
            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Connect to the broker
                    var connectResult = await client.ConnectAsync(options);

                    // Check if connection is successful and authentication is successful
                    if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
                    {
                        return connectResult;
                    }
                }
                catch (Exception)
                {
                    // Connection failed, retry
                }

                await Task.Delay(delayMilliseconds);
            }

            throw new InvalidOperationException("Unable to connect to broker and authenticate after multiple attempts.");
        }

        /// <summary>
        /// Creates MQTT client options based on the provided benchmark parameters.
        /// </summary>
        /// <param name="parameters">The benchmark parameters to use for creating the options.</param>
        /// <returns>The created MQTT client options.</returns>
        public static MqttClientOptions CreateMqttClientOptions(BenchmarkParameters parameters)
        {
            return new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer(parameters.ServerUri, parameters.Port)
                .WithCleanSession(parameters.CleanSession)
                .WithCredentials(parameters.Username, parameters.Password)
                .WithKeepAlivePeriod(parameters.KeepAlivePeriod)
                .WithTimeout(parameters.ConnectionTimeOut)
                .WithProtocolVersion(parameters.MqttVersion)
                .Build();
        }
    }
}
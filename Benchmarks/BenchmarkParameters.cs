namespace Proxus_MQTT_Bench.Benchmarks;

public record BenchmarkParameters
{
    public required string? BrokerName { get; init; }
    public required string ServerUri { get; init; }
    public int Port { get; init; }
    public bool CleanSession { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public TimeSpan KeepAlivePeriod { get; init; }
    public TimeSpan ConnectionTimeOut { get; init; }
    public MqttProtocolVersion MqttVersion { get; init; }
    public int PublisherCount { get; init; }
    public int SubscriberCount { get; set; }
    public int MessageCount { get; init; }
    public int MessageSize { get; init; }
    public int Qos { get; init; }
    public bool Retain { get; init; }
}
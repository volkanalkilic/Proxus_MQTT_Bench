namespace Proxus_MQTT_Bench.Infrastructure;

public record BrokerInfo
{
    public string? Name { get; init; }
    public string? ServerUri { get; init; }
    public int Port { get; init; }
}
using RegisterRabbitMq;
using Xunit;

namespace RegisterRabbitMq;

/// <summary>
/// Testes unitários de <see cref="AsyncApiChannelReader"/> (sem rede, sem
/// arquivos do repositório — o fixture inline espelha a forma real de
/// <c>contracts/asyncapi/diagnostics-v1.yaml</c> para não acoplar o teste ao
/// layout do repo).
/// </summary>
public sealed class AsyncApiChannelReaderTests
{
    private const string TwoChannelsYaml = """
        asyncapi: 3.1.0
        info:
          title: Fixture
          version: 1.0.0
        channels:
          diagnosticsPublish:
            address: diagnostics.ping
            description: Routing key publicada na exchange diagnostics.topic.
            bindings:
              amqp:
                is: routingKey
                exchange:
                  name: diagnostics.topic
                  type: topic
                  durable: true
          diagnosticsConsume:
            address: notification.diagnostics
            description: Fila consumida pelo worker.
            bindings:
              amqp:
                is: queue
                queue:
                  name: notification.diagnostics
                  durable: true
        """;

    [Fact]
    public void ReadChannels_ExtractsExchangeNameForRoutingKeyBinding()
    {
        var channels = AsyncApiChannelReader.ReadChannels(TwoChannelsYaml);

        Assert.Contains(channels, c => c.TopicName == "diagnostics.topic");
    }

    [Fact]
    public void ReadChannels_ExtractsQueueNameForQueueBinding()
    {
        var channels = AsyncApiChannelReader.ReadChannels(TwoChannelsYaml);

        Assert.Contains(channels, c => c.TopicName == "notification.diagnostics");
    }

    [Fact]
    public void ReadChannels_PreservesDeclarationOrder()
    {
        var channels = AsyncApiChannelReader.ReadChannels(TwoChannelsYaml);

        Assert.Equal(["diagnostics.topic", "notification.diagnostics"], channels.Select(c => c.TopicName));
    }

    [Fact]
    public void ReadChannels_ReadsChannelDescription()
    {
        var channels = AsyncApiChannelReader.ReadChannels(TwoChannelsYaml);

        Assert.Equal(
            "Routing key publicada na exchange diagnostics.topic.",
            channels.Single(c => c.TopicName == "diagnostics.topic").Description);
    }

    [Fact]
    public void ReadChannels_ReturnsEmpty_WhenNoChannelsSection()
    {
        var channels = AsyncApiChannelReader.ReadChannels("asyncapi: 3.1.0\ninfo:\n  title: Sem canais\n  version: 1.0.0\n");

        Assert.Empty(channels);
    }

    [Fact]
    public void ReadChannels_Throws_ForUnsupportedBindingKind()
    {
        const string yaml = """
            asyncapi: 3.1.0
            channels:
              weird:
                bindings:
                  amqp:
                    is: unsupportedKind
            """;

        Assert.Throws<NotSupportedException>(() => AsyncApiChannelReader.ReadChannels(yaml));
    }
}

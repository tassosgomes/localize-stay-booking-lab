using System.Text.Json.Nodes;
using RegisterRabbitMq;
using Xunit;

namespace RegisterRabbitMq;

/// <summary>
/// Testes unitários do construtor de payload (task 9.0, V-06, generalizado
/// para ler <c>contracts/asyncapi/*.yaml</c>). Sem rede: só comprovam que o
/// JSON enviado a <c>/v1/services/messagingServices</c> e <c>/v1/topics</c>
/// tem os campos corretos. A chamada real contra o OpenMetadata é feita pelo
/// job <c>catalog-metadata</c> do CI.
/// </summary>
public sealed class PayloadBuilderTests
{
    private static readonly (string Name, string Description)[] DiagnosticsChannels =
    [
        ("diagnostics.topic", "Exchange de diagnóstico."),
        ("notification.diagnostics", "Fila de diagnóstico."),
    ];

    [Fact]
    public void MessagingServicePayload_HasCustomMessagingServiceType()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildMessagingServicePayload())!;

        Assert.Equal("CustomMessaging", payload["serviceType"]?.GetValue<string>());
    }

    [Fact]
    public void MessagingServicePayload_HasExpectedServiceName()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildMessagingServicePayload())!;

        Assert.Equal("localize-stay-rabbitmq", payload["name"]?.GetValue<string>());
    }

    [Fact]
    public void MessagingServicePayload_HasLocalizeStayTag()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildMessagingServicePayload())!;

        Assert.Contains(
            payload["tags"]?.AsArray() ?? new JsonArray(),
            tag => tag?["tagFQN"]?.GetValue<string>() == "localize-stay.localize-stay");
    }

    [Fact]
    public void MessagingServicePayload_UsesCustomMessagingConnection()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildMessagingServicePayload())!;

        Assert.Equal("CustomMessaging", payload["connection"]?["config"]?["type"]?.GetValue<string>());
    }

    [Fact]
    public void TopicPayloads_PreserveChannelOrderAndNames()
    {
        var topics = PayloadBuilder.BuildTopicPayloads(DiagnosticsChannels).Select(t => t.Name).ToList();

        Assert.Equal(["diagnostics.topic", "notification.diagnostics"], topics);
    }

    [Theory]
    [InlineData("diagnostics.topic")]
    [InlineData("notification.diagnostics")]
    public void TopicPayload_HasNameAndLocalizeStayTag(string topicName)
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload(topicName, "desc"))!;

        Assert.Equal(topicName, payload["name"]?.GetValue<string>());
        Assert.Contains(
            payload["tags"]?.AsArray() ?? new JsonArray(),
            tag => tag?["tagFQN"]?.GetValue<string>() == "localize-stay.localize-stay");
    }

    [Fact]
    public void TopicPayload_UsesChannelDescription()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload("diagnostics.topic", "Descrição real do canal."))!;

        Assert.Equal("Descrição real do canal.", payload["description"]?.GetValue<string>());
    }

    [Fact]
    public void TopicPayload_FallsBackToGenericDescription_WhenChannelHasNone()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload("diagnostics.topic"))!;

        Assert.Contains("diagnostics.topic", payload["description"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("diagnostics.topic")]
    [InlineData("notification.diagnostics")]
    public void TopicPayload_ReferencesMessagingService(string topicName)
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload(topicName, "desc"))!;

        // `service` é o nome do serviço como string simples (não um objeto
        // {type,name}) — confirmado no conector custom_messaging.py oficial.
        Assert.Equal("localize-stay-rabbitmq", payload["service"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("diagnostics.topic")]
    [InlineData("notification.diagnostics")]
    public void TopicPayload_HasPartitions(string topicName)
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload(topicName, "desc"))!;

        Assert.Equal(1, payload["partitions"]?.GetValue<int>());
    }
}

using System.Text.Json.Nodes;
using RegisterRabbitMq;
using Xunit;

namespace RegisterRabbitMq;

/// <summary>
/// Testes unitários do construtor de payload (task 9.0, V-06). Sem rede: só
/// comprovam que o JSON enviado a <c>/v1/services/messagingServices</c> e
/// <c>/v1/topics</c> tem os campos corretos. A chamada real contra o
/// <c>ecad-dev-openmetadata</c> é verificação manual do dono do homelab.
/// </summary>
public sealed class PayloadBuilderTests
{
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
            tag => tag?["tagFQN"]?.GetValue<string>() == "localize-stay");
    }

    [Fact]
    public void TopicPayloads_ContainBothDiagnosticTopics()
    {
        var topics = PayloadBuilder.BuildTopicPayloads().Select(t => t.Name).ToList();

        Assert.Equal(["diagnostics.topic", "notification.diagnostics"], topics);
    }

    [Theory]
    [InlineData("diagnostics.topic")]
    [InlineData("notification.diagnostics")]
    public void TopicPayload_HasNameAndLocalizeStayTag(string topicName)
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload(topicName))!;

        Assert.Equal(topicName, payload["name"]?.GetValue<string>());
        Assert.Contains(
            payload["tags"]?.AsArray() ?? new JsonArray(),
            tag => tag?["tagFQN"]?.GetValue<string>() == "localize-stay");
    }

    [Theory]
    [InlineData("diagnostics.topic")]
    [InlineData("notification.diagnostics")]
    public void TopicPayload_ReferencesMessagingService(string topicName)
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildTopicPayload(topicName))!;

        Assert.Equal("localize-stay-rabbitmq", payload["service"]?["name"]?.GetValue<string>());
    }

    [Fact]
    public void MessagingServicePayload_UsesCustomMessagingConnection()
    {
        var payload = JsonNode.Parse(PayloadBuilder.BuildMessagingServicePayload())!;

        Assert.Equal("CustomMessaging", payload["connection"]?["config"]?["type"]?.GetValue<string>());
    }
}

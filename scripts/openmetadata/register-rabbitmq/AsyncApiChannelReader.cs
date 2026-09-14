using YamlDotNet.RepresentationModel;

namespace RegisterRabbitMq;

/// <summary>
/// Lê a seção <c>channels</c> de um documento AsyncAPI 3.x (RabbitMQ/AMQP) e
/// extrai um tópico OpenMetadata por canal — nome da exchange (binding
/// <c>routingKey</c>) ou da fila (binding <c>queue</c>) — sem depender de uma
/// lista hardcoded. Usado para publicar automaticamente qualquer arquivo em
/// <c>contracts/asyncapi/*.yaml</c>, atuais ou futuros (ver comentário em
/// <c>diagnostics-v1.yaml</c>: "PRDs de negócio devem seguir este mesmo
/// padrão").
/// </summary>
public static class AsyncApiChannelReader
{
    /// <summary>
    /// Lê os canais na ordem em que aparecem no documento (a ordem de
    /// declaração do YAML é preservada por <see cref="YamlMappingNode"/>).
    /// </summary>
    public static IReadOnlyList<(string TopicName, string Description)> ReadChannels(string yamlContent)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yamlContent));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        if (!root.Children.TryGetValue(new YamlScalarNode("channels"), out var channelsNode))
        {
            return [];
        }

        var result = new List<(string, string)>();
        foreach (var (_, value) in ((YamlMappingNode)channelsNode).Children)
        {
            var channel = (YamlMappingNode)value;
            result.Add((ReadAmqpTopicName(channel), ReadDescription(channel)));
        }

        return result;
    }

    private static string ReadDescription(YamlMappingNode channel) =>
        channel.Children.TryGetValue(new YamlScalarNode("description"), out var description)
            ? ((YamlScalarNode)description).Value?.Trim() ?? string.Empty
            : string.Empty;

    private static string ReadAmqpTopicName(YamlMappingNode channel)
    {
        var bindings = (YamlMappingNode)channel.Children[new YamlScalarNode("bindings")];
        var amqp = (YamlMappingNode)bindings.Children[new YamlScalarNode("amqp")];
        var kind = ((YamlScalarNode)amqp.Children[new YamlScalarNode("is")]).Value;

        return kind switch
        {
            "routingKey" => ReadName(amqp, "exchange"),
            "queue" => ReadName(amqp, "queue"),
            _ => throw new NotSupportedException(
                $"bindings.amqp.is='{kind}' não suportado (esperado 'routingKey' ou 'queue')."),
        };
    }

    private static string ReadName(YamlMappingNode amqp, string resourceKey)
    {
        var resource = (YamlMappingNode)amqp.Children[new YamlScalarNode(resourceKey)];
        return ((YamlScalarNode)resource.Children[new YamlScalarNode("name")]).Value
            ?? throw new InvalidOperationException($"bindings.amqp.{resourceKey}.name ausente.");
    }
}

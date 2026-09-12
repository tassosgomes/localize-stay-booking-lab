using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RegisterRabbitMq;

/// <summary>
/// Constrói os payloads JSON de registro do vhost <c>/localize-stay</c> no
/// OpenMetadata (task 9.0, V-06). Puro e sem I/O: nenhuma chamada HTTP acontece
/// aqui — a execução real vive em <c>Program.cs</c> e é verificação manual do
/// dono do homelab contra o <c>ecad-dev-openmetadata</c> real.
/// </summary>
/// <remarks>
/// Contrato seguido (OpenMetadata 2.0.x, confirmado na documentação pública):
/// <list type="bullet">
/// <item><c>PUT /api/v1/services/messagingServices</c> (upsert) com
/// <c>serviceType: CustomMessaging</c> — valor válido do enum
/// <c>messagingServiceType</c>, com config de conexão do tipo
/// <c>CustomMessaging</c>. Por isso nenhum workaround (ex.: Kafka genérico) é
/// necessário no payload; a confirmação na build 2.0.1 instalada permanece
/// como verificação manual.</item>
/// <item><c>PUT /api/v1/topics</c> (upsert), um por exchange/fila real de V-03
/// (<c>diagnostics.topic</c>, <c>notification.diagnostics</c>).</item>
/// </list>
/// Toda entidade recebe a tag <c>localize-stay</c> para não confundir com os
/// ativos do <c>ecad-sba</c> no mesmo catálogo.
/// </remarks>
public static class PayloadBuilder
{
    public const string ServiceName = "localize-stay-rabbitmq";

    public const string ServiceType = "CustomMessaging";

    public const string ConnectionConfigType = "CustomMessaging";

    public const string TagFqn = "localize-stay";

    public const string Vhost = "/localize-stay";

    public static readonly IReadOnlyList<string> TopicNames =
    [
        "diagnostics.topic",
        "notification.diagnostics",
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Payload de upsert do serviço de mensageria
    /// (<c>/api/v1/services/messagingServices</c>).
    /// </summary>
    public static string BuildMessagingServicePayload()
    {
        var payload = new JsonObject
        {
            ["name"] = ServiceName,
            ["displayName"] = ServiceName,
            ["description"] = "Vhost /localize-stay do broker ecad-dev-rabbitmq (RabbitMQ compartilhado com o ecad-sba). "
                + "Topologia de diagnóstico da fundação (V-03): exchange diagnostics.topic, fila notification.diagnostics. "
                + "Registro manual — o OpenMetadata 2.0.x não tem conector de ingestão nativo para RabbitMQ.",
            ["serviceType"] = ServiceType,
            ["connection"] = new JsonObject
            {
                ["config"] = new JsonObject
                {
                    ["type"] = ConnectionConfigType,
                },
            },
            ["tags"] = TagLabels(),
        };

        return payload.ToJsonString(Options);
    }

    /// <summary>
    /// Payload de upsert de um tópico (<c>/api/v1/topics</c>), referenciando o
    /// serviço de mensageria pelo nome.
    /// </summary>
    public static string BuildTopicPayload(string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        var payload = new JsonObject
        {
            ["name"] = topicName,
            ["displayName"] = topicName,
            ["description"] = $"Exchange/fila {topicName} do vhost /localize-stay (topologia de diagnóstico, V-03).",
            ["service"] = new JsonObject
            {
                ["type"] = "messagingService",
                ["name"] = ServiceName,
            },
            ["tags"] = TagLabels(),
        };

        return payload.ToJsonString(Options);
    }

    /// <summary>
    /// Payloads de upsert dos dois tópicos reais de V-03, na ordem de
    /// <see cref="TopicNames" />.
    /// </summary>
    public static IReadOnlyList<(string Name, string Payload)> BuildTopicPayloads() =>
        TopicNames.Select(name => (name, BuildTopicPayload(name))).ToList();

    private static JsonArray TagLabels() =>
    [
        new JsonObject
        {
            ["tagFQN"] = TagFqn,
            ["source"] = "Classification",
            ["labelType"] = "Manual",
            ["state"] = "Confirmed",
        },
    ];
}

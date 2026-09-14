using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RegisterRabbitMq;

/// <summary>
/// Constrói os payloads JSON de registro do vhost <c>/localize-stay</c> no
/// OpenMetadata. Puro e sem I/O: nenhuma chamada HTTP acontece aqui — a
/// execução real vive em <c>Program.cs</c>, que lê a lista de tópicos a
/// partir dos arquivos <c>contracts/asyncapi/*.yaml</c> via
/// <see cref="AsyncApiChannelReader"/> e roda automaticamente no CI a cada
/// push em <c>main</c> (job <c>catalog-metadata</c>).
/// </summary>
/// <remarks>
/// Contrato seguido (OpenMetadata 2.0.x, confirmado contra o servidor real —
/// PUT é create-or-update por nome; POST só cria e responde 409 "Entity
/// already exists" numa segunda execução, por isso não serve pra um
/// publicador que roda em todo push):
/// <list type="bullet">
/// <item><c>PUT /api/v1/services/messagingServices</c> (upsert por nome) com
/// <c>serviceType: CustomMessaging</c> — valor válido do enum
/// <c>messagingServiceType</c>, com config de conexão do tipo
/// <c>CustomMessaging</c>. Por isso nenhum workaround (ex.: Kafka genérico) é
/// necessário no payload.</item>
/// <item><c>PUT /api/v1/topics</c> (upsert por nome), um por canal declarado
/// nos arquivos AsyncAPI (exchange para bindings <c>routingKey</c>, fila para
/// bindings <c>queue</c>).</item>
/// </list>
/// Toda entidade recebe a tag <c>localize-stay</c> para não confundir com os
/// ativos do <c>ecad-sba</c> no mesmo catálogo.
/// </remarks>
public static class PayloadBuilder
{
    public const string ServiceName = "localize-stay-rabbitmq";

    public const string ServiceType = "CustomMessaging";

    public const string ConnectionConfigType = "CustomMessaging";

    // FQN completo classification.tag — o CI garante que ambos existam antes
    // deste publicador rodar (ver step "Garantir classification/tag
    // localize-stay" em ci.yml). Um tagFQN de um segmento só (a classification
    // sozinha) não resolve: a API responde 404 "Entity not found: tag ...".
    public const string TagFqn = "localize-stay.localize-stay";

    public const string Vhost = "/localize-stay";

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
                + "Tópicos registrados a partir dos contratos versionados em contracts/asyncapi/*.yaml — "
                + "o OpenMetadata 2.0.x não tem conector de ingestão nativo para RabbitMQ, por isso o registro "
                + "é feito por este publicador dedicado (job catalog-metadata do CI).",
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
    /// serviço de mensageria pelo nome. A descrição vem do canal AsyncAPI de
    /// origem (<see cref="AsyncApiChannelReader"/>); um fallback genérico é
    /// usado quando o canal não declara <c>description</c>.
    /// </summary>
    public static string BuildTopicPayload(string topicName, string description = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        var payload = new JsonObject
        {
            ["name"] = topicName,
            ["displayName"] = topicName,
            ["description"] = string.IsNullOrWhiteSpace(description)
                ? $"Exchange/fila {topicName} do vhost /localize-stay."
                : description,
            // `service` é o NOME do serviço (string simples), não um objeto
            // {type,name} — confirmado no conector custom_messaging.py oficial
            // (CreateTopicRequest(service=service_name, ...)). `partitions` é
            // obrigatório no schema mesmo para brokers sem partição real
            // (RabbitMQ); 1 é o valor usado pelos conectores NATS/Pub-Sub
            // oficiais (também non-partitioned) para o mesmo campo.
            ["service"] = ServiceName,
            ["partitions"] = 1,
            ["tags"] = TagLabels(),
        };

        return payload.ToJsonString(Options);
    }

    /// <summary>
    /// Payloads de upsert de todos os canais lidos de
    /// <c>contracts/asyncapi/*.yaml</c>, na ordem em que foram fornecidos.
    /// </summary>
    public static IReadOnlyList<(string Name, string Payload)> BuildTopicPayloads(
        IEnumerable<(string Name, string Description)> channels) =>
        channels.Select(c => (c.Name, BuildTopicPayload(c.Name, c.Description))).ToList();

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

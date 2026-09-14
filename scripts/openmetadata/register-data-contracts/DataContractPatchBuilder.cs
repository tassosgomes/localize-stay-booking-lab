using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RegisterDataContracts;

/// <summary>
/// Constrói o JSON Patch (RFC 6902) que publica um <see cref="DataContractInfo"/>
/// na <c>table</c>/<c>view</c> correspondente do OpenMetadata: description,
/// tag <c>localize-stay</c> e as 4 Custom Properties definidas pelo bootstrap
/// (<c>scripts/openmetadata/bootstrap-custom-properties.sh</c>). Puro e sem
/// I/O — recebe o estado atual da entidade (já buscado via GET) para não
/// sobrescrever tags/custom properties definidas por fora deste publicador.
/// </summary>
public static class DataContractPatchBuilder
{
    // FQN completo classification.tag (mesma tag do register-rabbitmq) — o
    // CI garante que ambos existam antes deste publicador rodar. Um tagFQN
    // de um segmento só (a classification sozinha) não resolve: a API
    // responde 404 "Entity not found: tag ...".
    public const string TagFqn = "localize-stay.localize-stay";

    public const string RefProperty = "dataContractRef";

    public const string VersionProperty = "dataContractVersion";

    public const string StatusProperty = "dataContractStatus";

    public const string OwnerDomainProperty = "dataContractOwnerDomain";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string BuildPatch(JsonObject currentTable, DataContractInfo contract)
    {
        var tags = MergeLocalizeStayTag(currentTable);
        var extension = MergeCustomProperties(currentTable, contract);

        var patch = new JsonArray
        {
            new JsonObject { ["op"] = "add", ["path"] = "/description", ["value"] = contract.Description },
            new JsonObject { ["op"] = "add", ["path"] = "/tags", ["value"] = tags },
            new JsonObject { ["op"] = "add", ["path"] = "/extension", ["value"] = extension },
        };

        return patch.ToJsonString(Options);
    }

    private static JsonArray MergeLocalizeStayTag(JsonObject currentTable)
    {
        var tags = currentTable["tags"] is JsonArray existing
            ? (JsonArray)existing.DeepClone()
            : [];

        var alreadyTagged = tags.Any(t => t?["tagFQN"]?.GetValue<string>() == TagFqn);
        if (!alreadyTagged)
        {
            tags.Add(new JsonObject
            {
                ["tagFQN"] = TagFqn,
                ["source"] = "Classification",
                ["labelType"] = "Manual",
                ["state"] = "Confirmed",
            });
        }

        return tags;
    }

    private static JsonObject MergeCustomProperties(JsonObject currentTable, DataContractInfo contract)
    {
        var extension = currentTable["extension"] is JsonObject existing
            ? (JsonObject)existing.DeepClone()
            : [];

        extension[RefProperty] = contract.SourceFile;
        extension[VersionProperty] = contract.Version;
        extension[StatusProperty] = contract.Status;
        extension[OwnerDomainProperty] = contract.OwnerDomain;

        return extension;
    }
}

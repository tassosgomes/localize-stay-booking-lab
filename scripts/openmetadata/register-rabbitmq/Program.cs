using System.Net.Http.Headers;
using System.Text;

namespace RegisterRabbitMq;

/// <summary>
/// Registra no OpenMetadata o vhost <c>/localize-stay</c> (upsert do serviço
/// <c>CustomMessaging</c>) e um tópico por canal declarado em todo arquivo
/// <c>contracts/asyncapi/*.yaml</c> do repositório. Roda automaticamente no
/// job <c>catalog-metadata</c> do CI a cada push em <c>main</c> — o
/// OpenMetadata 2.0.x não tem conector de ingestão nativo para RabbitMQ, daí
/// este publicador dedicado em vez do conector padrão usado para OpenAPI.
/// </summary>
/// <remarks>
/// Credencial: o token (JWT do bot de ingestão do OpenMetadata, ou PAT de
/// escopo mínimo de escrita) é lido de <c>--pat &lt;token&gt;</c> ou da
/// variável de ambiente <c>OPENMETADATA_INGESTION_JWT</c> (compatibilidade:
/// <c>OPENMETADATA_PAT</c> também é aceita), com precedência do argumento.
/// Nenhum segredo é versionado ou impresso no log (o token é mascarado em
/// caso de erro).
/// <para />
/// Base da API em <c>--url &lt;base&gt;</c> ou <c>OPENMETADATA_BASE_URL</c>
/// (padrão <c>http://localhost:8585/api</c>); os endpoints chamados são
/// <c>/v1/services/messagingServices</c> e <c>/v1/topics</c> via POST — a
/// API do OpenMetadata usa POST (não PUT, que responde 405) para create-or-
/// update por nome nesses recursos. <c>--asyncapi-dir &lt;path&gt;</c> aponta
/// para os contratos
/// (padrão <c>contracts/asyncapi</c>, relativo ao diretório de execução).
/// <c>--dry-run</c> só imprime os payloads, sem rede.
/// </remarks>
public static class Program
{
    public const string PatEnvVar = "OPENMETADATA_INGESTION_JWT";

    public const string LegacyPatEnvVar = "OPENMETADATA_PAT";

    public const string BaseUrlEnvVar = "OPENMETADATA_BASE_URL";

    public const string DefaultBaseUrl = "http://localhost:8585/api";

    public const string DefaultAsyncApiDir = "contracts/asyncapi";

    public static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);

        if (!Directory.Exists(options.AsyncApiDir))
        {
            Console.Error.WriteLine($"Diretório de contratos AsyncAPI não encontrado: {options.AsyncApiDir}");
            return 2;
        }

        var files = Directory.EnumerateFiles(options.AsyncApiDir, "*.yaml")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"Nenhum contrato AsyncAPI (*.yaml) encontrado em {options.AsyncApiDir}");
            return 2;
        }

        var channels = new List<(string Name, string Description)>();
        foreach (var file in files)
        {
            channels.AddRange(AsyncApiChannelReader.ReadChannels(File.ReadAllText(file)));
        }

        var servicePayload = PayloadBuilder.BuildMessagingServicePayload();
        var topics = PayloadBuilder.BuildTopicPayloads(channels);

        if (options.DryRun)
        {
            Console.WriteLine("POST /v1/services/messagingServices");
            Console.WriteLine(servicePayload);
            foreach (var (name, payload) in topics)
            {
                Console.WriteLine($"POST /v1/topics [{name}]");
                Console.WriteLine(payload);
            }

            return 0;
        }

        var pat = ResolvePat(options.Pat, Environment.GetEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(pat))
        {
            Console.Error.WriteLine(
                $"PAT não informado. Use --pat <token> ou a variável de ambiente {PatEnvVar}.");
            return 2;
        }

        using var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var failures = 0;
        failures += await PostAsync(http, "/v1/services/messagingServices", servicePayload, "messaging service");
        foreach (var (name, payload) in topics)
        {
            failures += await PostAsync(http, "/v1/topics", payload, $"topic {name}");
        }

        if (failures > 0)
        {
            Console.Error.WriteLine($"{failures} chamada(s) falharam. Confira a UI do OpenMetadata e o PAT (escopo mínimo de escrita).");
            return 1;
        }

        Console.WriteLine($"Registro concluído: localize-stay-rabbitmq + {topics.Count} tópico(s) com tag localize-stay.");
        return 0;
    }

    internal sealed record CliOptions(string? Pat, string BaseUrl, string AsyncApiDir, bool DryRun);

    internal static CliOptions ParseArgs(string[] args)
    {
        string? pat = null;
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        var asyncApiDir = DefaultAsyncApiDir;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pat" when i + 1 < args.Length:
                    pat = args[++i];
                    break;
                case "--url" when i + 1 < args.Length:
                    baseUrl = args[++i];
                    break;
                case "--asyncapi-dir" when i + 1 < args.Length:
                    asyncApiDir = args[++i];
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"Argumento desconhecido: {args[i]}. Uso: [--pat <token>] [--url <base>] [--asyncapi-dir <path>] [--dry-run]");
            }
        }

        return new CliOptions(pat, baseUrl, asyncApiDir, dryRun);
    }

    internal static string? ResolvePat(string? cliPat, Func<string, string?> getEnvironmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(cliPat))
        {
            return cliPat;
        }

        return getEnvironmentVariable(PatEnvVar) is { Length: > 0 } jwt
            ? jwt
            : getEnvironmentVariable(LegacyPatEnvVar);
    }

    private static async Task<int> PostAsync(HttpClient http, string path, string payload, string label)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.PostAsync(path, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"POST {path} ({label}) -> {(int)response.StatusCode}. Resposta (PAT omitido): {body}");
                return 1;
            }

            Console.WriteLine($"POST {path} ({label}) -> {(int)response.StatusCode} OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"POST {path} ({label}) falhou: {ex.GetType().Name} (detalhes de rede, PAT omitido)");
            return 1;
        }
    }
}

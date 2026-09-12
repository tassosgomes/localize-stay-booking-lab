using System.Net.Http.Headers;
using System.Text;

namespace RegisterRabbitMq;

/// <summary>
/// Executa o registro real do vhost <c>/localize-stay</c> no
/// <c>ecad-dev-openmetadata</c> (task 9.0, V-06): upsert do serviço
/// <c>CustomMessaging</c> + upsert dos dois tópicos de V-03.
/// Uso destinado ao dono do homelab (verificação manual, fora do gate).
/// </summary>
/// <remarks>
/// Credencial: o Personal Access Token (escopo mínimo de escrita, gerado no
/// próprio OpenMetadata — nunca o token administrativo do Coolify) é lido de
/// <c>--pat &lt;token&gt;</c> ou da variável de ambiente
/// <c>OPENMETADATA_PAT</c>, com precedência do argumento. Nenhum segredo é
/// versionado ou impresso no log (o PAT é mascarado em caso de erro).
/// <para />
/// Base da API em <c>--url &lt;base&gt;</c> ou <c>OPENMETADATA_BASE_URL</c>
/// (padrão <c>http://localhost:8585/api</c>); os endpoints chamados são
/// <c>/v1/services/messagingServices</c> e <c>/v1/topics</c> via PUT (upsert
/// idempotente). <c>--dry-run</c> só imprime os payloads, sem rede.
/// </remarks>
public static class Program
{
    public const string PatEnvVar = "OPENMETADATA_PAT";

    public const string BaseUrlEnvVar = "OPENMETADATA_BASE_URL";

    public const string DefaultBaseUrl = "http://localhost:8585/api";

    public static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);

        var servicePayload = PayloadBuilder.BuildMessagingServicePayload();
        var topics = PayloadBuilder.BuildTopicPayloads();

        if (options.DryRun)
        {
            Console.WriteLine("PUT /v1/services/messagingServices");
            Console.WriteLine(servicePayload);
            foreach (var (name, payload) in topics)
            {
                Console.WriteLine($"PUT /v1/topics [{name}]");
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
        failures += await PutAsync(http, "/v1/services/messagingServices", servicePayload, "messaging service");
        foreach (var (name, payload) in topics)
        {
            failures += await PutAsync(http, "/v1/topics", payload, $"topic {name}");
        }

        if (failures > 0)
        {
            Console.Error.WriteLine($"{failures} chamada(s) falharam. Confira a UI do OpenMetadata e o PAT (escopo mínimo de escrita).");
            return 1;
        }

        Console.WriteLine("Registro concluído: localize-stay-rabbitmq + 2 tópicos com tag localize-stay.");
        return 0;
    }

    internal sealed record CliOptions(string? Pat, string BaseUrl, bool DryRun);

    internal static CliOptions ParseArgs(string[] args)
    {
        string? pat = null;
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

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
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new ArgumentException($"Argumento desconhecido: {args[i]}. Uso: [--pat <token>] [--url <base>] [--dry-run]");
            }
        }

        return new CliOptions(pat, baseUrl, dryRun);
    }

    internal static string? ResolvePat(string? cliPat, Func<string, string?> getEnvironmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(cliPat))
        {
            return cliPat;
        }

        return getEnvironmentVariable(PatEnvVar);
    }

    private static async Task<int> PutAsync(HttpClient http, string path, string payload, string label)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.PutAsync(path, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"PUT {path} ({label}) -> {(int)response.StatusCode}. Resposta (PAT omitido): {body}");
                return 1;
            }

            Console.WriteLine($"PUT {path} ({label}) -> {(int)response.StatusCode} OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PUT {path} ({label}) falhou: {ex.GetType().Name} (detalhes de rede, PAT omitido)");
            return 1;
        }
    }
}

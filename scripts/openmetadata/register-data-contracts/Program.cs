using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace RegisterDataContracts;

/// <summary>
/// Publica todo <c>contracts/data-contracts/*.md</c> (exceto
/// <c>TEMPLATE.md</c>) na <c>table</c>/<c>view</c> correspondente do
/// OpenMetadata: description + tag <c>localize-stay</c> + 4 Custom
/// Properties (<see cref="DataContractPatchBuilder"/>). Roda automaticamente
/// no job <c>catalog-metadata</c> do CI, depois da ingestão Postgres — a
/// tabela/view alvo precisa já existir no catálogo.
/// </summary>
/// <remarks>
/// Pré-requisito único (fora deste publicador): as 4 Custom Properties
/// precisam existir na entidade <c>table</c> do OpenMetadata antes da
/// primeira execução real — ver
/// <c>scripts/openmetadata/bootstrap-custom-properties.sh</c>.
/// <para />
/// Credencial: <c>--pat &lt;token&gt;</c> ou <c>OPENMETADATA_INGESTION_JWT</c>
/// (compatibilidade: <c>OPENMETADATA_PAT</c>). Base da API em
/// <c>--url &lt;base&gt;</c> ou <c>OPENMETADATA_BASE_URL</c>. Nenhum segredo é
/// versionado ou impresso no log.
/// <para />
/// <c>--dry-run</c> não faz nenhuma chamada de rede: simula a entidade atual
/// como vazia (sem tags/custom properties preexistentes), então o patch
/// impresso é aproximado — a execução real busca o estado atual antes de
/// montar o patch, para não sobrescrever tags/custom properties de terceiros.
/// </remarks>
public static class Program
{
    public const string PatEnvVar = "OPENMETADATA_INGESTION_JWT";

    public const string LegacyPatEnvVar = "OPENMETADATA_PAT";

    public const string BaseUrlEnvVar = "OPENMETADATA_BASE_URL";

    public const string DefaultBaseUrl = "http://localhost:8585/api";

    public const string DefaultDataContractsDir = "contracts/data-contracts";

    public const string TemplateFileName = "TEMPLATE.md";

    public static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);

        if (!Directory.Exists(options.DataContractsDir))
        {
            Console.Error.WriteLine($"Diretório de Data Contracts não encontrado: {options.DataContractsDir}");
            return 2;
        }

        var files = Directory.EnumerateFiles(options.DataContractsDir, "*.md")
            .Where(f => !string.Equals(Path.GetFileName(f), TemplateFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"Nenhum Data Contract (*.md, exceto {TemplateFileName}) encontrado em {options.DataContractsDir}");
            return 2;
        }

        var contracts = files
            .Select(f => DataContractParser.Parse(File.ReadAllText(f), Path.GetRelativePath(Directory.GetCurrentDirectory(), f)))
            .ToList();

        if (options.DryRun)
        {
            foreach (var contract in contracts)
            {
                var fqn = TableFqnBuilder.Build(contract);
                Console.WriteLine($"PATCH /v1/tables/name/{fqn} (simulado, entidade atual assumida vazia)");
                Console.WriteLine(DataContractPatchBuilder.BuildPatch([], contract));
            }

            return 0;
        }

        var pat = ResolvePat(options.Pat, Environment.GetEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(pat))
        {
            Console.Error.WriteLine($"Token não informado. Use --pat <token> ou a variável de ambiente {PatEnvVar}.");
            return 2;
        }

        using var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var failures = 0;
        foreach (var contract in contracts)
        {
            failures += await PublishAsync(http, contract);
        }

        if (failures > 0)
        {
            Console.Error.WriteLine($"{failures} Data Contract(s) falharam. Confira se a ingestão Postgres já rodou (tabela/view precisa existir no catálogo).");
            return 1;
        }

        Console.WriteLine($"Registro concluído: {contracts.Count} Data Contract(s) publicados no OpenMetadata.");
        return 0;
    }

    private static async Task<int> PublishAsync(HttpClient http, DataContractInfo contract)
    {
        var fqn = TableFqnBuilder.Build(contract);
        var path = $"/v1/tables/name/{fqn}";

        JsonObject current;
        try
        {
            using var getResponse = await http.GetAsync(path);
            if (!getResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine(
                    $"GET {path} -> {(int)getResponse.StatusCode}. A tabela/view ainda não existe no catálogo? Rode a ingestão Postgres antes deste passo.");
                return 1;
            }

            current = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync())?.AsObject() ?? [];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GET {path} falhou: {ex.GetType().Name} (detalhes de rede, token omitido)");
            return 1;
        }

        var patch = DataContractPatchBuilder.BuildPatch(current, contract);
        using var content = new StringContent(patch, Encoding.UTF8, "application/json-patch+json");
        try
        {
            using var patchResponse = await http.PatchAsync(path, content);
            var body = await patchResponse.Content.ReadAsStringAsync();
            if (!patchResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"PATCH {path} -> {(int)patchResponse.StatusCode}. Resposta (token omitido): {body}");
                return 1;
            }

            Console.WriteLine($"PATCH {path} -> {(int)patchResponse.StatusCode} OK ({contract.SourceFile})");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PATCH {path} falhou: {ex.GetType().Name} (detalhes de rede, token omitido)");
            return 1;
        }
    }

    internal sealed record CliOptions(string? Pat, string BaseUrl, string DataContractsDir, bool DryRun);

    internal static CliOptions ParseArgs(string[] args)
    {
        string? pat = null;
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        var dataContractsDir = DefaultDataContractsDir;
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
                case "--data-contracts-dir" when i + 1 < args.Length:
                    dataContractsDir = args[++i];
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new ArgumentException(
                        $"Argumento desconhecido: {args[i]}. Uso: [--pat <token>] [--url <base>] [--data-contracts-dir <path>] [--dry-run]");
            }
        }

        return new CliOptions(pat, baseUrl, dataContractsDir, dryRun);
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
}

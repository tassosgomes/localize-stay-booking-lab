namespace RegisterDataContracts;

/// <summary>
/// Dados extraídos de um Data Contract (<c>contracts/data-contracts/*.md</c>,
/// convenção própria do projeto — não é ODCS). <see cref="SourceFile"/> é o
/// caminho relativo usado para referência (<c>dataContractRef</c>) no
/// OpenMetadata.
/// </summary>
public sealed record DataContractInfo(
    string DatasetName,
    string Version,
    string OwnerDomain,
    string Schema,
    string Status,
    string Description,
    string SourceFile);

/// <summary>
/// Extrai os campos publicáveis de um Data Contract a partir das seções
/// "1. Identificação" (tabela `| Campo | Valor |`) e "2. Descrição", no
/// formato usado por <c>contracts/data-contracts/reservation_calendar_v1.md</c>.
/// Puro e sem I/O — o arquivo já vem lido pelo chamador.
/// </summary>
public static class DataContractParser
{
    public static DataContractInfo Parse(string markdown, string sourceFile)
    {
        var fields = ReadIdentificationTable(markdown);

        // "Schema de exposição" é o schema onde o dataset é publicado (ex.:
        // integration); nem todo contrato declara essa distinção — quando
        // ausente, o dataset vive diretamente no "Schema PostgreSQL de origem".
        var schema = fields.TryGetValue("Schema de exposição", out var exposedSchema) && !string.IsNullOrWhiteSpace(exposedSchema)
            ? exposedSchema
            : RequireField(fields, "Schema PostgreSQL de origem", sourceFile);

        return new DataContractInfo(
            DatasetName: RequireField(fields, "Nome do dataset", sourceFile),
            Version: RequireField(fields, "Versão", sourceFile),
            OwnerDomain: RequireField(fields, "Domínio dono", sourceFile),
            Schema: schema,
            Status: RequireField(fields, "Estado", sourceFile),
            Description: ReadSection(markdown, "Descrição"),
            SourceFile: sourceFile);
    }

    private static Dictionary<string, string> ReadIdentificationTable(string markdown)
    {
        var fields = new Dictionary<string, string>();
        var inTable = false;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimStart();

            if (line.StartsWith("## "))
            {
                if (inTable)
                {
                    break;
                }

                inTable = line.Contains("Identificação", StringComparison.Ordinal);
                continue;
            }

            if (!inTable || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
            if (cells.Count < 2 || cells[0] is "Campo" || cells[0].All(c => c == '-'))
            {
                continue;
            }

            fields[cells[0]] = cells[1].Trim('`').Trim();
        }

        return fields;
    }

    private static string ReadSection(string markdown, string headingContains)
    {
        var buffer = new List<string>();
        var collecting = false;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimStart();
            if (line.StartsWith("## "))
            {
                if (collecting)
                {
                    break;
                }

                collecting = line.Contains(headingContains, StringComparison.Ordinal);
                continue;
            }

            if (collecting)
            {
                buffer.Add(rawLine);
            }
        }

        return string.Join('\n', buffer).Trim();
    }

    private static string RequireField(Dictionary<string, string> fields, string key, string sourceFile) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Campo obrigatório ausente na tabela de Identificação de {sourceFile}: {key}");
}

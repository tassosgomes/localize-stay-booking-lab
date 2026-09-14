namespace RegisterDataContracts;

/// <summary>
/// Monta o FQN da <c>table</c>/<c>view</c> no OpenMetadata a partir de um Data
/// Contract, usando o mesmo <c>serviceName</c>/<c>database</c> configurados em
/// <c>scripts/openmetadata/ingestion-postgres.yaml</c>.
/// </summary>
public static class TableFqnBuilder
{
    public const string ServiceName = "localize-stay-postgres";

    public const string Database = "localize_stay";

    public static string Build(DataContractInfo contract) =>
        $"{ServiceName}.{Database}.{contract.Schema}.{contract.DatasetName}";
}

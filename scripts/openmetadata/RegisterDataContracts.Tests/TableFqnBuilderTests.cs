using RegisterDataContracts;
using Xunit;

namespace RegisterDataContracts;

public sealed class TableFqnBuilderTests
{
    [Fact]
    public void Build_ProducesServiceDatabaseSchemaTableFqn()
    {
        var contract = new DataContractInfo(
            DatasetName: "reservation_calendar_v1",
            Version: "v1",
            OwnerDomain: "booking",
            Schema: "integration",
            Status: "rascunho",
            Description: "desc",
            SourceFile: "contracts/data-contracts/reservation_calendar_v1.md");

        var fqn = TableFqnBuilder.Build(contract);

        Assert.Equal("localize-stay-postgres.localize_stay.integration.reservation_calendar_v1", fqn);
    }
}

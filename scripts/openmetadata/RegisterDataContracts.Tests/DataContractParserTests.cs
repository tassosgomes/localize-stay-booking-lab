using RegisterDataContracts;
using Xunit;

namespace RegisterDataContracts;

/// <summary>
/// Testes unitários de <see cref="DataContractParser"/> (sem I/O — fixtures
/// inline espelham a forma real de
/// <c>contracts/data-contracts/reservation_calendar_v1.md</c> e de
/// <c>TEMPLATE.md</c>, para não acoplar o teste ao layout do repositório).
/// </summary>
public sealed class DataContractParserTests
{
    private const string WithExposedSchema = """
        # Data Contract — `reservation_calendar_v1`

        ## 1. Identificação

        | Campo | Valor |
        |-------|-------|
        | Nome do dataset | `reservation_calendar_v1` |
        | Versão | `v1` |
        | Domínio dono | `booking` |
        | Time responsável | Domínio Booking |
        | Schema PostgreSQL de origem | `booking` (`Reservation`) |
        | Schema de exposição | `integration` |
        | Estado | `rascunho` |

        ## 2. Descrição

        Snapshot atual das Reservations em estado terminal.

        ## 3. Schema

        (irrelevante para este teste)
        """;

    private const string WithoutExposedSchema = """
        # Data Contract — `example_v1`

        ## 1. Identificação

        | Campo | Valor |
        |-------|-------|
        | Nome do dataset | `example_v1` |
        | Versão | `v1` |
        | Domínio dono | `catalog` |
        | Schema PostgreSQL de origem | `catalog` |
        | Estado | `estável` |

        ## 2. Descrição

        Descrição de exemplo.
        """;

    [Fact]
    public void Parse_ReadsAllIdentificationFields()
    {
        var contract = DataContractParser.Parse(WithExposedSchema, "contracts/data-contracts/reservation_calendar_v1.md");

        Assert.Equal("reservation_calendar_v1", contract.DatasetName);
        Assert.Equal("v1", contract.Version);
        Assert.Equal("booking", contract.OwnerDomain);
        Assert.Equal("rascunho", contract.Status);
    }

    [Fact]
    public void Parse_PrefersExposedSchema_WhenPresent()
    {
        var contract = DataContractParser.Parse(WithExposedSchema, "sourcefile.md");

        Assert.Equal("integration", contract.Schema);
    }

    [Fact]
    public void Parse_FallsBackToOriginSchema_WhenNoExposedSchema()
    {
        var contract = DataContractParser.Parse(WithoutExposedSchema, "sourcefile.md");

        Assert.Equal("catalog", contract.Schema);
    }

    [Fact]
    public void Parse_ReadsDescriptionSection()
    {
        var contract = DataContractParser.Parse(WithExposedSchema, "sourcefile.md");

        Assert.Equal("Snapshot atual das Reservations em estado terminal.", contract.Description);
    }

    [Fact]
    public void Parse_KeepsSourceFile()
    {
        var contract = DataContractParser.Parse(WithExposedSchema, "contracts/data-contracts/reservation_calendar_v1.md");

        Assert.Equal("contracts/data-contracts/reservation_calendar_v1.md", contract.SourceFile);
    }

    [Fact]
    public void Parse_Throws_WhenRequiredFieldMissing()
    {
        const string missingDatasetName = """
            ## 1. Identificação

            | Campo | Valor |
            |-------|-------|
            | Domínio dono | `catalog` |
            | Schema PostgreSQL de origem | `catalog` |
            | Estado | `rascunho` |

            ## 2. Descrição

            Sem nome do dataset.
            """;

        Assert.Throws<InvalidOperationException>(() => DataContractParser.Parse(missingDatasetName, "sourcefile.md"));
    }
}

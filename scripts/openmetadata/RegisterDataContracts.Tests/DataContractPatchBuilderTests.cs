using System.Text.Json.Nodes;
using RegisterDataContracts;
using Xunit;

namespace RegisterDataContracts;

public sealed class DataContractPatchBuilderTests
{
    private static readonly DataContractInfo Contract = new(
        DatasetName: "reservation_calendar_v1",
        Version: "v1",
        OwnerDomain: "booking",
        Schema: "integration",
        Status: "rascunho",
        Description: "Snapshot atual das Reservations em estado terminal.",
        SourceFile: "contracts/data-contracts/reservation_calendar_v1.md");

    [Fact]
    public void BuildPatch_SetsDescriptionFromContract()
    {
        var patch = JsonNode.Parse(DataContractPatchBuilder.BuildPatch([], Contract))!.AsArray();

        var descriptionOp = patch.Single(op => op!["path"]!.GetValue<string>() == "/description");
        Assert.Equal("Snapshot atual das Reservations em estado terminal.", descriptionOp!["value"]!.GetValue<string>());
    }

    [Fact]
    public void BuildPatch_AddsLocalizeStayTag_WhenNoTagsExist()
    {
        var patch = JsonNode.Parse(DataContractPatchBuilder.BuildPatch([], Contract))!.AsArray();

        var tags = patch.Single(op => op!["path"]!.GetValue<string>() == "/tags")!["value"]!.AsArray();
        Assert.Contains(tags, t => t?["tagFQN"]?.GetValue<string>() == "localize-stay");
    }

    [Fact]
    public void BuildPatch_PreservesExistingUnrelatedTags()
    {
        var current = new JsonObject
        {
            ["tags"] = new JsonArray
            {
                new JsonObject { ["tagFQN"] = "outra-tag", ["source"] = "Classification", ["labelType"] = "Manual", ["state"] = "Confirmed" },
            },
        };

        var patch = JsonNode.Parse(DataContractPatchBuilder.BuildPatch(current, Contract))!.AsArray();

        var tags = patch.Single(op => op!["path"]!.GetValue<string>() == "/tags")!["value"]!.AsArray();
        Assert.Contains(tags, t => t?["tagFQN"]?.GetValue<string>() == "outra-tag");
        Assert.Contains(tags, t => t?["tagFQN"]?.GetValue<string>() == "localize-stay");
    }

    [Fact]
    public void BuildPatch_DoesNotDuplicateLocalizeStayTag_WhenAlreadyPresent()
    {
        var current = new JsonObject
        {
            ["tags"] = new JsonArray
            {
                new JsonObject { ["tagFQN"] = "localize-stay", ["source"] = "Classification", ["labelType"] = "Manual", ["state"] = "Confirmed" },
            },
        };

        var patch = JsonNode.Parse(DataContractPatchBuilder.BuildPatch(current, Contract))!.AsArray();

        var tags = patch.Single(op => op!["path"]!.GetValue<string>() == "/tags")!["value"]!.AsArray();
        Assert.Single(tags, t => t?["tagFQN"]?.GetValue<string>() == "localize-stay");
    }

    [Fact]
    public void BuildPatch_SetsAllFourCustomProperties()
    {
        var patch = JsonNode.Parse(DataContractPatchBuilder.BuildPatch([], Contract))!.AsArray();

        var extension = patch.Single(op => op!["path"]!.GetValue<string>() == "/extension")!["value"]!.AsObject();
        Assert.Equal("contracts/data-contracts/reservation_calendar_v1.md", extension["dataContractRef"]!.GetValue<string>());
        Assert.Equal("v1", extension["dataContractVersion"]!.GetValue<string>());
        Assert.Equal("rascunho", extension["dataContractStatus"]!.GetValue<string>());
        Assert.Equal("booking", extension["dataContractOwnerDomain"]!.GetValue<string>());
    }

    [Fact]
    public void BuildPatch_PreservesExistingUnrelatedCustomProperties()
    {
        var current = new JsonObject
        {
            ["extension"] = new JsonObject { ["someOtherProperty"] = "keep-me" },
        };

        var patch = JsonNode.Parse(DataContractPatchBuilder.BuildPatch(current, Contract))!.AsArray();

        var extension = patch.Single(op => op!["path"]!.GetValue<string>() == "/extension")!["value"]!.AsObject();
        Assert.Equal("keep-me", extension["someOtherProperty"]!.GetValue<string>());
    }
}

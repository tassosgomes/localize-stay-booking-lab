using System.Text.Json.Serialization;

namespace LocalizeStay.Catalog.Api.Contracts.Properties;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdatePropertyRequest
{
    private string? _name;
    private string? _location;

    [JsonIgnore]
    public bool NameIsPresent { get; private set; }

    [JsonIgnore]
    public bool LocationIsPresent { get; private set; }

    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            NameIsPresent = true;
        }
    }

    public string? Location
    {
        get => _location;
        set
        {
            _location = value;
            LocationIsPresent = true;
        }
    }
}

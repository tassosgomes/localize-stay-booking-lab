using LocalizeStay.Catalog.Api.Contracts.Properties;
using LocalizeStay.Catalog.Application.Properties;
using LocalizeStay.Catalog.Application.Properties.Models;
using LocalizeStay.Catalog.Domain.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LocalizeStay.Catalog.Api.Controllers;

[ApiController]
[Route("v1/properties")]
public sealed class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    private readonly IPropertyService _propertyService = propertyService;

    [HttpPost(Name = "createProperty")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PropertyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorHandling.CatalogProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ErrorHandling.CatalogProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> CreateAsync(
        [FromHeader(Name = "X-Host-Reference-Id"), BindRequired] Guid hostReferenceId,
        [FromBody] CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _propertyService.CreateAsync(
            new CreatePropertyInput(hostReferenceId, request.Name, request.Location),
            cancellationToken);

        var response = Map(result);
        return Created($"/v1/properties/{response.Id}", response);
    }

    private static PropertyResponse Map(PropertyResult result) =>
        new()
        {
            Id = result.Id,
            Name = result.Name,
            Location = result.Location,
            HostReferenceId = result.HostReferenceId,
            Status = result.Status switch
            {
                PropertyStatus.Active => "active",
                PropertyStatus.Inactive => "inactive",
                _ => result.Status.ToString().ToLowerInvariant()
            }
        };
}

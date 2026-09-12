using LocalizeStay.Booking.Api.ErrorHandling;
using LocalizeStay.Booking.Api.Extensions;
using LocalizeStay.Booking.Infra.Catalog;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCorsConfiguration(builder.Configuration)
    .AddSwaggerConfiguration()
    .AddPersistenceConfiguration(builder.Configuration)
    .AddMessagingConfiguration(builder.Configuration)
    .AddHealthCheckConfiguration(builder.Configuration)
    .AddApplicationConfiguration()
    .AddCatalogAvailabilityClient(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseApplicationPipeline(app.Environment);

app.Run();

public partial class Program;

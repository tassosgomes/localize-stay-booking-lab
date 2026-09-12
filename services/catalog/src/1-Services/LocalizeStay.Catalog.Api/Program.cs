using LocalizeStay.Catalog.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCorsConfiguration(builder.Configuration)
    .AddSwaggerConfiguration()
    .AddPersistenceConfiguration(builder.Configuration)
    .AddApplicationConfiguration()
    .AddErrorHandlingConfiguration()
    .AddHealthCheckConfiguration();

var app = builder.Build();

app.UseApplicationPipeline(app.Environment);

app.Run();

public partial class Program;

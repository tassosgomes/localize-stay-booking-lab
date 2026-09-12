using LocalizeStay.Booking.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCorsConfiguration(builder.Configuration)
    .AddSwaggerConfiguration()
    .AddPersistenceConfiguration(builder.Configuration)
    .AddMessagingConfiguration(builder.Configuration)
    .AddHealthCheckConfiguration(builder.Configuration);

var app = builder.Build();

app.UseApplicationPipeline(app.Environment);

app.Run();

public partial class Program;

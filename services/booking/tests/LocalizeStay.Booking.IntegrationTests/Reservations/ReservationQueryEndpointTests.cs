using System.Net;
using System.Text.Json;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

// V-01: os 5 cenários do AC de RF-01 via WebApplicationFactory real —
// Postgres Testcontainers da coleção. Seed via IReservationRepository.AddAsync
// (caminho normal de F01, produz solicitada/pendente); confirmada/autorizado e
// cancelada/rejeitado via UPDATE SQL direto (ExecuteSqlInterpolated), porque
// nenhum método de domínio para essas transições existe ainda (fica para F04).
// Dívida documentada na TechSpec: os helpers de seed devem ser
// revisados/substituídos quando F04 introduzir a transição real. O GET não
// toca Catalog, então nenhum fake é necessário.
[Collection("BookingIntegrationTests")]
public sealed class ReservationQueryEndpointTests(CustomWebApplicationFactory factory)
{
    private const string ProblemContentType = "application/problem+json";

    private readonly CustomWebApplicationFactory _factory = factory;

    private static Reservation CreateSeedReservation()
    {
        var facts = new AvailabilityFacts(
            Active: true,
            MaxGuests: 4,
            AvailableForPeriod: true,
            PricePerNight: 350.00m,
            Currency: "BRL");

        return Reservation.Create(
            Guid.NewGuid(),
            "guest-marina-alves",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 13),
            2,
            facts);
    }

    [Fact]
    public async Task Get_reservation_by_id_when_solicitada_returns_200_with_pendente_and_null_cancellationReason()
    {
        var reservation = await SeedAsync();

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/v1/reservations/{reservation.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        AssertReservationDetail(root, reservation);
        Assert.Equal("solicitada", root.GetProperty("status").GetString());
        Assert.Equal("pendente", root.GetProperty("sagaStatus").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("cancellationReason").ValueKind);
    }

    [Fact]
    public async Task Get_reservation_by_id_when_confirmada_returns_200_with_autorizado_and_null_cancellationReason()
    {
        var reservation = await SeedAsync();
        await UpdateStateAsync(reservation.Id, "confirmada", "Authorized", cancellationReason: null);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/v1/reservations/{reservation.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        AssertReservationDetail(root, reservation);
        Assert.Equal("confirmada", root.GetProperty("status").GetString());
        Assert.Equal("autorizado", root.GetProperty("sagaStatus").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("cancellationReason").ValueKind);
    }

    [Fact]
    public async Task Get_reservation_by_id_when_cancelada_returns_200_with_rejeitado_and_cancellationReason()
    {
        const string cancellationReason = "Pagamento rejeitado pela simulação de Payment.";
        var reservation = await SeedAsync();
        await UpdateStateAsync(reservation.Id, "cancelada", "Rejected", cancellationReason);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/v1/reservations/{reservation.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        AssertReservationDetail(root, reservation);
        Assert.Equal("cancelada", root.GetProperty("status").GetString());
        Assert.Equal("rejeitado", root.GetProperty("sagaStatus").GetString());
        Assert.Equal(cancellationReason, root.GetProperty("cancellationReason").GetString());
    }

    [Fact]
    public async Task Get_reservation_by_id_with_unknown_id_returns_404_RESERVATION_NOT_FOUND()
    {
        var unknownId = Guid.NewGuid();

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/v1/reservations/{unknownId}");

        await AssertRejectionAsync(
            response,
            HttpStatusCode.NotFound,
            "RESERVATION_NOT_FOUND",
            "Reservation não encontrada",
            $"/v1/reservations/{unknownId}");
    }

    [Fact]
    public async Task Get_reservation_by_id_with_malformed_id_returns_400_VALIDATION_ERROR()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/v1/reservations/nao-e-um-uuid");

        await AssertRejectionAsync(
            response,
            HttpStatusCode.BadRequest,
            "VALIDATION_ERROR",
            "Requisição inválida",
            "/v1/reservations/nao-e-um-uuid");
    }

    private static void AssertReservationDetail(JsonElement root, Reservation reservation)
    {
        Assert.Equal(reservation.Id, Guid.Parse(root.GetProperty("id").GetString()!));
        Assert.Equal(reservation.AccommodationId, Guid.Parse(root.GetProperty("accommodationId").GetString()!));
        Assert.Equal("guest-marina-alves", root.GetProperty("guestReference").GetString());
        Assert.Equal("2026-10-10", root.GetProperty("checkIn").GetString());
        Assert.Equal("2026-10-13", root.GetProperty("checkOut").GetString());
        Assert.Equal(2, root.GetProperty("guestsCount").GetInt32());
        Assert.Equal(JsonValueKind.String, root.GetProperty("pricePerNight").ValueKind);
        Assert.Equal("350.00", root.GetProperty("pricePerNight").GetString());
        Assert.Equal("BRL", root.GetProperty("currency").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("totalAmount").ValueKind);
        Assert.Equal("1050.00", root.GetProperty("totalAmount").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("createdAt").GetString()));
        Assert.Equal(reservation.Id, Guid.Parse(root.GetProperty("correlationId").GetString()!));
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private static async Task AssertRejectionAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedTitle,
        string expectedInstance)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            ProblemContentType,
            response.Content.Headers.ContentType?.MediaType);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.Equal(expectedInstance, root.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
    }

    private async Task<Reservation> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var reservation = CreateSeedReservation();

        await repository.AddAsync(reservation, CancellationToken.None);

        return reservation;
    }

    private async Task UpdateStateAsync(Guid id, string status, string sagaState, string? cancellationReason)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE booking.reservations SET status = {status} WHERE id = {id}");
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE booking.reservation_sagas SET state = {sagaState}, cancellation_reason = {cancellationReason} WHERE reservation_id = {id}");
    }
}

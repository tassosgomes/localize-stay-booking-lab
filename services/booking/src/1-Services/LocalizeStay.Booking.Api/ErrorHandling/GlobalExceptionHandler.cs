using System.Text.Json;
using FluentValidation;
using LocalizeStay.Booking.Domain.Reservations.Exceptions;
using LocalizeStay.Booking.Infra.Catalog;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Api.ErrorHandling;

// Traduz cada exceção de domínio/integração no status/code exatos do
// api-contract.yaml (RFC 9457, Content-Type application/problem+json). Nasce
// nesta task: a fundação da Fase 0 não tinha exceção de negócio para tratar.
// Rejeições 422 são logadas como warning; 5xx como error com traceId.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemTypeBase = "https://localize-stay.lab/problems/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, type, title, detail) = exception switch
        {
            ValidationException ex => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "validation-error",
                "Requisição inválida",
                string.Join(" ", ex.Errors.Select(error => error.ErrorMessage))),
            PeriodoInvalidoException => (
                StatusCodes.Status422UnprocessableEntity,
                "PERIODO_INVALIDO",
                "periodo-invalido",
                "Período inválido",
                "A data de check-out deve ser posterior à data de check-in."),
            QuantidadeHospedesInvalidaException => (
                StatusCodes.Status422UnprocessableEntity,
                "QUANTIDADE_HOSPEDES_INVALIDA",
                "quantidade-hospedes-invalida",
                "Quantidade de hóspedes inválida",
                "O número de hóspedes deve ser maior que zero."),
            AcomodacaoIndisponivelException => (
                StatusCodes.Status422UnprocessableEntity,
                "ACOMODACAO_INDISPONIVEL",
                "acomodacao-indisponivel",
                "Acomodação indisponível",
                "A Accommodation informada não existe ou não está ativa."),
            CapacidadeExcedidaException => (
                StatusCodes.Status422UnprocessableEntity,
                "CAPACIDADE_EXCEDIDA",
                "capacidade-excedida",
                "Capacidade excedida",
                "O número de hóspedes solicitado excede a capacidade máxima da Accommodation."),
            PeriodoIndisponivelException => (
                StatusCodes.Status422UnprocessableEntity,
                "PERIODO_INDISPONIVEL",
                "periodo-indisponivel",
                "Período indisponível",
                "A Accommodation não está disponível em todo o período solicitado."),
            ReservationNotFoundException => (
                StatusCodes.Status404NotFound,
                "RESERVATION_NOT_FOUND",
                "reservation-not-found",
                "Reservation não encontrada",
                "Nenhuma Reservation existe com o identificador informado."),
            CatalogUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "CATALOG_INDISPONIVEL",
                "catalog-indisponivel",
                "Não foi possível validar a solicitação no momento",
                "A consulta síncrona a Catalog falhou. Tente novamente em instantes."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                null,
                "Erro interno",
                "Ocorreu um erro interno. Tente novamente mais tarde.")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path.ToString(),
            Type = type is null ? "about:blank" : ProblemTypeBase + type
        };
        problem.Extensions["code"] = code;
        if (status >= 500)
        {
            problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        }

        if (status >= 500)
        {
            logger.LogError(
                exception,
                "Requisição {Instance} terminou em {Status} {Code} (traceId={TraceId})",
                problem.Instance,
                status,
                code,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Rejeição aplicada à requisição {Instance}: {Status} {Code}",
                problem.Instance,
                status,
                code);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response
            .WriteAsJsonAsync(problem, JsonOptions, "application/problem+json", cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}

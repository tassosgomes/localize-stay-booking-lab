using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

// Query da consulta de reserva (RF-01 de F02). Recebe o identificador como
// string bruta (não Guid): a validação de formato é responsabilidade desta
// query/validator, não do model binding — permite distinguir 400 de 404 sem
// depender de constraint de rota ":guid" (que geraria 404 do próprio
// roteamento em vez do 400 controlado pelo contrato).
public sealed record GetReservationByIdQuery(string ReservationId) : IQuery<Reservation>;

// API pública da feature Consulta de Reserva: consumidores importam
// exclusivamente ReservationLookupPage por aqui — módulos internos (api,
// components, validation) não são exportados (react-architecture; mesma
// convenção de reservation-request).
export { default as ReservationLookupPage } from './pages/ReservationLookupPage.tsx';

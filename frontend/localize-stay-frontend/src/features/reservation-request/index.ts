// API pública da feature Solicitação de Reserva: consumidores importam
// exclusivamente ReservationRequestPage por aqui — módulos internos (api,
// components, validation, errors, types) não são exportados (react-
// architecture; mesma convenção definida na techspec §Estrutura de Pastas).
export { default as ReservationRequestPage } from './pages/ReservationRequestPage.tsx';

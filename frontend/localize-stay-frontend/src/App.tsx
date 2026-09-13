import { useEffect, useState, type MouseEvent } from 'react';
import { ReservationLookupPage } from './features/reservation-lookup/index.ts';
import { PropertyWorkspacePage } from './features/property-registration/index.ts';
import { ReservationRequestPage } from './features/reservation-request/index.ts';
import ServiceStatus from './components/ServiceStatus/ServiceStatus.tsx';
import './App.css';

// Roteamento mínimo do frontend (ADR-003): sem biblioteca de router, a rota
// atual é lida de window.location.pathname (âncoras navegam via pushState;
// popstate cobre back/forward). `/properties` é a jornada de Cadastro de
// Property; `/reservations` é registrada pela API pública da feature de
// Solicitação de Reserva; `/reservations/consultar` é registrada pela API
// pública da feature de Consulta de Reserva (rota irmã, não
// `/reservations/:id` — a consulta parte de um identificador informado).
function currentPathname(): string {
  return window.location.pathname;
}

export default function App() {
  const [pathname, setPathname] = useState(currentPathname);

  useEffect(() => {
    function syncPath(): void {
      setPathname(currentPathname());
    }

    window.addEventListener('popstate', syncPath);
    return () => {
      window.removeEventListener('popstate', syncPath);
    };
  }, []);

  function navigate(event: MouseEvent<HTMLAnchorElement>, nextPath: string): void {
    event.preventDefault();
    if (currentPathname() === nextPath) {
      return;
    }
    window.history.pushState({}, '', nextPath);
    setPathname(nextPath);
  }

  const onReservations = pathname === '/reservations';
  const onReservationLookup = pathname === '/reservations/consultar';

  const navigation = (
    <nav className="app-nav" aria-label="Navegação principal">
      <a href="/" onClick={(event) => navigate(event, '/')}>
        Service status
      </a>
      <a href="/properties" onClick={(event) => navigate(event, '/properties')}>
        Cadastro de hospedagem
      </a>
      <a href="/reservations" onClick={(event) => navigate(event, '/reservations')}>
        Solicitar reserva
      </a>
      <a href="/reservations/consultar" onClick={(event) => navigate(event, '/reservations/consultar')}>
        Consultar reserva
      </a>
    </nav>
  );

  if (pathname === '/properties') {
    return (
      <div className="app-shell">
        {navigation}
        <main>
          <PropertyWorkspacePage />
        </main>
      </div>
    );
  }

  return (
    <>
      <div
        style={{
          display: 'flex',
          justifyContent: 'center',
          padding: 'var(--space-base) var(--space-base) 0',
        }}
      >
        {navigation}
      </div>
      <main
        style={{
          minHeight: '100%',
          display: 'flex',
          justifyContent: 'center',
          padding: 'var(--space-section) var(--space-base)',
        }}
      >
        {onReservations ? <ReservationRequestPage /> : onReservationLookup ? <ReservationLookupPage /> : <ServiceStatus />}
      </main>
    </>
  );
}

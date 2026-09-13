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
  const onProperties = pathname === '/properties';
  const onStatus = pathname === '/';

  const navigation = (
    <header className="top-nav">
      <div className="top-nav__container">
        <a href="/" className="top-nav__brand" onClick={(event) => navigate(event, '/')}>
          <svg
            className="top-nav__brand-icon"
            width="28"
            height="28"
            viewBox="0 0 32 32"
            fill="none"
            aria-hidden="true"
          >
            <path
              d="M16 3L3 13V28C3 28.5523 3.44772 29 4 29H11V19C11 17.8954 11.8954 17 13 17H19C20.1046 17 21 17.8954 21 19V29H28C28.5523 29 29 28.5523 29 28V13L16 3Z"
              fill="var(--color-primary)"
            />
          </svg>
          <span className="top-nav__brand-text">Localize Stay</span>
        </a>
        <nav className="app-nav top-nav__tabs" aria-label="Navegação principal">
          <a
            href="/properties"
            className={`top-nav__tab ${onProperties ? 'top-nav__tab--active' : ''}`}
            onClick={(event) => navigate(event, '/properties')}
          >
            Cadastro de hospedagem
          </a>
          <a
            href="/reservations"
            className={`top-nav__tab ${onReservations ? 'top-nav__tab--active' : ''}`}
            onClick={(event) => navigate(event, '/reservations')}
          >
            Solicitar reserva
          </a>
          <a
            href="/reservations/consultar"
            className={`top-nav__tab ${onReservationLookup ? 'top-nav__tab--active' : ''}`}
            onClick={(event) => navigate(event, '/reservations/consultar')}
          >
            Consultar reserva
          </a>
          <a
            href="/"
            className={`top-nav__tab ${onStatus ? 'top-nav__tab--active' : ''}`}
            onClick={(event) => navigate(event, '/')}
          >
            Service status
          </a>
        </nav>
      </div>
    </header>
  );

  return (
    <div className="app-shell">
      {navigation}
      <main className="app-main">
        {onReservations ? (
          <ReservationRequestPage />
        ) : onReservationLookup ? (
          <ReservationLookupPage />
        ) : onProperties ? (
          <PropertyWorkspacePage />
        ) : (
          <ServiceStatus />
        )}
      </main>
    </div>
  );
}

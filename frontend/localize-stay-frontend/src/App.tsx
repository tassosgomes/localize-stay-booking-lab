import { useEffect, useState, type MouseEvent } from 'react';
import { PropertyWorkspacePage } from './features/property-registration/index.ts';
import ServiceStatus from './components/ServiceStatus/ServiceStatus.tsx';
import './App.css';

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

  const navigation = (
    <nav className="app-nav" aria-label="Navegação principal">
      <a href="/" onClick={(event) => navigate(event, '/')}>
        Service status
      </a>
      <a href="/properties" onClick={(event) => navigate(event, '/properties')}>
        Cadastro de hospedagem
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
        <ServiceStatus />
      </main>
    </>
  );
}

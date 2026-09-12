import { useEffect, useState } from 'react';
import { getEnv } from '../../config/env.ts';
import { checkAllServices, type ServiceHealth } from '../../services/healthService.ts';
import './ServiceStatus.css';

// Única tela da fundação (V-04): busca GET /health/ready dos 3 serviços via
// fetch direto (CORS, sem gateway/BFF — ADR-003) e renderiza cada status.
export default function ServiceStatus() {
  const [services, setServices] = useState<ServiceHealth[] | null>(null);

  useEffect(() => {
    let cancelled = false;
    void checkAllServices(getEnv()).then((result) => {
      if (!cancelled) {
        setServices(result);
      }
    });
    return () => {
      cancelled = true;
    };
  }, []);

  if (services === null) {
    return <p className="status-card__loading">Loading service status…</p>;
  }

  return (
    <section className="status-card" aria-label="Service status">
      <h1 className="status-card__title">Service status</h1>
      <ul className="status-card__list">
        {services.map((service) => (
          <li
            key={service.name}
            className={`status-row${service.status === 'Unhealthy' ? ' status-row--unhealthy' : ''}`}
          >
            <span className="status-row__dot" aria-hidden="true" />
            {service.name}: {service.status}
            {service.status === 'Unhealthy' ? ` (${service.detail})` : null}
          </li>
        ))}
      </ul>
    </section>
  );
}

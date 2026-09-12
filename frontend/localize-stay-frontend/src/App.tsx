import ServiceStatus from './components/ServiceStatus/ServiceStatus.tsx';

export default function App() {
  return (
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
  );
}

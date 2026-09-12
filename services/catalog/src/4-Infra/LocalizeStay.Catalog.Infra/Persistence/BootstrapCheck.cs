namespace LocalizeStay.Catalog.Infra.Persistence;

// Sentinela descartável da fundação (V-01): prova que a migration inicial e a
// role do serviço funcionam no schema próprio. Sem semântica de negócio — o
// primeiro PRD de Catalog a substitui pela primeira migration real.
public sealed class BootstrapCheck
{
    public Guid Id { get; set; }
}

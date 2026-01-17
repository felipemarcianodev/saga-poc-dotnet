namespace SagaPoc.Common.DDD;

/// <summary>
/// Classe base para agregados raiz no padrão DDD.
/// Agrega eventos de domínio que são publicados quando mudanças acontecem.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<object> _eventosDominio = new();

    /// <summary>
    /// Obtém os eventos de domínio não publicados.
    /// </summary>
    public IReadOnlyCollection<object> EventosDominio => _eventosDominio.AsReadOnly();

    /// <summary>
    /// Adiciona um evento de domínio à lista de eventos pendentes.
    /// </summary>
    /// <param name="evento">Evento a ser adicionado.</param>
    protected void AdicionarEvento(object evento)
    {
        _eventosDominio.Add(evento);
    }

    /// <summary>
    /// Limpa todos os eventos de domínio (geralmente após publicação).
    /// </summary>
    public void LimparEventos()
    {
        _eventosDominio.Clear();
    }
}

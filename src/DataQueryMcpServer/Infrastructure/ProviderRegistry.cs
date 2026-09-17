using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Infrastructure;

/// <summary>
/// Resolves the single implementation of <typeparamref name="T"/> registered for a given <see cref="DbProvider"/>.
/// Shared by connection factories, schema readers, and statement classifiers so each provider-specific
/// concern is implemented once per provider without duplicating lookup/dictionary boilerplate.
/// </summary>
public sealed class ProviderRegistry<T> where T : class
{
    private readonly IReadOnlyDictionary<DbProvider, T> _itemsByProvider;

    public ProviderRegistry(IEnumerable<T> items, Func<T, DbProvider> providerSelector)
    {
        _itemsByProvider = items.ToDictionary(providerSelector);
    }

    public T Resolve(DbProvider provider)
    {
        if (!_itemsByProvider.TryGetValue(provider, out var item))
        {
            throw new NotSupportedException($"No {typeof(T).Name} is registered for provider '{provider}'.");
        }

        return item;
    }
}

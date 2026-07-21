#if WINDOWS7_LEGACY
namespace System.Collections.Generic;

internal static class LegacyCollectionExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> values, TKey key)
    {
        TValue value;
        return values.TryGetValue(key, out value) ? value : default;
    }
}
#endif

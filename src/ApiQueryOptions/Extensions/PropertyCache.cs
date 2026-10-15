using System.Collections.Concurrent;
using System.Reflection;

namespace ApiQueryOptions.Extensions;

/// <summary>
/// Looks up readable public instance properties by name, preferring an exact-case match and
/// falling back to a case-insensitive one. Each type is reflected over once; the cache grows
/// with the number of types queried, never with the property names clients send.
/// </summary>
internal static class PropertyCache
{
    private static readonly ConcurrentDictionary<Type, TypeProperties> _types = new();

    /// <summary>
    /// Returns the property of <paramref name="type"/> named <paramref name="name"/>, or
    /// <c>null</c> when there is no readable, non-indexed public instance property with that name.
    /// </summary>
    public static PropertyInfo? Find(Type type, string name) =>
        _types.GetOrAdd(type, static t => new TypeProperties(t)).Find(name);

    /// <summary>
    /// The readable properties of one type, indexed by exact and case-insensitive name.
    /// </summary>
    private sealed class TypeProperties
    {
        private readonly Dictionary<string, PropertyInfo> exact = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PropertyInfo> ignoreCase = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reflects over the public instance properties of <paramref name="type"/>.
        /// </summary>
        public TypeProperties(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || property.GetGetMethod() is null)
                {
                    continue;
                }

                Add(exact, property);
                Add(ignoreCase, property);
            }
        }

        /// <summary>
        /// Returns the exact-case match for <paramref name="name"/>, else a case-insensitive match, else <c>null</c>.
        /// </summary>
        public PropertyInfo? Find(string name) =>
            exact.TryGetValue(name, out PropertyInfo? property) || ignoreCase.TryGetValue(name, out property)
                ? property
                : null;

        private static void Add(Dictionary<string, PropertyInfo> properties, PropertyInfo property)
        {
            // A property re-declared with 'new' appears once per declaring type; the most derived one wins.
            if (!properties.TryGetValue(property.Name, out PropertyInfo? existing)
                || property.DeclaringType!.IsSubclassOf(existing.DeclaringType!))
            {
                properties[property.Name] = property;
            }
        }
    }
}

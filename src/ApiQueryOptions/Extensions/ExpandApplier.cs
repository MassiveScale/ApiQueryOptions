using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ApiQueryOptions.Options;

namespace ApiQueryOptions.Extensions;

/// <summary>
/// Lets <see cref="IQueryableExtensions.Apply{T}(IQueryable{T}, ApiQueryOptions{T})"/> apply
/// <c>$expand</c> through the <c>ApiQueryOptions.EntityFrameworkCore</c> package when it is installed,
/// without the core package referencing EF Core.
/// The EF Core method is found by name once and cached as a delegate per entity type.
/// </summary>
internal static class ExpandApplier
{
    internal const string EntityFrameworkCoreTypeName =
        "ApiQueryOptions.EntityFrameworkCore.Extensions.EFQueryableExtensions, ApiQueryOptions.EntityFrameworkCore";

    internal const string ApplyExpandMethodName = "ApplyExpand";

    private static readonly Lazy<MethodInfo?> applyExpandMethod = new(() => FindApplyExpandMethod(EntityFrameworkCoreTypeName));

    /// <summary>
    /// Applies <paramref name="expand"/> as EF Core <c>.Include()</c> calls, or returns
    /// <paramref name="query"/> unchanged when the EF Core package is not installed or
    /// <typeparamref name="T"/> is a value type.
    /// </summary>
    public static IQueryable<T> Apply<T>(IQueryable<T> query, ExpandQueryOption expand, ApiQueryOptionsSettings settings)
    {
        Func<IQueryable<T>, ExpandQueryOption, ApiQueryOptionsSettings, IQueryable<T>>? apply = Cache<T>.Apply;
        return apply is null ? query : apply(query, expand, settings);
    }

    /// <summary>
    /// Finds the generic <c>ApplyExpand</c> method on the type named by
    /// <paramref name="assemblyQualifiedTypeName"/>, or returns <c>null</c> when the type or method does not exist.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods,
        "ApiQueryOptions.EntityFrameworkCore.Extensions.EFQueryableExtensions", "ApiQueryOptions.EntityFrameworkCore")]
    internal static MethodInfo? FindApplyExpandMethod(string assemblyQualifiedTypeName)
    {
        Type? type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);

        return type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(m => m.Name == ApplyExpandMethodName
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 3);
    }

    /// <summary>
    /// Holds the per-type delegate. The runtime initializes it once, thread-safely, on first use.
    /// </summary>
    private static class Cache<T>
    {
        public static readonly Func<IQueryable<T>, ExpandQueryOption, ApiQueryOptionsSettings, IQueryable<T>>? Apply = Create();

        private static Func<IQueryable<T>, ExpandQueryOption, ApiQueryOptionsSettings, IQueryable<T>>? Create()
        {
            MethodInfo? method = applyExpandMethod.Value;

            // EF Core's Include requires a reference type.
            if (method is null || typeof(T).IsValueType)
            {
                return null;
            }

            return method.MakeGenericMethod(typeof(T))
                .CreateDelegate<Func<IQueryable<T>, ExpandQueryOption, ApiQueryOptionsSettings, IQueryable<T>>>();
        }
    }
}

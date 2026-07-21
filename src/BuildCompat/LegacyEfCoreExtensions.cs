#if WINDOWS7_LEGACY
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

internal static class LegacyEfCoreExtensions
{
    // EF Core 3.1 predates these convenience APIs. SQLite still receives the
    // same explicit decimal column definition used by the modern build.
    public static PropertyBuilder<TProperty> HasPrecision<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        int precision,
        int scale)
    {
        propertyBuilder.HasColumnType("decimal(" + precision + "," + scale + ")");
        return propertyBuilder;
    }

    public static void Clear(this ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries())
            entry.State = EntityState.Detached;
    }
}
#endif

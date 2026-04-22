using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ZeroAlloc.ValueObjects.EfCore;

public static class TypedIdModelConfigurationExtensions
{
    /// <summary>
    /// Scans the given assembly (or the calling assembly) for <c>[TypedId]</c> structs and registers
    /// a <see cref="TypedIdValueConverter{TId,TBacking}"/> for each on the model configuration builder,
    /// so EF Core stores them as their backing type without per-property <c>HasConversion</c>.
    /// </summary>
    public static ModelConfigurationBuilder AddTypedIdConventions(
        this ModelConfigurationBuilder builder,
        Assembly? scan = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        scan ??= Assembly.GetCallingAssembly();

        var typedIdAttr = typeof(TypedIdAttribute);
        var propertiesMethodOpen = FindPropertiesMethodOpen();

        foreach (var t in scan.GetTypes())
        {
            if (!t.IsValueType) continue;
            var attrs = t.GetCustomAttributes(typedIdAttr, inherit: false);
            if (attrs.Length == 0) continue;

            // Find the Value property to determine backing type at runtime
            var valueProp = t.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueProp is null) continue;

            var backing = valueProp.PropertyType;
            if (backing != typeof(Guid) && backing != typeof(long)) continue;

            // Invoke builder.Properties<TId>().HaveConversion<TypedIdValueConverter<TId, TBacking>>()
            // via reflection — EF Core's fluent API is generic.
            var propertiesMethod = propertiesMethodOpen.MakeGenericMethod(t);
            var propBuilder = propertiesMethod.Invoke(builder, null);
            if (propBuilder is null) continue;

            var converterType = typeof(TypedIdValueConverter<,>).MakeGenericType(t, backing);

            var haveConv = FindHaveConversionOpen(propBuilder.GetType());
            if (haveConv is null) continue;
            haveConv.MakeGenericMethod(converterType).Invoke(propBuilder, null);
        }

        return builder;
    }

    private static MethodInfo FindPropertiesMethodOpen()
    {
        var methods = typeof(ModelConfigurationBuilder)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (var m in methods)
        {
            if (!string.Equals(m.Name, "Properties", StringComparison.Ordinal)) continue;
            if (!m.IsGenericMethodDefinition) continue;
            if (m.GetParameters().Length != 0) continue;
            return m;
        }

        throw new InvalidOperationException(
            "ModelConfigurationBuilder.Properties<T>() not found — unexpected EF Core version.");
    }

    private static MethodInfo? FindHaveConversionOpen(Type propBuilderType)
    {
        var methods = propBuilderType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (var m in methods)
        {
            if (!string.Equals(m.Name, "HaveConversion", StringComparison.Ordinal)) continue;
            if (!m.IsGenericMethodDefinition) continue;
            if (m.GetGenericArguments().Length != 1) continue;
            if (m.GetParameters().Length != 0) continue;
            return m;
        }

        return null;
    }
}

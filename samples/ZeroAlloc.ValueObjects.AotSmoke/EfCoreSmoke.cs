using System;
using Microsoft.EntityFrameworkCore;
using ZeroAlloc.ValueObjects.EfCore;

namespace ZeroAlloc.ValueObjects.AotSmoke;

// ZeroAlloc.ValueObjects.EfCore under NativeAOT, as far as EF Core 9 allows without suppressing
// its own warnings. EF Core marks the DbContext constructor RequiresUnreferencedCode and
// RequiresDynamicCode, and ModelConfigurationBuilder can only be constructed through EF-internal
// API, so no model can be built here while TreatWarningsAsErrors stays on. What can be covered is:
//  * the generated registration: AddZeroAllocValueObjectsAotSmokeTypedIdConventions is called, so
//    ILC compiles it and every AddTypedIdConvention it calls, with their closed
//    Properties<TId>().HaveConversion<TypedIdValueConverter<TId, TBacking>>() calls, and the trim
//    and AOT analysis of the publish checks them. Without a builder it can run only to its
//    argument check;
//  * TypedIdValueConverter, which EF Core runs on every read and write of a typed-ID column: it is
//    built and run both ways, Guid- and Int64-backed.
internal static class EfCoreSmoke
{
    public static string? Run()
    {
        try
        {
            ZeroAllocValueObjectsAotSmokeTypedIdModelConfigurationExtensions.AddZeroAllocValueObjectsAotSmokeTypedIdConventions(null!);
            return "EF Core: the generated registration accepted a null builder";
        }
        catch (ArgumentNullException e) when (string.Equals(e.ParamName, "builder", StringComparison.Ordinal))
        {
            // Expected: the registration was compiled ahead of time and ran its argument check.
        }

        var order = OrderId.New();
        var orderConverter = new TypedIdValueConverter<OrderId, Guid>();
        if (orderConverter.ConvertToProvider(order) is not Guid guid || guid != order.Value)
            return "EF Core: TypedIdValueConverter<OrderId, Guid> wrote the wrong value";
        if (orderConverter.ConvertFromProvider(guid) is not OrderId orderRound || orderRound != order)
            return "EF Core: TypedIdValueConverter<OrderId, Guid> read the wrong value";

        var message = SnowflakeMessageId.New();
        var messageConverter = new TypedIdValueConverter<SnowflakeMessageId, long>();
        if (messageConverter.ConvertToProvider(message) is not long raw || raw != message.Value)
            return "EF Core: TypedIdValueConverter<SnowflakeMessageId, long> wrote the wrong value";
        if (messageConverter.ConvertFromProvider(raw) is not SnowflakeMessageId messageRound || messageRound != message)
            return "EF Core: TypedIdValueConverter<SnowflakeMessageId, long> read the wrong value";

        return null;
    }
}

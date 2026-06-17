using Microsoft.Kiota.Abstractions.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WingetIntune.Extensions;

public static class StringExtensions
{
    public static async Task<T?> ParseJson<T>(this string serializedContent, CancellationToken cancellationToken) where T : IParsable
    {
        var deserializer = new Microsoft.Kiota.Serialization.Json.JsonParseNodeFactory();
        using var memoryStream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(serializedContent));
        var jsonParseNode = await deserializer.GetRootParseNodeAsync(deserializer.ValidContentType, memoryStream, cancellationToken);


        return jsonParseNode.GetObjectValue<T>(GetFactoryFromType<T>());
    }

    private static ParsableFactory<T> GetFactoryFromType<T>() where T : IParsable
    {
        var type = typeof(T);
        var factoryMethod = Array.Find(type.GetMethods(), static x => x.IsStatic && "CreateFromDiscriminatorValue".Equals(x.Name, StringComparison.OrdinalIgnoreCase)) ??
                            throw new InvalidOperationException($"No factory method found for type {type.Name}");
        return (ParsableFactory<T>)factoryMethod.CreateDelegate(typeof(ParsableFactory<T>));
    }
}

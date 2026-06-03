using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prismedia.Contracts.Jellyfin;

/// <summary>
/// Serializes <see cref="Guid"/> values in Jellyfin's wire format: the dashless "N" form (32 hex
/// characters). Real Jellyfin emits every id this way (see its <c>JsonGuidConverter</c>), and strict
/// clients such as Manet decode ids with a parser that expects exactly that shape — a dashed GUID
/// fails to decode and the whole item is dropped. Reading accepts either form.
/// </summary>
public sealed class JellyfinGuidConverter : JsonConverter<Guid> {
    /// <inheritdoc />
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? Guid.Empty : Guid.Parse(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("N", CultureInfo.InvariantCulture));
}

/// <summary>
/// Serializes dates in Jellyfin's wire format: UTC with a trailing "Z" and 7 fractional digits
/// (e.g. <c>2026-06-02T14:10:52.1797219Z</c>). System.Text.Json's default for <see cref="DateTimeOffset"/>
/// emits a numeric offset (<c>+00:00</c>); strict clients with a fixed date parser reject that and drop
/// the item. Reading accepts any ISO 8601 form.
/// </summary>
public sealed class JellyfinDateConverter : JsonConverter<DateTimeOffset?> {
    private const string JellyfinFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    /// <inheritdoc />
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options) {
        if (value is { } date) {
            writer.WriteStringValue(date.UtcDateTime.ToString(JellyfinFormat, CultureInfo.InvariantCulture));
        } else {
            writer.WriteNullValue();
        }
    }
}

/// <summary>Nullable companion to <see cref="JellyfinGuidConverter"/>; emits null as JSON null.</summary>
public sealed class JellyfinNullableGuidConverter : JsonConverter<Guid?> {
    /// <inheritdoc />
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? null : Guid.Parse(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options) {
        if (value is { } guid) {
            writer.WriteStringValue(guid.ToString("N", CultureInfo.InvariantCulture));
        } else {
            writer.WriteNullValue();
        }
    }
}

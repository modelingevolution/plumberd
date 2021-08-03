using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelingEvolution.Plumberd.Serialization
{
    /// <summary>
    /// <see cref="T:System.Text.Json.Serialization.JsonConverterFactory" /> to convert <see cref="T:System.TimeSpan" /> to and from strings. Supports <see cref="T:System.Nullable`1" />.
    /// </summary>
    /// <remarks>
    /// TimeSpans are transposed using the constant ("c") format specifier: [-][d.]hh:mm:ss[.fffffff].
    /// </remarks>
    public class JsonTimeSpanConverter : JsonConverterFactory
    {
        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert == typeof(TimeSpan))
                return true;
            return typeToConvert.IsGenericType && JsonTimeSpanConverter.IsNullableTimeSpan(typeToConvert);
        }

        /// <inheritdoc />
        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return !typeToConvert.IsGenericType ? (JsonConverter)new JsonTimeSpanConverter.JsonStandardTimeSpanConverter() : (JsonConverter)new JsonTimeSpanConverter.JsonNullableTimeSpanConverter();
        }

        private static bool IsNullableTimeSpan(Type typeToConvert)
        {
            Type underlyingType = Nullable.GetUnderlyingType(typeToConvert);
            return underlyingType != (Type)null && underlyingType == typeof(TimeSpan);
        }

        internal class JsonStandardTimeSpanConverter : JsonConverter<TimeSpan>
        {
            /// <inheritdoc />
            public override TimeSpan Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                return reader.TokenType == JsonTokenType.String ? TimeSpan.ParseExact(reader.GetString(), "c", (IFormatProvider)CultureInfo.InvariantCulture) : throw new JsonException();
            }

            /// <inheritdoc />
            public override void Write(
                Utf8JsonWriter writer,
                TimeSpan value,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString("c", (IFormatProvider)CultureInfo.InvariantCulture));
            }
        }

        internal class JsonNullableTimeSpanConverter : JsonConverter<TimeSpan?>
        {
            /// <inheritdoc />
            public override TimeSpan? Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                return reader.TokenType == JsonTokenType.String ? new TimeSpan?(TimeSpan.ParseExact(reader.GetString(), "c", (IFormatProvider)CultureInfo.InvariantCulture)) : throw new JsonException();
            }

            /// <inheritdoc />
            public override void Write(
                Utf8JsonWriter writer,
                TimeSpan? value,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.Value.ToString("c", (IFormatProvider)CultureInfo.InvariantCulture));
            }
        }
    }
}
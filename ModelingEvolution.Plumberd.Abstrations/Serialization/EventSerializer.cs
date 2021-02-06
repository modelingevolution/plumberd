using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelingEvolution.Plumberd.Metadata;


namespace ModelingEvolution.Plumberd.Serialization
{
    
    public interface IMetadataSerializerFactory
    {
        IMetadataSerializer Get(IContext context);
        IMetadataSerializer Get(ContextScope context);
        void RegisterSchemaForContext(IMetadataSchema schema, ContextScope contextType);
        void RegisterSerializerForContext(IMetadataSerializer schema, ContextScope contextType);
    }

    public class MetadataSerializerFactory : IMetadataSerializerFactory
    {
        private readonly Dictionary<ContextScope, IMetadataSerializer> _schemas;
        private Lazy<IMetadataSerializer> _genericMetadataSerializer;
        public MetadataSerializerFactory()
        {
            _schemas = new Dictionary<ContextScope, IMetadataSerializer>();
            _genericMetadataSerializer = new Lazy<IMetadataSerializer>(CreateGenericSerializer);
        }

        private IMetadataSerializer CreateGenericSerializer()
        {
            var enrichers = _schemas.Values
                .SelectMany(x => x.Schema.Enrichers.Values.Select(x=>x.Clone()))
                .ToArray();
                                
            var schema = new MetadataSchema();
            schema.IgnoreDuplicates();

            foreach(var i in enrichers)
                i.RegisterSchema(schema);
            
            return new MetadataSerializer(schema);
        }

        public IMetadataSerializer Get(ContextScope context)
        {
            return _schemas[context];
        }
        public IMetadataSerializer Get(IContext context)
        {
            if (context is ICommandHandlerContext)
                return _schemas[ContextScope.Command];
            else if (context is IEventHandlerContext)
                return _schemas[ContextScope.Event];
            else if (context is ICommandInvocationContext)
                return _schemas[ContextScope.Invocation];
            return _genericMetadataSerializer.Value;
        }
        public void RegisterSerializerForContext(IMetadataSerializer schema, ContextScope contextType)
        {
            _schemas.Add(contextType, schema);
        }
        public void RegisterSchemaForContext(IMetadataSchema schema, ContextScope contextType)
        {
            RegisterSerializerForContext(new MetadataSerializer(schema),contextType );
        }
    }

    public interface IMetadataSerializer
    {
        IMetadataSchema Schema { get; }
        byte[] Serialize(IMetadata m);
        IMetadata Deserialize(byte[] data);
    }
    public interface IRecordSerializer
    {
        byte[] Serialize(IRecord ev, IMetadata m);
        IRecord Deserialize(byte[] data, IMetadata m);
    }

    
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

    public sealed class RecordSerializer : IRecordSerializer
    {
        private JsonSerializerOptions _options;
        public RecordSerializer()
        {
            _options = new JsonSerializerOptions();
            _options.Converters.Add(new JsonTimeSpanConverter());
            
        }
        //private static readonly JsonSerializerSettings JSON_SETTINGS = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };
        public byte[] Serialize(IRecord ev, IMetadata m)
        {
            if (ev is ILink l)
            {
                return Encoding.UTF8.GetBytes($"{l.SourceStreamPosition}@{l.SourceCategory}-{l.SourceStreamId}");
            } 
            else 
                return JsonSerializer.SerializeToUtf8Bytes(ev, ev.GetType(), _options);
        }

        public IRecord Deserialize(byte[] data, IMetadata m)
        {
            return (IRecord)JsonSerializer.Deserialize(data.AsSpan(), m.TryResolveNativeType(), _options);
        }
    }
    public sealed class MetadataSerializer : IMetadataSerializer
    {
        public IMetadataSchema Schema { get; }
        private readonly JsonSerializerOptions _options;
        public MetadataSerializer(IMetadataSchema schema)
        {
            Schema = schema;
            _options = new JsonSerializerOptions();
            _options.Converters.Add(new MetadataJsonConverter(schema));
        }

        public byte[] Serialize( IMetadata m)
        {
            return JsonSerializer.SerializeToUtf8Bytes(m, typeof(IMetadata), _options);
        }

        public IMetadata Deserialize(byte[] data)
        {
            return JsonSerializer.Deserialize<IMetadata>(new ReadOnlySpan<byte>(data), _options);
        }
    }

    public class MetadataJsonConverter : JsonConverter<IMetadata>
    {
        private readonly IMetadataSchema _schema;
        private readonly JsonSerializerOptions _options;
        public MetadataJsonConverter(IMetadataSchema schema)
        {
            _schema = schema;
            _options = new JsonSerializerOptions();
            _options.Converters.Add(new JsonTimeSpanConverter());
        }

        public override IMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            IMetadata m = new Metadata.Metadata(_schema, true);
            while (reader.Read())
            {
                if(reader.TokenType == JsonTokenType.EndObject)
                {
                    return m;
                }

                // Get the key.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                string propertyName = reader.GetString();
                var property = _schema[propertyName];
                if (property != null)
                {
                    var value = JsonSerializer.Deserialize(ref reader, property.Type, _options);
                    m[property] = value;
                }
            }

            return m;
        }

        public override void Write(Utf8JsonWriter writer, IMetadata value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var i in value.Schema.WriteProperties)
            {
                var v = value[i];
                writer.WritePropertyName(i.Name);
                JsonSerializer.Serialize(writer, v, i.Type, _options);
            }
            writer.WriteEndObject();
        }
    }
}

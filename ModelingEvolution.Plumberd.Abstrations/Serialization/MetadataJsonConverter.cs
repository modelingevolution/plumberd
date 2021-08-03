﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Serialization
{
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
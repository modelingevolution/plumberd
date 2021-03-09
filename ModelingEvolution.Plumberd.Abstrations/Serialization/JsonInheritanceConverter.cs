using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProtoBuf.Meta;


namespace ModelingEvolution.Plumberd.Serialization
{
    public class JsonInheritanceConverter<TBaseType> : JsonConverter<TBaseType>
        where TBaseType : class
    {
        private readonly Dictionary<string, Type> _typeIndex;

        public JsonInheritanceConverter()
        {
            var model = RuntimeTypeModel.Default;
            var type = typeof(TBaseType);
            if (model.IsDefined(type))
            {
                this._typeIndex = model[type].GetSubtypes().ToDictionary(x => x.DerivedType.Type.Name, x => x.DerivedType.Type);
            }
            else this._typeIndex = new Dictionary<string, Type>();
        }
        public override TBaseType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) return null;
            if (!reader.Read()) return null;
            if (reader.TokenType != JsonTokenType.PropertyName) return null;

            var type = reader.GetString();
            if (!reader.Read()) return null;

            TBaseType result = null;
            if (_typeIndex.TryGetValue(type, out var t))
                result = (TBaseType)JsonSerializer.Deserialize(ref reader, t);

            reader.Read();
            return result;
        }

        public override void Write(Utf8JsonWriter writer, TBaseType value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(value.GetType().Name.ToString());

            JsonSerializer.Serialize(writer, value, value.GetType());

            writer.WriteEndObject();
        }
    }
}

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vocalith.Scribe
{
    internal sealed class SerializedFieldConverter : JsonConverter<SerializedField>
    {
        public override void WriteJson(JsonWriter writer, SerializedField value, JsonSerializer serializer)
        {
            var obj = new JObject
            {
                ["Type"] = JToken.FromObject(value.Type, serializer)
            };

            if (value.Type == FieldType.Node)
            {
                obj["Node"] = value.Node != null ? JToken.FromObject(value.Node, serializer) : null;
            }
            else
            {
                obj["Value"] = value.Value != null ? JToken.FromObject(value.Value, serializer) : null;
            }

            obj.WriteTo(writer);
        }

        public override SerializedField ReadJson(JsonReader reader, Type objectType, SerializedField existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var type = obj["Type"].ToObject<FieldType>();
            var field = new SerializedField { Type = type };

            if (type == FieldType.Node)
            {
                field.Node = obj["Node"]?.ToObject<NodeFrame>(serializer);
            }
            else
            {
                if (obj.TryGetValue("Value", out var valToken))
                {
                    field.Value = valToken.ToObject<object>(serializer);
                }
            }

            return field;
        }
    }
}

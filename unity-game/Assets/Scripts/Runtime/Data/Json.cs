using System.Collections.Generic;
using Newtonsoft.Json;

namespace TFY.CatAdventure.Data
{
    /// <summary>One JSON configuration for every file and every Firebase payload.</summary>
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace, // lists/dicts from JSON replace defaults instead of appending
            FloatParseHandling = FloatParseHandling.Double,
        };

        public static string Write(object o, bool pretty = false) => JsonConvert.SerializeObject(o, pretty ? Formatting.Indented : Formatting.None, Settings);
        public static T Read<T>(string s) => JsonConvert.DeserializeObject<T>(s, Settings);

        /// <summary>Firebase SDK objects (IDictionary / IList trees) → typed C# objects via a JSON round trip.</summary>
        public static T Convert<T>(object firebaseValue) => Read<T>(Write(firebaseValue));

        /// <summary>Typed object → Dictionary tree for Firebase callables / Firestore writes.</summary>
        public static Dictionary<string, object> ToDictionary(object o) => JsonConvert.DeserializeObject<Dictionary<string, object>>(Write(o), new JsonSerializerSettings { Converters = { new PlainObjectConverter() } });

        /// <summary>Turns nested JObject/JArray into Dictionary/List (what the Firebase SDK expects).</summary>
        private class PlainObjectConverter : JsonConverter
        {
            public override bool CanConvert(System.Type t) => t == typeof(object);
            public override bool CanWrite => false;
            public override void WriteJson(JsonWriter w, object v, JsonSerializer s) => throw new System.NotSupportedException();
            public override object ReadJson(JsonReader r, System.Type t, object existing, JsonSerializer s)
            {
                switch (r.TokenType)
                {
                    case JsonToken.StartObject:
                        var d = new Dictionary<string, object>();
                        while (r.Read() && r.TokenType != JsonToken.EndObject) { var key = (string)r.Value; r.Read(); d[key] = ReadJson(r, t, null, s); }
                        return d;
                    case JsonToken.StartArray:
                        var list = new List<object>();
                        while (r.Read() && r.TokenType != JsonToken.EndArray) list.Add(ReadJson(r, t, null, s));
                        return list;
                    default: return r.Value;
                }
            }
        }
    }
}

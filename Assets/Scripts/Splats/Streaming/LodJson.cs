// Metadata validation adapted from UnitySplats (MIT); parsing uses project Newtonsoft.Json.
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace Hypocycloid.Reverie.Splats
{
    internal static class LodJson
    {
        public static object Parse(string json)
        {
            using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json))
                { DateParseHandling = Newtonsoft.Json.DateParseHandling.None, MaxDepth = 256 };
            return ConvertToken(Newtonsoft.Json.Linq.JToken.Load(reader));
        }

        static object ConvertToken(Newtonsoft.Json.Linq.JToken token)
        {
            if (token is Newtonsoft.Json.Linq.JObject obj)
            {
                var result = new Dictionary<string, object>();
                foreach (var property in obj.Properties()) result.Add(property.Name, ConvertToken(property.Value));
                return result;
            }
            if (token is Newtonsoft.Json.Linq.JArray array)
            {
                var result = new List<object>(array.Count);
                foreach (var value in array) result.Add(ConvertToken(value));
                return result;
            }
            return ((Newtonsoft.Json.Linq.JValue)token).Value;
        }

        public static Dictionary<string, object> Object(object value, string name) =>
            value as Dictionary<string, object> ??
            throw new FormatException($"JSON member '{name}' must be an object.");

        public static Dictionary<string, object> OptionalObject(
            this Dictionary<string, object> value, string name)
        {
            return value.TryGetValue(name, out object child) && child != null
                ? Object(child, name)
                : null;
        }

        public static List<object> Array(object value, string name) =>
            value as List<object> ?? throw new FormatException($"JSON member '{name}' must be an array.");

        public static string String(object value, string name) =>
            value as string ?? throw new FormatException($"JSON member '{name}' must be a string.");

        public static int Int(object value, string name)
        {
            long number = value switch
            {
                long i => i,
                double d when Math.Abs(d - Math.Round(d)) < 1e-9 => (long)Math.Round(d),
                _ => throw new FormatException($"JSON member '{name}' must be an integer."),
            };
            if (number < int.MinValue || number > int.MaxValue)
                throw new FormatException($"JSON member '{name}' is outside Int32 range.");
            return (int)number;
        }

        public static float Float(object value, string name)
        {
            double number = value switch
            {
                long i => i,
                double d => d,
                _ => throw new FormatException($"JSON member '{name}' must be numeric."),
            };
            if (double.IsNaN(number) || double.IsInfinity(number) ||
                number < -float.MaxValue || number > float.MaxValue)
                throw new FormatException($"JSON member '{name}' is not a finite float.");
            return (float)number;
        }

        public static int RequiredInt(this Dictionary<string, object> value, string name) =>
            value.TryGetValue(name, out object child)
                ? Int(child, name)
                : throw new FormatException($"Required JSON member '{name}' is missing.");

        public static int OptionalInt(this Dictionary<string, object> value, string name, int fallback) =>
            value.TryGetValue(name, out object child) && child != null ? Int(child, name) : fallback;

        public static bool OptionalBool(this Dictionary<string, object> value, string name, bool fallback) =>
            value.TryGetValue(name, out object child) && child != null
                ? child is bool b ? b : throw new FormatException($"JSON member '{name}' must be Boolean.")
                : fallback;

        public static List<object> RequiredArray(this Dictionary<string, object> value, string name) =>
            value.TryGetValue(name, out object child)
                ? Array(child, name)
                : throw new FormatException($"Required JSON member '{name}' is missing.");

        public static float[] FloatArray(this Dictionary<string, object> value, string name, int expected = -1)
        {
            List<object> array = value.RequiredArray(name);
            if (expected >= 0 && array.Count != expected)
                throw new FormatException($"JSON member '{name}' must contain {expected} values.");
            var result = new float[array.Count];
            for (int i = 0; i < result.Length; i++) result[i] = Float(array[i], $"{name}[{i}]");
            return result;
        }

        public static string[] StringArray(this Dictionary<string, object> value, string name)
        {
            List<object> array = value.RequiredArray(name);
            var result = new string[array.Count];
            for (int i = 0; i < result.Length; i++) result[i] = String(array[i], $"{name}[{i}]");
            return result;
        }
    }
}

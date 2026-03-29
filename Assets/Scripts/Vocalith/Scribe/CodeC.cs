using System;
using System.Collections.Generic;
using Vocalith.Scribe;


namespace Vocalith.Scribe
{
    public interface ICodec<T>
    {
        FieldType FieldType { get; }            // 该类型使用的类型码
        object Write(in T value);               // 转成 JSON 友好的结构
        T Read(object value);                   // 从 JSON 结构读取
    }
    public static class CodecRegistry
    {
        private static readonly Dictionary<Type, object> _map = new();

        public static void Register<T>(ICodec<T> c) => _map[typeof(T)] = c;

        public static bool TryGet<T>(out ICodec<T> c)
        {
            if (_map.TryGetValue(typeof(T), out var o)) { c = (ICodec<T>)o; return true; }
            c = null; return false;
        }
    }

    // 注册内置类型的 Codec
    // Bool
public sealed class BoolCodec : ICodec<bool>
{
    public FieldType FieldType => FieldType.Bool;
    public object Write(in bool v) => v;
    public bool Read(object value) => value is bool b ? b : false;
}

// Int
public sealed class IntCodec : ICodec<int>
{
    public FieldType FieldType => FieldType.Int32;
    public object Write(in int v) => v;
    public int Read(object value) => Convert.ToInt32(value);
}

// Float
public sealed class FloatCodec : ICodec<float>
{
    public FieldType FieldType => FieldType.Single;
    public object Write(in float v) => v;
    public float Read(object value) => Convert.ToSingle(value);
}

// String (null 支持)
public sealed class StringCodec : ICodec<string>
{
    public FieldType FieldType => FieldType.String;
    public object Write(in string v) => v;
    public string Read(object value) => value as string;
}

public sealed class LongCodec : ICodec<long>
{
    public FieldType FieldType => FieldType.Int64;
    public object Write(in long v) => v;
    public long Read(object value) => Convert.ToInt64(value);
}

// Enum<T> （统一写成 Int32）
public sealed class EnumCodec<T> : ICodec<T> where T : struct, Enum
{
    public FieldType FieldType => FieldType.EnumInt32;
    public object Write(in T v) => Convert.ToInt32(v);
    public T Read(object value) => (T)Enum.ToObject(typeof(T), Convert.ToInt32(value));
}

    // Dictionary<string,string>
    public sealed class DictStrStrCodec : ICodec<Dictionary<string, string>>
    {
        public FieldType FieldType => FieldType.DictStrStr; // 之前已定义
        public object Write(in Dictionary<string, string> dict) => dict;
        public Dictionary<string, string> Read(object value)
        {
            if (value == null) return null;
            if (value is Dictionary<string, string> dict) return dict;
            if (value is Newtonsoft.Json.Linq.JObject obj) return obj.ToObject<Dictionary<string, string>>();
            return null;
        }

        
    }
    // Dictionary<string,float>
    public sealed class DictStrFloatCodec : ICodec<Dictionary<string, float>>
    {
        public FieldType FieldType => FieldType.DictStrFloat; // 之前已定义
        public object Write(in Dictionary<string, float> dict) => dict;
        public Dictionary<string, float> Read(object value)
        {
            if (value == null) return null;
            if (value is Dictionary<string, float> dict) return dict;
            if (value is Newtonsoft.Json.Linq.JObject obj) return obj.ToObject<Dictionary<string, float>>();
            return null;
        }
    }
    // Dictionary<string,EnumInt32>
    public sealed class DictStrEnumInt32Codec<T> : ICodec<Dictionary<string, T>> where T : struct, Enum
    {
        public FieldType FieldType => FieldType.DictStrEnumInt32; // 之前已定义
        public object Write(in Dictionary<string, T> dict) => dict;
        public Dictionary<string, T> Read(object value)
        {
            if (value == null) return null;
            if (value is Dictionary<string, T> dict) return dict;
            if (value is Newtonsoft.Json.Linq.JObject obj)
            {
                var raw = obj.ToObject<Dictionary<string, int>>();
                var result = new Dictionary<string, T>();
                foreach (var kv in raw) result[kv.Key] = (T)Enum.ToObject(typeof(T), kv.Value);
                return result;
            }
            return null;
        }
    }
}
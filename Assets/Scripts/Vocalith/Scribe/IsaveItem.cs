

using System;
using System.Collections.Generic;

namespace Vocalith.Scribe
{
    public interface ISaveItem : IExposable
    {
        string TypeId { get; }
    }

    /// <summary>类型注册表：限制可反序列化的类型，避免任意反射构造（安全）</summary>
    public static class PolymorphRegistry
    {
        private static readonly Dictionary<string, Func<ISaveItem>> _ctors = new();

        public static void Register<T>(string typeId) where T : ISaveItem, new()
            => _ctors[typeId] = static () => new T();

        public static bool TryCreate(string typeId, out ISaveItem obj)
        {
            if (_ctors.TryGetValue(typeId, out var f)) { obj = f(); return true; }
            obj = null; return false;
        }
    }
}
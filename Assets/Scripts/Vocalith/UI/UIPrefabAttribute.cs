using System;

namespace Vocalith.UI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UIPrefabAttribute : Attribute
    {
        public string Path { get; }
        public UIPrefabAttribute(string path) => Path = path;
    }
}
using System.Collections.Generic;
using System.IO;

namespace Vocalith.Scribe
{
        public static class Scribe_Deep
    {
        public static void Look<T>(string tag, ref T obj) where T : class, IExposable, new()
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                if (obj == null) { Scribe.WriteField(FieldType.Null, tag, null); return; }
                using var node = new Scribe.NodeScope(tag);
                obj.ExposeData();
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec)) { obj = null; return; }
                if (rec.Type == FieldType.Null) { obj = null; return; }
                if (rec.Type != FieldType.Node) { obj = null; return; }
                using var node = new Scribe.NodeScope(tag);
                obj ??= new T();
                obj.ExposeData();
            }
        }
    }


}

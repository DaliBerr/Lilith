

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Vocalith.Scribe
{
     public static class Scribe_Polymorph
    {
        /// <summary>List&lt;ISaveItem&gt;：元素作为 Node 写入，前置写入 TypeId（string）</summary>
        public static void LookList(string tag, ref List<ISaveItem> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                if (list == null) { Scribe.WriteField(FieldType.ListPoly, tag, null); return; }

                var payload = new List<Dictionary<string, object>>(list.Count);

                foreach (var it in list)
                {
                    var typeId = it?.TypeId ?? string.Empty;
                    if (it == null)
                    {
                        payload.Add(new Dictionary<string, object> { { "TypeId", typeId }, { "Node", null } });
                        continue;
                    }

                    var frame = Scribe.CaptureNode(it.ExposeData);
                    payload.Add(new Dictionary<string, object> { { "TypeId", typeId }, { "Node", frame } });
                }

                Scribe.WriteField(FieldType.ListPoly, tag, payload);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListPoly) { list = null; return; }

                List<Dictionary<string, object>> payload = null;
                if (rec.Value is JArray arr)
                    payload = arr.ToObject<List<Dictionary<string, object>>>();
                else if (rec.Value is IEnumerable<Dictionary<string, object>> dicts)
                    payload = dicts.ToList();

                if (payload == null) { list = null; return; }
                list = new List<ISaveItem>(payload.Count);
                foreach (var entry in payload)
                {
                    var typeId = entry.TryGetValue("TypeId", out var tid) ? tid as string : null;
                    if (string.IsNullOrEmpty(typeId)) { list.Add(null); continue; }
                    if (!PolymorphRegistry.TryCreate(typeId, out var obj)) { list.Add(null); continue; }
                    var frame = entry.TryGetValue("Node", out var nodeObj) ? nodeObj as NodeFrame : null;
                    if (frame == null && entry.TryGetValue("Node", out var maybeToken) && maybeToken is JToken token)
                        frame = token.ToObject<NodeFrame>();

                    Scribe.WithFrame(frame, obj.ExposeData);
                    list.Add(obj);
                }
            }
            else throw new System.InvalidOperationException();
        }
    }
}

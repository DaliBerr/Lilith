using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Vocalith.Scribe
{
        public static class Scribe_Collections
    {
        public static void Look(string tag, ref List<int> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                Scribe.WriteField(FieldType.ListInt, tag, list);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListInt) { list = null; return; }
                if (rec.Value is JArray arr)
                {
                    list = arr.ToObject<List<int>>();
                }
                else if (rec.Value is IEnumerable<int> ints)
                {
                    list = new List<int>(ints);
                }
                else list = null;
            }
        }
        public static void Look(string tag, ref List<string> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                Scribe.WriteField(FieldType.ListStr, tag, list);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListStr) { list = null; return; }
                if (rec.Value is JArray arr)
                {
                    list = arr.ToObject<List<string>>();
                }
                else if (rec.Value is IEnumerable<string> strs)
                {
                    list = new List<string>(strs);
                }
                else list = null;
            }
        }

        /// <summary>
        /// 读写普通 JSON DTO 列表，不要求元素实现 IExposable。
        /// </summary>
        /// <param name="tag">字段标签。</param>
        /// <param name="list">待读写的 DTO 列表。</param>
        /// <typeparam name="T">可被 Json.NET 直接序列化的 DTO 类型。</typeparam>
        /// <returns>无</returns>
        public static void LookJsonList<T>(string tag, ref List<T> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                Scribe.WriteField(FieldType.ListJson, tag, list);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListJson) { list = null; return; }
                if (rec.Value == null) { list = null; return; }
                if (rec.Value is JArray arr)
                {
                    list = arr.ToObject<List<T>>();
                }
                else if (rec.Value is IEnumerable<T> items)
                {
                    list = new List<T>(items);
                }
                else list = null;
            }
        }

        public static void LookDeep<T>(string tag, ref List<T> list) where T : class, IExposable, new()
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                if (list == null) { Scribe.WriteField(FieldType.ListDeep, tag, null); return; }
                var nodes = new List<NodeFrame>(list.Count);
                foreach (var item in list)
                {
                    if (item == null)
                    {
                        nodes.Add(null);
                        continue;
                    }
                    nodes.Add(Scribe.CaptureNode(item.ExposeData));
                }
                Scribe.WriteField(FieldType.ListDeep, tag, nodes);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListDeep) { list = null; return; }
                var nodes = rec.Value switch
                {
                    JArray arr => arr.ToObject<List<NodeFrame>>(),
                    IEnumerable<NodeFrame> n => n.ToList(),
                    _ => null
                };

                if (nodes == null) { list = null; return; }
                list = new List<T>(nodes.Count);
                foreach (var nf in nodes)
                {
                    if (nf == null) { list.Add(null); continue; }
                    var t = new T();
                    Scribe.WithFrame(nf, t.ExposeData);
                    list.Add(t);
                }
            }
        }
    }
}

using System.Collections.Generic;

namespace Vocalith.Scribe
{
    internal sealed class NodeFrame
    {
        // 一个 tag 只保留“最后一次出现”的字段，足够覆盖“改名/新增/替换”的常见场景
        public Dictionary<string, SerializedField> Fields { get; set; } = new();

        public void Set(string tag, SerializedField field) => Fields[tag] = field;

        public bool TryGet(string tag, out SerializedField rec) => Fields.TryGetValue(tag, out rec);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Vocalith.Scribe
{
    public enum ScribeMode { Inactive, Saving, Loading }

    public enum FieldType : byte
    {
        Null = 0,
        Int32 = 1,
        Single = 2,
        Bool = 3,
        String = 4,
        EnumInt32 = 5,
        Int64 = 6,

        DictStrInt = 10,
        DictStrFloat = 11,
        DictStrBool = 12,
        DictStrStr = 13,
        DictStrEnumInt32 = 14,

        Node = 20, // 对象/嵌套帧
        ListInt = 30,
        ListFloat = 31,
        ListBool = 32,
        ListStr = 33,
        ListDeep = 34, // payload 里是 N 个 Node 串联
        ListJson = 35, // payload 里是可直接 JSON 序列化的普通 DTO 列表

        RefId = 40, // 引用（string id）
        ListRefId = 41,
        ListPoly = 63,
    }

    public interface IExposable { void ExposeData(); }

    internal sealed class SerializedField
    {
        public FieldType Type { get; set; }
        public object Value { get; set; }
        public NodeFrame Node { get; set; }
    }

    internal sealed class SaveDocument
    {
        public int Version { get; set; }
        public NodeFrame Root { get; set; }
    }

    public static class Scribe
    {
        public static ScribeMode mode { get; private set; } = ScribeMode.Inactive;
        internal static int fileVersion;
        public static int FileVersion => fileVersion;

        private static readonly JsonSerializerSettings _serializerSettings = new()
        {
            Formatting = Formatting.Indented,
            Converters = { new SerializedFieldConverter() }
        };

        // 写入
        private static StreamWriter _rootWriter;

        // JSON document & node stack
        private static SaveDocument _document;
        private static readonly Stack<NodeFrame> _frameStack = new();

        // ========== 生命周期 ==========
        public static void InitSaving(Stream stream, int version = 1)
        {
            if (mode != ScribeMode.Inactive) throw new InvalidOperationException("Scribe already active.");
            _rootWriter = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
            mode = ScribeMode.Saving;
            fileVersion = version;
            _document = new SaveDocument { Version = fileVersion, Root = new NodeFrame() };
            _frameStack.Clear();
            _frameStack.Push(_document.Root);
        }

        public static void InitLoading(Stream stream)
        {
            if (mode != ScribeMode.Inactive) throw new InvalidOperationException("Scribe already active.");
            mode = ScribeMode.Loading;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            if (TryParseJsonDocument(data, out var doc))
            {
                _document = doc;
                fileVersion = doc.Version;
                _frameStack.Clear();
                _frameStack.Push(doc.Root ?? new NodeFrame());
            }
            else
            {
                mode = ScribeMode.Inactive;
                throw new InvalidDataException("Unsupported save file format. JSON-only loader does not accept legacy binary/TLV data.");
            }
        }

        public static void FinalizeWriting()
        {
            if (mode != ScribeMode.Saving) return;
            if (_document != null)
            {
                var json = JsonConvert.SerializeObject(_document, _serializerSettings);
                _rootWriter.Write(json);
                _rootWriter.Flush();
            }
            _frameStack.Clear();
            _document = null;
            _rootWriter = null;
            mode = ScribeMode.Inactive;
        }

        public static void FinalizeLoading()
        {
            if (mode != ScribeMode.Loading) return;
            _frameStack.Clear();
            _document = null;
            mode = ScribeMode.Inactive;
        }

        // ========== Node 作用域（保存/读取都可用） ==========
        public sealed class NodeScope : IDisposable
        {
            private readonly string _tag;
            private readonly bool _isSaving;
            private readonly NodeFrame _childFrame;
            private bool _disposed;

            // 保存：在父 frame 上写一个 Node，其内容为 child frame
            public NodeScope(string tag)
            {
                _tag = tag;
                _isSaving = (mode == ScribeMode.Saving);
                if (_isSaving)
                {
                    _childFrame = new NodeFrame();
                    _frameStack.Push(_childFrame);
                }
                else
                {
                    var cur = _frameStack.Peek();
                    if (cur.TryGet(tag, out var rec) && rec.Type == FieldType.Node)
                    {
                        if (rec.Node != null) _frameStack.Push(rec.Node);
                        else if (rec.Value is NodeFrame nf)
                            _frameStack.Push(nf);
                        else
                            _frameStack.Push(new NodeFrame());
                    }
                    else
                    {
                        _frameStack.Push(new NodeFrame());
                    }
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (_isSaving)
                {
                    _frameStack.Pop();
                    var parent = _frameStack.Peek();
                    parent.Set(_tag, new SerializedField { Type = FieldType.Node, Node = _childFrame });
                }
                else
                {
                    _frameStack.Pop();
                }
            }
        }

        private static bool TryParseJsonDocument(byte[] data, out SaveDocument doc)
        {
            doc = null;
            try
            {
                var text = Encoding.UTF8.GetString(data);

                // ★ 去掉 UTF-8 BOM（\uFEFF），防止挡在最前面
                if (text.Length > 0 && text[0] == '\uFEFF')
                {
                    text = text.Substring(1);
                }

                var trimmed = text.TrimStart();
                if (!trimmed.StartsWith("{")) return false;

                doc = JsonConvert.DeserializeObject<SaveDocument>(text, _serializerSettings);
                return doc != null;
            }
            catch
            {
                doc = null;
                return false;
            }
        }

        // ========== 根对象 Look ==========
        // 把根对象放进标签 "__root" 的 Node 里
        public static void Look<T>(ref T obj) where T : class, IExposable, new()
        {
            if (mode == ScribeMode.Saving)
            {
                using var root = new NodeScope("__root");
                Scribe_Deep.Look("root", ref obj);
            }
            else if (mode == ScribeMode.Loading)
            {
                using var root = new NodeScope("__root");
                Scribe_Deep.Look("root", ref obj);
            }
            else throw new InvalidOperationException("Scribe not active.");
        }

        internal static void WriteField(FieldType t, string tag, object value, NodeFrame node = null)
        {
            _frameStack.Peek().Set(tag, new SerializedField { Type = t, Value = value, Node = node });
        }

        // 读“当前帧”里的字段
        internal static bool TryGetField(string tag, out SerializedField rec)
            => _frameStack.Peek().TryGet(tag, out rec);

        /// <summary>
        /// 在独立节点里执行 expose，并返回捕获到的节点数据。
        /// </summary>
        /// <param name="expose">向当前节点写入数据的回调。</param>
        /// <returns>捕获到的节点。</returns>
        internal static NodeFrame CaptureNode(Action expose)
        {
            var frame = new NodeFrame();
            _frameStack.Push(frame);
            try
            {
                expose?.Invoke();
                return frame;
            }
            finally
            {
                _frameStack.Pop();
            }
        }

        /// <summary>
        /// 在指定节点上下文中执行 expose，用于读取嵌套节点。
        /// </summary>
        /// <param name="frame">要临时压栈的节点。</param>
        /// <param name="expose">在该节点上执行的读写回调。</param>
        /// <returns>无</returns>
        internal static void WithFrame(NodeFrame frame, Action expose)
        {
            _frameStack.Push(frame ?? new NodeFrame());
            try
            {
                expose?.Invoke();
            }
            finally
            {
                _frameStack.Pop();
            }
        }

        public static class Scribe_Generic
    {
        public static void Look<T>(string tag, ref T value, T defaultValue = default, bool forceSave = false)
        {
            var ty = typeof(T);

            // 类型门禁：遇到复杂对象，提示改用 Deep/Refs
            if (typeof(ILoadReferenceable).IsAssignableFrom(ty))
                throw new InvalidOperationException($"Type {ty.Name} is reference-like. Use Scribe_Refs.Look.");
            if (typeof(IExposable).IsAssignableFrom(ty))
                throw new InvalidOperationException($"Type {ty.Name} implements IExposable. Use Scribe_Deep.Look.");

            if (Scribe.mode == ScribeMode.Saving)
            {
                if (!forceSave)
                {
                    if (value == null && defaultValue == null) return;
                    if (value != null && value.Equals(defaultValue)) return;
                }

                if (value == null) { Scribe.WriteField(FieldType.Null, tag, null); return; }

                if (!CodecRegistry.TryGet<T>(out var codec))
                    throw new NotSupportedException($"No codec registered for {ty.Name}.");

                var toWrite = value;
                Scribe.WriteField(codec.FieldType, tag, codec.Write(in toWrite));
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec))
                {
                    value = defaultValue;
                    return;
                }
                if (rec.Type == FieldType.Null) { value = default; return; }

                if (!CodecRegistry.TryGet<T>(out var codec))
                    throw new NotSupportedException($"No codec registered for {ty.Name}.");

                value = codec.Read(rec.Value);
            }
            else throw new InvalidOperationException("Scribe not active.");
        }

        // public static void LookRef<TRef>(string tag, ref TRef obj) where TRef : class, ILoadReferenceable
        //     => Scribe_Refs.Look(tag, ref obj);

        public static void LookDeep<TDeep>(string tag, ref TDeep obj) where TDeep : class, IExposable, new()
            => Scribe_Deep.Look(tag, ref obj);
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using Vocalith.Logging;


namespace Vocalith.EventSystem
{
    //Not for public use
    public sealed class EventBus {
        private readonly Dictionary<Type, List<Delegate>> _map = new();
        // private readonly object _gate = new();

        public IDisposable Subscribe<T>(Action<T> h) {
            // lock (_gate) {
            if (!_map.TryGetValue(typeof(T), out var list)) _map[typeof(T)] = list = new();
            list.Add(h);
            // }
            return new Disposer(() => Unsubscribe(h));
        }

        public void Unsubscribe<T>(Action<T> h) {
            // lock (_gate) {
                if (_map.TryGetValue(typeof(T), out var list)) list.Remove(h);
            //}
        }

        public void Publish<T>(T evt) {
            Delegate[] snapshot;
            // lock (_gate) {
                if (!_map.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
                snapshot = list.ToArray();
            //}
            for (int i = 0; i < snapshot.Length; i++) {
                try { ((Action<T>)snapshot[i]).Invoke(evt); }
                catch (Exception ex)
                {
                    GameDebug.LogError($"EventBus handler for {typeof(T)} threw exception: {ex}");
                    Log.Error($"EventBus handler for {typeof(T)} threw exception: {ex}");
                }
            }
        }

        private sealed class Disposer : IDisposable {
            private Action? d; public Disposer(Action d) => this.d = d;
            public void Dispose() { d?.Invoke(); d = null; }
        }
    }
}
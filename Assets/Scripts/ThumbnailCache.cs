using System.Collections.Generic;
using UnityEngine;

namespace DogaShiwakeru
{
    /// <summary>
    /// 動画パスをキーとしたサムネイル Texture2D のメモリキャッシュ。
    /// セッション中は保持し続け、ファイル削除・移動時は明示的に除去する。
    /// </summary>
    public static class ThumbnailCache
    {
        private static readonly Dictionary<string, Texture2D> _cache =
            new Dictionary<string, Texture2D>();

        public static bool TryGet(string path, out Texture2D texture)
        {
            return _cache.TryGetValue(path, out texture);
        }

        public static bool HasEntry(string path) => _cache.ContainsKey(path);

        public static void Store(string path, Texture2D texture)
        {
            // 既存エントリがあれば古いテクスチャを破棄してから上書き
            if (_cache.TryGetValue(path, out var old) && old != null)
                Object.Destroy(old);
            _cache[path] = texture;
        }

        /// <summary>
        /// ファイル削除・移動時に呼ぶ。テクスチャを GPU メモリごと解放する。
        /// </summary>
        public static void Remove(string path)
        {
            if (_cache.TryGetValue(path, out var tex))
            {
                if (tex != null) Object.Destroy(tex);
                _cache.Remove(path);
            }
        }

        public static void Clear()
        {
            foreach (var tex in _cache.Values)
            {
                if (tex != null) Object.Destroy(tex);
            }
            _cache.Clear();
        }
    }
}

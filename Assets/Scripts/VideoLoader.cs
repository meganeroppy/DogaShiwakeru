using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DogaShiwakeru
{
    public class VideoLoader
    {
        private static readonly string[] VideoExtensions = { "*.mp4" };

        // ConcurrentDictionary for thread-safe access from background scan tasks
        private readonly ConcurrentDictionary<string, bool> _dirContainsVideosCache = new ConcurrentDictionary<string, bool>();

        public void ClearDirectoryCache()
        {
            _dirContainsVideosCache.Clear();
        }

        public List<string> LoadVideosFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError($"Directory not found: {directoryPath}");
                return new List<string>();
            }

            var videoFiles = Directory.GetFiles(directoryPath, "*.mp4").ToList();
            return videoFiles;
        }

        public bool DirectoryContainsVideos(string directoryPath, int maxDepth = 2, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested) return false;
            if (_dirContainsVideosCache.TryGetValue(directoryPath, out bool cached))
                return cached;
            bool result = DirectoryContainsVideosRecursive(directoryPath, 0, maxDepth, ct);
            if (!ct.IsCancellationRequested)
                _dirContainsVideosCache[directoryPath] = result;
            return result;
        }

        private bool DirectoryContainsVideosRecursive(string directoryPath, int currentDepth, int maxDepth, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return false;
            if (currentDepth >= maxDepth) return true;

            try
            {
                foreach (var extension in VideoExtensions)
                {
                    if (ct.IsCancellationRequested) return false;
                    if (Directory.EnumerateFiles(directoryPath, extension).Any()) return true;
                }

                foreach (var subDir in Directory.EnumerateDirectories(directoryPath))
                {
                    if (DirectoryContainsVideosRecursive(subDir, currentDepth + 1, maxDepth, ct)) return true;
                }
            }
            catch
            {
                // Silently ignore — called from background thread, no Unity API allowed
                return false;
            }

            return false;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DogaShiwakeru
{
    public class VideoLoader
    {
        private static readonly string[] VideoExtensions = { "*.mp4" }; // Keep as array for future expansion

        public List<string> LoadVideosFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError($"Directory not found: {directoryPath}");
                return new List<string>();
            }

            // Non-recursive search for the main display
            var videoFiles = Directory.GetFiles(directoryPath, "*.mp4")
                                      .ToList();
            
            return videoFiles;
        }

        public bool DirectoryContainsVideos(string directoryPath, int maxDepth = 2)
        {
            return DirectoryContainsVideosRecursive(directoryPath, 0, maxDepth);
        }

        private bool DirectoryContainsVideosRecursive(string directoryPath, int currentDepth, int maxDepth)
        {
            if (currentDepth >= maxDepth)
            {
                // Reached max depth, assume it *might* have videos without searching further.
                return true;
            }

            try
            {
                // Check for video files at the current level.
                foreach (var extension in VideoExtensions)
                {
                    if (Directory.EnumerateFiles(directoryPath, extension).Any())
                    {
                        return true; // Found a video, no need to go deeper.
                    }
                }

                // If no videos at this level, check subdirectories.
                foreach (var subDir in Directory.EnumerateDirectories(directoryPath))
                {
                    if (DirectoryContainsVideosRecursive(subDir, currentDepth + 1, maxDepth))
                    {
                        return true; // Found a video in a subdirectory.
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not access directory {directoryPath}: {ex.Message}");
                return false; // Assume no videos if access is denied.
            }

            // Scanned all allowed depths and found no videos.
            return false;
        }
    }
}
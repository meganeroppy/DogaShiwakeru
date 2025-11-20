using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DogaShiwakeru
{
    public class VideoLoader
    {
        public List<string> LoadVideosFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError($"Directory not found: {directoryPath}");
                return new List<string>();
            }

            var videoFiles = Directory.GetFiles(directoryPath, "*.mp4")
                                      .ToList();

            Debug.Log($"Found {videoFiles.Count} video files in {directoryPath}");
            return videoFiles;
        }
    }
}

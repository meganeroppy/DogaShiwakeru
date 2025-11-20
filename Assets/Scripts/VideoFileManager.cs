using System.IO;
using UnityEngine;

namespace DogaShiwakeru
{
    public class VideoFileManager
    {
        public bool MoveVideoFile(string sourceFilePath, string destinationDirectory, bool createDirectory = true)
        {
            if (!File.Exists(sourceFilePath))
            {
                Debug.LogError($"Source file not found: {sourceFilePath}");
                return false;
            }

            if (createDirectory && !Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
                Debug.Log($"Created directory: {destinationDirectory}");
            }
            else if (!Directory.Exists(destinationDirectory))
            {
                Debug.LogError($"Destination directory does not exist: {destinationDirectory}");
                return false;
            }

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath = Path.Combine(destinationDirectory, fileName);

            try
            {
                File.Move(sourceFilePath, destinationFilePath);
                Debug.Log($"Moved file from {sourceFilePath} to {destinationFilePath}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error moving file {sourceFilePath} to {destinationFilePath}: {ex.Message}");
                return false;
            }
        }

        public bool DeleteVideoFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"File not found for deletion: {filePath}");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Debug.Log($"Permanently deleted file: {filePath}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error deleting file {filePath}: {ex.Message}");
                return false;
            }
        }
    }
}

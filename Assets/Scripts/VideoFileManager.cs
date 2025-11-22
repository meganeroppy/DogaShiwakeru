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
        public bool RenameVideoFile(string sourceFilePath, string newFileName)
        {
            if (!File.Exists(sourceFilePath))
            {
                Debug.LogError($"Source file not found: {sourceFilePath}");
                return false;
            }
            
            // Basic validation for the new file name to prevent directory traversal issues.
            if (string.IsNullOrEmpty(newFileName) || newFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogError($"Invalid new file name: {newFileName}");
                return false;
            }

            string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
            string destinationFilePath = Path.Combine(sourceDirectory, newFileName);

            if (File.Exists(destinationFilePath))
            {
                 Debug.LogError($"A file with the name {newFileName} already exists in the directory.");
                return false;
            }

            try
            {
                File.Move(sourceFilePath, destinationFilePath);
                Debug.Log($"Renamed file from {sourceFilePath} to {destinationFilePath}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error renaming file {sourceFilePath}: {ex.Message}");
                return false;
            }
        }
    }
}

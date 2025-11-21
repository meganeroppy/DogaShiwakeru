using UnityEngine;
using System.Collections.Generic;
using TMPro; // For TextMeshPro if used, otherwise UnityEngine.UI.Text
using SFB;
using System.IO;

namespace DogaShiwakeru
{
    public class MainController : MonoBehaviour
    {
        // Reference to the VideoLoader
        private VideoLoader _videoLoader;
        private VideoFileManager _videoFileManager;
        public VideoGridManager videoGridManager; // Assign in Inspector
        public TextMeshProUGUI videoCountText; // Assign in Inspector (or UnityEngine.UI.Text)
        public RectTransform canvasRectTransform; // Assign the main Canvas RectTransform in Inspector

        private string _currentVideoDirectory;
        private bool _isMuted = false; // Global mute state

        void Start()
        {
            Debug.Log("MainController started. Opening directory selection dialog.");
            _videoLoader = new VideoLoader();
            _videoFileManager = new VideoFileManager();
            OpenDirectoryDialog();
        }

        private const string LAST_VIDEO_DIRECTORY_KEY = "LastVideoDirectory";

        private void OpenDirectoryDialog()
        {
            Debug.Log("Attempting to open synchronous folder panel...");

            string initialPath = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, "");
            if (!Directory.Exists(initialPath))
            {
                initialPath = ""; // Reset if the saved path no longer exists
            }

            var paths = SFB.StandaloneFileBrowser.OpenFolderPanel("Select Video Directory", initialPath, false);
            Debug.Log($"Synchronous folder panel returned. Number of paths: {paths.Length}");

            if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                // The native dialog can return a path padded with null characters. Trim them.
                string rawPath = paths[0].TrimEnd('\0');
                Debug.Log($"Raw path from dialog (trimmed): '{rawPath}'");
                _currentVideoDirectory = Path.GetFullPath(rawPath);
                Debug.Log($"Normalized path: '{_currentVideoDirectory}'");

                PlayerPrefs.SetString(LAST_VIDEO_DIRECTORY_KEY, _currentVideoDirectory);
                PlayerPrefs.Save(); // Ensure the preference is saved to disk

                LoadVideos(_currentVideoDirectory);
            }
            else
            {
                Debug.LogWarning("Directory selection cancelled or no directory selected.");
                // Optionally, you could add code here to quit the application or prompt the user again.
            }
        }

        private void LoadVideos(string directoryPath)
        {
            var videoFiles = _videoLoader.LoadVideosFromDirectory(directoryPath);
            if (videoGridManager != null)
            {
                videoGridManager.DisplayVideos(videoFiles);
                UpdateVideoCountDisplay(videoFiles.Count);
                ApplyGlobalMuteState(); // Apply mute state after loading videos
            }
            else
            {
                Debug.LogError("VideoGridManager is not assigned in MainController.");
            }
            Debug.Log($"Number of video files loaded: {videoFiles.Count}");
        }

        private void UpdateVideoCountDisplay(int count)
        {
            if (videoCountText != null)
            {
                videoCountText.text = $"Videos: {count}";
            }
            else
            {
                Debug.LogWarning("Video count TextMeshProUGUI not assigned in MainController.");
            }
        }

        private void ApplyGlobalMuteState()
        {
            for (int i = 0; i < videoGridManager.GetVideoCount(); i++)
            {
                VideoPlayerUI videoUI = videoGridManager.GetVideoUI(i);
                if (videoUI != null && videoGridManager.GetSelectedVideoIndex() != i)
                {
                    videoUI.SetMute(_isMuted);
                }
            }
            // Ensure the selected video's mute state is handled by its selection logic
        }

        void Update()
        {
            if (videoGridManager == null) return;

            // Handle selection movement
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                videoGridManager.MoveSelection(-1, _isMuted);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                videoGridManager.MoveSelection(1, _isMuted);
            }

            // Handle deselect all
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                videoGridManager.DeselectAll();
            }

            // Handle 'Delete Candidate' (D key) and 'Immediate Delete' (Shift+D)
            if (Input.GetKeyDown(KeyCode.D))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null && !string.IsNullOrEmpty(_currentVideoDirectory))
                {
                    string sourcePath = selectedVideo.GetVideoPath();

                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        // Immediate Delete (Shift+D)
                        if (_videoFileManager.DeleteVideoFile(sourcePath))
                        {
                            Debug.Log($"Video permanently deleted: {sourcePath}");
                            LoadVideos(_currentVideoDirectory); // Refresh the list
                        }
                    }
                    else
                    {
                        // Delete Candidate (D key)
                        string delFolderPath = Path.Combine(_currentVideoDirectory, "del");

                        if (_videoFileManager.MoveVideoFile(sourcePath, delFolderPath))
                        {
                            Debug.Log($"Video moved to 'del' folder: {sourcePath}");
                            LoadVideos(_currentVideoDirectory); // Refresh the list
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("No video selected or current directory not set for delete operation.");
                }
            }

            // Handle 'Nice Video' (N key)
            if (Input.GetKeyDown(KeyCode.N))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null && !string.IsNullOrEmpty(_currentVideoDirectory))
                {
                    string sourcePath = selectedVideo.GetVideoPath();
                    string niceFolderPath = Path.Combine(_currentVideoDirectory, "nice");

                    if (_videoFileManager.MoveVideoFile(sourcePath, niceFolderPath))
                    {
                        Debug.Log($"Video moved to 'nice' folder: {sourcePath}");
                        LoadVideos(_currentVideoDirectory); // Refresh the list
                    }
                }
                else
                {
                    Debug.LogWarning("No video selected or current directory not set for 'Nice Video' operation.");
                }
            }

            // Handle 'Open in Explorer' (O key)
            if (Input.GetKeyDown(KeyCode.O))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null)
                {
                    string videoPath = selectedVideo.GetVideoPath();
                    if (File.Exists(videoPath))
                    {
                        // Open the folder and select the file
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{videoPath}\"");
                        Debug.Log($"Opened in Explorer: {videoPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"File not found for 'Open in Explorer': {videoPath}");
                    }
                }
                else
                {
                    Debug.LogWarning("No video selected for 'Open in Explorer' operation.");
                }
            }

            // Handle Mute/Unmute (M key)
            if (Input.GetKeyDown(KeyCode.M))
            {
                _isMuted = !_isMuted;
                ApplyGlobalMuteState();

                // Reapply selection state to handle selected video's mute/unmute correctly
                int selectedIndex = videoGridManager.GetSelectedVideoIndex();
                if (selectedIndex != -1)
                {
                    videoGridManager.SetSelectedVideo(selectedIndex, _isMuted); // Pass the new mute state
                }
                Debug.Log($"Global mute toggled: {_isMuted}");
            }

            // Handle Fullscreen (F key)
            if (Input.GetKeyDown(KeyCode.F))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null && canvasRectTransform != null)
                {
                    selectedVideo.ToggleFullscreen(canvasRectTransform);
                }
                else
                {
                    Debug.LogWarning("No video selected or Canvas RectTransform not assigned for Fullscreen operation.");
                }
            }
        }
    }
}

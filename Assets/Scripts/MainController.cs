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
        public string initialVideoPath; // Assign a video path in the Inspector to load on start

        private string _currentVideoDirectory;
        private bool _isCurrentlyFullscreen = false;
        private float _currentVolume = 1.0f;

        // For OnGUI filename display
        private string _fullscreenDisplayFileName = "";
        private float _fileNameDisplayTimer = 0f;
        private const float FILENAME_DISPLAY_DURATION = 3.0f; // 3 seconds
        private int _lastSelectedIndex = -1;

        // For OnGUI volume display
        private string _volumeDisplayText = "";
        private float _volumeDisplayTimer = 0f;
        private const float VOLUME_DISPLAY_DURATION = 2.0f; // 2 seconds

        void Start()
        {
            Debug.Log("MainController started.");
            _videoLoader = new VideoLoader();
            _videoFileManager = new VideoFileManager();

            if (videoGridManager != null && canvasRectTransform != null)
            {
                videoGridManager.canvasRectTransform = canvasRectTransform;
            }
            else
            {
                Debug.LogError("VideoGridManager or CanvasRectTransform not assigned in MainController.");
            }

            // Priority 1: Inspector Path
            if (!string.IsNullOrEmpty(initialVideoPath))
            {
                // Check if the path is a directory
                if (Directory.Exists(initialVideoPath))
                {
                    Debug.Log($"Launching with video directory specified in Inspector: {initialVideoPath}");
                    _currentVideoDirectory = initialVideoPath;
                    LoadVideos(initialVideoPath);
                    return; // Success, exit Start()
                }
                // Check if the path is a file
                else if (File.Exists(initialVideoPath))
                {
                    Debug.Log($"Launching with single video file specified in Inspector: {initialVideoPath}");
                    List<string> singleVideoList = new List<string> { initialVideoPath };
                    videoGridManager.DisplayVideos(singleVideoList);
                    UpdateVideoCountDisplay(singleVideoList.Count);
                    if (singleVideoList.Count > 0)
                    {
                        videoGridManager.SetSelectedVideo(0, _isCurrentlyFullscreen);
                    }
                    return; // Success, exit Start()
                }
                else
                {
                    Debug.LogError($"The 'Initial Video Path' in Inspector is not a valid file or directory: '{initialVideoPath}'. Opening directory dialog as a fallback.");
                    OpenDirectoryDialog();
                    return; // Exit Start()
                }
            }

            // Priority 2: Command-Line Arguments (only if Inspector path is empty)
            string[] args = System.Environment.GetCommandLineArgs();
            string cmdVideoPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-video" && i + 1 < args.Length)
                {
                    cmdVideoPath = args[i + 1];
                    break;
                }
            }

            if (!string.IsNullOrEmpty(cmdVideoPath))
            {
                 if (File.Exists(cmdVideoPath))
                {
                    Debug.Log($"Launching with video specified via command-line: {cmdVideoPath}");
                    List<string> singleVideoList = new List<string> { cmdVideoPath };
                    videoGridManager.DisplayVideos(singleVideoList);
                    UpdateVideoCountDisplay(singleVideoList.Count);
                    if (singleVideoList.Count > 0)
                    {
                        videoGridManager.SetSelectedVideo(0, _isCurrentlyFullscreen);
                    }
                    return; // Success, exit Start()
                }
                else
                {
                    Debug.LogError($"The video path from command-line is invalid or file not found: '{cmdVideoPath}'. Opening directory dialog as a fallback.");
                    OpenDirectoryDialog();
                    return; // Exit Start()
                }
            }

            // Fallback: No path provided
            Debug.Log("No initial video path specified. Opening directory selection dialog.");
            OpenDirectoryDialog();
        }

        private const string LAST_VIDEO_DIRECTORY_KEY = "LastVideoDirectory";

        private void OpenDirectoryDialog()
        {
            Debug.Log("Attempting to open folder panel...");
            string initialPath = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, "");
            Debug.Log($"Loaded last directory from PlayerPrefs: '{initialPath}'");

            if (string.IsNullOrEmpty(initialPath) || !Directory.Exists(initialPath))
            {
                Debug.LogWarning($"Saved path ('{initialPath}') is invalid or does not exist. Defaulting to MyDocuments.");
                initialPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            }

            string[] paths = null;
            try
            {
                Debug.Log($"Using initial path for dialog: '{initialPath}'");
                paths = SFB.StandaloneFileBrowser.OpenFolderPanel("Select Video Directory", initialPath, false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"An error occurred opening the file browser: {e.Message}");
                // Fallback: try opening without an initial path if the previous attempt failed
                paths = SFB.StandaloneFileBrowser.OpenFolderPanel("Select Video Directory", "", false);
            }

            Debug.Log($"Folder panel returned. Number of paths: {(paths != null ? paths.Length : 0)}");

            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                string rawPath = paths[0].TrimEnd('\0');
                _currentVideoDirectory = Path.GetFullPath(rawPath);
                Debug.Log($"Selected directory: '{_currentVideoDirectory}'");

                Debug.Log($"Saving '{_currentVideoDirectory}' to PlayerPrefs with key '{LAST_VIDEO_DIRECTORY_KEY}'.");
                PlayerPrefs.SetString(LAST_VIDEO_DIRECTORY_KEY, _currentVideoDirectory);
                PlayerPrefs.Save();

                LoadVideos(_currentVideoDirectory);
            }
            else
            {
                Debug.LogWarning("Directory selection cancelled or no directory selected.");
            }
        }

        private void LoadVideos(string directoryPath, int indexToSelectAfterLoad = 0)
        {
            var videoFiles = _videoLoader.LoadVideosFromDirectory(directoryPath);
            if (videoGridManager != null)
            {
                videoGridManager.DisplayVideos(videoFiles);
                UpdateVideoCountDisplay(videoFiles.Count);
                ApplyGlobalVolume(); // Set initial volume for all loaded videos

                // After loading, select the appropriate video.
                if (videoFiles.Count > 0)
                {
                    int newCount = videoFiles.Count;
                    int finalIndex = indexToSelectAfterLoad;

                    // If the old index is now out of bounds (we deleted the last item),
                    // select the new last item.
                    if (finalIndex >= newCount)
                    {
                        finalIndex = newCount - 1;
                    }

                    videoGridManager.SetSelectedVideo(finalIndex, _isCurrentlyFullscreen);
                }
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

                void Update()

                {

                    if (videoGridManager == null) return;

        

                    // --- Logic to detect video switch and trigger filename display ---

                    int currentSelectedIndex = videoGridManager.GetSelectedVideoIndex();

                    if (currentSelectedIndex != _lastSelectedIndex)

                    {

                        if (_isCurrentlyFullscreen && currentSelectedIndex != -1)

                        {

                            var selectedVideo = videoGridManager.GetVideoUI(currentSelectedIndex);

                            if (selectedVideo != null)

                            {

                                _fullscreenDisplayFileName = Path.GetFileName(selectedVideo.GetVideoPath());

                                _fileNameDisplayTimer = FILENAME_DISPLAY_DURATION;

                            }

                        }

                        _lastSelectedIndex = currentSelectedIndex;

                    }

        

                    if (_fileNameDisplayTimer > 0)

                    {

                        _fileNameDisplayTimer -= Time.deltaTime;

                    }

        

                                // Handle selection movement and seeking

        

                                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))

        

                                {

        

                                    bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        

                                    bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        

                                    

        

                                    // --- Selection Movement Logic (now with Ctrl) ---

        

                                    if (isCtrlPressed)

        

                                    {

        

                                        int direction = Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1;

        

                                        videoGridManager.MoveSelection(direction, _isCurrentlyFullscreen);

        

                                    }

        

                                    // --- Seeking Logic (default and Shift) ---

        

                                    else

        

                                    {

        

                                        VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();

        

                                        if (selectedVideo != null)

        

                                        {

        

                                            float seekSeconds = 0;

        

                                            if (isShiftPressed)

        

                                            {

        

                                                seekSeconds = 300.0f; // 5 minutes

        

                                            }

        

                                            else // No modifier

        

                                            {

        

                                                seekSeconds = 10.0f; // 10 seconds

        

                                            }

        

                    

        

                                            if (Input.GetKeyDown(KeyCode.LeftArrow))

        

                                            {

        

                                                selectedVideo.Seek(-seekSeconds);

        

                                            }

        

                                            else

        

                                            {

        

                                                selectedVideo.Seek(seekSeconds);

        

                                            }

        

                                        }

        

                                    }

        

                                }

                                                                

                                                                // Handle Volume Control

                                                                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))

                                                                {

                                                                    if (Input.GetKeyDown(KeyCode.UpArrow))

                                                                    {

                                                                        _currentVolume += 0.1f;

                                                                    }

                                                                    else

                                                                    {

                                                                        _currentVolume -= 0.1f;

                                                                    }

                                                                                    _currentVolume = Mathf.Clamp01(_currentVolume);

                                                                                    

                                                                                    _volumeDisplayText = $"Volume: {_currentVolume:P0}";

                                                                                    _volumeDisplayTimer = VOLUME_DISPLAY_DURATION;

                                                                    

                                                                                    Debug.Log($"Volume set to: {_currentVolume:P0}");

                                                                                    ApplyGlobalVolume();

                                                                                }

                                                                    

                                                                                if (_volumeDisplayTimer > 0)

                                                                                {

                                                                                    _volumeDisplayTimer -= Time.deltaTime;

                                                                                }

                                                                    

                                                                                // Handle deselect all

                                                                                if (Input.GetKeyDown(KeyCode.Escape))

                                                                                {

                                                                                    videoGridManager.DeselectAll(_isCurrentlyFullscreen);
                // After deselecting, we are no longer in fullscreen mode.
                if (_isCurrentlyFullscreen)
                {
                    _isCurrentlyFullscreen = false;
                }
            }

            // Handle 'Delete Candidate' (D key) and 'Immediate Delete' (Shift+D)
            if (Input.GetKeyDown(KeyCode.D))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null && !string.IsNullOrEmpty(_currentVideoDirectory))
                {
                    int currentIndex = videoGridManager.GetSelectedVideoIndex();
                    string sourcePath = selectedVideo.GetVideoPath();

                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        // Immediate Delete (Shift+D)
                        if (_videoFileManager.DeleteVideoFile(sourcePath))
                        {
                            Debug.Log($"Video permanently deleted: {sourcePath}");
                            LoadVideos(_currentVideoDirectory, currentIndex); // Refresh the list
                        }
                    }
                    else
                    {
                        // Delete Candidate (D key)
                        string delFolderPath = Path.Combine(_currentVideoDirectory, "del");

                        if (_videoFileManager.MoveVideoFile(sourcePath, delFolderPath))
                        {
                            Debug.Log($"Video moved to 'del' folder: {sourcePath}");
                            LoadVideos(_currentVideoDirectory, currentIndex); // Refresh the list
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
                    int currentIndex = videoGridManager.GetSelectedVideoIndex();
                    string sourcePath = selectedVideo.GetVideoPath();
                    string niceFolderPath = Path.Combine(_currentVideoDirectory, "nice");

                    if (_videoFileManager.MoveVideoFile(sourcePath, niceFolderPath))
                    {
                        Debug.Log($"Video moved to 'nice' folder: {sourcePath}");
                        LoadVideos(_currentVideoDirectory, currentIndex); // Refresh the list
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
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null)
                {
                    selectedVideo.ToggleMute();
                }
            }

            // Handle Fullscreen (F key)
            if (Input.GetKeyDown(KeyCode.F))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null && canvasRectTransform != null)
                {
                    // ToggleFullscreen returns the new state
                    _isCurrentlyFullscreen = selectedVideo.ToggleFullscreen(canvasRectTransform);
                }
                else
                {
                    Debug.LogWarning("No video selected or Canvas RectTransform not assigned for Fullscreen operation.");
                }
            }
        }

        private void ApplyGlobalVolume()
        {
            for (int i = 0; i < videoGridManager.GetVideoCount(); i++)
            {
                VideoPlayerUI videoUI = videoGridManager.GetVideoUI(i);
                if (videoUI != null)
                {
                    videoUI.SetVolume(_currentVolume);
                }
            }
        }

        void OnGUI()
        {
            if (_isCurrentlyFullscreen && _fileNameDisplayTimer > 0)
            {
                // Define a style for the text to ensure it's visible
                GUIStyle style = new GUIStyle();
                style.fontSize = 24;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.UpperCenter;

                // Create a simple drop shadow for better readability by drawing the text in black behind the white text
                GUI.color = Color.black;
                Rect shadowRect = new Rect(0, 21, Screen.width, 40);
                GUI.Label(shadowRect, _fullscreenDisplayFileName, style);

                // Draw the main text in white
                GUI.color = Color.white;
                Rect textRect = new Rect(0, 20, Screen.width, 40);
                GUI.Label(textRect, _fullscreenDisplayFileName, style);
            }

            // Display Volume Level
            if (_volumeDisplayTimer > 0)
            {
                GUIStyle volumeStyle = new GUIStyle();
                volumeStyle.fontSize = 24;
                volumeStyle.normal.textColor = Color.white;
                volumeStyle.alignment = TextAnchor.LowerCenter;

                // Shadow
                GUI.color = Color.black;
                Rect shadowRect = new Rect(0, Screen.height - 41, Screen.width, 40);
                GUI.Label(shadowRect, _volumeDisplayText, volumeStyle);

                // Main text
                GUI.color = Color.white;
                Rect textRect = new Rect(0, Screen.height - 40, Screen.width, 40);
                GUI.Label(textRect, _volumeDisplayText, volumeStyle);
            }
        }
    }
}

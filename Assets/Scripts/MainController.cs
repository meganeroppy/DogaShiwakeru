using UnityEngine;
using System.Collections.Generic;
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
        
        // For OnGUI video count display
        private string _videoCountText = "";

        // For Save Mode
        private bool _isSaveModeActive = false;
        private string _saveModeInputString = "";
        private List<string> _saveModeSuggestions = new List<string>();
        private int _saveModeSuggestionIndex = -1;

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

            // --- Determine which path to load on startup ---

            // Priority 1: Inspector Path
            if (!string.IsNullOrEmpty(initialVideoPath))
            {
                if (Directory.Exists(initialVideoPath))
                {
                    Debug.Log($"Launching with video directory specified in Inspector: {initialVideoPath}");
                    LoadVideos(initialVideoPath);
                    return;
                }
                else if (File.Exists(initialVideoPath))
                {
                    Debug.Log($"Launching with single video file specified in Inspector: {initialVideoPath}");
                    videoGridManager.DisplayVideos(new List<string> { initialVideoPath });
                    UpdateVideoCountDisplay(1);
                    videoGridManager.SetSelectedVideo(0, _isCurrentlyFullscreen);
                    return;
                }
                else
                {
                    Debug.LogWarning($"The 'Initial Video Path' in Inspector is not a valid file or directory: '{initialVideoPath}'. Proceeding to next check.");
                }
            }

            // Priority 2: Command-Line Arguments
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
                    videoGridManager.DisplayVideos(new List<string> { cmdVideoPath });
                    UpdateVideoCountDisplay(1);
                    videoGridManager.SetSelectedVideo(0, _isCurrentlyFullscreen);
                    return;
                }
                else
                {
                    Debug.LogWarning($"The video path from command-line is invalid or file not found: '{cmdVideoPath}'. Proceeding to next check.");
                }
            }

            // Priority 3: Last Opened Directory from PlayerPrefs
            string lastDirectory = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, "");
            if (!string.IsNullOrEmpty(lastDirectory) && Directory.Exists(lastDirectory))
            {
                Debug.Log($"Found last opened directory in PlayerPrefs: '{lastDirectory}'. Loading automatically.");
                LoadVideos(lastDirectory);
                return;
            }

            // Fallback: No valid path found, open the dialog
            Debug.Log("No valid initial path found. Opening directory selection dialog.");
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
                paths = SFB.StandaloneFileBrowser.OpenFolderPanel("Select Video Directory", "", false);
            }

            Debug.Log($"Folder panel returned. Number of paths: {(paths != null ? paths.Length : 0)}");

            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                string rawPath = paths[0].TrimEnd('\0');
                LoadVideos(Path.GetFullPath(rawPath));
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
                ApplyGlobalVolume();

                if (videoFiles.Count > 0)
                {
                    _currentVideoDirectory = directoryPath;
                    PlayerPrefs.SetString(LAST_VIDEO_DIRECTORY_KEY, _currentVideoDirectory);
                    PlayerPrefs.Save();
                    Debug.Log($"Saved '{_currentVideoDirectory}' as last opened directory.");

                    int newCount = videoFiles.Count;
                    int finalIndex = indexToSelectAfterLoad;
                    if (finalIndex >= newCount)
                    {
                        finalIndex = newCount - 1;
                    }
                    videoGridManager.SetSelectedVideo(finalIndex, _isCurrentlyFullscreen);
                }
                else
                {
                    Debug.LogWarning($"No video files found in '{directoryPath}'. Opening directory selection dialog.");
                    OpenDirectoryDialog();
                }
            }
            else
            {
                Debug.LogError("VideoGridManager is not assigned in MainController.");
            }
        }
        
        private void UpdateVideoCountDisplay(int count)
        {
            _videoCountText = $"Videos: {count}";
        }
        
        void Update()
        {
            if (videoGridManager == null) return;
            if (!_isSaveModeActive)
            {
                HandleNormalInput();
            }
        }

        private void HandleNormalInput()
        {
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

            if (_fileNameDisplayTimer > 0) _fileNameDisplayTimer -= Time.deltaTime;
            if (_volumeDisplayTimer > 0) _volumeDisplayTimer -= Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (isCtrlPressed)
                {
                    videoGridManager.MoveSelection(Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1, _isCurrentlyFullscreen);
                }
                else
                {
                    VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                    if (selectedVideo != null)
                    {
                        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        float seekSeconds = isShiftPressed ? 300.0f : 10.0f;
                        selectedVideo.Seek(Input.GetKeyDown(KeyCode.LeftArrow) ? -seekSeconds : seekSeconds);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                _currentVolume += Input.GetKeyDown(KeyCode.UpArrow) ? 0.1f : -0.1f;
                _currentVolume = Mathf.Clamp01(_currentVolume);
                _volumeDisplayText = $"Volume: {_currentVolume:P0}";
                _volumeDisplayTimer = VOLUME_DISPLAY_DURATION;
                Debug.Log($"Volume set to: {_currentVolume:P0}");
                ApplyGlobalVolume();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (videoGridManager.GetSelectedVideoIndex() == -1)
                {
                    Debug.Log("Escape pressed with no video selected. Opening directory selection dialog.");
                    OpenDirectoryDialog();
                }
                else
                {
                    videoGridManager.DeselectAll(_isCurrentlyFullscreen);
                    if (_isCurrentlyFullscreen) _isCurrentlyFullscreen = false;
                }
            }

            if (Input.GetKeyDown(KeyCode.D)) HandleFileOperation("del");
            if (Input.GetKeyDown(KeyCode.N)) HandleFileOperation("nice");

            if (Input.GetKeyDown(KeyCode.S))
            {
                if (videoGridManager.GetSelectedVideoUI() != null)
                {
                    _isSaveModeActive = true;
                    _saveModeInputString = "";
                    _saveModeSuggestionIndex = -1;
                    UpdateSaveSuggestions();
                    Debug.Log("Entered Save Mode.");
                }
                else
                {
                    Debug.LogWarning("No video selected to save.");
                }
            }
            
            if (Input.GetKeyDown(KeyCode.O))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{selectedVideo.GetVideoPath()}\"");
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null) selectedVideo.ToggleMute();
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null) _isCurrentlyFullscreen = selectedVideo.ToggleFullscreen(canvasRectTransform);
            }
        }
        
        private void HandleFileOperation(string folderName)
        {
            VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
            if (selectedVideo != null && !string.IsNullOrEmpty(_currentVideoDirectory))
            {
                int currentIndex = videoGridManager.GetSelectedVideoIndex();
                string sourcePath = selectedVideo.GetVideoPath();
                string destFolderPath = Path.Combine(_currentVideoDirectory, folderName);

                if (_videoFileManager.MoveVideoFile(sourcePath, destFolderPath))
                {
                    Debug.Log($"Video moved to '{folderName}' folder: {sourcePath}");
                    LoadVideos(_currentVideoDirectory, currentIndex);
                }
            }
        }

        private void UpdateSaveSuggestions()
        {
            _saveModeSuggestions.Clear();
            _saveModeSuggestionIndex = -1;
            if (string.IsNullOrEmpty(_saveModeInputString) || string.IsNullOrEmpty(_currentVideoDirectory)) return;

            try
            {
                var subDirs = Directory.GetDirectories(_currentVideoDirectory);
                foreach (var dir in subDirs)
                {
                    string dirName = new DirectoryInfo(dir).Name;
                    if (dirName.StartsWith(_saveModeInputString, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _saveModeSuggestions.Add(dirName);
                    }
                }
                if (_saveModeSuggestions.Count > 0) _saveModeSuggestionIndex = 0;
            }
            catch(System.Exception e) { Debug.LogError($"Error searching for subdirectories: {e.Message}"); }
        }

        private void PerformSaveAction()
        {
            string targetFolderName = _saveModeInputString.Trim();
            if (string.IsNullOrEmpty(targetFolderName))
            {
                Debug.LogWarning("Save folder name is empty. Aborting save.");
                _isSaveModeActive = false;
                return;
            }
            
            HandleFileOperation(targetFolderName);
            _isSaveModeActive = false;
        }
        
        private void ApplyGlobalVolume()
        {
            for (int i = 0; i < videoGridManager.GetVideoCount(); i++)
            {
                VideoPlayerUI videoUI = videoGridManager.GetVideoUI(i);
                if (videoUI != null) videoUI.SetVolume(_currentVolume);
            }
        }

        void OnGUI()
        {
            if (_isSaveModeActive)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Escape) { _isSaveModeActive = false; e.Use(); }
                    else if (e.keyCode == KeyCode.Tab)
                    {
                        if (_saveModeSuggestions.Count > 0)
                        {
                            _saveModeSuggestionIndex = (_saveModeSuggestionIndex + 1) % _saveModeSuggestions.Count;
                            _saveModeInputString = _saveModeSuggestions[_saveModeSuggestionIndex];
                            UpdateSaveSuggestions();
                        }
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { PerformSaveAction(); e.Use(); }
                }

                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                Rect boxRect = new Rect((Screen.width - 400) / 2, (Screen.height - 300) / 2, 400, 300);
                GUI.Box(boxRect, "Save to Subfolder");
                GUI.SetNextControlName("SaveInput");
                string newText = GUI.TextField(new Rect(boxRect.x + 10, boxRect.y + 30, boxRect.width - 20, 30), _saveModeInputString, new GUIStyle(GUI.skin.textField) { fontSize = 18 });
                if (newText != _saveModeInputString) { _saveModeInputString = newText; UpdateSaveSuggestions(); }
                GUI.FocusControl("SaveInput");

                if (_saveModeSuggestions.Count > 0)
                {
                    GUIStyle sStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
                    GUIStyle hStyle = new GUIStyle(sStyle) { normal = { textColor = Color.yellow } };
                    for (int i = 0; i < _saveModeSuggestions.Count; i++)
                    {
                        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 70 + (i * 25), boxRect.width - 20, 25), _saveModeSuggestions[i], (i == _saveModeSuggestionIndex) ? hStyle : sStyle);
                    }
                }
            }
            else
            {
                GUIStyle style = new GUIStyle { fontSize = 20, normal = { textColor = Color.white }, alignment = TextAnchor.UpperLeft };
                GUI.color = Color.black;
                GUI.Label(new Rect(11, 11, 300, 30), _videoCountText, style);
                GUI.color = Color.white;
                GUI.Label(new Rect(10, 10, 300, 30), _videoCountText, style);

                if (_isCurrentlyFullscreen && _fileNameDisplayTimer > 0)
                {
                    style.alignment = TextAnchor.UpperCenter; style.fontSize = 24;
                    GUI.color = Color.black; GUI.Label(new Rect(0, 21, Screen.width, 40), _fullscreenDisplayFileName, style);
                    GUI.color = Color.white; GUI.Label(new Rect(0, 20, Screen.width, 40), _fullscreenDisplayFileName, style);
                }
                if (_volumeDisplayTimer > 0)
                {
                    style.alignment = TextAnchor.LowerCenter; style.fontSize = 24;
                    GUI.color = Color.black; GUI.Label(new Rect(0, Screen.height - 41, Screen.width, 40), _volumeDisplayText, style);
                    GUI.color = Color.white; GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 40), _volumeDisplayText, style);
                }
            }
        }
    }
}
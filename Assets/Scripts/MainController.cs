using UnityEngine;
using System.Collections.Generic;
using SFB;
using System.IO;
using System.Linq;

namespace DogaShiwakeru
{
    public class MainController : MonoBehaviour
    {
        private VideoLoader _videoLoader;
        private VideoFileManager _videoFileManager;
        public VideoGridManager videoGridManager;
        public RectTransform canvasRectTransform;
        public string initialVideoPath;

        private string _currentVideoDirectory;
        private bool _isCurrentlyFullscreen = false;
        private float _currentVolume = 1.0f;

        private string _fullscreenDisplayFileName = "";
        private int _lastSelectedIndex = -1;

        private string _volumeDisplayText = "";
        private float _volumeDisplayTimer = 0f;
        private const float VOLUME_DISPLAY_DURATION = 2.0f;
        
        private string _videoCountText = "";

        private bool _isSaveModeActive = false;
        private string _saveModeInputString = "";
        private List<string> _saveModeSuggestions = new List<string>();
        private int _saveModeSuggestionIndex = -1;

        private const string LAST_VIDEO_DIRECTORY_KEY = "LastVideoDirectory";
        private const string VOLUME_KEY = "LastVolumeLevel";

        void Start()
        {
            Debug.Log("MainController started.");
            _videoLoader = new VideoLoader();
            _videoFileManager = new VideoFileManager();

            // Load the last saved volume, defaulting to 100%
            _currentVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1.0f);

            if (videoGridManager == null || canvasRectTransform == null)
            {
                Debug.LogError("VideoGridManager or CanvasRectTransform not assigned in MainController.");
            }

            // --- Simplified Startup Logic ---
            string lastDirectory = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, "");

            if (!string.IsNullOrEmpty(lastDirectory) && Directory.Exists(lastDirectory))
            {
                // Check if the directory contains any video files before loading
                var videoFiles = _videoLoader.LoadVideosFromDirectory(lastDirectory);
                if (videoFiles.Any())
                {
                    Debug.Log($"Loading last opened directory: '{lastDirectory}'.");
                    LoadVideos(lastDirectory);
                    return;
                }
                else
                {
                     Debug.LogWarning($"Last opened directory '{lastDirectory}' contains no video files.");
                }
            }
            
            // Fallback for all other cases
            Debug.Log("No valid last opened directory found. Opening directory selection dialog.");
            OpenDirectoryDialog();
        }

        private void OpenDirectoryDialog()
        {
            string initialPath = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
            string[] paths = SFB.StandaloneFileBrowser.OpenFolderPanel("Select Video Directory", initialPath, false);

            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                LoadVideos(Path.GetFullPath(paths[0].TrimEnd('\0')));
            }
            else
            {
                Debug.LogWarning("Directory selection cancelled.");
                // If user cancels, we should show an empty grid, not re-trigger the dialog
                videoGridManager.DisplayVideos(new List<string>());
                UpdateVideoCountDisplay(0);
            }
        }

        private void LoadVideos(string directoryPath, int indexToSelectAfterLoad = 0)
        {
            var videoFiles = _videoLoader.LoadVideosFromDirectory(directoryPath);
            UpdateVideoCountDisplay(videoFiles.Count);

            if (videoGridManager != null)
            {
                videoGridManager.DisplayVideos(videoFiles);
                ApplyGlobalVolume();

                if (videoFiles.Count > 0)
                {
                    _currentVideoDirectory = directoryPath;
                    PlayerPrefs.SetString(LAST_VIDEO_DIRECTORY_KEY, _currentVideoDirectory);
                    PlayerPrefs.Save();

                    int finalIndex = Mathf.Clamp(indexToSelectAfterLoad, 0, videoFiles.Count - 1);
                    videoGridManager.SetSelectedVideo(finalIndex, _isCurrentlyFullscreen);
                }
                else
                {
                    // If loading results in an empty list (e.g., after last file is moved),
                    // clear the saved key so the dialog opens next time.
                    PlayerPrefs.DeleteKey(LAST_VIDEO_DIRECTORY_KEY);
                }
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
                if (currentSelectedIndex != -1)
                {
                    var selectedVideo = videoGridManager.GetVideoUI(currentSelectedIndex);
                    if (selectedVideo != null)
                    {
                        _fullscreenDisplayFileName = Path.GetFileName(selectedVideo.GetVideoPath());
                    }
                }
                else
                {
                    _fullscreenDisplayFileName = ""; // Clear filename when nothing is selected
                }
                _lastSelectedIndex = currentSelectedIndex;
            }

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
                ApplyGlobalVolume();
                PlayerPrefs.SetFloat(VOLUME_KEY, _currentVolume);
                PlayerPrefs.Save();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (videoGridManager.GetSelectedVideoIndex() == -1)
                {
                    OpenDirectoryDialog();
                }
                else
                {
                    videoGridManager.DeselectAll(_isCurrentlyFullscreen);
                    if (_isCurrentlyFullscreen) _isCurrentlyFullscreen = false;
                }
            }
            
            if (Input.GetKeyDown(KeyCode.D)) HandleFileOperation(Path.Combine(_currentVideoDirectory, "del"));
            if (Input.GetKeyDown(KeyCode.N)) HandleFileOperation(Path.Combine(_currentVideoDirectory, "nice"));

            if (Input.GetKeyDown(KeyCode.S))
            {
                if (videoGridManager.GetSelectedVideoUI() != null)
                {
                    _isSaveModeActive = true;
                    _saveModeInputString = "";
                    _saveModeSuggestionIndex = -1;
                    UpdateSaveSuggestions();
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
        
        private void HandleFileOperation(string destFolderPath)
        {
            VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
            if (selectedVideo != null && !string.IsNullOrEmpty(_currentVideoDirectory))
            {
                int currentIndex = videoGridManager.GetSelectedVideoIndex();
                string sourcePath = selectedVideo.GetVideoPath();

                if (_videoFileManager.MoveVideoFile(sourcePath, destFolderPath))
                {
                    LoadVideos(_currentVideoDirectory, currentIndex);
                    _lastSelectedIndex = -1; // Force a refresh of the selection state
                }
            }
        }

        private void UpdateSaveSuggestions()
        {
            _saveModeSuggestions.Clear();
            _saveModeSuggestionIndex = -1;
            if (string.IsNullOrEmpty(_saveModeInputString) || string.IsNullOrEmpty(_currentVideoDirectory)) return;
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

        private void PerformSaveAction()
        {
            string targetFolderName = _saveModeInputString.Trim();
            if (!string.IsNullOrEmpty(targetFolderName))
            {
                 HandleFileOperation(Path.Combine(_currentVideoDirectory, targetFolderName));
                 _lastSelectedIndex = -1; // Force a refresh of the selection state
            }
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

                // Display filename always if something is selected
                if (!string.IsNullOrEmpty(_fullscreenDisplayFileName))
                {
                    style.alignment = TextAnchor.UpperLeft; style.fontSize = 20;
                    GUI.color = Color.black; GUI.Label(new Rect(11, 41, Screen.width, 30), _fullscreenDisplayFileName, style);
                    GUI.color = Color.white; GUI.Label(new Rect(10, 40, Screen.width, 30), _fullscreenDisplayFileName, style);
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
using UnityEngine;
using System.Collections.Generic;
using SFB;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using UnityEngine.EventSystems;

namespace DogaShiwakeru
{
    public class MainController : MonoBehaviour
    {
        // --- Fields ---
        private VideoLoader _videoLoader;
        private VideoFileManager _videoFileManager;
        public VideoGridManager videoGridManager;
        public RectTransform canvasRectTransform;

        private List<string> _allVideoPaths = new List<string>();
        private const int MAX_VIDEOS_ON_SCREEN = 20;

        private string _currentVideoDirectory = "";
        private float _currentVolume = 1.0f;
        private string _fullscreenDisplayFileName = "";
        private int _lastSelectedIndex = -1;
        private string _volumeDisplayText = "";
        private float _volumeDisplayTimer = 0f;
        private const float VOLUME_DISPLAY_DURATION = 2.0f;
        private string _videoCountText = "";

        private bool _isSaveModeActive = false;
        private bool _isRenameModeActive = false;
        private bool _isNavigateDownModeActive = false;
        private string _modalInputString = "";
        private List<string> _modalSuggestions = new List<string>();
        private int _modalSuggestionIndex = -1;
        
        private bool _performSaveQueued = false;
        private bool _performRenameQueued = false;
        private bool _performNavigateDownQueued = false;
        private bool _focusResetQueued = false;

        private const string LAST_VIDEO_DIRECTORY_KEY = "LastVideoDirectory";
        private const string VOLUME_KEY = "LastVolumeLevel";
        
        // --- Methods ---

        void Start()
        {
            _videoLoader = new VideoLoader();
            _videoFileManager = new VideoFileManager();
            _currentVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1.0f);

            if (videoGridManager != null) videoGridManager.canvasRectTransform = canvasRectTransform;
            else Debug.LogError("VideoGridManager not assigned.");

            string lastDirectory = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, "");
            if (!string.IsNullOrEmpty(lastDirectory) && Directory.Exists(lastDirectory))
            {
                LoadAllVideoPaths(lastDirectory);
            }
            else
            {
                OpenDirectoryDialog();
            }
        }

        private void OpenDirectoryDialog()
        {
            string initialPath = PlayerPrefs.GetString(LAST_VIDEO_DIRECTORY_KEY, System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
            string[] paths = SFB.StandaloneFileBrowser.OpenFolderPanel("Select Video Directory", initialPath, false);

            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                LoadAllVideoPaths(Path.GetFullPath(paths[0].TrimEnd('\0')));
            }
            else
            {
                _currentVideoDirectory = "";
                _allVideoPaths.Clear();
                RefreshGridDisplay();
            }
        }
        
        private void LoadAllVideoPaths(string directoryPath)
        {
            if(!Directory.Exists(directoryPath))
            {
                Debug.LogError($"Directory not found: {directoryPath}");
                OpenDirectoryDialog();
                return;
            }
            
            _allVideoPaths = _videoLoader.LoadVideosFromDirectory(directoryPath);
            _currentVideoDirectory = directoryPath;
            PlayerPrefs.SetString(LAST_VIDEO_DIRECTORY_KEY, _currentVideoDirectory);
            PlayerPrefs.Save();
            
            RefreshGridDisplay();
        }

        private void RefreshGridDisplay(int indexToSelect = 0, bool forceFullscreen = false)
        {
            UpdateVideoCountDisplay(_allVideoPaths.Count);
            
            var videosToDisplay = _allVideoPaths.Take(MAX_VIDEOS_ON_SCREEN).ToList();
            videoGridManager.DisplayVideos(videosToDisplay);
            ApplyGlobalVolume();
            
            if (videosToDisplay.Any())
            {
                int finalIndex = Mathf.Clamp(indexToSelect, 0, videosToDisplay.Count - 1);
                videoGridManager.SelectAndPossiblyFullscreen(finalIndex, forceFullscreen); 
            }
            
            _focusResetQueued = true;
        }
        
        private void UpdateVideoCountDisplay(int count)
        {
            _videoCountText = $"Total Videos: {count}";
        }
        
        void Update()
        {
            if (videoGridManager == null) return;

            if (_focusResetQueued)
            {
                Debug.Log("[DIAG] Executing queued focus reset.");
                EventSystem.current.SetSelectedGameObject(null);
                _focusResetQueued = false;
            }
            
            if (Input.anyKeyDown && !(_isSaveModeActive || _isRenameModeActive || _isNavigateDownModeActive))
            {
                string focusedObjectName = EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : "null";
                Debug.Log($"[DIAG-UPDATE] KeyDown detected in Normal Mode. Focused: {focusedObjectName}.");
            }

            if (_performSaveQueued) { _performSaveQueued = false; PerformSaveAction(); }
            if (_performRenameQueued) { _performRenameQueued = false; PerformRenameAction(); }
            if (_performNavigateDownQueued) { _performNavigateDownQueued = false; PerformNavigateDownAction(); }

            if (!_isSaveModeActive && !_isRenameModeActive && !_isNavigateDownModeActive)
            {
                HandleNormalInput();
            }
        }

        private void HandleNormalInput()
        {
            int currentSelectedIndex = videoGridManager.GetSelectedVideoIndex();
            if (currentSelectedIndex != _lastSelectedIndex)
            {
                var selectedVideo = videoGridManager.GetVideoUI(currentSelectedIndex);
                _fullscreenDisplayFileName = selectedVideo != null ? Path.GetFileName(selectedVideo.GetVideoPath()) : "";
                _lastSelectedIndex = currentSelectedIndex;
            }

            if (_volumeDisplayTimer > 0) _volumeDisplayTimer -= Time.deltaTime;

            bool isFullscreen = videoGridManager.IsFullscreen(); 

            if (Input.GetKeyDown(KeyCode.S))
            {
                Debug.Log("[DIAG] S key processed.");
                if (videoGridManager.GetSelectedVideoUI() != null)
                {
                    _isSaveModeActive = true;
                    _modalInputString = "";
                    _modalSuggestionIndex = -1;
                    UpdateSubdirectorySuggestions();
                    _focusResetQueued = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("[DIAG] R key processed.");
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null)
                {
                    _isRenameModeActive = true;
                    _modalInputString = Path.GetFileName(selectedVideo.GetVideoPath());
                    _modalSuggestions.Clear();
                    _modalSuggestionIndex = -1;
                    _focusResetQueued = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                HandleFileOperation(Path.Combine(_currentVideoDirectory, "del"), isFullscreen);
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                HandleFileOperation(Path.Combine(_currentVideoDirectory, "nice"), isFullscreen);
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                videoGridManager.ToggleFullscreenOnSelected();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (videoGridManager.GetSelectedVideoIndex() == -1) { OpenDirectoryDialog(); } 
                else { videoGridManager.DeselectOrExitFullscreen(); }
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null)
                {
                    if (selectedVideo.videoPlayer.isPlaying) { selectedVideo.Pause(); } 
                    else { selectedVideo.Play(); }
                }
            }
            else if (Input.GetKeyDown(KeyCode.Backspace))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null) selectedVideo.videoPlayer.time = 0;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (isCtrlPressed)
                {
                    videoGridManager.MoveSelection(Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1); 
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
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if(isCtrlPressed)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        if (string.IsNullOrEmpty(_currentVideoDirectory)) return;
                        DirectoryInfo parentDir = Directory.GetParent(_currentVideoDirectory);
                        if(parentDir != null)
                        {
                            LoadAllVideoPaths(parentDir.FullName);
                        }
                    }
                    else // Down Arrow
                    {
                        if(!string.IsNullOrEmpty(_currentVideoDirectory))
                        {
                            _isNavigateDownModeActive = true;
                            _modalInputString = "";
                            _modalSuggestionIndex = -1;
                            UpdateSubdirectorySuggestions();
                            _focusResetQueued = true;
                        }
                    }
                }
                else
                {
                    _currentVolume += Input.GetKeyDown(KeyCode.UpArrow) ? 0.1f : -0.1f;
                    _currentVolume = Mathf.Clamp01(_currentVolume);
                    _volumeDisplayText = $"Volume: {_currentVolume:P0}";
                    _volumeDisplayTimer = VOLUME_DISPLAY_DURATION;
                    ApplyGlobalVolume();
                    PlayerPrefs.SetFloat(VOLUME_KEY, _currentVolume);
                    PlayerPrefs.Save();
                }
            }
            else if (Input.GetKeyDown(KeyCode.O))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{selectedVideo.GetVideoPath()}\"");
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null) selectedVideo.ToggleMute();
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
                if (selectedVideo != null)
                {
                    string fileName = Path.GetFileNameWithoutExtension(selectedVideo.GetVideoPath());
                    string encodedFileName = UnityEngine.Networking.UnityWebRequest.EscapeURL(fileName);
                    Application.OpenURL($"https://www.google.com/search?q={encodedFileName}");
                }
            }
        }
        
        private void HandleFileOperation(string destFolderPath, bool wasFullscreen)
        {
            VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
            if (selectedVideo != null)
            {
                int currentIndexOnScreen = videoGridManager.GetSelectedVideoIndex();
                string sourcePath = selectedVideo.GetVideoPath();
                if (_videoFileManager.MoveVideoFile(sourcePath, destFolderPath))
                {
                    _allVideoPaths.Remove(sourcePath);
                    RefreshGridDisplay(currentIndexOnScreen, wasFullscreen);
                    _lastSelectedIndex = -1;
                }
            }
        }

        private void UpdateSubdirectorySuggestions()
        {
            _modalSuggestions.Clear();
            _modalSuggestionIndex = -1;
            if (string.IsNullOrEmpty(_currentVideoDirectory) || !Directory.Exists(_currentVideoDirectory)) return;
            try
            {
                var subDirs = Directory.GetDirectories(_currentVideoDirectory);
                foreach (var dir in subDirs)
                {
                    string dirName = new DirectoryInfo(dir).Name;
                    if (string.IsNullOrEmpty(_modalInputString) || dirName.StartsWith(_modalInputString, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (_videoLoader.DirectoryContainsVideos(dir))
                        {
                            _modalSuggestions.Add(dirName);
                        }
                    }
                }
            }
            catch(System.Exception e) { Debug.LogError($"Error getting subdirectories: {e.Message}"); }
            if (_modalSuggestions.Count > 0) _modalSuggestionIndex = 0;
        }

        private void PerformSaveAction()
        {
            string targetFolderName = _modalInputString.Trim();
            if (!string.IsNullOrEmpty(targetFolderName))
            {
                 HandleFileOperation(Path.Combine(_currentVideoDirectory, targetFolderName), videoGridManager.IsFullscreen());
            }
            _isSaveModeActive = false;
            _focusResetQueued = true;
        }
        
        private void PerformRenameAction()
        {
            VideoPlayerUI selectedVideo = videoGridManager.GetSelectedVideoUI();
            if (selectedVideo != null)
            {
                int currentIndexOnScreen = videoGridManager.GetSelectedVideoIndex();
                string sourcePath = selectedVideo.GetVideoPath();
                string newFileName = _modalInputString.Trim();

                if (!string.IsNullOrEmpty(newFileName) && !Path.GetFileName(sourcePath).Equals(newFileName, System.StringComparison.Ordinal) && 
                    _videoFileManager.RenameVideoFile(sourcePath, newFileName))
                {
                    int masterIndex = _allVideoPaths.FindIndex(p => p == sourcePath);
                    if(masterIndex != -1)
                    {
                        _allVideoPaths[masterIndex] = Path.Combine(Path.GetDirectoryName(sourcePath), newFileName);
                    }
                    RefreshGridDisplay(currentIndexOnScreen, videoGridManager.IsFullscreen());
                    _lastSelectedIndex = -1;
                }
            }
            _isRenameModeActive = false;
            _focusResetQueued = true;
        }

        private void PerformNavigateDownAction()
        {
            string targetSubDir = _modalInputString.Trim();
            if (string.IsNullOrEmpty(targetSubDir))
            {
                _isNavigateDownModeActive = false;
                _focusResetQueued = true;
                return;
            }

            string newPath = Path.Combine(_currentVideoDirectory, targetSubDir);
            if(Directory.Exists(newPath))
            {
                LoadAllVideoPaths(newPath);
                _isNavigateDownModeActive = false;
                _focusResetQueued = true;
            }
            else
            {
                Debug.LogWarning($"Directory not found: {newPath}. Please correct the name or press Escape.");
            }
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
            if (_isSaveModeActive || _isRenameModeActive || _isNavigateDownModeActive)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Escape) 
                    { 
                        Debug.Log("[DIAG-ONGUI] Escape key pressed. Exiting modal.");
                        _isSaveModeActive = false; 
                        _isRenameModeActive = false; 
                        _isNavigateDownModeActive = false;
                        _focusResetQueued = true; 
                        Input.ResetInputAxes(); // Attempt to reset Input state
                        e.Use(); 
                    }
                    else if (e.keyCode == KeyCode.Tab && (_isSaveModeActive || _isNavigateDownModeActive)) 
                    { 
                        Debug.Log("[DIAG-ONGUI] Tab key pressed.");
                        if (_modalSuggestions.Count > 0)
                        {
                            _modalSuggestionIndex = (_modalSuggestionIndex + 1) % _modalSuggestions.Count;
                            _modalInputString = _modalSuggestions[_modalSuggestionIndex];
                        }
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) 
                    { 
                        Debug.Log("[DIAG-ONGUI] Enter key pressed.");
                        if (_isRenameModeActive) _performRenameQueued = true; 
                        else if (_isSaveModeActive) _performSaveQueued = true;
                        else if (_isNavigateDownModeActive) _performNavigateDownQueued = true;
                        e.Use(); 
                    }
                }
                
                // --- UI Drawing ---
                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                Rect boxRect = new Rect((Screen.width - 400) / 2, (Screen.height - 300) / 2, 400, 300);
                
                string boxTitle = "Action";
                if (_isRenameModeActive) boxTitle = "Rename File";
                else if (_isSaveModeActive) boxTitle = "Save to Subfolder";
                else if (_isNavigateDownModeActive) boxTitle = "Navigate to Subfolder";

                GUI.Box(boxRect, boxTitle);
                
                GUI.SetNextControlName("SaveInput");
                string newText = GUI.TextField(new Rect(boxRect.x + 10, boxRect.y + 30, boxRect.width - 20, 30), _modalInputString, new GUIStyle(GUI.skin.textField) { fontSize = 18 });
                if (newText != _modalInputString) 
                { 
                    _modalInputString = newText;
                    if (_isSaveModeActive || _isNavigateDownModeActive) UpdateSubdirectorySuggestions(); 
                }
                GUI.FocusControl("SaveInput");

                if ((_isSaveModeActive || _isNavigateDownModeActive) && _modalSuggestions.Count > 0)
                {
                    GUIStyle sStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
                    GUIStyle hStyle = new GUIStyle(sStyle) { normal = { textColor = Color.yellow } };
                    for (int i = 0; i < _modalSuggestions.Count; i++)
                    {
                        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 70 + (i * 25), boxRect.width - 20, 25), _modalSuggestions[i], (i == _modalSuggestionIndex) ? hStyle : sStyle);
                    }
                }
            }
            else
            {
                // --- Normal UI Overlays ---
                GUIStyle style = new GUIStyle { fontSize = 20, normal = { textColor = Color.white }, alignment = TextAnchor.UpperLeft };
                
                GUI.color = Color.black;
                GUI.Label(new Rect(11, 11, 300, 30), _videoCountText, style);
                GUI.color = Color.white;
                GUI.Label(new Rect(10, 10, 300, 30), _videoCountText, style);

                style.alignment = TextAnchor.UpperRight;
                Rect dirRect = new Rect(Screen.width - 710, 10, 700, 60);
                GUI.color = Color.black;
                GUI.Label(new Rect(dirRect.x + 1, dirRect.y + 1, dirRect.width, dirRect.height), _currentVideoDirectory, style);
                GUI.color = Color.white;
                GUI.Label(dirRect, _currentVideoDirectory, style);

                if (!string.IsNullOrEmpty(_fullscreenDisplayFileName))
                {
                    style.alignment = TextAnchor.UpperLeft;
                    style.fontSize = 20;
                    style.wordWrap = true; 

                    Rect filenameRect = new Rect(10, 40, Screen.width - 20, 60); 

                    GUI.color = Color.black; 
                    GUI.Label(new Rect(filenameRect.x + 1, filenameRect.y + 1, filenameRect.width, filenameRect.height), _fullscreenDisplayFileName, style);
                    GUI.color = Color.white; 
                    GUI.Label(filenameRect, _fullscreenDisplayFileName, style);
                }
                
                if (_volumeDisplayTimer > 0)
                {
                    style.alignment = TextAnchor.LowerCenter; style.fontSize = 24; style.wordWrap = false; 
                    GUI.color = Color.black; GUI.Label(new Rect(0, Screen.height - 41, Screen.width, 40), _volumeDisplayText, style);
                    GUI.color = Color.white; GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 40), _volumeDisplayText, style);
                }
            }
        }
    }
}
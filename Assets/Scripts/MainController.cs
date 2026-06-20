using UnityEngine;
using System.Collections.Generic;
using SFB;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private float _currentPlaybackSpeed = 1.0f;
        private string _speedDisplayText = "";
        private float _speedDisplayTimer = 0f;
        private const float SPEED_DISPLAY_DURATION = 2.0f;

        private bool _isSaveModeActive = false;
        private bool _isRenameModeActive = false;
        private bool _isNavigateDownModeActive = false;
        private bool _isDriveSelectModeActive = false;
        private bool _isBookmarkModeActive = false;
        private string _modalInputString = "";
        private List<string> _modalSuggestions = new List<string>();
        private int _modalSuggestionIndex = -1;
        
        private bool _focusResetQueued = false;

        private bool _speedSavePending = false; // defer PlayerPrefs.Save() to key release
        private float _suggestionDebounceTimer = 0f;
        private const float SUGGESTION_DEBOUNCE_SECONDS = 0.12f;

        private List<string> _bookmarks = new List<string>();

        private const string LAST_VIDEO_DIRECTORY_KEY = "LastVideoDirectory";
        private const string VOLUME_KEY = "LastVolumeLevel";
        private const string PLAYBACK_SPEED_KEY = "LastPlaybackSpeed";
        private const string BOOKMARKS_KEY = "AppBookmarks";

        // Key Repeat Settings
        private float _keyRepeatTimer = 0f;
        private const float KEY_REPEAT_DELAY = 0.4f;
        private const float KEY_REPEAT_RATE = 0.05f; // Very fast repeat for "ガーって" effect
        
        private ThumbnailGenerator _thumbnailGenerator;

        // Cached GUIStyles — created once to avoid per-frame allocations in OnGUI
        private GUIStyle _modalTextFieldStyle;
        private GUIStyle _modalSuggestionStyle;
        private GUIStyle _modalSuggestionHighlightStyle;
        private GUIStyle _hudStyleUpperLeft;
        private GUIStyle _hudStyleUpperRight;
        private GUIStyle _hudStyleLowerCenter;
        private GUIStyle _hudStyleFullscreenName;

        // Async suggestion scanning
        private CancellationTokenSource _suggestionsCts;
        private List<string> _pendingSuggestions;
        private readonly object _suggestionsLock = new object();

        // --- Methods ---

        void Start()
        {
            // Set high target frame rate for smooth video playback
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1; // Sync with monitor refresh rate to prevent tearing and stutter

            _videoLoader = new VideoLoader();
            _videoFileManager = new VideoFileManager();
            _currentVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1.0f);
            
            // Load and apply saved playback speed
            _currentPlaybackSpeed = PlayerPrefs.GetFloat(PLAYBACK_SPEED_KEY, 1.0f);
            // Show speed on startup
            _speedDisplayText = $"Speed: x{_currentPlaybackSpeed:0.00}";
            _speedDisplayTimer = SPEED_DISPLAY_DURATION;

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
            LoadBookmarks();
            
            _thumbnailGenerator = gameObject.AddComponent<ThumbnailGenerator>();
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
            
            // Clear thumbnail textures from GPU when navigating to a different directory.
            // Same-directory refresh (R key) keeps the cache so thumbnails don't flicker.
            if (directoryPath != _currentVideoDirectory)
                ThumbnailCache.Clear();

            _allVideoPaths = _videoLoader.LoadVideosFromDirectory(directoryPath);
            _currentVideoDirectory = directoryPath;
            _videoLoader.ClearDirectoryCache();
            PlayerPrefs.SetString(LAST_VIDEO_DIRECTORY_KEY, _currentVideoDirectory);
            PlayerPrefs.Save();
            
            RefreshGridDisplay();
        }

        private void RefreshGridDisplay(int indexToSelect = 0, bool forceFullscreen = false)
        {
            UpdateVideoCountDisplay(_allVideoPaths.Count);
            
            var videosToDisplay = _allVideoPaths.Take(MAX_VIDEOS_ON_SCREEN).ToList();
            videoGridManager.DisplayVideos(videosToDisplay);
            videoGridManager.SetSelectionPlaybackSpeed(_currentPlaybackSpeed); // Apply global speed to new grid
            ApplyGlobalVolume();
            
            if (videosToDisplay.Any())
            {
                int finalIndex = Mathf.Clamp(indexToSelect, 0, videosToDisplay.Count - 1);
                videoGridManager.SelectAndPossiblyFullscreen(finalIndex, forceFullscreen); 
            }
            
            _focusResetQueued = true;
            Input.ResetInputAxes(); 
        }
        
        private void UpdateVideoCountDisplay(int count)
        {
            _videoCountText = $"Total Videos: {count}";
        }
        
        private float _fpsTimer = 0f;
        private int _frameCount = 0;

        void Update()
        {
            if (videoGridManager == null) return;

            // Diagnostic: Monitor Main Thread FPS
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1.0f)
            {
                float fps = _frameCount / _fpsTimer;
                if (fps < 30f)
                {
                    Debug.LogWarning($"[MainController] Low FPS detected: {fps:F1} FPS. Possible main thread bottleneck.");
                }
                _frameCount = 0;
                _fpsTimer = 0;
            }

            // Debounce: fire the suggestion scan after the user stops typing
            if (_suggestionDebounceTimer > 0f)
            {
                _suggestionDebounceTimer -= Time.unscaledDeltaTime;
                if (_suggestionDebounceTimer <= 0f)
                {
                    _suggestionDebounceTimer = 0f;
                    if (_isSaveModeActive || _isNavigateDownModeActive)
                        LaunchSuggestionScan();
                }
            }

            // Apply suggestions computed on background thread
            lock (_suggestionsLock)
            {
                if (_pendingSuggestions != null)
                {
                    _modalSuggestions = _pendingSuggestions;
                    _pendingSuggestions = null;
                    _modalSuggestionIndex = -1; // Tab cycling starts from -1 so first Tab selects index 0
                }
            }

            if (_focusResetQueued)
            {
                EventSystem.current.SetSelectedGameObject(null);
                _focusResetQueued = false;
            }


            bool modalActive = _isSaveModeActive || _isRenameModeActive || _isNavigateDownModeActive || _isDriveSelectModeActive || _isBookmarkModeActive;

            if (modalActive)
            {
                // Escape でモーダルを閉じる
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _isSaveModeActive = false;
                    _isRenameModeActive = false;
                    _isNavigateDownModeActive = false;
                    _isDriveSelectModeActive = false;
                    _isBookmarkModeActive = false;
                    _focusResetQueued = true;
                    Input.ResetInputAxes();
                    return;
                }

                // Enter で確定
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (_isRenameModeActive) PerformRenameAction();
                    else if (_isSaveModeActive) PerformSaveAction();
                    else if (_isNavigateDownModeActive) PerformNavigateDownAction();
                    else if (_isDriveSelectModeActive) PerformDriveSelectAction();
                    else if (_isBookmarkModeActive) PerformBookmarkJumpAction();
                    return;
                }

                // Tab で候補を循環
                if (Input.GetKeyDown(KeyCode.Tab) && (_isSaveModeActive || _isNavigateDownModeActive || _isDriveSelectModeActive || _isBookmarkModeActive))
                {
                    if (_modalSuggestions.Count > 0)
                    {
                        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        if (shift)
                        {
                            _modalSuggestionIndex--;
                            if (_modalSuggestionIndex < 0) _modalSuggestionIndex = _modalSuggestions.Count - 1;
                        }
                        else
                        {
                            _modalSuggestionIndex = (_modalSuggestionIndex + 1) % _modalSuggestions.Count;
                        }
                        if (!_isDriveSelectModeActive && !_isBookmarkModeActive)
                            _modalInputString = _modalSuggestions[_modalSuggestionIndex];
                        UpdateThumbnailPreview();
                    }
                    return;
                }

                // テキスト入力：Input.inputString は OS キーボードバッファ直読みでフレーム間の全キーを取得
                if (!_isDriveSelectModeActive && !_isBookmarkModeActive && !string.IsNullOrEmpty(Input.inputString))
                {
                    bool changed = false;
                    foreach (char c in Input.inputString)
                    {
                        if (c == '\b') // Backspace
                        {
                            if (_modalInputString.Length > 0)
                            {
                                _modalInputString = _modalInputString.Substring(0, _modalInputString.Length - 1);
                                changed = true;
                            }
                        }
                        else if (c == '\r' || c == '\n') { /* Enter は上で処理済み */ }
                        else if (c >= ' ') // 印字可能文字
                        {
                            _modalInputString += c;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        _modalSuggestionIndex = -1;
                        if (_isSaveModeActive || _isNavigateDownModeActive)
                            UpdateSubdirectorySuggestions();
                    }
                }
            }
            else
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
                
                // Show speed when selection changes
                _speedDisplayText = $"Speed: x{_currentPlaybackSpeed:0.00}";
                _speedDisplayTimer = SPEED_DISPLAY_DURATION;
                
                _lastSelectedIndex = currentSelectedIndex;
            }



            if (_volumeDisplayTimer > 0) _volumeDisplayTimer -= Time.deltaTime;
            if (_speedDisplayTimer > 0) _speedDisplayTimer -= Time.deltaTime;

            bool isFullscreen = videoGridManager.IsFullscreen(); 
            VideoPlayerUI currentVideo = videoGridManager.GetSelectedVideoUI();

            // Speed Control with Repeat Logic
            float speedChange = 0f;
            bool speedChanged = false;

            // W key (Reset) - No repeat needed
            if (Input.GetKeyDown(KeyCode.W))
            {
                _currentPlaybackSpeed = 1.0f;
                speedChanged = true;
            }
            // Q/E keys (Decrease/Increase) - With repeat
            else if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
            {
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
                {
                    // First press
                    _keyRepeatTimer = KEY_REPEAT_DELAY;
                    speedChange = Input.GetKey(KeyCode.Q) ? -0.25f : 0.25f;
                }
                else
                {
                    // Holding down
                    _keyRepeatTimer -= Time.deltaTime;
                    if (_keyRepeatTimer <= 0)
                    {
                        _keyRepeatTimer = KEY_REPEAT_RATE;
                        speedChange = Input.GetKey(KeyCode.Q) ? -0.25f : 0.25f;
                    }
                }
            }

            if (speedChange != 0 || speedChanged)
            {
                if (speedChange != 0)
                {
                    _currentPlaybackSpeed += speedChange;
                    _currentPlaybackSpeed = Mathf.Clamp(_currentPlaybackSpeed, 0.25f, 10.0f);
                }

                videoGridManager.SetSelectionPlaybackSpeed(_currentPlaybackSpeed);
                PlayerPrefs.SetFloat(PLAYBACK_SPEED_KEY, _currentPlaybackSpeed);
                _speedSavePending = true; // Save deferred to key release to avoid per-repeat disk I/O

                _speedDisplayText = $"Speed: x{_currentPlaybackSpeed:0.00}";
                _speedDisplayTimer = SPEED_DISPLAY_DURATION;
            }

            // Flush PlayerPrefs only when Q/E/W is released
            if (_speedSavePending && (Input.GetKeyUp(KeyCode.Q) || Input.GetKeyUp(KeyCode.E) || Input.GetKeyUp(KeyCode.W)))
            {
                PlayerPrefs.Save();
                _speedSavePending = false;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                if (currentVideo != null)
                {
                    _isSaveModeActive = true;
                    _modalInputString = "";
                    _modalSuggestionIndex = -1;
                    LaunchSuggestionScan(); // immediate on open, no debounce needed
                    _focusResetQueued = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                if (currentVideo != null)
                {
                    _isRenameModeActive = true;
                    _modalInputString = Path.GetFileName(currentVideo.GetVideoPath());
                    _modalSuggestions.Clear();
                    _modalSuggestionIndex = -1;
                    _focusResetQueued = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                // Reload all video thumbnails
                if (!string.IsNullOrEmpty(_currentVideoDirectory))
                {
                    LoadAllVideoPaths(_currentVideoDirectory);
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
                if (currentVideo != null)
                {
                    if (currentVideo.IsPlaying) { currentVideo.Pause(); }
                    else { currentVideo.Play(); }
                }
            }
            else if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (currentVideo != null) currentVideo.SeekToPercent(0);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (isCtrlPressed)
                {
                    videoGridManager.MoveSelection(Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1); 
                }
                else if (currentVideo != null)
                {
                    bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    float seekSeconds = isShiftPressed ? 300.0f : 15.0f;
                    currentVideo.Seek(Input.GetKeyDown(KeyCode.LeftArrow) ? -seekSeconds : seekSeconds);
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
                        else // At drive root, show drive selector
                        {
                            _isDriveSelectModeActive = true;
                            _modalInputString = "";
                            UpdateDriveSuggestions();
                            _focusResetQueued = true;
                                }
                    }
                    else // Down Arrow
                    {
                        if(!string.IsNullOrEmpty(_currentVideoDirectory))
                        {
                            _isNavigateDownModeActive = true;
                            _modalInputString = "";
                            _modalSuggestionIndex = -1;
                            LaunchSuggestionScan(); // immediate on open, no debounce needed
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
                if (currentVideo != null) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{currentVideo.GetVideoPath()}\"");
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                if (currentVideo != null) currentVideo.ToggleMute();
            }
            else if (Input.GetKeyDown(KeyCode.Delete))
            {
                if (currentVideo != null)
                {
                    string sourcePath = currentVideo.GetVideoPath();
                    // Permanently delete
                    if (_videoFileManager.DeleteVideoFile(sourcePath))
                    {
                        ThumbnailCache.Remove(sourcePath); // キャッシュから除去
                        _allVideoPaths.Remove(sourcePath);
                        RefreshGridDisplay(videoGridManager.GetSelectedVideoIndex(), isFullscreen);
                        _lastSelectedIndex = -1;
                    }
                }
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                if (!string.IsNullOrEmpty(_currentVideoDirectory) && !_bookmarks.Contains(_currentVideoDirectory))
                {
                    _bookmarks.Add(_currentVideoDirectory);
                    SaveBookmarks();
                }
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                 _isBookmarkModeActive = true;
                 _modalInputString = "";
                 _modalSuggestions = new List<string>(_bookmarks);
                 _modalSuggestionIndex = _modalSuggestions.Count > 0 ? 0 : -1;
                 _focusResetQueued = true;
            }
            else
            {
                // Percentage Seek Logic
                if (currentVideo != null)
                {
                    for (int i = 0; i <= 9; i++)
                    {
                        if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                        {
                            currentVideo.SeekToPercent(i * 0.1f);
                            break; 
                        }
                    }
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
                    ThumbnailCache.Remove(sourcePath); // 移動元のキャッシュを除去
                    _allVideoPaths.Remove(sourcePath);
                    RefreshGridDisplay(currentIndexOnScreen, wasFullscreen);
                    _lastSelectedIndex = -1;
                }
            }
        }

        private void UpdateDriveSuggestions()
        {
            _modalSuggestions.Clear();
            _modalSuggestionIndex = -1;
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var d in drives)
                {
                    if (d.IsReady)
                    {
                        _modalSuggestions.Add(d.Name);
                    }
                }
            }
            catch(System.Exception e) { Debug.LogError($"Error getting drives: {e.Message}"); }
            
            if (_modalSuggestions.Count > 0) 
            {
                _modalSuggestionIndex = 0;
                UpdateThumbnailPreview();
            }
        }

        // Called from text-change in OnGUI: only resets the debounce timer, no allocation
        private void UpdateSubdirectorySuggestions()
        {
            _suggestionsCts?.Cancel(); // stop any in-flight scan immediately
            _suggestionDebounceTimer = SUGGESTION_DEBOUNCE_SECONDS;
        }

        // Called from Update() when the debounce timer fires, or directly on modal open (no debounce needed)
        private void LaunchSuggestionScan()
        {
            _suggestionsCts?.Cancel();
            _suggestionsCts = new CancellationTokenSource();
            var ct = _suggestionsCts.Token;

            // Snapshot mutable state before handing off to background thread
            string inputSnapshot = _modalInputString ?? "";
            string dirSnapshot = _currentVideoDirectory;
            bool isSaveMode = _isSaveModeActive;
            var loader = _videoLoader;

            Task.Run(() =>
            {
                var results = new List<string>();
                try
                {
                    if (string.IsNullOrEmpty(dirSnapshot) || !Directory.Exists(dirSnapshot)) return;

                    // "anime/title" のようなサブフォルダパスに対応
                    int lastSep = inputSnapshot.LastIndexOfAny(new char[] { '/', '\\' });
                    string basePath = lastSep >= 0 ? inputSnapshot.Substring(0, lastSep) : "";
                    string filter   = lastSep >= 0 ? inputSnapshot.Substring(lastSep + 1) : inputSnapshot;

                    string searchDir = string.IsNullOrEmpty(basePath)
                        ? dirSnapshot
                        : Path.Combine(dirSnapshot, basePath);

                    if (!Directory.Exists(searchDir)) return;

                    const int MaxSuggestions = 30;
                    var subDirs = Directory.GetDirectories(searchDir);
                    foreach (var dir in subDirs)
                    {
                        if (ct.IsCancellationRequested) return;
                        if (results.Count >= MaxSuggestions) break;
                        string dirName = new DirectoryInfo(dir).Name;
                        if (string.IsNullOrEmpty(filter) || dirName.StartsWith(filter, System.StringComparison.OrdinalIgnoreCase))
                        {
                            string suggestion = string.IsNullOrEmpty(basePath) ? dirName : basePath + "/" + dirName;
                            if (isSaveMode || loader.DirectoryContainsVideos(dir, ct: ct))
                            {
                                results.Add(suggestion);
                            }

                            // マッチしたフォルダの直下サブフォルダも展開して候補に追加
                            if (string.IsNullOrEmpty(basePath))
                            {
                                foreach (var childDir in Directory.GetDirectories(dir))
                                {
                                    if (ct.IsCancellationRequested) return;
                                    if (results.Count >= MaxSuggestions) break;
                                    string childName = new DirectoryInfo(childDir).Name;
                                    string childSuggestion = dirName + "/" + childName;
                                    if (isSaveMode || loader.DirectoryContainsVideos(childDir, ct: ct))
                                    {
                                        results.Add(childSuggestion);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { /* ignore — no Unity API on background thread */ }

                if (!ct.IsCancellationRequested)
                {
                    lock (_suggestionsLock) { _pendingSuggestions = results; }
                }
            }, ct);
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

        private void PerformDriveSelectAction()
        {
            if (_modalSuggestionIndex >= 0 && _modalSuggestionIndex < _modalSuggestions.Count)
            {
                string targetDrive = _modalSuggestions[_modalSuggestionIndex];
                LoadAllVideoPaths(targetDrive);
            }
            _isDriveSelectModeActive = false;
            _focusResetQueued = true;
        }

        private void PerformBookmarkJumpAction()
        {
            if (_modalSuggestionIndex >= 0 && _modalSuggestionIndex < _modalSuggestions.Count)
            {
                string targetPath = _modalSuggestions[_modalSuggestionIndex];
                if (Directory.Exists(targetPath))
                {
                    LoadAllVideoPaths(targetPath);
                }
            }
            _isBookmarkModeActive = false;
            _focusResetQueued = true;
        }

        private void LoadBookmarks()
        {
            string stored = PlayerPrefs.GetString(BOOKMARKS_KEY, "");
            if (!string.IsNullOrEmpty(stored))
            {
                _bookmarks = stored.Split(new char[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        private void SaveBookmarks()
        {
            if (_bookmarks.Count > 0)
            {
                string stored = string.Join(";", _bookmarks);
                PlayerPrefs.SetString(BOOKMARKS_KEY, stored);
            }
            else
            {
                PlayerPrefs.DeleteKey(BOOKMARKS_KEY);
            }
            PlayerPrefs.Save();
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
            if (_isSaveModeActive || _isRenameModeActive || _isNavigateDownModeActive || _isDriveSelectModeActive || _isBookmarkModeActive)
            {
                // キー入力は Update() の Input.inputString / GetKeyDown で処理済み
                // OnGUI はモーダルの描画のみ担当

                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                
                // Increased size by 1.5x (400x300 -> 600x450)
                float boxWidth = 600;
                float boxHeight = 450;
                Rect boxRect = new Rect((Screen.width - boxWidth) / 2, (Screen.height - boxHeight) / 2, boxWidth, boxHeight);
                
                string boxTitle = "Action";
                if (_isRenameModeActive) boxTitle = "Rename File";
                else if (_isSaveModeActive) boxTitle = "Save to Subfolder";
                else if (_isNavigateDownModeActive) boxTitle = "Navigate to Subfolder";
                else if (_isDriveSelectModeActive) boxTitle = "Select Drive";
                else if (_isBookmarkModeActive) boxTitle = "Jump to Bookmark";

                GUI.Box(boxRect, boxTitle);

                if (!_isDriveSelectModeActive && !_isBookmarkModeActive)
                {
                    if (_modalTextFieldStyle == null)
                        _modalTextFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 18 };
                    // 入力は Update() の Input.inputString で処理。
                    // GUI.TextField は IMGUI がキーを横取りするため GUI.Label で表示のみ行う
                    bool cursorOn = Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 2 == 0;
                    string displayText = _modalInputString + (cursorOn ? "|" : "");
                    Rect inputRect = new Rect(boxRect.x + 10, boxRect.y + 30, boxRect.width - 20, 30);
                    // テキストフィールド風の背景を描画してから文字を Label で重ねる
                    GUI.Box(inputRect, GUIContent.none);
                    GUI.Label(inputRect, "  " + displayText, _modalTextFieldStyle);
                }

                if ((_isSaveModeActive || _isNavigateDownModeActive || _isDriveSelectModeActive || _isBookmarkModeActive) && _modalSuggestions.Count > 0)
                {
                    if (_modalSuggestionStyle == null)
                    {
                        _modalSuggestionStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
                        _modalSuggestionHighlightStyle = new GUIStyle(_modalSuggestionStyle) { normal = { textColor = Color.yellow } };
                    }
                    GUIStyle sStyle = _modalSuggestionStyle;
                    GUIStyle hStyle = _modalSuggestionHighlightStyle;
                    float startY = (_isDriveSelectModeActive || _isBookmarkModeActive) ? boxRect.y + 30 : boxRect.y + 70;
                    
                    // Limit the number of items shown if they exceed the box height
                    int maxItems = Mathf.FloorToInt((boxHeight - (startY - boxRect.y)) / 25) - 1;
                    int displayStart = 0;
                    if (_modalSuggestions.Count > maxItems && _modalSuggestionIndex > maxItems / 2)
                    {
                        displayStart = Mathf.Min(_modalSuggestionIndex - (maxItems / 2), _modalSuggestions.Count - maxItems);
                    }
                    
                    for (int i = 0; i < maxItems && (displayStart + i) < _modalSuggestions.Count; i++)
                    {
                        int index = displayStart + i;
                        string rawText = _modalSuggestions[index];
                        string displayText = TruncateMiddle(rawText, 65); // Truncate if too long
                        
                        GUI.Label(new Rect(boxRect.x + 10, startY + (i * 25), boxRect.width - 20, 25), displayText, (index == _modalSuggestionIndex) ? hStyle : sStyle);
                    }
                }
                
                // Draw Thumbnails
                if (_thumbnailGenerator != null && _thumbnailGenerator.GeneratedThumbnails.Count > 0)
                {
                     float thumbScale = 0.8f; 
                     float thumbW = 256 * thumbScale;
                     float thumbH = 144 * thumbScale;
                     float padding = 10;
                     float totalWidth = _thumbnailGenerator.GeneratedThumbnails.Count * (thumbW + padding) - padding;
                     float startX = (Screen.width - totalWidth) / 2;
                     float startY = boxRect.yMax + 20;
                     
                     for(int i=0; i < _thumbnailGenerator.GeneratedThumbnails.Count; i++)
                     {
                         if (_thumbnailGenerator.GeneratedThumbnails[i] != null)
                         {
                             GUI.DrawTexture(new Rect(startX + i * (thumbW + padding), startY, thumbW, thumbH), _thumbnailGenerator.GeneratedThumbnails[i]);
                         }
                     }
                }
            }
            else
            {
                // Initialise cached HUD styles once — avoids allocating new GUIStyle every frame
                if (_hudStyleUpperLeft == null)
                {
                    _hudStyleUpperLeft   = new GUIStyle { fontSize = 20, normal = { textColor = Color.white }, alignment = TextAnchor.UpperLeft };
                    _hudStyleUpperRight  = new GUIStyle { fontSize = 20, normal = { textColor = Color.white }, alignment = TextAnchor.UpperRight };
                    _hudStyleFullscreenName = new GUIStyle { fontSize = 20, normal = { textColor = Color.white }, alignment = TextAnchor.UpperLeft, wordWrap = true };
                    _hudStyleLowerCenter = new GUIStyle { fontSize = 24, normal = { textColor = Color.white }, alignment = TextAnchor.LowerCenter };
                }

                const int TOP_MARGIN = 20;

                GUI.color = Color.black;
                GUI.Label(new Rect(11, TOP_MARGIN + 1, 300, 30), _videoCountText, _hudStyleUpperLeft);
                GUI.color = Color.white;
                GUI.Label(new Rect(10, TOP_MARGIN, 300, 30), _videoCountText, _hudStyleUpperLeft);

                Rect dirRect = new Rect(Screen.width - 710, TOP_MARGIN, 700, 60);
                GUI.color = Color.black;
                GUI.Label(new Rect(dirRect.x + 1, dirRect.y + 1, dirRect.width, dirRect.height), _currentVideoDirectory, _hudStyleUpperRight);
                GUI.color = Color.white;
                GUI.Label(dirRect, _currentVideoDirectory, _hudStyleUpperRight);

                if (!string.IsNullOrEmpty(_fullscreenDisplayFileName))
                {
                    Rect filenameRect = new Rect(10, TOP_MARGIN + 30, Screen.width - 20, 60);
                    GUI.color = Color.black;
                    GUI.Label(new Rect(filenameRect.x + 1, filenameRect.y + 1, filenameRect.width, filenameRect.height), _fullscreenDisplayFileName, _hudStyleFullscreenName);
                    GUI.color = Color.white;
                    GUI.Label(filenameRect, _fullscreenDisplayFileName, _hudStyleFullscreenName);
                }

                if (_volumeDisplayTimer > 0)
                {
                    GUI.color = Color.black; GUI.Label(new Rect(0, Screen.height - 41, Screen.width, 40), _volumeDisplayText, _hudStyleLowerCenter);
                    GUI.color = Color.white; GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 40), _volumeDisplayText, _hudStyleLowerCenter);
                }

                if (_speedDisplayTimer > 0)
                {
                    float yPos = (_volumeDisplayTimer > 0) ? Screen.height - 80 : Screen.height - 40;
                    GUI.color = Color.black; GUI.Label(new Rect(0, yPos - 1, Screen.width, 40), _speedDisplayText, _hudStyleLowerCenter);
                    GUI.color = Color.white; GUI.Label(new Rect(0, yPos, Screen.width, 40), _speedDisplayText, _hudStyleLowerCenter);
                }
            }
        }

        private string TruncateMiddle(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            int half = (maxChars - 3) / 2;
            return text.Substring(0, half) + "..." + text.Substring(text.Length - half);
        }
        
        private void UpdateThumbnailPreview()
        {
             if (_modalSuggestions.Count > 0 && _modalSuggestionIndex >= 0 && _modalSuggestionIndex < _modalSuggestions.Count)
             {
                  // Differentiate between Drive selection (which are full paths or drive letters) and Subdirectories (names only)
                  string path = "";
                  if (_isDriveSelectModeActive || _isBookmarkModeActive)
                  {
                      path = _modalSuggestions[_modalSuggestionIndex];
                  }
                  else
                  {
                      path = Path.Combine(_currentVideoDirectory, _modalSuggestions[_modalSuggestionIndex]);
                  }
                  
                  if (Directory.Exists(path))
                  {
                      _thumbnailGenerator.UpdateThumbnails(path);
                  }
             }
        }
    }
}

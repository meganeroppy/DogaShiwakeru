using System.Collections.Generic;
using UnityEngine;

namespace DogaShiwakeru
{
    public class VideoGridManager : MonoBehaviour
    {
        public VideoPlayerUI videoPlayerUIPrefab;
        public Transform gridParent;
        public RectTransform canvasRectTransform;

        private List<VideoPlayerUI> _currentVideoUIs = new List<VideoPlayerUI>();
        private int _selectedVideoIndex = -1;
        private int _fullscreenVideoIndex = -1;
        private float _currentSelectionSpeed = 1.0f;
        private Coroutine _prepareCoroutine;

        // The MainController now controls how many are activated initially
        // private const int VISIBLE_BUFFER_COUNT = 20; 

        public bool IsFullscreen()
        {
            return _fullscreenVideoIndex != -1;
        }

        public void DisplayVideos(List<string> videoPaths)
        {
            ClearVideos();
            if (videoPaths == null || videoPaths.Count == 0)
            {
                Debug.Log("No video files to display in grid.");
                return;
            }

            // Instantiate all videos provided by MainController
            for (int i = 0; i < videoPaths.Count; i++)
            {
                VideoPlayerUI videoUI = Instantiate(videoPlayerUIPrefab, gridParent);
                videoUI.Init(videoPaths[i]);
                videoUI.SetAutoPlay(false);
                videoUI.SetMute(true);
                _currentVideoUIs.Add(videoUI);
            }
            
            // Start staggered prepare to avoid simultaneous decode overload
            if (_prepareCoroutine != null) StopCoroutine(_prepareCoroutine);
            _prepareCoroutine = StartCoroutine(StaggeredPrepareCoroutine());
        }

        // Prepare videos one at a time to prevent FPS drop from simultaneous decode
        private System.Collections.IEnumerator StaggeredPrepareCoroutine()
        {
            const int MAX_SIMULTANEOUS = 2;
            int activeCount = 0;

            for (int i = 0; i < _currentVideoUIs.Count; i++)
            {
                var ui = _currentVideoUIs[i];
                if (ui == null) continue;

                // Wait until active concurrent prepares are below the limit
                while (activeCount >= MAX_SIMULTANEOUS)
                {
                    activeCount = 0;
                    foreach (var v in _currentVideoUIs)
                    {
                        if (v != null && v.IsPreparingOrPlaying()) activeCount++;
                    }
                    yield return new UnityEngine.WaitForSeconds(0.3f);
                }

                if (ui != null)
                {
                    ui.Activate();
                    activeCount++;
                }

                // Small delay between each activation to spread the load
                yield return new UnityEngine.WaitForSeconds(0.1f);
            }
        }

        private void ClearVideos()
        {
            if (_prepareCoroutine != null)
            {
                StopCoroutine(_prepareCoroutine);
                _prepareCoroutine = null;
            }

            foreach (VideoPlayerUI videoUI in _currentVideoUIs)
            {
                if (videoUI != null) Destroy(videoUI.gameObject);
            }
            _currentVideoUIs.Clear();
            _selectedVideoIndex = -1;
            _fullscreenVideoIndex = -1;
        }

        public VideoPlayerUI GetVideoUI(int index)
        {
            return (index >= 0 && index < _currentVideoUIs.Count) ? _currentVideoUIs[index] : null;
        }

        public int GetVideoCount() => _currentVideoUIs.Count;
        public VideoPlayerUI GetSelectedVideoUI() => GetVideoUI(_selectedVideoIndex);
        public int GetSelectedVideoIndex() => _selectedVideoIndex;

        public void SetSelectedVideo(int index, bool shouldPlay = true)
        {
            if (_selectedVideoIndex == index) return;

            // Deselect old: switch to low-FPS thumbnail mode
            if (_selectedVideoIndex != -1)
            {
                var oldSelectedUI = GetVideoUI(_selectedVideoIndex);
                if (oldSelectedUI != null)
                {
                    oldSelectedUI.SetSelected(false);
                    oldSelectedUI.SetMute(true);
                    oldSelectedUI.SetThumbnailMode(); // Drop to ~0.4fps
                }
            }

            _selectedVideoIndex = index;

            // Select new: restore full-quality playback
            var newSelectedUI = GetVideoUI(_selectedVideoIndex);
            if (newSelectedUI != null)
            {
                newSelectedUI.SetSelected(true);
                newSelectedUI.SetMute(false);
                newSelectedUI.SetPlaybackSpeed(_currentSelectionSpeed);
                newSelectedUI.RestorePlayMode(shouldPlay); // Full FPS, full quality
            }
        }

        public void SetSelectionPlaybackSpeed(float speed)
        {
            Debug.Log($"[VideoGridManager] SetSelectionPlaybackSpeed: {speed}");
            _currentSelectionSpeed = speed;
            var selectedUI = GetSelectedVideoUI();
            if (selectedUI != null)
            {
                selectedUI.SetPlaybackSpeed(_currentSelectionSpeed);
            }
            else
            {
                Debug.LogWarning("[VideoGridManager] No selected video UI to set speed on.");
            }
        }

        public void SelectAndPossiblyFullscreen(int index, bool makeFullscreen, bool shouldPlay = true)
        {
            // If currently in fullscreen and moving to a new selection, exit old fullscreen first.
            if (IsFullscreen() && _fullscreenVideoIndex != index)
            {
                ExitFullscreen();
            }

            SetSelectedVideo(index, shouldPlay);

            if (makeFullscreen && _selectedVideoIndex != -1)
            {
                EnterFullscreen(_selectedVideoIndex);
            }
        }

        public void MoveSelection(int direction)
        {
            if (_currentVideoUIs.Count == 0) return;

            bool wasPlaying = false;
            var currentUI = GetSelectedVideoUI();
            if (currentUI != null)
            {
                wasPlaying = currentUI.IsPlaying;
            }

            int newIndex = _selectedVideoIndex + direction;
            if (_selectedVideoIndex == -1)
            {
                newIndex = (direction > 0) ? 0 : _currentVideoUIs.Count - 1;
            }
            else
            {
                newIndex = _selectedVideoIndex + direction;
                if (newIndex < 0) newIndex = _currentVideoUIs.Count - 1;
                else if (newIndex >= _currentVideoUIs.Count) newIndex = 0;
            }

            SelectAndPossiblyFullscreen(newIndex, IsFullscreen(), wasPlaying);
        }

        public void DeselectOrExitFullscreen()
        {
            if (IsFullscreen())
            {
                ExitFullscreen();
            }
            else
            {
                SetSelectedVideo(-1);
            }
        }

        public void ToggleFullscreenOnSelected()
        {
            if (_selectedVideoIndex == -1) return;

            if (IsFullscreen())
            {
                if (_selectedVideoIndex == _fullscreenVideoIndex) // If the selected one IS the fullscreen one, toggle off
                {
                    ExitFullscreen();
                }
                else // A different video is fullscreen, switch to the selected one
                {
                    ExitFullscreen();
                    EnterFullscreen(_selectedVideoIndex);
                }
            }
            else
            {
                EnterFullscreen(_selectedVideoIndex);
            }
        }
        
        public void EnterFullscreen(int index)
        {
            if (index == -1) return;
            var videoUI = GetVideoUI(index);
            if (videoUI != null && !videoUI.IsFullscreen())
            {
                videoUI.Activate(); // Ensure video is activated before going fullscreen
                videoUI.SetPlaybackSpeed(_currentSelectionSpeed); // Always current speed in fullscreen
                videoUI.ToggleFullscreen(canvasRectTransform);
                _fullscreenVideoIndex = index;
            }
        }

        private void ExitFullscreen()
        {
            if (_fullscreenVideoIndex == -1) return;
            var videoUI = GetVideoUI(_fullscreenVideoIndex);
            if (videoUI != null && videoUI.IsFullscreen())
            {
                videoUI.ToggleFullscreen(canvasRectTransform);
                // After exiting fullscreen, the video is still the selected one, so it should play at current selected speed.
                videoUI.SetPlaybackSpeed(_currentSelectionSpeed); 
            }
            _fullscreenVideoIndex = -1;
            ReorderGrid();
        }

        private void ReorderGrid()
        {
            for (int i = 0; i < _currentVideoUIs.Count; i++)
            {
                if (_currentVideoUIs[i] != null)
                {
                    _currentVideoUIs[i].transform.SetSiblingIndex(i);
                }
            }
        }
    }
}
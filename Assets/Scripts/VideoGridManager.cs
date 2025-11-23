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

            // Instantiate all videos provided by MainController (which is already limited to MAX_VIDEOS_ON_SCREEN)
            for (int i = 0; i < videoPaths.Count; i++)
            {
                VideoPlayerUI videoUI = Instantiate(videoPlayerUIPrefab, gridParent);
                videoUI.Init(videoPaths[i]); // Init path for all
                
                videoUI.Activate(); // Activate (prepare) all displayed videos
                videoUI.SetPlaybackSpeed(0.25f); // Set to 0.25f for all non-selected displayed thumbnails
                
                videoUI.SetMute(true);
                _currentVideoUIs.Add(videoUI);
            }
        }

        private void ClearVideos()
        {
            foreach (VideoPlayerUI videoUI in _currentVideoUIs)
            {
                Destroy(videoUI.gameObject);
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

        public void SetSelectedVideo(int index)
        {
            if (_selectedVideoIndex == index) return;

            // Deselect old
            if (_selectedVideoIndex != -1)
            {
                var oldSelectedUI = GetVideoUI(_selectedVideoIndex);
                if (oldSelectedUI != null)
                {
                    oldSelectedUI.SetSelected(false);
                    oldSelectedUI.SetMute(true);
                    oldSelectedUI.SetPlaybackSpeed(0.25f); // Set to 0.25f when deselected
                }
            }

            _selectedVideoIndex = index;

            // Select new
            var newSelectedUI = GetVideoUI(_selectedVideoIndex);
            if (newSelectedUI != null)
            {
                newSelectedUI.SetSelected(true);
                newSelectedUI.SetMute(false);
                newSelectedUI.SetPlaybackSpeed(1.0f); // Selected video always plays full speed
            }
        }

        public void SelectAndPossiblyFullscreen(int index, bool makeFullscreen)
        {
            // If currently in fullscreen and moving to a new selection, exit old fullscreen first.
            if (IsFullscreen() && _fullscreenVideoIndex != index)
            {
                ExitFullscreen();
            }

            SetSelectedVideo(index);

            if (makeFullscreen && _selectedVideoIndex != -1)
            {
                EnterFullscreen(_selectedVideoIndex);
            }
        }

        public void MoveSelection(int direction)
        {
            if (_currentVideoUIs.Count == 0) return;

            int newIndex = _selectedVideoIndex + direction;
            if (_selectedVideoIndex == -1)
            {
                newIndex = (direction > 0) ? 0 : _currentVideoUIs.Count - 1;
            }
            else
            {
                newIndex = (newIndex + _currentVideoUIs.Count) % _currentVideoUIs.Count;
            }

            SelectAndPossiblyFullscreen(newIndex, IsFullscreen());
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
                videoUI.SetPlaybackSpeed(1.0f); // Always full speed in fullscreen
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
                // After exiting fullscreen, the video is still the selected one, so it should play at full speed.
                videoUI.SetPlaybackSpeed(1.0f); 
            }
            _fullscreenVideoIndex = -1;
        }
    }
}
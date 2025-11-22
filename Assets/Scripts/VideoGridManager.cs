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
        private int _fullscreenVideoIndex = -1; // The single source of truth for fullscreen state

        public bool IsFullscreen()
        {
            return _fullscreenVideoIndex != -1;
        }

        public void DisplayVideos(List<string> videoPaths)
        {
            ClearVideos();
            if (videoPaths == null) return;
            foreach (string path in videoPaths)
            {
                VideoPlayerUI videoUI = Instantiate(videoPlayerUIPrefab, gridParent);
                videoUI.SetVideo(path);
                videoUI.SetPlaybackSpeed(0.1f);
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
                    oldSelectedUI.SetPlaybackSpeed(0.1f);
                }
            }

            _selectedVideoIndex = index;

            // Select new
            var newSelectedUI = GetVideoUI(_selectedVideoIndex);
            if (newSelectedUI != null)
            {
                newSelectedUI.SetSelected(true);
                newSelectedUI.SetMute(false);
                newSelectedUI.SetPlaybackSpeed(1.0f);
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
                // If currently in fullscreen
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
            else // Not in fullscreen, so enter fullscreen with the selected video
            {
                EnterFullscreen(_selectedVideoIndex);
            }
        }
        
        private void EnterFullscreen(int index)
        {
            if (index == -1) return;
            var videoUI = GetVideoUI(index);
            if (videoUI != null && !videoUI.IsFullscreen())
            {
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
            }
            _fullscreenVideoIndex = -1;
        }
    }
}
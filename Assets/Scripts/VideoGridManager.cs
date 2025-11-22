using System.Collections.Generic;
using UnityEngine;

namespace DogaShiwakeru
{
    public class VideoGridManager : MonoBehaviour
    {
        public VideoPlayerUI videoPlayerUIPrefab;
        public Transform gridParent;

        private List<VideoPlayerUI> _currentVideoUIs = new List<VideoPlayerUI>();
        private int _selectedVideoIndex = -1;

        public void DisplayVideos(List<string> videoPaths)
        {
            ClearVideos();

            if (videoPaths == null || videoPaths.Count == 0)
            {
                Debug.Log("No video files to display.");
                return;
            }

            foreach (string path in videoPaths)
            {
                VideoPlayerUI videoUI = Instantiate(videoPlayerUIPrefab, gridParent);
                videoUI.SetVideo(path);
                videoUI.SetPlaybackSpeed(0.1f); // Low FPS
                videoUI.SetMute(true); // Mute all videos on load
                _currentVideoUIs.Add(videoUI);
            }
            Debug.Log($"Displayed {videoPaths.Count} videos in the grid.");
        }

        private void ClearVideos()
        {
            foreach (VideoPlayerUI videoUI in _currentVideoUIs)
            {
                Destroy(videoUI.gameObject);
            }
            _currentVideoUIs.Clear();
            _selectedVideoIndex = -1;
            Debug.Log("Cleared existing video UIs.");
        }

        public VideoPlayerUI GetVideoUI(int index)
        {
            if (index >= 0 && index < _currentVideoUIs.Count)
            {
                return _currentVideoUIs[index];
            }
            return null;
        }

        public int GetVideoCount()
        {
            return _currentVideoUIs.Count;
        }

        public RectTransform canvasRectTransform; // Assign from MainController

        public void SetSelectedVideo(int index, bool isFullscreenMode)
        {
            if (_selectedVideoIndex == index) return; // No change if re-selecting the same video

            // Deselect the old video
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                var oldSelectedUI = _currentVideoUIs[_selectedVideoIndex];
                oldSelectedUI.SetSelected(false);
                oldSelectedUI.SetMute(true);
                oldSelectedUI.SetPlaybackSpeed(0.1f);
                // If in fullscreen mode, the old video must exit fullscreen.
                if (isFullscreenMode)
                {
                    oldSelectedUI.ToggleFullscreen(canvasRectTransform);
                }
            }

            _selectedVideoIndex = index;

            // Select the new video
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                var newSelectedUI = _currentVideoUIs[_selectedVideoIndex];
                newSelectedUI.SetSelected(true);
                newSelectedUI.SetMute(false); // Always unmute the selected video
                newSelectedUI.SetPlaybackSpeed(1.0f);
                // If in fullscreen mode, the new video must enter fullscreen.
                if (isFullscreenMode)
                {
                    newSelectedUI.ToggleFullscreen(canvasRectTransform);
                }
                Debug.Log($"Selected video at index: {index}");
            }
            else
            {
                Debug.Log("No video selected.");
            }
        }

        public VideoPlayerUI GetSelectedVideoUI()
        {
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                return _currentVideoUIs[_selectedVideoIndex];
            }
            return null;
        }

        public int GetSelectedVideoIndex()
        {
            return _selectedVideoIndex;
        }

        public void MoveSelection(int direction, bool isFullscreenMode) // -1 for left, 1 for right
        {
            if (_currentVideoUIs.Count == 0) return;

            int newIndex = _selectedVideoIndex + direction;

            if (_selectedVideoIndex == -1) // If nothing is selected, start from the beginning or end.
            {
                newIndex = (direction > 0) ? 0 : _currentVideoUIs.Count - 1;
            }
            else
            {
                if (newIndex < 0) newIndex = _currentVideoUIs.Count - 1;
                else if (newIndex >= _currentVideoUIs.Count) newIndex = 0;
            }

            SetSelectedVideo(newIndex, isFullscreenMode);
        }

        public void DeselectAll(bool isFullscreenMode)
        {
            // In fullscreen, deselecting all should exit the fullscreen view
            if (isFullscreenMode && _selectedVideoIndex != -1)
            {
                _currentVideoUIs[_selectedVideoIndex].ToggleFullscreen(canvasRectTransform);
            }
            SetSelectedVideo(-1, false); // Always exit fullscreen mode logic when deselecting
        }
    }
}

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

        public RectTransform canvasRectTransform;

        public void SetSelectedVideo(int index, bool maintainFullscreen = false)
        {
            Debug.Log($"[DIAG] VideoGridManager.SetSelectedVideo called with index: {index}, maintainFullscreen: {maintainFullscreen}");
            if (_selectedVideoIndex == index) return;

            // Get safe references to the old and new UI objects
            VideoPlayerUI oldSelectedUI = null;
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                oldSelectedUI = _currentVideoUIs[_selectedVideoIndex];
            }

            _selectedVideoIndex = index;

            VideoPlayerUI newSelectedUI = null;
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                newSelectedUI = _currentVideoUIs[_selectedVideoIndex];
            }

            Debug.Log($"[DIAG] Old UI: {(oldSelectedUI != null ? oldSelectedUI.name : "null")}, New UI: {(newSelectedUI != null ? newSelectedUI.name : "null")}");

            // --- Process NEW selection FIRST ---
            if (newSelectedUI != null)
            {
                newSelectedUI.SetSelected(true);
                newSelectedUI.SetMute(false);
                newSelectedUI.SetPlaybackSpeed(1.0f);
                if (maintainFullscreen)
                {
                    Debug.Log($"[DIAG] New UI: maintainFullscreen is true. IsFullscreen() = {newSelectedUI.IsFullscreen()}. Calling ToggleFullscreen.");
                    newSelectedUI.ToggleFullscreen(canvasRectTransform);
                }
            }

            // --- Process OLD selection SECOND ---
            if (oldSelectedUI != null)
            {
                if (maintainFullscreen)
                {
                    Debug.Log($"[DIAG] Old UI: maintainFullscreen is true. IsFullscreen() = {oldSelectedUI.IsFullscreen()}. Calling ToggleFullscreen.");
                    oldSelectedUI.ToggleFullscreen(canvasRectTransform);
                }
                oldSelectedUI.SetSelected(false);
                oldSelectedUI.SetMute(true);
                oldSelectedUI.SetPlaybackSpeed(0.1f);
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

        public void MoveSelection(int direction, bool isFullscreenMode)
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

            SetSelectedVideo(newIndex, isFullscreenMode);
        }

        public void DeselectAll(bool isFullscreenMode)
        {
            if (isFullscreenMode && _selectedVideoIndex != -1)
            {
                _currentVideoUIs[_selectedVideoIndex].ToggleFullscreen(canvasRectTransform);
            }
            SetSelectedVideo(-1, false);
        }
    }
}
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

        public void SetSelectedVideo(int index, bool isMutedGlobally = false)
        {
            if (_currentVideoUIs.Count == 0) return;

            // Deselect the old video and explicitly mute it.
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                _currentVideoUIs[_selectedVideoIndex].SetSelected(false);
                _currentVideoUIs[_selectedVideoIndex].SetMute(true); // Always mute deselected video
                _currentVideoUIs[_selectedVideoIndex].SetPlaybackSpeed(0.1f);
            }

            _selectedVideoIndex = index;

            // Select the new video.
            if (_selectedVideoIndex != -1 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                _currentVideoUIs[_selectedVideoIndex].SetSelected(true);
                // Unmute the selected video ONLY if the global mute is off.
                _currentVideoUIs[_selectedVideoIndex].SetMute(isMutedGlobally);
                _currentVideoUIs[_selectedVideoIndex].SetPlaybackSpeed(1.0f);
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

        public void MoveSelection(int direction, bool isMutedGlobally) // -1 for left, 1 for right
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

            SetSelectedVideo(newIndex, isMutedGlobally);
        }

        public void DeselectAll()
        {
            SetSelectedVideo(-1);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;

namespace DogaShiwakeru
{
    public class VideoPlayerUI : MonoBehaviour
    {
        public RawImage videoDisplay;
        public VideoPlayer videoPlayer;
        public GameObject selectionHighlight;
        public RectTransform videoDisplayRectTransform; // Assign in Inspector

        private string _videoPath;
        private bool _isFullScreen = false;
        private Vector2 _originalSizeDelta;
        private Vector3 _originalLocalScale;
        private Vector3 _originalLocalPosition;
        private Transform _originalParent;
        private int _originalSiblingIndex;

        void Awake()
        {
            if (videoDisplay == null) videoDisplay = GetComponent<RawImage>();
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (videoDisplayRectTransform == null) videoDisplayRectTransform = GetComponent<RectTransform>();

            _originalSizeDelta = videoDisplayRectTransform.sizeDelta;
            _originalLocalScale = transform.localScale;
            _originalLocalPosition = transform.localPosition;
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();

            // Ensure the VideoPlayer is set up correctly
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = new RenderTexture(256, 256, 0);
            videoDisplay.texture = videoPlayer.targetTexture;

            // Subscribe to the prepareCompleted event
            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.loopPointReached += OnVideoEnd;

            SetSelected(false);
        }

        public void SetVideo(string path)
        {
            _videoPath = path;
            videoPlayer.url = "file://" + path; // Prepending "file://" is more robust
            Debug.Log($"Preparing video: {Path.GetFileName(path)}");
            videoPlayer.Prepare(); // Start the asynchronous preparation
        }

        // This function is called when the video is ready to play
        private void OnPrepareCompleted(VideoPlayer source)
        {
            Debug.Log($"Video prepared, now playing: {Path.GetFileName(source.url)}");
            source.Play();
        }

        public string GetVideoPath()
        {
            return _videoPath;
        }

        public void Pause()
        {
            videoPlayer.Pause();
        }

        public void SetMute(bool mute)
        {
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.SetDirectAudioMute(i, mute);
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            videoPlayer.playbackSpeed = speed;
        }

        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(isSelected);
            }
            else
            {
                Debug.LogWarning("Selection highlight not assigned for VideoPlayerUI.");
            }
        }

        public void ToggleFullscreen(RectTransform canvasRectTransform)
        {
            _isFullScreen = !_isFullScreen;

            if (_isFullScreen)
            {
                // Save original state if not already saved (should be in Awake, but as a safeguard)
                if (_originalParent == null)
                {
                    _originalSizeDelta = videoDisplayRectTransform.sizeDelta;
                    _originalLocalScale = transform.localScale;
                    _originalLocalPosition = transform.localPosition;
                    _originalParent = transform.parent;
                    _originalSiblingIndex = transform.GetSiblingIndex();
                }

                // Move to canvas root to overlay everything
                transform.SetParent(canvasRectTransform, true);
                videoDisplayRectTransform.anchorMin = Vector2.zero;
                videoDisplayRectTransform.anchorMax = Vector2.one;
                videoDisplayRectTransform.sizeDelta = Vector2.zero;
                videoDisplayRectTransform.anchoredPosition = Vector2.zero;
                transform.SetAsLastSibling(); // Bring to front

                // Resize render texture for better quality in fullscreen
                if (videoPlayer.targetTexture != null)
                {
                    videoPlayer.targetTexture.Release();
                }
                videoPlayer.targetTexture = new RenderTexture(Screen.width, Screen.height, 0);
                videoDisplay.texture = videoPlayer.targetTexture;

                Debug.Log("Entered fullscreen mode.");
            }
            else
            {
                // Restore original state
                transform.SetParent(_originalParent, true);
                transform.SetSiblingIndex(_originalSiblingIndex);
                videoDisplayRectTransform.sizeDelta = _originalSizeDelta;
                transform.localScale = _originalLocalScale;
                transform.localPosition = _originalLocalPosition;
                videoDisplayRectTransform.anchorMin = new Vector2(0.5f, 0.5f); // Assuming default center pivot
                videoDisplayRectTransform.anchorMax = new Vector2(0.5f, 0.5f);

                // Restore original render texture size
                if (videoPlayer.targetTexture != null)
                {
                    videoPlayer.targetTexture.Release();
                }
                videoPlayer.targetTexture = new RenderTexture(256, 256, 0); // Restore to original thumbnail size
                videoDisplay.texture = videoPlayer.targetTexture;

                Debug.Log("Exited fullscreen mode.");
            }
        }

        private void OnVideoEnd(VideoPlayer vp)
        {
            // Optional: Handle video ending if not looping
        }

        void OnDestroy()
        {
            if (videoPlayer != null && videoPlayer.targetTexture != null)
            {
                videoPlayer.targetTexture.Release();
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System;
using TMPro; // Required for TextMeshProUGUI

namespace DogaShiwakeru
{
    public class VideoPlayerUI : MonoBehaviour
    {
        public RawImage videoDisplay;
        public VideoPlayer videoPlayer;
        public GameObject selectionHighlight;
        public RectTransform videoDisplayRectTransform;
        public Slider progressSlider; // Assign a UI Slider in the Inspector
        public TextMeshProUGUI timeDisplayText; // Assign a TextMeshProUGUI in the Inspector

        private const int THUMBNAIL_RESOLUTION = 128;

        private string _videoPath;
        private bool _isMuted = true;
        private float _volume = 1.0f;
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

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = new RenderTexture(THUMBNAIL_RESOLUTION, THUMBNAIL_RESOLUTION, 0);
            videoDisplay.texture = videoPlayer.targetTexture;

            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.loopPointReached += OnVideoEnd;

            if (progressSlider != null)
            {
                progressSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }

            UpdateProgressUI(false); // Hide UI on awake
            SetSelected(false);
        }

        void Update()
        {
            if (videoPlayer.isPlaying && videoPlayer.length > 0)
            {
                if (progressSlider != null)
                {
                    progressSlider.value = (float)(videoPlayer.time / videoPlayer.length);
                }
                if (timeDisplayText != null)
                {
                    string currentTime = TimeSpan.FromSeconds(videoPlayer.time).ToString(@"mm\:ss");
                    string totalTime = TimeSpan.FromSeconds(videoPlayer.length).ToString(@"mm\:ss");
                    timeDisplayText.text = $"{currentTime} / {totalTime}";
                }
            }
        }
        
        public void OnSliderValueChanged(float value)
        {
            if (videoPlayer.isPrepared)
            {
                videoPlayer.time = videoPlayer.length * value;
            }
        }
        
        private void UpdateProgressUI(bool isVisible)
        {
            if (progressSlider != null)
            {
                progressSlider.gameObject.SetActive(isVisible);
            }
            if (timeDisplayText != null)
            {
                timeDisplayText.gameObject.SetActive(isVisible);
            }
        }

        public void SetVideo(string path)
        {
            _videoPath = path;
            videoPlayer.url = "file://" + path;
            Debug.Log($"Preparing video: {Path.GetFileName(path)}");
            videoPlayer.Prepare();
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            Debug.Log($"Video prepared: {Path.GetFileName(source.url)}. Applying initial mute state ({_isMuted}).");
            // Ensure mute state and volume is applied before playing
            for (ushort i = 0; i < source.audioTrackCount; i++)
            {
                source.SetDirectAudioMute(i, _isMuted);
                source.SetDirectAudioVolume(i, _volume);
            }
            source.Play();
            Debug.Log($"Now playing: {Path.GetFileName(source.url)}");
        }

        public string GetVideoPath()
        {
            return _videoPath;
        }

        public void Pause()
        {
            videoPlayer.Pause();
        }

        public void Seek(float seconds)
        {
            if (videoPlayer.isPrepared)
            {
                double newTime = videoPlayer.time + seconds;
                newTime = Math.Max(0, Math.Min(videoPlayer.length, newTime));
                videoPlayer.time = newTime;
            }
        }

        public void ToggleMute()
        {
            SetMute(!_isMuted);
        }
        
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            if (!videoPlayer.isPrepared) return;

            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.SetDirectAudioVolume(i, _volume);
            }
        }

        public void SetMute(bool mute)
        {
            _isMuted = mute;
            if (!videoPlayer.isPrepared) return; // Don't try to mute if not ready, OnPrepareCompleted will handle it

            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.SetDirectAudioMute(i, _isMuted);
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
                // Show highlight only if selected AND not in fullscreen.
                selectionHighlight.SetActive(isSelected && !_isFullScreen);
            }
            // Show progress UI only if in fullscreen.
            UpdateProgressUI(_isFullScreen);
        }

        public bool ToggleFullscreen(RectTransform canvasRectTransform)
        {
            _isFullScreen = !_isFullScreen;
            
            // Update UI visibility based on the new fullscreen state.
            UpdateProgressUI(_isFullScreen); 
            if (selectionHighlight != null)
            {
                // Highlight is active only when NOT in fullscreen (assuming this video is selected).
                selectionHighlight.SetActive(!_isFullScreen);
            }

            if (_isFullScreen)
            {
                if (_originalParent == null)
                {
                    _originalParent = transform.parent;
                    _originalSiblingIndex = transform.GetSiblingIndex();
                }

                transform.SetParent(canvasRectTransform, true);
                videoDisplayRectTransform.anchorMin = Vector2.zero;
                videoDisplayRectTransform.anchorMax = Vector2.one;
                videoDisplayRectTransform.sizeDelta = Vector2.zero;
                videoDisplayRectTransform.anchoredPosition = Vector2.zero;
                transform.SetAsLastSibling();

                if (videoPlayer.targetTexture != null) videoPlayer.targetTexture.Release();
                videoPlayer.targetTexture = new RenderTexture(Screen.width, Screen.height, 0);
                videoDisplay.texture = videoPlayer.targetTexture;

                Debug.Log("Entered fullscreen mode.");
            }
            else
            {
                transform.SetParent(_originalParent, true);
                transform.SetSiblingIndex(_originalSiblingIndex);
                videoDisplayRectTransform.sizeDelta = _originalSizeDelta;
                transform.localScale = _originalLocalScale;
                transform.localPosition = _originalLocalPosition;
                videoDisplayRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                videoDisplayRectTransform.anchorMax = new Vector2(0.5f, 0.5f);

                if (videoPlayer.targetTexture != null) videoPlayer.targetTexture.Release();
                videoPlayer.targetTexture = new RenderTexture(THUMBNAIL_RESOLUTION, THUMBNAIL_RESOLUTION, 0);
                videoDisplay.texture = videoPlayer.targetTexture;
                
                Debug.Log("Exited fullscreen mode.");
            }
            return _isFullScreen;
        }

        private void OnVideoEnd(VideoPlayer vp)
        {
            // Optional: Handle video ending if not looping
        }

        void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepareCompleted;
                videoPlayer.loopPointReached -= OnVideoEnd;
                if (videoPlayer.targetTexture != null)
                {
                    videoPlayer.targetTexture.Release();
                }
            }
        }
    }
}

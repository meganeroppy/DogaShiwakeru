using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System;
using TMPro;
using UnityEngine.EventSystems;

namespace DogaShiwakeru
{
    public class VideoPlayerUI : MonoBehaviour
    {
        public RawImage videoDisplay;
        public VideoPlayer videoPlayer;
        public GameObject selectionHighlight;
        public RectTransform videoDisplayRectTransform;
        public Slider progressSlider;
        public TextMeshProUGUI timeDisplayText;

        private const int THUMBNAIL_RESOLUTION = 256;

        private string _videoPath;
        private bool _isMuted = true;
        private float _volume = 1.0f;
        private bool _isFullScreen = false;
        private bool _isActivated = false; // Flag to check if player is prepared/preparing

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

            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.loopPointReached += OnVideoEnd;

            if (progressSlider != null)
            {
                progressSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }

            UpdateProgressUI(false);
            SetSelected(false);
        }

        public void Init(string path)
        {
            _videoPath = path;
            _isActivated = false;
            // Clear texture from previous use if this is a pooled object
            videoDisplay.texture = null;
            if (videoPlayer.targetTexture != null)
            {
                videoPlayer.targetTexture.Release();
                videoPlayer.targetTexture = null;
            }
            // Stop any ongoing video preparation/playback
            videoPlayer.Stop();
            videoPlayer.url = null;
        }

        public void Activate()
        {
            if (_isActivated) return; // Don't re-activate if already done

            _isActivated = true;
            if (videoPlayer.targetTexture == null)
            {
                 videoPlayer.targetTexture = new RenderTexture(THUMBNAIL_RESOLUTION, THUMBNAIL_RESOLUTION, 0);
                 videoDisplay.texture = videoPlayer.targetTexture;
            }
            
            videoPlayer.url = "file://" + _videoPath;
            videoPlayer.Prepare();
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
            if (videoPlayer.isPrepared) { videoPlayer.time = videoPlayer.length * value; }
        }

        private System.Collections.IEnumerator ResetFocusAfterDelay()
        {
            yield return null; // Wait for one frame
            EventSystem.current.SetSelectedGameObject(null);
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

        private void OnPrepareCompleted(VideoPlayer source)
        {
            for (ushort i = 0; i < source.audioTrackCount; i++)
            {
                source.SetDirectAudioMute(i, _isMuted);
                source.SetDirectAudioVolume(i, _volume);
            }
            source.Play();
        }

        public string GetVideoPath() => _videoPath;
        public void Pause() => videoPlayer.Pause();
        public void Play() => videoPlayer.Play();
        public void Seek(float seconds)
        {
            if (videoPlayer.isPrepared)
            {
                double newTime = videoPlayer.time + seconds;
                newTime = Math.Max(0, Math.Min(videoPlayer.length, newTime));
                videoPlayer.time = newTime;
            }
        }
        public void ToggleMute() => SetMute(!_isMuted);
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
            if (!videoPlayer.isPrepared) return;
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.SetDirectAudioMute(i, _isMuted);
            }
        }
        public void SetPlaybackSpeed(float speed)
        {
            // Ensure the video is activated (prepared) before setting speed
            if (!_isActivated)
            {
                Activate();
            }
            videoPlayer.playbackSpeed = speed;
        }
        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(isSelected && !_isFullScreen);
            }
            UpdateProgressUI(_isFullScreen);
        }
        public bool IsFullscreen() => _isFullScreen;
        public bool ToggleFullscreen(RectTransform canvasRectTransform)
        {
            _isFullScreen = !_isFullScreen;
            
            UpdateProgressUI(_isFullScreen); 
            if (selectionHighlight != null)
            {
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
            }
            return _isFullScreen; // CRITICAL: Ensure return statement is always present
        }

        private void OnVideoEnd(VideoPlayer vp) { }

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
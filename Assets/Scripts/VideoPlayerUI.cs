using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections; // Added for IEnumerator and WaitForSeconds

namespace DogaShiwakeru
{
    public class VideoPlayerUI : MonoBehaviour
    {
        [Header("Component References")]
        public RawImage videoDisplay;
        public VideoPlayer videoPlayer;
        public GameObject selectionHighlight;
        public RectTransform videoDisplayRectTransform; // The child object with the RawImage
        
        [Header("UI Elements")]
        public Slider progressSlider;
        public TextMeshProUGUI timeDisplayText;
        
        private AspectRatioFitter _aspectRatioFitter;
        private string _totalTimeFormatted;

        private const int THUMBNAIL_HEIGHT = 256;

        private string _videoPath;
        private bool _isMuted = true;
        private float _volume = 1.0f;
        private bool _isFullScreen = false;
        private bool _isActivated = false;

        // Store original transform state
        private Vector3 _originalLocalScale;
        private Vector3 _originalLocalPosition;
        private Transform _originalParent;
        private int _originalSiblingIndex;

                void Awake()

                {

                    if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();

        

                    if (videoDisplay != null)

                    {

                        _aspectRatioFitter = videoDisplay.GetComponent<AspectRatioFitter>();

                    }

        

                                                        if (timeDisplayText != null)

        

                                                        {

        

                                                            timeDisplayText.enableAutoSizing = false;

        

                                                            timeDisplayText.fontSize = 20;

        

                                            

        

                                                                            var rectTransform = timeDisplayText.rectTransform;

        

                                            

        

                                                                            if (rectTransform != null)

        

                                            

        

                                                                            {

        

                                            

        

                                                                                // Anchor to the bottom-right corner

        

                                            

        

                                                                                rectTransform.anchorMin = new Vector2(1, 0);

        

                                            

        

                                                                                rectTransform.anchorMax = new Vector2(1, 0);

        

                                            

        

                                                                                rectTransform.pivot = new Vector2(1, 0);

        

                                            

        

                                                                                

        

                                            

        

                                                                                                    // Add some padding from the corner

        

                                            

        

                                                                                

        

                                            

        

                                                                                                    rectTransform.anchoredPosition = new Vector2(-10, 20);

        

                                            

        

                                                                                

        

                                            

        

                                                                                                    

        

                                            

        

                                                                                

        

                                            

        

                                                                                                    // Ensure enough space for the text and prevent wrapping

        

                                            

        

                                                                                rectTransform.sizeDelta = new Vector2(150, 30); // Width 150, Height 30

        

                                            

        

                                                                            }

        

                                            

        

                                                                            

        

                                            

        

                                                                                                            // Align the text to the right

        

                                            

        

                                                                            

        

                                            

        

                                                                                                            timeDisplayText.alignment = TMPro.TextAlignmentOptions.BottomRight;

        

                                            

        

                                                                            

        

                                            

        

                                                                                                            timeDisplayText.enableWordWrapping = false; // Prevent vertical wrapping

        

                                            

        

                                                                            

        

                                            

        

                                                                                                        }

        

                                            

        

                                                                            

        

                                            

        

                                                                                            

        

                                            

        

                                                                            

        

                                            

        

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
            _totalTimeFormatted = "00:00.0"; // Reset
            videoDisplay.texture = null;
            if (videoPlayer.targetTexture != null)
            {
                videoPlayer.targetTexture.Release();
                videoPlayer.targetTexture = null;
            }
            videoPlayer.Stop();
            videoPlayer.url = null;
        }

        public void Activate()
        {
            if (_isActivated) return;
            _isActivated = true;
            
            // A temporary small texture is fine, it will be recreated OnPrepareCompleted
            if (videoPlayer.targetTexture == null)
            {
                 videoPlayer.targetTexture = new RenderTexture(THUMBNAIL_HEIGHT, THUMBNAIL_HEIGHT, 0);
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
                    string currentTime = FormatTime(videoPlayer.time);
                    timeDisplayText.text = $"{currentTime} / {_totalTimeFormatted}";
                }
            }
        }
        
        public void OnSliderValueChanged(float value)
        {
            if (videoPlayer.isPrepared) { videoPlayer.time = videoPlayer.length * value; }
        }
        
        private void UpdateProgressUI(bool isVisible)
        {
            if (progressSlider != null) progressSlider.gameObject.SetActive(isVisible);
            if (timeDisplayText != null) timeDisplayText.gameObject.SetActive(isVisible);
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            RecreateRenderTextureForThumbnail();
            
            _totalTimeFormatted = FormatTime(source.length);

            if (_aspectRatioFitter != null && source.texture != null && source.texture.height > 0)
            {
                float videoAspectRatio = (float)source.texture.width / (float)source.texture.height;
                _aspectRatioFitter.aspectRatio = videoAspectRatio;
            }

            for (ushort i = 0; i < source.audioTrackCount; i++)
            {
                source.SetDirectAudioMute(i, _isMuted);
                source.SetDirectAudioVolume(i, _volume);
            }
            source.Play();
        }

        private string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:00}:{1:00}.{2}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds / 100);
        }
        
        private void RecreateRenderTextureForThumbnail()
        {
            if (videoPlayer.texture == null || videoPlayer.texture.height == 0) return; // Use videoPlayer.texture for original dimensions

            float videoAspectRatio = (float)videoPlayer.texture.width / (float)videoPlayer.texture.height;
            int width = Mathf.RoundToInt(THUMBNAIL_HEIGHT * videoAspectRatio);
            int height = THUMBNAIL_HEIGHT;

            if (videoPlayer.targetTexture != null) videoPlayer.targetTexture.Release();
            videoPlayer.targetTexture = new RenderTexture(width, height, 0);
            videoDisplay.texture = videoPlayer.targetTexture;
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

        public void SeekToPercent(float percent)
        {
            if (videoPlayer.isPrepared && videoPlayer.length > 0)
            {
                float clampedPercent = Mathf.Clamp01(percent);
                videoPlayer.time = videoPlayer.length * clampedPercent;
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
            if (!_isActivated) Activate();
            videoPlayer.playbackSpeed = speed;
        }
        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null) selectionHighlight.SetActive(isSelected);
            UpdateProgressUI(_isFullScreen);
        }
        public bool IsFullscreen() => _isFullScreen;
        public bool ToggleFullscreen(RectTransform canvasRectTransform)
        {
            _isFullScreen = !_isFullScreen;
            
            UpdateProgressUI(_isFullScreen); 
            if (selectionHighlight != null) selectionHighlight.SetActive(!_isFullScreen);

            var rootRectTransform = (RectTransform)transform;

            if (_isFullScreen)
            {
                // We no longer touch the AspectRatioFitter. We just re-parent and stretch.
                // The fitter on the child RawImage will continue to work within its new, larger parent.
                
                if (_originalParent == null)
                {
                    _originalParent = transform.parent;
                    _originalSiblingIndex = transform.GetSiblingIndex();
                    _originalLocalScale = transform.localScale;
                    _originalLocalPosition = transform.localPosition;
                }

                transform.SetParent(canvasRectTransform, true);
                transform.SetAsLastSibling();

                rootRectTransform.anchorMin = Vector2.zero;
                rootRectTransform.anchorMax = Vector2.one;
                rootRectTransform.sizeDelta = Vector2.zero;
                rootRectTransform.anchoredPosition = Vector2.zero;
                rootRectTransform.localScale = Vector3.one;
                
                // Recreate RenderTexture for full screen resolution
                if (videoPlayer.targetTexture != null) videoPlayer.targetTexture.Release();
                videoPlayer.targetTexture = new RenderTexture(Screen.width, Screen.height, 0);
                videoDisplay.texture = videoPlayer.targetTexture;

                // Force a redraw for vertical videos only to fix aspect ratio issues without impacting horizontal videos.
                if (videoPlayer.texture != null && videoPlayer.texture.height > videoPlayer.texture.width)
                {
                    StartCoroutine(RefreshDisplayCoroutine());
                }
            }
            else
            {
                // No need to touch the AspectRatioFitter here either.

                transform.SetParent(_originalParent, true);
                transform.SetSiblingIndex(_originalSiblingIndex);
                transform.localScale = _originalLocalScale;
                transform.localPosition = _originalLocalPosition;

                // Recreate RenderTexture for thumbnail resolution
                RecreateRenderTextureForThumbnail();
            }
            return _isFullScreen;
        }

        private System.Collections.IEnumerator RefreshDisplayCoroutine()
        {
            if (videoPlayer != null)
            {
                videoPlayer.enabled = false;
                yield return new WaitForSeconds(0.1f); // Wait for 0.1 seconds
                videoPlayer.enabled = true;
                yield return new WaitForSeconds(0.1f); // Wait another 0.1 seconds
                videoPlayer.Play();
            }
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
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using RenderHeads.Media.AVProVideo;

namespace DogaShiwakeru
{
    public class VideoPlayerUI : MonoBehaviour
    {
        [Header("Component References")]
        public RawImage videoDisplay;
        public MediaPlayer mediaPlayer;
        public GameObject selectionHighlight;
        public RectTransform videoDisplayRectTransform;
        
        [Header("UI Elements")]
        public Slider progressSlider;
        public TextMeshProUGUI timeDisplayText;
        
        private AspectRatioFitter _aspectRatioFitter;
        private string _totalTimeFormatted;
        private string _videoPath;
        private bool _isMuted = true;
        private float _volume = 1.0f;
        private bool _isActivated = false;
        private float _targetPlaybackSpeed = 1.0f;
        private bool _autoPlay = false;
        private float _uiUpdateTimer = 0f;
        private double _prepareStartTime;

        private bool _isLoading = false;
        private bool _pendingPause = false;

        private bool _isFullScreen = false;
        public bool IsFullscreen() => _isFullScreen;

        private Vector3 _originalLocalScale;
        private Vector3 _originalLocalPosition;
        private Transform _originalParent;
        private int _originalSiblingIndex;

        public bool IsPlaying => mediaPlayer != null && mediaPlayer.Control != null && mediaPlayer.Control.IsPlaying();

        void Awake()
        {
            if (mediaPlayer == null) mediaPlayer = GetComponent<MediaPlayer>();
            if (mediaPlayer == null) mediaPlayer = gameObject.AddComponent<MediaPlayer>();
            
            // Silence "No MediaReference" errors by disabling auto-open
            mediaPlayer.AutoOpen = false;

            if (videoDisplay != null)
            {
                _aspectRatioFitter = videoDisplay.GetComponent<AspectRatioFitter>();
            }

            mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
            
            _originalLocalScale = transform.localScale;
            _originalLocalPosition = transform.localPosition;
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();

            if (timeDisplayText != null)
            {
                timeDisplayText.enableAutoSizing = false;
                timeDisplayText.fontSize = 20;
                timeDisplayText.alignment = TextAlignmentOptions.BottomRight;
            }

            if (progressSlider != null)
            {
                progressSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }

            SetSelected(false);
            UpdateProgressUI(false);
        }

        private void OnDestroy()
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Events.RemoveListener(OnMediaPlayerEvent);
                mediaPlayer.CloseMedia();
            }
        }

        public void Init(string path)
        {
            _videoPath = path;
            _isActivated = false;
            _isLoading = false;
            _pendingPause = false;
            _totalTimeFormatted = "00:00";
            if (videoDisplay != null) videoDisplay.texture = null;
            if (mediaPlayer != null) mediaPlayer.CloseMedia();
        }

        public void Activate()
        {
            if (_isActivated || mediaPlayer == null) return;
            _isActivated = true;
            _isLoading = true;
            _pendingPause = false;
            _prepareStartTime = Time.realtimeSinceStartupAsDouble;

            // AVPro v3: OpenMedia
            mediaPlayer.OpenMedia(new MediaPath(_videoPath, MediaPathType.AbsolutePathOrURL), _autoPlay);
        }

        public void Deactivate()
        {
            if (!_isActivated) return;
            _isActivated = false;
            _isLoading = false;
            _pendingPause = false;
            mediaPlayer.CloseMedia();
            if (videoDisplay != null) videoDisplay.texture = null;
        }

        public bool IsLoading() => _isLoading;

        public bool IsPreparingOrPlaying()
        {
            if (!_isActivated) return false;
            if (_isLoading) return true;
            if (mediaPlayer.Control == null) return true;
            return mediaPlayer.Control.IsPlaying() || mediaPlayer.Control.IsPaused();
        }

        void Update()
        {
            if (mediaPlayer == null || mediaPlayer.Control == null) return;

            if (mediaPlayer.TextureProducer != null)
            {
                Texture tex = mediaPlayer.TextureProducer.GetTexture();
                if (tex != null && (videoDisplay.texture != tex || _uiUpdateTimer >= 0.5f)) // Periodic check for flip
                {
                    videoDisplay.texture = tex;
                    UpdateAspectRatio(tex.width, tex.height);

                    // Fix upside down issue
                    if (mediaPlayer.TextureProducer.RequiresVerticalFlip())
                    {
                        videoDisplay.uvRect = new Rect(0f, 1f, 1f, -1f); // Flip Y
                    }
                    else
                    {
                        videoDisplay.uvRect = new Rect(0f, 0f, 1f, 1f); // Normal
                    }

                    // サムネイル取得後に一時停止（非再生モードの初回フレーム表示用）
                    if (_pendingPause)
                    {
                        _pendingPause = false;
                        if (mediaPlayer.Control != null) mediaPlayer.Control.Pause();
                    }
                }
            }

            if (mediaPlayer.Control.IsPlaying() || _isActivated)
            {
                _uiUpdateTimer += Time.deltaTime;
                if (_uiUpdateTimer >= 0.1f)
                {
                    _uiUpdateTimer = 0;
                    UpdateUI();
                }
            }
        }

        private void UpdateUI()
        {
            if (mediaPlayer.Control == null || mediaPlayer.Info == null) return;

            double time = mediaPlayer.Control.GetCurrentTime();
            double duration = mediaPlayer.Info.GetDuration();

            if (duration > 0)
            {
                if (progressSlider != null) progressSlider.value = (float)(time / duration);
                if (timeDisplayText != null) timeDisplayText.text = $"{FormatTime(time)} / {_totalTimeFormatted}";
            }
        }

        public void OnSliderValueChanged(float value)
        {
            // Optional: implement manual scrubbing logic here
        }

        private void OnMediaPlayerEvent(MediaPlayer mp, MediaPlayerEvent.EventType et, ErrorCode errorCode)
        {
            switch (et)
            {
                case MediaPlayerEvent.EventType.MetaDataReady:
                    _totalTimeFormatted = FormatTime(mp.Info.GetDuration());
                    UpdateAspectRatio(mp.Info.GetVideoWidth(), mp.Info.GetVideoHeight());
                    break;

                case MediaPlayerEvent.EventType.FirstFrameReady:
                    _isLoading = false;
                    // OpenMedia 後に AVPro がオーディオ設定をリセットする場合があるため
                    // Play() 前に必ずミュート・ボリュームを再適用する
                    mp.AudioMuted = _isMuted;
                    mp.AudioVolume = _volume;
                    // 常に Play() してテクスチャを確実に描画させる。
                    // autoPlay でない場合は Update() でテクスチャ取得後に Pause() する。
                    mp.Control.Play();
                    if (!_autoPlay)
                    {
                        _pendingPause = true;
                    }
                    break;

                case MediaPlayerEvent.EventType.Error:
                    _isLoading = false;
                    _pendingPause = false;
                    Debug.LogError($"[VideoPlayerUI] AVPro Error: {errorCode} on {Path.GetFileName(_videoPath)}");
                    break;
            }
        }

        private void UpdateAspectRatio(int width, int height)
        {
            if (_aspectRatioFitter != null && height > 0)
            {
                _aspectRatioFitter.aspectRatio = (float)width / height;
            }
        }

        private string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:00}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }

        public void SetThumbnailMode()
        {
            _autoPlay = false;
            if (mediaPlayer.Control != null) mediaPlayer.Control.Pause();
        }

        public void RestorePlayMode(bool shouldPlay)
        {
            _autoPlay = shouldPlay;
            _pendingPause = false; // 再生モード復帰時はペンディング一時停止をキャンセル
            if (mediaPlayer != null && mediaPlayer.Control != null && mediaPlayer.Control.CanPlay())
            {
                mediaPlayer.AudioMuted = _isMuted;
                mediaPlayer.AudioVolume = _volume;
                mediaPlayer.PlaybackRate = _targetPlaybackSpeed;
                if (shouldPlay) mediaPlayer.Play();
                else mediaPlayer.Pause();
            }
            else
            {
                Activate();
            }
        }

        public string GetVideoPath() => _videoPath;
        public void Pause() => mediaPlayer.Control.Pause();
        public void Play()
        {
            if (mediaPlayer.Control != null) mediaPlayer.Control.Play();
            else Activate();
        }

        public void Seek(float seconds)
        {
            if (mediaPlayer.Control != null)
                mediaPlayer.Control.Seek(mediaPlayer.Control.GetCurrentTime() + seconds);
        }

        public void SeekToPercent(float percent)
        {
            if (mediaPlayer.Control != null && mediaPlayer.Info.GetDuration() > 0)
                mediaPlayer.Control.Seek(mediaPlayer.Info.GetDuration() * Mathf.Clamp01(percent));
        }

        public void ToggleMute() => SetMute(!_isMuted);
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            if (mediaPlayer != null) mediaPlayer.AudioVolume = _volume;
        }

        public void SetMute(bool mute)
        {
            _isMuted = mute;
            if (mediaPlayer != null) mediaPlayer.AudioMuted = mute;
        }

        public void SetPlaybackSpeed(float speed)
        {
            _targetPlaybackSpeed = speed;
            if (mediaPlayer != null) mediaPlayer.PlaybackRate = speed;
        }

        public void SetAutoPlay(bool autoPlay) => _autoPlay = autoPlay;
        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null) selectionHighlight.SetActive(isSelected);
            UpdateProgressUI(_isFullScreen || isSelected);
        }

        private void UpdateProgressUI(bool isVisible)
        {
            if (progressSlider != null) progressSlider.gameObject.SetActive(isVisible);
            if (timeDisplayText != null) timeDisplayText.gameObject.SetActive(isVisible);
        }

        public void ToggleFullscreen(RectTransform canvasRect) => SetFullscreen(!_isFullScreen, canvasRect);

        public void SetFullscreen(bool fullscreen, RectTransform canvasRect)
        {
            if (_isFullScreen == fullscreen) return;
            _isFullScreen = fullscreen;

            if (_isFullScreen && canvasRect != null)
            {
                if (selectionHighlight != null) selectionHighlight.SetActive(false); // Hide highlight in fullscreen
                transform.SetParent(canvasRect, true);
                RectTransform rect = GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            else
            {
                transform.SetParent(_originalParent, true);
                transform.SetSiblingIndex(_originalSiblingIndex);
                transform.localScale = _originalLocalScale;
                transform.localPosition = _originalLocalPosition;
                RectTransform rect = GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
            }
            UpdateProgressUI(_isFullScreen);
        }
    }
}
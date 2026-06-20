using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using TMPro;
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

        // 非選択動画のサムネイル更新間隔（秒）。この間隔で1フレームだけシークする
        private const float THUMBNAIL_SEEK_INTERVAL = 2.0f;

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
        private bool _usingCachedThumbnail = false;
        private bool _pendingThumbnailCapture = false;
        private bool _isSelected = false;

        // サムネイルモード（非選択）状態管理
        private bool _isThumbnailMode = false;
        private float _thumbnailSeekTimer = 0f;
        private bool _textureNeedsUpdate = false; // シーク後にテクスチャ更新が必要

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

            mediaPlayer.AutoOpen = false;

            if (videoDisplay != null)
                _aspectRatioFitter = videoDisplay.GetComponent<AspectRatioFitter>();

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
                progressSlider.onValueChanged.AddListener(OnSliderValueChanged);

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
            _usingCachedThumbnail = false;
            _pendingThumbnailCapture = false;
            _isThumbnailMode = false;
            _textureNeedsUpdate = false;
            _totalTimeFormatted = "00:00";
            if (videoDisplay != null) videoDisplay.texture = null;
            if (mediaPlayer != null) mediaPlayer.CloseMedia();
        }

        public void SetCachedThumbnail(Texture2D tex)
        {
            _usingCachedThumbnail = true;
            if (videoDisplay != null && tex != null)
            {
                videoDisplay.texture = tex;
                videoDisplay.uvRect = new Rect(0f, 0f, 1f, 1f);
                UpdateAspectRatio(tex.width, tex.height);
            }
        }

        public bool HasCachedThumbnail() => _usingCachedThumbnail;

        public void Activate()
        {
            if (_isActivated || mediaPlayer == null) return;
            _usingCachedThumbnail = false;
            _isActivated = true;
            _isLoading = true;
            _pendingPause = false;
            _pendingThumbnailCapture = false;
            _isThumbnailMode = false;
            _textureNeedsUpdate = false;
            _prepareStartTime = Time.realtimeSinceStartupAsDouble;
            mediaPlayer.OpenMedia(new MediaPath(_videoPath, MediaPathType.AbsolutePathOrURL), _autoPlay);
        }

        public void Deactivate()
        {
            if (!_isActivated) return;
            _isActivated = false;
            _isLoading = false;
            _pendingPause = false;
            _usingCachedThumbnail = false;
            _pendingThumbnailCapture = false;
            _isThumbnailMode = false;
            _textureNeedsUpdate = false;
            mediaPlayer.CloseMedia();
            if (videoDisplay != null) videoDisplay.texture = null;
        }

        public bool IsLoading() => _isLoading;
        public bool IsActivated() => _isActivated;

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

            // キャッシュサムネイル表示中は AVPro 呼び出しなし
            if (_usingCachedThumbnail) return;

            bool isPlaying = mediaPlayer.Control.IsPlaying();

            // ── サムネイルモード: 低FPSシーク ──────────────────────────────
            // 非選択動画は THUMBNAIL_SEEK_INTERVAL 秒ごとに1フレームだけ進める（0.5fps相当）
            if (_isThumbnailMode && _isActivated && !_isLoading)
            {
                _thumbnailSeekTimer -= Time.deltaTime;
                if (_thumbnailSeekTimer <= 0f)
                {
                    _thumbnailSeekTimer = THUMBNAIL_SEEK_INTERVAL;
                    if (mediaPlayer.Info != null)
                    {
                        double duration = mediaPlayer.Info.GetDuration();
                        if (duration > 0)
                        {
                            double current = mediaPlayer.Control.GetCurrentTime();
                            mediaPlayer.Control.Seek((current + THUMBNAIL_SEEK_INTERVAL) % duration);
                            _textureNeedsUpdate = true;
                        }
                    }
                }
            }

            // ── テクスチャ更新 ──────────────────────────────────────────────
            // 再生中 or シーク直後 or 初回フレーム待ち のときだけ AVPro を問い合わせる
            bool needsTextureCheck = isPlaying || _textureNeedsUpdate || _pendingThumbnailCapture || _pendingPause;
            if (needsTextureCheck && mediaPlayer.TextureProducer != null)
            {
                Texture tex = mediaPlayer.TextureProducer.GetTexture();
                if (tex != null)
                {
                    bool requiresFlip = mediaPlayer.TextureProducer.RequiresVerticalFlip();

                    if (isPlaying || videoDisplay.texture != tex)
                    {
                        videoDisplay.texture = tex;
                        UpdateAspectRatio(tex.width, tex.height);
                        videoDisplay.uvRect = requiresFlip
                            ? new Rect(0f, 1f, 1f, -1f)
                            : new Rect(0f, 0f, 1f, 1f);

                        if (!isPlaying) _textureNeedsUpdate = false; // シーク済みフレームを受け取った
                    }

                    if (_pendingThumbnailCapture)
                    {
                        _pendingThumbnailCapture = false;
                        CaptureAndCacheThumbnail(tex, requiresFlip);
                    }

                    if (_pendingPause)
                    {
                        _pendingPause = false;
                        mediaPlayer.Control.Pause();

                        // 初回フレームキャプチャ完了 → サムネイルモードに移行
                        // シークタイマーをランダムオフセットで初期化（全動画が同タイミングでシークしないよう分散）
                        _isThumbnailMode = true;
                        _thumbnailSeekTimer = UnityEngine.Random.Range(0f, THUMBNAIL_SEEK_INTERVAL);
                    }
                }
            }

            // ── プログレスUI更新 ────────────────────────────────────────────
            // 再生中の動画だけ更新（非選択動画はポーズ中なので不要）
            if (isPlaying)
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

        public void OnSliderValueChanged(float value) { }

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
                    _pendingThumbnailCapture = true;
                    mp.AudioMuted = _isMuted;
                    mp.AudioVolume = _volume;
                    mp.Control.Play();
                    if (!_autoPlay)
                        _pendingPause = true;
                    break;

                case MediaPlayerEvent.EventType.Error:
                    _isLoading = false;
                    _pendingPause = false;
                    _pendingThumbnailCapture = false;
                    Debug.LogError($"[VideoPlayerUI] AVPro Error: {errorCode} on {Path.GetFileName(_videoPath)}");
                    break;
            }
        }

        private void UpdateAspectRatio(int width, int height)
        {
            if (_aspectRatioFitter != null && height > 0)
                _aspectRatioFitter.aspectRatio = (float)width / height;
        }

        private void CaptureAndCacheThumbnail(Texture sourceTex, bool requiresFlip)
        {
            if (string.IsNullOrEmpty(_videoPath) || ThumbnailCache.HasEntry(_videoPath)) return;

            const int MAX_DIM = 256;
            float scale = (float)MAX_DIM / Mathf.Max(sourceTex.width, sourceTex.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(sourceTex.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(sourceTex.height * scale));

            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);

            if (requiresFlip)
                Graphics.Blit(sourceTex, rt, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else
                Graphics.Blit(sourceTex, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex2d = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex2d.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex2d.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            ThumbnailCache.Store(_videoPath, tex2d);
        }

        private string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:00}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }

        /// <summary>非選択になったときに呼ぶ。デコードを止めて低FPSサムネイルモードに移行。</summary>
        public void SetThumbnailMode()
        {
            _autoPlay = false;
            _isThumbnailMode = true;
            _thumbnailSeekTimer = UnityEngine.Random.Range(0f, THUMBNAIL_SEEK_INTERVAL);

            if (mediaPlayer != null && mediaPlayer.Control != null && mediaPlayer.Control.IsPlaying())
                mediaPlayer.Control.Pause();
        }

        /// <summary>選択されたときに呼ぶ。フル再生モードに復帰。</summary>
        public void RestorePlayMode(bool shouldPlay)
        {
            _autoPlay = shouldPlay;
            _pendingPause = false;
            _isThumbnailMode = false;
            _textureNeedsUpdate = false;

            if (_usingCachedThumbnail)
            {
                _usingCachedThumbnail = false;
                _isActivated = false;
            }

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
            _isSelected = isSelected;
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
                if (selectionHighlight != null) selectionHighlight.SetActive(false);
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
                if (selectionHighlight != null) selectionHighlight.SetActive(_isSelected);
            }
            UpdateProgressUI(_isFullScreen || _isSelected);
        }
    }
}

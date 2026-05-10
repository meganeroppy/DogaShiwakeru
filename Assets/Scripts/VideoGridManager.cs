using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace DogaShiwakeru
{
    public class VideoGridManager : MonoBehaviour
    {
        public VideoPlayerUI videoPlayerUIPrefab;
        public Transform gridParent;
        public RectTransform canvasRectTransform;

        private List<VideoPlayerUI> _currentVideoUIs = new List<VideoPlayerUI>();
        private int _selectedVideoIndex = -1;
        private int _fullscreenVideoIndex = -1;
        private float _currentSelectionSpeed = 1.0f;
        private Coroutine _prepareCoroutine;

        // The MainController now controls how many are activated initially
        // private const int VISIBLE_BUFFER_COUNT = 20; 

        public bool IsFullscreen()
        {
            return _fullscreenVideoIndex != -1;
        }

        public void DisplayVideos(List<string> videoPaths)
        {
            if (videoPaths == null) videoPaths = new List<string>();

            // 進行中のロードコルーチンを停止
            if (_prepareCoroutine != null)
            {
                StopCoroutine(_prepareCoroutine);
                _prepareCoroutine = null;
            }

            // フルスクリーン中なら先に解除
            if (IsFullscreen()) ExitFullscreen();

            // 現在選択中（再生中）の動画パスを記憶
            // DisplayVideos 後に RefreshGridDisplay が即座に再選択するため、その間の停止を防ぐ
            string currentSelectedPath = null;
            if (_selectedVideoIndex >= 0 && _selectedVideoIndex < _currentVideoUIs.Count)
            {
                var sel = _currentVideoUIs[_selectedVideoIndex];
                if (sel != null) currentSelectedPath = sel.GetVideoPath();
            }

            // 現在表示中の GO をパスで索引
            var existingByPath = new Dictionary<string, VideoPlayerUI>();
            foreach (var ui in _currentVideoUIs)
            {
                if (ui == null) continue;
                string p = ui.GetVideoPath();
                if (!string.IsNullOrEmpty(p) && !existingByPath.ContainsKey(p))
                    existingByPath[p] = ui;
            }

            // 新しいリストを構築（既存 GO を再利用 or 新規 Instantiate）
            var newUIs = new List<VideoPlayerUI>();
            var reusedPaths = new HashSet<string>();

            for (int i = 0; i < videoPaths.Count; i++)
            {
                string path = videoPaths[i];

                if (existingByPath.TryGetValue(path, out VideoPlayerUI existing))
                {
                    // 同じパスの GO を再利用 ― Instantiate/Destroy なし
                    reusedPaths.Add(path);

                    if (path == currentSelectedPath)
                    {
                        // 選択中（再生中）の動画はそのまま継続させる。
                        // 直後の SelectAndPossiblyFullscreen がハイライト等を正しく復元する。
                    }
                    else
                    {
                        existing.SetSelected(false);
                        existing.SetMute(true);
                        existing.SetAutoPlay(false);
                        // 再生中だった場合のみ停止（キャッシュ表示中は不要）
                        if (existing.IsActivated() && !existing.HasCachedThumbnail())
                            existing.SetThumbnailMode();
                    }
                    newUIs.Add(existing);
                }
                else
                {
                    // 新規パス ― GO を作成
                    VideoPlayerUI newUI = Instantiate(videoPlayerUIPrefab, gridParent);
                    newUI.Init(path);
                    newUI.SetAutoPlay(false);
                    newUI.SetMute(true);
                    if (ThumbnailCache.TryGet(path, out Texture2D cachedTex))
                        newUI.SetCachedThumbnail(cachedTex);
                    newUIs.Add(newUI);
                }
            }

            // 不要になった既存 GO を破棄
            foreach (var kvp in existingByPath)
            {
                if (!reusedPaths.Contains(kvp.Key) && kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }

            _currentVideoUIs = newUIs;
            _selectedVideoIndex = -1;
            _fullscreenVideoIndex = -1;

            // グリッド上の描画順を更新
            ReorderGrid();

            if (_currentVideoUIs.Count == 0)
            {
                Debug.Log("No video files to display in grid.");
                return;
            }

            // キャッシュ未取得・未アクティブな動画だけをスタッガードロード
            _prepareCoroutine = StartCoroutine(StaggeredPrepareCoroutine());
        }

        // Prepare videos one at a time to prevent FPS drop from simultaneous decode
        private System.Collections.IEnumerator StaggeredPrepareCoroutine()
        {
            const int MAX_SIMULTANEOUS = 2;

            for (int i = 0; i < _currentVideoUIs.Count; i++)
            {
                var ui = _currentVideoUIs[i];
                if (ui == null) continue;

                // キャッシュ表示中、またはすでにアクティブ（再利用 GO）はスキップ
                if (ui.HasCachedThumbnail() || ui.IsActivated()) continue;

                // ロード中（FirstFrameReady 未着）の動画数が上限以下になるまで待つ
                int loadingCount;
                do
                {
                    loadingCount = 0;
                    foreach (var v in _currentVideoUIs)
                    {
                        if (v != null && v.IsLoading()) loadingCount++;
                    }
                    if (loadingCount >= MAX_SIMULTANEOUS)
                        yield return new UnityEngine.WaitForSeconds(0.3f);
                } while (loadingCount >= MAX_SIMULTANEOUS);

                if (ui != null)
                {
                    ui.Activate();
                }

                // Small delay between each activation to spread the load
                yield return new UnityEngine.WaitForSeconds(0.1f);
            }
        }

        private void ClearVideos()
        {
            if (_prepareCoroutine != null)
            {
                StopCoroutine(_prepareCoroutine);
                _prepareCoroutine = null;
            }

            foreach (VideoPlayerUI videoUI in _currentVideoUIs)
            {
                if (videoUI != null) Destroy(videoUI.gameObject);
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

        public void SetSelectedVideo(int index, bool shouldPlay = true)
        {
            if (_selectedVideoIndex == index) return;

            // Deselect old: switch to low-FPS thumbnail mode
            if (_selectedVideoIndex != -1)
            {
                var oldSelectedUI = GetVideoUI(_selectedVideoIndex);
                if (oldSelectedUI != null)
                {
                    oldSelectedUI.SetSelected(false);
                    oldSelectedUI.SetMute(true);
                    oldSelectedUI.SetThumbnailMode(); // Drop to ~0.4fps
                }
            }

            _selectedVideoIndex = index;

            // Select new: restore full-quality playback
            var newSelectedUI = GetVideoUI(_selectedVideoIndex);
            if (newSelectedUI != null)
            {
                newSelectedUI.SetSelected(true);
                newSelectedUI.SetMute(false);
                newSelectedUI.SetPlaybackSpeed(_currentSelectionSpeed);
                newSelectedUI.RestorePlayMode(shouldPlay); // Full FPS, full quality
            }
        }

        public void SetSelectionPlaybackSpeed(float speed)
        {
            Debug.Log($"[VideoGridManager] SetSelectionPlaybackSpeed: {speed}");
            _currentSelectionSpeed = speed;
            var selectedUI = GetSelectedVideoUI();
            if (selectedUI != null)
            {
                selectedUI.SetPlaybackSpeed(_currentSelectionSpeed);
            }
            else
            {
                Debug.LogWarning("[VideoGridManager] No selected video UI to set speed on.");
            }
        }

        public void SelectAndPossiblyFullscreen(int index, bool makeFullscreen, bool shouldPlay = true)
        {
            // If currently in fullscreen and moving to a new selection, exit old fullscreen first.
            if (IsFullscreen() && _fullscreenVideoIndex != index)
            {
                ExitFullscreen();
            }

            SetSelectedVideo(index, shouldPlay);

            if (makeFullscreen && _selectedVideoIndex != -1)
            {
                EnterFullscreen(_selectedVideoIndex);
            }
        }

        public void MoveSelection(int direction)
        {
            if (_currentVideoUIs.Count == 0) return;

            bool wasPlaying = false;
            var currentUI = GetSelectedVideoUI();
            if (currentUI != null)
            {
                wasPlaying = currentUI.IsPlaying;
            }

            int newIndex = _selectedVideoIndex + direction;
            if (_selectedVideoIndex == -1)
            {
                newIndex = (direction > 0) ? 0 : _currentVideoUIs.Count - 1;
            }
            else
            {
                newIndex = _selectedVideoIndex + direction;
                if (newIndex < 0) newIndex = _currentVideoUIs.Count - 1;
                else if (newIndex >= _currentVideoUIs.Count) newIndex = 0;
            }

            SelectAndPossiblyFullscreen(newIndex, IsFullscreen(), wasPlaying);
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
            else
            {
                EnterFullscreen(_selectedVideoIndex);
            }
        }
        
        public void EnterFullscreen(int index)
        {
            if (index == -1) return;
            var videoUI = GetVideoUI(index);
            if (videoUI != null && !videoUI.IsFullscreen())
            {
                videoUI.Activate(); // Ensure video is activated before going fullscreen
                videoUI.SetPlaybackSpeed(_currentSelectionSpeed); // Always current speed in fullscreen
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
                // After exiting fullscreen, the video is still the selected one, so it should play at current selected speed.
                videoUI.SetPlaybackSpeed(_currentSelectionSpeed); 
            }
            _fullscreenVideoIndex = -1;
            ReorderGrid();
        }

        private void ReorderGrid()
        {
            for (int i = 0; i < _currentVideoUIs.Count; i++)
            {
                if (_currentVideoUIs[i] != null)
                {
                    _currentVideoUIs[i].transform.SetSiblingIndex(i);
                }
            }
        }
    }
}
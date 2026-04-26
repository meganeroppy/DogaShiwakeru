using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DogaShiwakeru
{
    public class ThumbnailGenerator : MonoBehaviour
    {
        public List<Texture2D> GeneratedThumbnails = new List<Texture2D>();
        private VideoPlayer _videoPlayer;
        private Coroutine _generationCoroutine;
        private const int MAX_THUMBNAILS = 5;
        private const int THUMB_WIDTH = 256;
        private const int THUMB_HEIGHT = 144;

        void Awake()
        {
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // No audio needed for thumbnails
            
            // Create a shared target texture for generation
            _videoPlayer.targetTexture = new RenderTexture(THUMB_WIDTH, THUMB_HEIGHT, 16);
        }

        public void UpdateThumbnails(string directoryPath)
        {
            if (_generationCoroutine != null) StopCoroutine(_generationCoroutine);
            _generationCoroutine = StartCoroutine(GenerateThumbnailsRoutine(directoryPath));
        }

        private IEnumerator GenerateThumbnailsRoutine(string directoryPath)
        {
            // Clear existing thumbnails
            foreach (var tex in GeneratedThumbnails)
            {
                if (tex != null) Destroy(tex);
            }
            GeneratedThumbnails.Clear();

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                yield break;
            }

            // Get video files
            string[] videoFiles = Directory.GetFiles(directoryPath, "*.mp4");
            
            // Take the first few
            var filesToProcess = videoFiles.Take(MAX_THUMBNAILS).ToList();

            foreach (var filePath in filesToProcess)
            {
                _videoPlayer.url = "file://" + filePath;
                _videoPlayer.Prepare();

                // Wait until prepared
                float timeout = 2.0f;
                while (!_videoPlayer.isPrepared && timeout > 0)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                if (_videoPlayer.isPrepared)
                {
                    // Start playing to buffer frames
                    _videoPlayer.Play();
                    
                    // Wait a few frames for the video to actually render to the texture
                    for(int i=0; i<5; i++) yield return null; 
                    yield return new WaitForEndOfFrame();

                    _videoPlayer.Pause();

                    RenderTexture renderTex = _videoPlayer.targetTexture;
                    
                    if (renderTex != null && renderTex.IsCreated())
                    {
                        // Create a generic Texture2D to copy the content
                        Texture2D thumb = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGB24, false);
                        
                        // Remember the active render texture
                        RenderTexture.active = renderTex;
                        
                        // Read pixels
                        thumb.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                        thumb.Apply();
                        
                        // Restore active render texture
                        RenderTexture.active = null;
    
                        GeneratedThumbnails.Add(thumb);
                    }
                }
            }
            
            _videoPlayer.Stop();
        }

        void OnDestroy()
        {
            foreach (var tex in GeneratedThumbnails)
            {
                if (tex != null) Destroy(tex);
            }
            GeneratedThumbnails.Clear();
            
            if (_videoPlayer != null && _videoPlayer.targetTexture != null)
            {
                _videoPlayer.targetTexture.Release();
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace LowHpAlarm
{
    /// <summary>
    /// Manages the red vignette effect that appears when the player's HP is low.
    /// </summary>
    public class VignetteEffect
    {
        // Constants for vignette appearance and behavior
        private const float MinAlpha = 0.25f;    // Minimum alpha when HP is at threshold
        private const float MaxAlpha = 0.75f;    // Maximum alpha when HP is near zero
        private const float SmoothSpeed = 4f;    // Speed of alpha interpolation (higher = faster)
        private const float AlphaLogThreshold = 0.01f; // Minimum alpha change to log
        private const float AlphaStart = 0.35f;  // Start of the SmoothStep gradient for distance-based alpha

        private Image _vignetteImage;
        private float _currentAlpha;

        /// <summary>
        /// Creates and initializes the vignette GameObject and Image component.
        /// Call once when the effect is needed (e.g., on session start).
        /// </summary>
        public void Initialize()
        {
            if (_vignetteImage != null) return;

            // Create a persistent GameObject to hold the vignette
            GameObject go = new GameObject("LowHpVignette");
            UnityEngine.Object.DontDestroyOnLoad(go);

            // Setup Canvas for screen-space overlay
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Render on top of everything

            // Setup CanvasScaler for resolution independence
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Create the actual Image component
            GameObject imgGo = new GameObject("Vignette");
            imgGo.transform.SetParent(go.transform, false);
            _vignetteImage = imgGo.AddComponent<Image>();
            _vignetteImage.sprite = Sprite.Create(CreateVignetteTexture(), new Rect(0f, 0f, 256f, 256f), new Vector2(0.5f, 0.5f));
            _vignetteImage.raycastTarget = false; // Prevent blocking UI interactions

            // Initialize with fully transparent
            RectTransform rt = _vignetteImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Color c = _vignetteImage.color;
            c.a = 0f;
            _vignetteImage.color = c;

            LowHpAlarmPlugin.Log("Vignette created");
        }

        /// <summary>
        /// Updates the vignette alpha based on the player's current HP percentage.
        /// Should be called each frame (or on HP change) while in gameplay.
        /// </summary>
        /// <param name="hpPercent">Current HP as a percentage (0-100).</param>
        public void Update(float hpPercent)
        {
            // Compute target alpha: 0 when HP is above threshold, otherwise interpolated based on HP
            float targetAlpha = 0f;
            if (LowHpAlarmPlugin.VisualAlarmHp.Value > 0f && hpPercent > 0f && hpPercent <= LowHpAlarmPlugin.VisualAlarmHp.Value)
            {
                // t goes from 0 (HP at threshold) to 1 (HP at 0)
                float t = 1f - hpPercent / LowHpAlarmPlugin.VisualAlarmHp.Value;
                targetAlpha = Mathf.Lerp(MinAlpha, MaxAlpha, t) * LowHpAlarmPlugin.VignetteIntensity.Value;
            }
            _currentAlpha = targetAlpha;

            //TODO: move to separate method that called regularly 
            //\ Smoothly interpolate current alpha towards target
            //\ float oldAlpha = _currentAlpha;
            //\ _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * SmoothSpeed);

            //\ Log only when alpha changes significantly to avoid log spam
            //\ if (Mathf.Abs(oldAlpha - _currentAlpha) > AlphaLogThreshold)
            //\ {
            //\     LowHpAlarmPlugin.Log($"UpdateVignette: alpha={oldAlpha:F2} -> {_currentAlpha:F2} (target={targetAlpha:F2}, HP={hpPercent:F1}%)");
            //\ }

            LowHpAlarmPlugin.Log($"UpdateVignette: alpha= {_currentAlpha:F2} (HP={hpPercent:F1}%)");

            // Apply the alpha to the vignette's color
            Color c = _vignetteImage.color;
            c.a = _currentAlpha;
            _vignetteImage.color = c;
        }

        /// <summary>
        /// Cleans up the vignette GameObject. Call when the effect is no longer needed (e.g., on session end).
        /// </summary>
        public void Cleanup()
        {
            if (_vignetteImage == null) return;

            GameObject go = _vignetteImage.transform.root.gameObject;
            if (go != null) UnityEngine.Object.Destroy(go);
            _vignetteImage = null;
            _currentAlpha = 0f;

            LowHpAlarmPlugin.Log("Vignette destroyed");
        }

        /// <summary>
        /// Generates the radial gradient texture used for the vignette.
        /// </summary>
        /// <returns>A texture2D with radial alpha gradient.</returns>
        private Texture2D CreateVignetteTexture()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = Mathf.Sqrt(center.x * center.x + center.y * center.y);
            Color baseColor = Utils.ParseHexColor(LowHpAlarmPlugin.VignetteColor.Value);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.SmoothStep(AlphaStart, 1f, dist);
                    pixels[y * size + x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * baseColor.a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
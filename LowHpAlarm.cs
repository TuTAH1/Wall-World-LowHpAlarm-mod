using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Windows.Core;

namespace LowHpAlarm
{
	[BepInPlugin(GUID, NAME, VERSION)]
	public class LowHpAlarmPlugin : BaseUnityPlugin
	{
		public const string GUID = "com.wallworld.lowhp";
		public const string NAME = "Low HP Alarm";
		public const string VERSION = "1.1.2";
		
		private const string TALENT_WINDOW_ID = "talents";
		
		#region Configuration
		public static ConfigEntry<bool> TestMode;
		public static ConfigEntry<float> VisualAlarmHp;
		public static ConfigEntry<float> AudioAlarmHp;
		public static ConfigEntry<float> MenuOpenHp;
		public static ConfigEntry<bool> Logging;
		public static ConfigEntry<float> VignetteIntensity;
		public static ConfigEntry<string> VignetteColor;
		#endregion
		
		#region State
		private static Image vignetteImage;
		private static float currentVignetteAlpha;
		
		private static AudioSource audioSource;
		private static AudioClip alarmClip;
		private static bool isAlarmPlaying = false;
		
		private static float menuLockUntil;
		
		private static SpiderEntity playerSpider;
		private static float lastHpPercent = 100f;
		
		private static BepInEx.Logging.ManualLogSource pluginLogger;
		#endregion
		
		#region Unity Lifecycle
		void Awake()
		{
			pluginLogger = Logger;
			
			TestMode = Config.Bind("General", "test", false, "If true, auto-open menu on ANY HP decrease");
			VisualAlarmHp = Config.Bind("General", "visualAlarmHp", 30f, "HP% where red vignette starts (0 = disabled)");
			AudioAlarmHp = Config.Bind("General", "audioAlarmHp", 25f, "HP% where alarm sound plays (0 = disabled)");
			MenuOpenHp = Config.Bind("General", "menuOpenHp", 10f, "HP% where improvements menu auto-opens (0 = disabled)");
			Logging = Config.Bind("General", "logging", false, "Enable detailed logging");
			VignetteIntensity = Config.Bind("General", "vignetteIntensity", 1f, "Vignette opacity multiplier (0-1)");
			VignetteColor = Config.Bind("General", "vignetteColor", "FF0000FF", "Vignette color in hex (#rgb, #rgba, #rrggbb, #rrggbbaa)");
			
			CreateVignette();
			CreateAudio();
			
			Harmony.CreateAndPatchAll(typeof(LowHpAlarmPlugin));
			
			Log("Mod loaded, detailed logging enabled");
		}
		
		private int updateCounter = 0;
		
		void Update()
		{
			updateCounter++;
			if (updateCounter % 300 == 0)
			{
				Log($"Update() running (frame {updateCounter}) {GetHpStatus()}");
			}
			UpdateVignette();
			UpdateAlarm();
		}
		#endregion

		#region Vignette
static void UpdateVignette()
		{
			//: Null checks
			if (vignetteImage == null)
			{
				Log("UpdateVignette: vignetteImage is null, re-creating");
				CreateVignette();
				if (vignetteImage == null)
				{
					Log("UpdateVignette: still null after re-creation");
					return;
				}
			}
			
			float percent = GetHpPercent();
			float targetAlpha = 0f;
			
			if (VisualAlarmHp.Value > 0f && percent > 0f && percent <= VisualAlarmHp.Value)
			{
				float t = 1f - percent / VisualAlarmHp.Value;
				targetAlpha = Mathf.Lerp(0.25f, 0.75f, t) * VignetteIntensity.Value;
			}
			
			float oldAlpha = currentVignetteAlpha;
			currentVignetteAlpha = Mathf.Lerp(currentVignetteAlpha, targetAlpha, Time.deltaTime * 4f);
			
			if (Mathf.Abs(oldAlpha - currentVignetteAlpha) > 0.01f)
			{
				Log($"UpdateVignette: alpha={currentVignetteAlpha:F2} (target={targetAlpha:F2}, HP={percent:F1}%) {GetHpStatus()}");
			}
			
			Color c2 = vignetteImage.color;
			c2.a = currentVignetteAlpha;
			vignetteImage.color = c2;
		}
		
		static void CreateVignette()
		{
			GameObject go = new GameObject("LowHpVignette");
			DontDestroyOnLoad(go);
			
			Canvas canvas = go.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 9999;
			
			CanvasScaler scaler = go.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.matchWidthOrHeight = 0.5f;
			
			GameObject imgGo = new GameObject("Vignette");
			imgGo.transform.SetParent(go.transform, false);
			vignetteImage = imgGo.AddComponent<Image>();
			vignetteImage.sprite = Sprite.Create(CreateVignetteTexture(), new Rect(0f, 0f, 256f, 256f), new Vector2(0.5f, 0.5f));
			vignetteImage.raycastTarget = false;
			
			RectTransform rt = vignetteImage.rectTransform;
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			
			Color c = vignetteImage.color;
			c.a = 0f;
			vignetteImage.color = c;
			Log("Vignette created");
		}
		
		static Texture2D CreateVignetteTexture()
		{
			int size = 256;
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
			Color[] pixels = new Color[size * size];
			Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
			float maxDist = Mathf.Sqrt(center.x * center.x + center.y * center.y);
			Color baseColor = Utils.ParseHexColor(VignetteColor.Value);
			
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
					float alpha = Mathf.SmoothStep(0.35f, 1f, dist);
					pixels[y * size + x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * baseColor.a);
				}
			}
			
			tex.SetPixels(pixels);
			tex.Apply();
			return tex;
		}
		#endregion
		
		#region Audio
		static void UpdateAlarm()
		{
			//: Null checks
			if (audioSource == null || alarmClip == null)
			{
				if (audioSource == null)
					pluginLogger.LogError("audioSource is null, re-creating");
				if (alarmClip == null)
					pluginLogger.LogError("alarmClip is null, re-creating");
				CreateAudio();
				if (audioSource == null || alarmClip == null)
				{
					pluginLogger.LogError("UpdateAlarm: still null after re-creation");
					return;
				}
			}
			
			float hp = GetHpPercent();
			bool shouldPlay = AudioAlarmHp.Value > 0f && hp > 0f && hp <= AudioAlarmHp.Value;
			
			if (shouldPlay && !isAlarmPlaying)
			{
				audioSource.clip = alarmClip;
				audioSource.loop = true;
				audioSource.Play();
				isAlarmPlaying = true;
				Log($"UpdateAlarm: Alarm started playing (HP={hp:F1}%) {GetHpStatus()}");
			}
			else if (!shouldPlay && isAlarmPlaying)
			{
				audioSource.Stop();
				isAlarmPlaying = false;
				Log($"UpdateAlarm: Alarm stopped (HP={hp:F1}%) {GetHpStatus()}");
			}
		}
		
		static void CreateAudio()
		{
			GameObject go = new GameObject("LowHpAlarmAudio");
			DontDestroyOnLoad(go);
			audioSource = go.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 0f;
			audioSource.volume = 1f;
			audioSource.loop = true;
			
			LoadAlarmClip();
			Log("Audio created");
		}
		
		static void LoadAlarmClip()
		{
			try
			{
				string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
				string wavPath = Path.Combine(Path.GetDirectoryName(assemblyPath), "LowHpAlarmResources", "alarm.wav");
				pluginLogger.LogInfo($"Looking for alarm.wav at: {wavPath}");
				pluginLogger.LogInfo($"File exists: {File.Exists(wavPath)}");

				if (File.Exists(wavPath))
				{
					byte[] wavData = File.ReadAllBytes(wavPath);
					alarmClip = WavUtility.ToAudioClip(wavData, "alarm.wav");
					if (alarmClip != null)
					{
						Log("Loaded alarm.wav from Resources");
					}
					else
					{
						pluginLogger.LogError("Failed to convert alarm.wav to AudioClip");
					}
				}
				else
				{
					pluginLogger.LogError($"Failed to load alarm.wav from {wavPath}");
				}
			}
			catch (System.Exception ex)
			{
				pluginLogger.LogError($"Failed to load alarm.wav: {ex.Message}");
			}
		}
		#endregion
		
		#region Menu
		static void OpenTalentMenu()
		{
			try
			{
				WindowsAggregator.SetWindow(TALENT_WINDOW_ID, false);
				menuLockUntil = Time.realtimeSinceStartup + 0.5f;
				Log($"Opened talent menu, lock until {menuLockUntil:F1}s {GetHpStatus()}");
			}
			catch (System.Exception ex)
			{
				pluginLogger.LogError($"Error opening talent menu: {ex.Message}");
			}
		}
		#endregion
		
		#region Helpers
		static void Log(string message)
		{
			if (Logging != null && Logging.Value && pluginLogger != null)
			{
				pluginLogger.LogInfo(message);
			}
		}
		
		static string GetHpStatus()
		{
			float percent = GetHpPercent();
			bool vignetteActive = VisualAlarmHp.Value > 0f && percent > 0f && percent <= VisualAlarmHp.Value;
			bool audioActive = AudioAlarmHp.Value > 0f && percent > 0f && percent <= AudioAlarmHp.Value;
			return $"[HP:{percent:F1}% Vignette:{vignetteActive} Audio:{audioActive} Playing:{isAlarmPlaying}]";
		}
		
		static float GetHpPercent()
		{
			if (playerSpider == null || playerSpider.HealthInternal == null)
			{
				return 100f;
			}
			HealthEntity health = playerSpider.HealthInternal;
			float delta = health.Delta;
			if (delta <= 0f)
			{
				return 100f;
			}
			return health.Current / delta * 100f;
		}
		#endregion
		
		#region Harmony Patches
		[HarmonyPostfix]
		[HarmonyPatch(typeof(SpiderEntity), "Init")]
		static void SpiderInitPostfix(SpiderEntity __instance, Action callBack)
		{
			if (__instance == null)
			{
				Log("SpiderEntity.Init: instance is null");
				return;
			}
			
			if (__instance.HealthInternal != null)
			{
				__instance.HealthInternal.OnChanged -= OnHealthChanged;
				__instance.HealthInternal.OnChanged += OnHealthChanged;
				Log($"SpiderEntity.Init: subscribed to HealthInternal {GetHpStatus()}");
			}
			else
			{
				Log("SpiderEntity.Init: HealthInternal is null");
			}
			
			playerSpider = __instance;
			lastHpPercent = GetHpPercent();
			Log($"Spider initialized {GetHpStatus()}");
			
			// Recreate vignette if it was destroyed on previous session end
			if (vignetteImage == null)
			{
				CreateVignette();
			}
			isAlarmPlaying = false;
		}
		
		[HarmonyPostfix]
		[HarmonyPatch(typeof(SpiderEntity), "Deinit")]
		static void SpiderDeinitPostfix(SpiderEntity __instance)
		{
			if (__instance == null)
			{
				return;
			}
			
			Log("SpiderEntity.Deinit: session ending, cleaning up effects");
			
			if (audioSource != null && isAlarmPlaying)
			{
				audioSource.Stop();
				isAlarmPlaying = false;
				Log("Alarm stopped on session end");
			}
			
			if (vignetteImage != null)
			{
			 GameObject go = vignetteImage.transform.root.gameObject;
			 if (go != null) UnityEngine.Object.Destroy(go);
			 vignetteImage = null;
			 currentVignetteAlpha = 0f;
			 Log("Vignette destroyed on session end");
			}
		}
		
		[HarmonyPrefix]
		[HarmonyPatch(typeof(WindowsAggregator), "CloseWindow")]
		static bool CloseWindowPrefix(string windowId)
		{
			if (windowId == TALENT_WINDOW_ID && Time.realtimeSinceStartup < menuLockUntil)
			{
				Log($"Blocked close of {windowId} (lock active) {GetHpStatus()}");
				return false;
			}
			return true;
		}
		
		[HarmonyPrefix]
		[HarmonyPatch(typeof(WindowsAggregator), "SetOrCloseWindow")]
		static bool SetOrCloseWindowPrefix(string windowId, bool isClosePrev)
		{
			if (windowId == TALENT_WINDOW_ID && Time.realtimeSinceStartup < menuLockUntil)
			{
				Log($"Blocked toggle of {windowId} (lock active) {GetHpStatus()}");
				return false;
			}
			return true;
		}
		
		[HarmonyPrefix]
		[HarmonyPatch(typeof(WindowsAggregator), "OpenPreviousWindow")]
		static bool OpenPreviousWindowPrefix()
		{
			if (Time.realtimeSinceStartup < menuLockUntil)
			{
				Log($"Blocked OpenPreviousWindow (lock active) {GetHpStatus()}");
				return false;
			}
			return true;
		}
		#endregion
		
		#region Event Handlers
		static void OnHealthChanged(HealthChangeContext context)
		{
			if (context == null)
			{
				Log("OnHealthChanged: context is null");
				return;
			}
			
			Log($"OnHealthChanged: Delta={context.Delta:F2} {GetHpStatus()}");
			
			float percent = GetHpPercent();
			float prevPercent = lastHpPercent;
			
			if (context.Delta < 0f)
			{
				Log($"Damage taken: {prevPercent:F1}% -> {percent:F1}% {GetHpStatus()}");
				
				if (TestMode.Value)
				{
					Log($"Test mode: Opening menu {GetHpStatus()}");
					OpenTalentMenu();
				}
				else if (MenuOpenHp.Value > 0f && prevPercent > MenuOpenHp.Value && percent <= MenuOpenHp.Value && percent > 0f)
				{
					Log($"Low HP threshold crossed: Opening menu {GetHpStatus()}");
					OpenTalentMenu();
				}
			}
			else if (context.Delta > 0f)
			{
				Log($"Healing: {prevPercent:F1}% -> {percent:F1}% {GetHpStatus()}");
			}
			else
			{
				Log($"No delta change {GetHpStatus()}");
			}
			
			UpdateVignette();
			UpdateAlarm();
			
			lastHpPercent = percent;
		}
		#endregion
		
		
	}
}

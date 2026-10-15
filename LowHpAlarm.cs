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
		public const string VERSION = "1.2.0";
		
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
		private static VignetteEffect _vignetteEffect;
		private static AudioEffect _audioEffect;
		
		private static float menuLockUntil;
		
		internal static SpiderEntity playerSpider;
		private static float lastHpPercent = 100f;
		
		private static TestModeHandler _testModeHandler;
		
		internal static BepInEx.Logging.ManualLogSource pluginLogger;
		
		// Menu lock duration after opening talent window
		private const float MenuLockDuration = 0.5f;
		#endregion
		
		#region Unity Lifecycle
		void Awake()
		{
			pluginLogger = Logger;
			
			TestMode = Config.Bind("General", "test", false, "If true, W adds 10% max HP, S removes 10% max HP");
			VisualAlarmHp = Config.Bind("General", "visualAlarmHp", 30f, "HP% where red vignette starts (0 = disabled)");
			AudioAlarmHp = Config.Bind("General", "audioAlarmHp", 25f, "HP% where alarm sound plays (0 = disabled)");
			MenuOpenHp = Config.Bind("General", "menuOpenHp", 10f, "HP% where improvements menu auto-opens (0 = disabled)");
			Logging = Config.Bind("General", "logging", false, "Enable detailed logging");
			VignetteIntensity = Config.Bind("General", "vignetteIntensity", 1f, "Vignette opacity multiplier (0-1)");
			VignetteColor = Config.Bind("General", "vignetteColor", "FF0000FF", "Vignette color in hex (#rgb, #rgba, #rrggbb, #rrggbbaa)");
			
			// Ensure effects are ready (idempotent)
			_vignetteEffect = new VignetteEffect();
			_audioEffect = new AudioEffect();
			_vignetteEffect.Initialize();
			_audioEffect.Initialize();
			
			// Create TestModeHandler as a dedicated MonoBehaviour so Update() is reliably called
			GameObject testModeGo = new GameObject("TestModeHandler");
			UnityEngine.Object.DontDestroyOnLoad(testModeGo);
			_testModeHandler = testModeGo.AddComponent<TestModeHandler>();
			
			Harmony.CreateAndPatchAll(typeof(LowHpAlarmPlugin));
			
			Log("Mod loaded, detailed logging enabled");
		}
		
		#endregion
		
		#region Menu
		/// <summary>
		/// Opens the talent/improvements window and sets a menu lock to prevent immediate closing.
		/// </summary>
		static void OpenTalentMenu()
		{
			try
			{
				WindowsAggregator.SetWindow(TALENT_WINDOW_ID, false);
				menuLockUntil = Time.realtimeSinceStartup + MenuLockDuration;
				Log($"Opened talent menu, lock until {menuLockUntil:F1}s {GetHpStatus()}");
			}
			catch (System.Exception ex)
			{
				pluginLogger.LogError($"Error opening talent menu: {ex.Message}");
			}
		}
		#endregion
		
		#region Helpers
		/// <summary>
		/// Logs a message if logging is enabled in config.
		/// </summary>
		public static void Log(string message)
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
			return $"[HP:{percent:F1}% Vignette:{vignetteActive} Audio:{audioActive} Playing:{_audioEffect.IsPlaying}]";
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
		/// <summary>
		/// Called when a new SpiderEntity is initialized. Subscribes to health changes and
		/// (re)initializes effects for the new session.
		/// </summary>
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
			
			// (Re)initialize effects for the new session
			_vignetteEffect.Initialize();
			_audioEffect.Initialize();
			// AudioEffect.IsPlaying is false by default after Initialize
		}
		
		/// <summary>
		/// Called when the SpiderEntity is deinitialized (e.g., returning to main menu).
		/// Cleans up effects to prevent them from persisting in the menu.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(SpiderEntity), "Deinit")]
		static void SpiderDeinitPostfix(SpiderEntity __instance)
		{
			if (__instance == null)
			{
				return;
			}
			
			Log("SpiderEntity.Deinit: session ending, cleaning up effects");
			
			// Clean up effects
			_vignetteEffect.Cleanup();
			_audioEffect.Cleanup();
		}
		
		/// <summary>
		/// Prevents the talent window from being closed while the menu lock is active.
		/// </summary>
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
		
		/// <summary>
		/// Prevents the talent window from being toggled while the menu lock is active.
		/// </summary>
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
		
		/// <summary>
		/// Prevents opening the previous window while the menu lock is active.
		/// </summary>
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
		/// <summary>
		/// Called when the player's health changes. Updates effects and auto-opens
		/// the talent menu when HP crosses the configured threshold.
		/// </summary>
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
				
				if (MenuOpenHp.Value > 0f && prevPercent > MenuOpenHp.Value && percent <= MenuOpenHp.Value && percent > 0f)
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
			
			// Update visual and audio effects based on current HP percentage
			_vignetteEffect.Update(GetHpPercent());
			_audioEffect.Update(GetHpPercent());
			
			lastHpPercent = percent;
		}
		#endregion
	}
}
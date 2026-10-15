using UnityEngine;

namespace LowHpAlarm
{
	/// <summary>
	/// MonoBehaviour that handles test mode HP manipulation via keyboard input.
	/// W adds 10% of max HP, S removes 10% of max HP.
	/// </summary>
	public class TestModeHandler : MonoBehaviour
	{
		private const float HpChangePercent = 0.1f;

		//# Unity Lifecycle
		/// <summary>
		/// Called every frame. Checks for W/S key presses and modifies the player's HP.
		/// Only active when TestMode config is enabled.
		/// </summary>
		void Update()
		{
			if (!LowHpAlarmPlugin.TestMode.Value)
				return;

			SpiderEntity playerSpider = LowHpAlarmPlugin.playerSpider;
			if (playerSpider == null || playerSpider.HealthInternal == null)
				return;

			HealthEntity health = playerSpider.HealthInternal;
			float maxHp = health.Max;
			if (maxHp <= 0f)
				return;

			float hpChange = maxHp * HpChangePercent;

			if (Input.GetKeyDown(KeyCode.W))
			{
				AddHealth(health, playerSpider, hpChange);
			}
			else if (Input.GetKeyDown(KeyCode.S))
			{
				RemoveHealth(health, playerSpider, hpChange);
			}
		}

		/// <summary>
		/// Adds HP to the player. If the player is dead, revives them with the specified amount.
		/// </summary>
		private void AddHealth(HealthEntity health, SpiderEntity playerSpider, float hpChange)
		{
			if (health.IsAlive)
			{
				float targetHp = Mathf.Min(health.Current + hpChange, health.Max);
				health.Set(targetHp, playerSpider);
			}
			else
			{
				health.Revive(playerSpider, HpChangePercent);
			}
			LowHpAlarmPlugin.Log($"TestMode W: HP={health.Current:F2}/{health.Max:F2} ({GetPercent(health):F1}%)");
		}

		/// <summary>
		/// Removes HP from the player. Does nothing if the player is dead.
		/// </summary>
		private void RemoveHealth(HealthEntity health, SpiderEntity playerSpider, float hpChange)
		{
			if (!health.IsAlive)
				return;

			float targetHp = Mathf.Max(health.Current - hpChange, 0f);
			health.Set(targetHp, playerSpider);
			LowHpAlarmPlugin.Log($"TestMode S: HP={health.Current:F2}/{health.Max:F2} ({GetPercent(health):F1}%)");
		}

		/// <summary>
		/// Calculates current HP as a percentage of max HP.
		/// </summary>
		private static float GetPercent(HealthEntity health) =>
			health.Max <= 0f ? 100f : health.Current / health.Max * 100f;
	}
}

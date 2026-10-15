using System;
using UnityEngine;

namespace LowHpAlarm
{
	public static class Utils
	{
		public static Color ParseHexColor(string hex)
		{
			try
			{
				hex = hex.Trim().Replace("#", "").ToLowerInvariant();
				int r, g, b, a;
				
				if (hex.Length == 3)
				{
					r = Convert.ToInt32(hex.Substring(0, 1) + hex.Substring(0, 1), 16);
					g = Convert.ToInt32(hex.Substring(1, 1) + hex.Substring(1, 1), 16);
					b = Convert.ToInt32(hex.Substring(2, 1) + hex.Substring(2, 1), 16);
					a = 255;
				}
				else if (hex.Length == 4)
				{
					r = Convert.ToInt32(hex.Substring(0, 1) + hex.Substring(0, 1), 16);
					g = Convert.ToInt32(hex.Substring(1, 1) + hex.Substring(1, 1), 16);
					b = Convert.ToInt32(hex.Substring(2, 1) + hex.Substring(2, 1), 16);
					a = Convert.ToInt32(hex.Substring(3, 1) + hex.Substring(3, 1), 16);
				}
				else if (hex.Length == 6)
				{
					r = Convert.ToInt32(hex.Substring(0, 2), 16);
					g = Convert.ToInt32(hex.Substring(2, 2), 16);
					b = Convert.ToInt32(hex.Substring(4, 2), 16);
					a = 255;
				}
				else if (hex.Length == 8)
				{
					r = Convert.ToInt32(hex.Substring(0, 2), 16);
					g = Convert.ToInt32(hex.Substring(2, 2), 16);
					b = Convert.ToInt32(hex.Substring(4, 2), 16);
					a = Convert.ToInt32(hex.Substring(6, 2), 16);
				}
				else
				{
					return new Color(1f, 0f, 0f, 1f);
				}
				
				return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
			}
			catch (Exception)
			{
			}
			return new Color(1f, 0f, 0f, 1f);
		}
	}
}

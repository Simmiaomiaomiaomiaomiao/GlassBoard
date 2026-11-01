using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassBoard;

public static class WidgetRules
{
	public static readonly string[] Kinds = new string[2] { "clock", "weather" };

	public static readonly string[] Names = new string[2] { "时钟", "天气" };

	public static bool Enabled(WidgetOptions o, string kind)
	{
		return kind switch
		{
			"weather" => o.Weather, 
			"clock" => o.Clock, 
			_ => false, 
		};
	}

	public static int Id(string kind)
	{
		int num = Array.IndexOf(Kinds, kind);
		return (num >= 0) ? num : 0;
	}

	public static void Enable(WidgetOptions o, string kind, bool value)
	{
		switch (kind)
		{
		case "clock":
			o.Clock = value;
			break;
		case "weather":
			o.Weather = value;
			break;
		}
	}

	public static Dictionary<int, List<string>> Layout(WidgetOptions o)
	{
		return (from k in Kinds
			where Enabled(o, k)
			select k).ToDictionary(Id, (string k) => new List<string> { k });
	}

	public static void Recover(WidgetOptions o)
	{
		string[] kinds = Kinds;
		foreach (string kind in kinds)
		{
			Enable(o, kind, value: true);
		}
		o.Normalize();
	}
}

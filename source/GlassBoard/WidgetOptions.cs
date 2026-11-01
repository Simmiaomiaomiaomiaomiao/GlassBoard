using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GlassBoard;

[DataContract]
public class WidgetOptions
{
	[DataMember]
	public bool Clock;

	[DataMember]
	public bool Weather;

	/// <summary>旧配置的面板槽位（0–3）。只用于把老布局里的位置迁移到独立组件窗口。</summary>
	[DataMember]
	public int ClockPanel;

	/// <summary>旧配置的面板槽位（0–3）。只用于把老布局里的位置迁移到独立组件窗口。</summary>
	[DataMember]
	public int WeatherPanel;

	[DataMember]
	public bool Topmost;

	[DataMember]
	public bool Locked;

	/// <summary>按组件 id 保存的位置与尺寸：0 = 时钟，1 = 天气。</summary>
	[DataMember]
	public List<WidgetPanelPosition> Panels = new List<WidgetPanelPosition>();

	[DataMember]
	public List<ClockAlarm> Alarms = new List<ClockAlarm>();

	[DataMember]
	public DateTime? TimerDueUtc;

	[DataMember]
	public double TimerPausedSeconds;

	[DataMember]
	public string City = "";

	[DataMember]
	public double OpacityPercent = 72.0;

	[DataMember]
	public bool HideControls;

	[DataMember]
	public MaterialOptions Material = new MaterialOptions();

	[DataMember]
	public List<WidgetStyle> Styles = new List<WidgetStyle>();

	[OnDeserializing]
	private void Defaults(StreamingContext context)
	{
		City = "";
		OpacityPercent = 72.0;
		Panels = new List<WidgetPanelPosition>();
	}

	public void Normalize()
	{
		if (Material == null)
		{
			Material = new MaterialOptions();
		}
		Material.Normalize();
		Alarms = (Alarms ?? new List<ClockAlarm>()).Where((ClockAlarm a) => a != null).Take(50).ToList();
		foreach (ClockAlarm alarm in Alarms)
		{
			alarm.Normalize();
		}
		TimerPausedSeconds = LayoutRules.Clamp(TimerPausedSeconds, 0.0, 86400.0, 0.0);
		City = (City ?? "").Trim();
		if (City.Length > 100)
		{
			City = City.Substring(0, 100);
		}
		OpacityPercent = LayoutRules.Clamp(OpacityPercent, 0.0, 100.0, 72.0);
		MigratePanels();
		Panels = (from p in Panels ?? new List<WidgetPanelPosition>()
			where p != null
			group p by p.Id into g
			select g.First()).ToList();
		foreach (WidgetPanelPosition panel in Panels)
		{
			panel.Normalize();
		}
		NormalizeStyles();
	}

	/// <summary>把旧的四槽位布局映射到每组件独立窗口：时钟 = 0，天气 = 1。</summary>
	private void MigratePanels()
	{
		List<WidgetPanelPosition> list = (Panels ?? new List<WidgetPanelPosition>()).Where((WidgetPanelPosition p) => p != null).ToList();
		Panels = list;
		if (list.Count <= 2 && list.All((WidgetPanelPosition p) => p.Id >= 0 && p.Id <= 1))
		{
			return;
		}
		int clockLegacy = Math.Max(0, Math.Min(3, ClockPanel));
		int weatherLegacy = Math.Max(0, Math.Min(3, WeatherPanel));
		WidgetPanelPosition clockSource = list.FirstOrDefault((WidgetPanelPosition p) => p.Id == clockLegacy) ?? list.OrderBy((WidgetPanelPosition p) => p.Id).FirstOrDefault();
		WidgetPanelPosition weatherSource = list.FirstOrDefault((WidgetPanelPosition p) => p.Id == weatherLegacy);
		List<WidgetPanelPosition> result = new List<WidgetPanelPosition>();
		if (clockSource != null)
		{
			result.Add(clockSource);
		}
		if (clockSource != null && weatherSource == clockSource)
		{
			WidgetPanelPosition copy = clockSource.Copy();
			copy.Top = ((copy.Top ?? 0.0) + 280.0);
			result.Add(copy);
		}
		else if (weatherSource != null)
		{
			result.Add(weatherSource);
		}
		for (int i = 0; i < result.Count; i++)
		{
			result[i].Id = i;
		}
		ClockPanel = 0;
		WeatherPanel = 1;
		Panels = result;
	}

	public WidgetOptions Copy()
	{
		WidgetOptions obj = (WidgetOptions)MemberwiseClone();
		obj.Styles = (Styles ?? new List<WidgetStyle>()).Select((WidgetStyle x) => x.Copy()).ToList();
		obj.Material = (Material ?? new MaterialOptions()).Copy();
		obj.Alarms = (Alarms ?? new List<ClockAlarm>()).Select((ClockAlarm a) => a.Copy()).ToList();
		obj.Panels = (from p in Panels ?? new List<WidgetPanelPosition>()
			where p != null
			select p.Copy()).ToList();
		return obj;
	}

	public WidgetPanelPosition Position(int id)
	{
		WidgetPanelPosition widgetPanelPosition = Panels.FirstOrDefault((WidgetPanelPosition x) => x.Id == id);
		if (widgetPanelPosition == null)
		{
			widgetPanelPosition = new WidgetPanelPosition
			{
				Id = id
			};
			Panels.Add(widgetPanelPosition);
		}
		return widgetPanelPosition;
	}

	public void NormalizeStyles()
	{
		Styles = (from x in Styles ?? new List<WidgetStyle>()
			where x != null && WidgetRules.Kinds.Contains(x.Kind)
			group x by x.Kind into g
			select g.First()).ToList();
		foreach (WidgetStyle style in Styles)
		{
			style.Normalize();
		}
	}

	public WidgetStyle Style(string kind)
	{
		if (Styles == null)
		{
			Styles = new List<WidgetStyle>();
		}
		WidgetStyle widgetStyle = Styles.FirstOrDefault((WidgetStyle x) => x.Kind == kind);
		if (widgetStyle == null)
		{
			widgetStyle = new WidgetStyle
			{
				Kind = kind
			};
			Styles.Add(widgetStyle);
		}
		return widgetStyle;
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace GlassBoard;

[DataContract]
public class Preferences
{
	[DataMember]
	public List<Entry> Apps = new List<Entry>();

	[DataMember(EmitDefaultValue = false)]
	public List<string> DockOrder = new List<string>();

	[DataMember]
	public int FormatVersion;

	[DataMember(EmitDefaultValue = false)]
	public long SaveRevision;

	[DataMember]
	public bool Light;

	[DataMember]
	public double DockGap = 12.0;

	[DataMember]
	public bool Imported;

	[DataMember]
	public int PanelSize = 1;

	[DataMember]
	public double BoardIconSize = 48.0;

	[DataMember]
	public double BoardIconGap = 5.0;

	[DataMember(EmitDefaultValue = false)]
	public double? OpacityPercent = 72.0;

	[DataMember(EmitDefaultValue = false)]
	public List<string> Removed = new List<string>();

	[DataMember(EmitDefaultValue = false)]
	public DockOptions Dock = new DockOptions();

	[DataMember(EmitDefaultValue = false)]
	public List<string> Groups = new List<string> { "创作", "办公", "游戏" };

	[DataMember(EmitDefaultValue = false)]
	public List<Workspace> Workspaces = new List<Workspace>();

	[DataMember(EmitDefaultValue = false)]
	public PlacementOptions Placement = new PlacementOptions();

	[DataMember(EmitDefaultValue = false)]
	public WidgetOptions Widgets = new WidgetOptions();

	[DataMember]
	public bool AutoGrouping = true;

	[DataMember]
	public bool AutoCheckUpdates;

	[DataMember]
	public string StartupTiming = "priority";

	/// <summary>开机自启动（写入登录计划任务，同时拉起 Dock 与已启用的组件）。</summary>
	[DataMember]
	public bool AutoStart = true;

	/// <summary>是否已按默认值初始化过一次开机启动（用于「默认开启」只在首次生效）。</summary>
	[DataMember]
	public bool AutoStartConfigured;

	[DataMember]
	public bool UpdateDirectConnection;

	[DataMember]
	public bool BoardDismissOnDeactivate;

	[DataMember]
	public bool BoardHideSearch;

	[DataMember]
	public bool BoardHideGroups;

	[DataMember]
	public bool BoardHideFolders;

	/// <summary>打开大面板时默认定位到的分组；空字符串表示"全部"。</summary>
	[DataMember(EmitDefaultValue = false)]
	public string BoardStartGroup = "";

	[DataMember]
	public MaterialOptions BoardMaterial = new MaterialOptions();

	[DataMember(EmitDefaultValue = false)]
	public List<string> RemovedIdentities = new List<string>();

	[DataMember]
	public long LastUpdateCheckUtc;

	[DataMember]
	public Dictionary<string, string> LearnedGroups = new Dictionary<string, string>();

	[OnDeserializing]
	private void Defaults(StreamingContext context)
	{
		AutoGrouping = true;
		BoardIconSize = 48.0;
		BoardIconGap = 5.0;
	}

	public void Normalize()
	{
		if (BoardMaterial == null)
		{
			BoardMaterial = new MaterialOptions();
		}
		BoardMaterial.Normalize();
		if (LearnedGroups == null)
		{
			LearnedGroups = new Dictionary<string, string>();
		}
		if (RemovedIdentities == null)
		{
			RemovedIdentities = new List<string>();
		}
		if (Apps == null)
		{
			Apps = new List<Entry>();
		}
		Apps = Apps.Where((Entry a) => a != null && !string.IsNullOrWhiteSpace(a.Path)).ToList();
		foreach (Entry app in Apps)
		{
			if (string.IsNullOrWhiteSpace(app.Name))
			{
				app.Name = Path.GetFileNameWithoutExtension(app.Path);
			}
			FeatureRules.NormalizeMemberships(app);
		}
		Apps = EntryRules.NormalizeEntries(Apps);
		RemovalRules.ReconcileActive(this);
		DockOrderRules.Normalize(this);
		BoardIconSize = LayoutRules.Clamp(BoardIconSize, 28.0, 80.0, 48.0);
		BoardIconGap = LayoutRules.Clamp(BoardIconGap, 0.0, 20.0, 5.0);
		DockGap = LayoutRules.Clamp(DockGap, 0.0, 160.0, 12.0);
		PanelSize = Math.Max(0, Math.Min(2, PanelSize));
		OpacityPercent = LayoutRules.GlassOpacity(OpacityPercent ?? 72.0);
		if (Removed == null)
		{
			Removed = new List<string>();
		}
		if (Dock == null)
		{
			Dock = new DockOptions
			{
				OpacityPercent = OpacityPercent.Value
			};
		}
		Dock.Normalize();
		DockLayoutRules.SyncApps(this);
		Groups = FeatureRules.Groups(Groups, Apps);
		BoardStartGroup = (BoardStartGroup ?? "").Trim();
		if (BoardStartGroup.Length > 0 && BoardStartGroup != "未分组" && !Groups.Contains(BoardStartGroup))
		{
			BoardStartGroup = "";
		}
		if (Workspaces == null)
		{
			Workspaces = new List<Workspace>();
		}
		Workspaces = Workspaces.Where((Workspace x) => x != null && !string.IsNullOrWhiteSpace(x.Name)).ToList();
		foreach (Workspace workspace in Workspaces)
		{
			workspace.Normalize();
		}
		StartupTiming = StartupRules.Mode(StartupTiming);
		if (Placement == null)
		{
			Placement = new PlacementOptions();
		}
		Placement.Normalize();
		if (Widgets == null)
		{
			Widgets = new WidgetOptions();
		}
		Widgets.Normalize();
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;

namespace GlassBoard;

public sealed class WidgetManager : IDisposable
{
	public readonly Controller C;

	public readonly WeatherEngine Weather;

	private readonly WidgetRegistry<WidgetWindow> windows = new WidgetRegistry<WidgetWindow>();

	private readonly DispatcherTimer timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private bool closed;

	private bool syncing;

	private bool pending;

	private bool recreate;

	private bool queued;

	private DateTime lastWeatherCheck = DateTime.MinValue;

	public string LastError = "";

	private readonly List<Window> reminderWindows = new List<Window>();

	internal List<WidgetWindow> SnapshotWindows => windows.Values.ToList();

	public WidgetManager(Controller c)
	{
		C = c;
		Weather = new WeatherEngine(c);
		Weather.Changed += delegate
		{
			foreach (WidgetWindow value in windows.Values)
			{
				if (value.IsVisible)
				{
					value.UpdateWeather();
				}
			}
		};
		timer.Tick += delegate
		{
			if (closed)
			{
				return;
			}
			if ((DateTime.UtcNow - lastWeatherCheck).TotalSeconds >= 30.0)
			{
				lastWeatherCheck = DateTime.UtcNow;
				Weather.Update();
			}
			foreach (WidgetWindow value2 in windows.Values)
			{
				if (value2.IsVisible)
				{
					value2.Tick();
				}
			}
		};
		timer.Start();
	}

	public void QueueSync()
	{
		if (closed || queued)
		{
			return;
		}
		queued = true;
		System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			queued = false;
			if (closed)
			{
				return;
			}
			try
			{
				Sync();
			}
			catch (Exception ex)
			{
				Store.Log(ex);
				LastError = "组件刷新失败：" + ex.Message;
				Store.Trace("WIDGET QUEUED FAILED " + ex);
			}
		});
	}

	public void SetVisible(bool visible)
	{
		foreach (WidgetWindow value in windows.Values)
		{
			if (visible && !C.GlassSuppressed)
			{
				value.Show();
				value.Tick();
				value.UpdateWeather();
			}
			else
			{
				value.Hide();
			}
		}
	}

	public void Reopen()
	{
		recreate = true;
		Sync();
	}

	public void Sync()
	{
		if (closed)
		{
			return;
		}
		if (syncing)
		{
			pending = true;
			return;
		}
		syncing = true;
		try
		{
			do
			{
				pending = false;
				Reconcile();
			}
			while (pending && !closed);
		}
		finally
		{
			syncing = false;
		}
	}

	private void Reconcile()
	{
		WidgetOptions widgetOptions = C.Prefs.Widgets;
		widgetOptions.Normalize();
		Dictionary<int, List<string>> dictionary = WidgetRules.Layout(widgetOptions);
		LastError = "";
		Store.Trace("WIDGET SYNC enabled=" + string.Join(",", WidgetRules.Kinds.Where((string k) => WidgetRules.Enabled(widgetOptions, k))) + " panels=" + dictionary.Count);
		bool flag = recreate;
		recreate = false;
		foreach (int key in windows.Keys)
		{
			if (windows.TryGetValue(key, out var value) && (flag || value.IsStopped || !dictionary.TryGetValue(key, out var value2) || !value.Kinds.SequenceEqual(value2)))
			{
				windows.Remove(key, value);
				TryStop(value);
			}
		}
		foreach (KeyValuePair<int, List<string>> item in dictionary)
		{
			if (!windows.TryGetValue(item.Key, out var value3))
			{
				value3 = new WidgetWindow(this, item.Key);
				windows.Add(item.Key, value3);
			}
			try
			{
				value3.UpdateOptions(item.Value);
				if (C.GlassSuppressed)
				{
					value3.Hide();
				}
				else if (!value3.IsVisible)
				{
					value3.Show();
				}
				Store.Trace("WIDGET SHOW panel=" + item.Key + " kinds=" + string.Join(",", item.Value) + " visible=" + value3.IsVisible + " loaded=" + value3.IsLoaded + " rect=" + value3.Left + "," + value3.Top + "," + value3.Width + "," + value3.Height);
			}
			catch (Exception ex)
			{
				windows.Remove(item.Key, value3);
				TryStop(value3);
				Store.Log(ex);
				Store.Trace("WIDGET FAILED panel=" + item.Key + " error=" + ex);
				LastError = "组件 " + ((item.Key >= 0 && item.Key < WidgetRules.Names.Length) ? WidgetRules.Names[item.Key] : item.Key.ToString()) + " 打开失败：" + ex.Message;
			}
		}
		timer.Interval = TimeSpan.FromSeconds((dictionary.Count != 0) ? 1 : 30);
		Weather.Update();
	}

	private void TryStop(WidgetWindow w)
	{
		try
		{
			w.Stop();
		}
		catch (Exception ex)
		{
			Store.Log(ex);
		}
	}

	public void Enable(string kind, bool value)
	{
		WidgetRules.Enable(C.Prefs.Widgets, kind, value);
		C.Save();
		Store.Trace("WIDGET COMMAND kind=" + kind + " enabled=" + value);
		QueueSync();
	}

	public void Closed(WidgetWindow window, bool disable)
	{
		if (!windows.Remove(window.PanelId, window) || !(!closed && disable))
		{
			return;
		}
		foreach (string kind in window.Kinds)
		{
			WidgetRules.Enable(C.Prefs.Widgets, kind, value: false);
		}
		C.Save();
		Store.Trace("WIDGET CLOSED panel=" + window.PanelId);
		QueueSync();
	}

	public void Dispose()
	{
		closed = true;
		timer.Stop();
		foreach (Window item in reminderWindows.ToList())
		{
			item.Close();
		}
		foreach (WidgetWindow value in windows.Values)
		{
			windows.Remove(value.PanelId, value);
			TryStop(value);
		}
		Weather.Stop();
	}
}

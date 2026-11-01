using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GlassBoard;

public sealed class WidgetWindow : GlassWindow
{










	private FrameworkElement weatherIcon;

	private Border widgetInputArea;

	private readonly WidgetManager M;

	private readonly Thumb moveGrip;

	private readonly Thumb resizeGrip;

	public readonly int PanelId;

	public List<string> Kinds = new List<string>();

	private bool stopped;

	private bool building;

	private bool resizing;

	private string signature = "";

	private StackPanel stack;

	private ScrollViewer viewport;

	private Viewbox contentScale;

	private double minimumWidgetHeight = 200.0;

	private TextBlock clock;

	private TextBlock date;


	private TextBlock weatherTemperature;

	private TextBlock weatherPlace;

	private TextBlock weatherDetails;

	private TextBlock weatherStatus;

	private bool BareShell => Kinds.Any((string k) => StyleFor(k).Frameless);

	private string AppearanceSignature => string.Join("|", Kinds.Select((string k) => StyleFor(k).Signature())) + "/" + M.C.Prefs.Widgets.Locked;

	public bool IsStopped => stopped;

	private bool ControlsHidden => M.C.Prefs.Widgets.Position(PanelId).HideControls ?? M.C.Prefs.Widgets.HideControls;

	internal void CheckAppearance()
	{
		string[] kinds = WidgetRules.Kinds;
		foreach (string text in kinds)
		{
			WidgetStyle widgetStyle = M.C.Prefs.Widgets.Style(text);
			widgetStyle.Frameless = true;
			widgetStyle.TextColor = "#33CCFF";
			for (int j = 0; j < 1; j++)
			{
				widgetStyle.ClockStyle = j;
				UpdateOptions(new List<string> { text });
				UpdateLayout();
				if (Surface.Visibility != Visibility.Collapsed || moveGrip.Visibility != Visibility.Collapsed || resizeGrip.Visibility != Visibility.Collapsed)
				{
					throw new Exception("无框组件仍有外框或编辑控件");
				}
				Border border = stack.Children.OfType<Border>().Single();
				if (border.Background != null || border.BorderThickness.Left != 0.0 || border.Padding.Left > 4.0)
				{
					throw new Exception("无框组件仍有卡片底板或过多留白");
				}
				if (base.ContextMenu == null || base.ContextMenu.Items.Count == 0)
				{
					throw new Exception("无框组件缺少恢复入口");
				}
				if (!base.ContextMenu.Items.OfType<MenuItem>().Any((MenuItem item) => item.Header as string == "恢复组件边框（退出无框模式）"))
				{
					throw new Exception("无框组件缺少直接恢复边框入口");
				}
				Root.Measure(new Size(base.Width - 28.0, base.Height - 28.0));
				Root.Arrange(new Rect(14.0, 14.0, base.Width - 28.0, base.Height - 28.0));
				Root.UpdateLayout();
				if (!(widgetInputArea.Background is SolidColorBrush { Color: { A: 1 } }) || widgetInputArea.Margin.Left != -14.0)
				{
					throw new Exception("无框组件缺少扩大的输入区域");
				}
				Point[] array = new Point[4]
				{
					new Point(1.0, 1.0),
					new Point(widgetInputArea.ActualWidth - 1.0, 1.0),
					new Point(1.0, widgetInputArea.ActualHeight - 1.0),
					new Point(widgetInputArea.ActualWidth / 2.0, widgetInputArea.ActualHeight / 2.0)
				};
				foreach (Point point in array)
				{
					if (widgetInputArea.InputHitTest(point) == null)
					{
						throw new Exception("无框组件空白区域不能右键");
					}
				}
				if (!((text switch
				{
					"weather" => weatherPlace, 
					_ => date, 
				}).Foreground is SolidColorBrush { Color: { R: 51 }, Color: { G: 204 } }))
				{
					throw new Exception("文字配色未应用");
				}
			}
			widgetStyle.Frameless = false;
			UpdateOptions(new List<string> { text });
			if (Surface.Visibility != Visibility.Visible)
			{
				throw new Exception("组件外框无法恢复");
			}
		}
	}

	private WidgetStyle StyleFor(string kind)
	{
		return M.C.Prefs.Widgets.Style(kind);
	}

	private void InitializeBareDragging()
	{
		widgetInputArea = new Border
		{
			Tag = "widgetInputArea",
			Margin = new Thickness(-14.0),
			Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
		};
		Root.Children.Insert(0, widgetInputArea);
		Root.PreviewMouseLeftButtonDown += delegate(object a, MouseButtonEventArgs e)
		{
			if (BareShell && !M.C.Prefs.Widgets.Locked && e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
			{
				DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
				while (dependencyObject != null && dependencyObject != Root)
				{
					if (dependencyObject is ButtonBase || dependencyObject is TextBoxBase || dependencyObject is ComboBox || dependencyObject is Thumb || dependencyObject is ScrollBar)
					{
						return;
					}
					dependencyObject = ((dependencyObject is Visual) ? VisualTreeHelper.GetParent(dependencyObject) : LogicalTreeHelper.GetParent(dependencyObject));
				}
				if (dependencyObject == Root)
				{
					e.Handled = true;
					try
					{
						DragMove();
						SnapToNeighbors();
						Rect rect = DesktopWindowPolicy.WorkArea(this);
						base.Left = FeatureRules.Position(base.Left, rect.Left, rect.Width, base.Width);
						base.Top = FeatureRules.Position(base.Top, rect.Top, rect.Height, base.Height);
						WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
						widgetPanelPosition.Left = base.Left;
						widgetPanelPosition.Top = base.Top;
						M.C.Save();
					}
					catch (InvalidOperationException)
					{
					}
				}
			}
		};
	}

	/// <summary>拖动结束后吸附到桌面中心线或相邻组件边缘。</summary>
	private void SnapToNeighbors()
	{
		if (M.C.Prefs.Widgets.Locked)
		{
			return;
		}
		List<Rect> list = new List<Rect>();
		foreach (WidgetWindow item in M.SnapshotWindows)
		{
			if (item != this && item.IsVisible)
			{
				list.Add(new Rect(item.Left, item.Top, item.Width, item.Height));
			}
		}
		Rect workArea = DesktopWindowPolicy.WorkArea(this);
		Point point = SnapRules.Snap(base.Left, base.Top, base.Width, base.Height, workArea, list);
		base.Left = point.X;
		base.Top = point.Y;
	}

	private Brush StyleInk(string kind)
	{		WidgetStyle widgetStyle = StyleFor(kind);
		Brush brush = ((!(widgetStyle.TextColor == "")) ? ((Brush)new BrushConverter().ConvertFromString(widgetStyle.TextColor)) : (M.C.Prefs.Light ? Polish.Ink(light: true) : Brushes.White));
		if (brush is SolidColorBrush { Color: var color })
		{
			color.A = (byte)Math.Round(255.0 * widgetStyle.TextOpacity / 100.0);
			SolidColorBrush solidColorBrush2 = new SolidColorBrush(color);
			solidColorBrush2.Freeze();
			return solidColorBrush2;
		}
		return brush;
	}

	private void PaintText(DependencyObject root, WidgetStyle s, Brush ink)
	{
		if (root is TextBlock textBlock)
		{
			textBlock.Foreground = ink;
			textBlock.Opacity = 1.0;
			textBlock.Effect = (s.TextShadow ? new DropShadowEffect
			{
				BlurRadius = 3.0,
				ShadowDepth = 1.0,
				Opacity = 0.65,
				Color = Colors.Black
			} : null);
		}
		if (root is Control control)
		{
			control.Foreground = ink;
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			PaintText(VisualTreeHelper.GetChild(root, i), s, ink);
		}
	}

	private void PaintCard(Border card, string kind)
	{
		PaintText(card, StyleFor(kind), StyleInk(kind));
	}

	private void ApplyCardAppearance()
	{
		if (stack == null)
		{
			return;
		}
		foreach (Border item in stack.Children.OfType<Border>())
		{
			string text = item.Tag as string;
			if (WidgetRules.Kinds.Contains(text))
			{
				WidgetStyle widgetStyle = StyleFor(text);
				double num = WidgetSizing.Background(M.C.Prefs.Widgets, M.C.Prefs.Widgets.Position(PanelId));
				item.Background = ((widgetStyle.Frameless || num == 0.0) ? null : GlassMaterials.Fill(CurrentMaterial, LightTheme, num));
				item.BorderBrush = (widgetStyle.Frameless ? null : Polish.Line(LightTheme));
				item.BorderThickness = new Thickness((!widgetStyle.Frameless) ? 1 : 0);
				PaintCard(item, text);
			}
		}
		if (BareShell)
		{
			ReleaseBackdrop();
			Surface.Visibility = Visibility.Collapsed;
			{
				foreach (Border item2 in from b in Root.Children.OfType<Border>()
					where b.Tag as string == "material"
					select b)
				{
					item2.Visibility = Visibility.Collapsed;
				}
				return;
			}
		}
		Surface.Visibility = Visibility.Visible;
		foreach (Border item3 in from b in Root.Children.OfType<Border>()
			where b.Tag as string == "material"
			select b)
		{
			item3.Visibility = Visibility.Visible;
		}
	}

	private void AddAppearanceMenu(ContextMenu menu, string kind)
	{
		MenuItem menuItem = new MenuItem
		{
			Header = "外观 DIY · 文字颜色 / 无框 / 样式"
		};
		menuItem.Click += delegate
		{
			M.C.CustomizeWidgets(kind, this);
		};
		menu.Items.Insert(0, menuItem);
		MenuItem bare = new MenuItem
		{
			Header = "无框模式",
			IsCheckable = true,
			IsChecked = StyleFor(kind).Frameless
		};
		bare.Click += delegate
		{
			StyleFor(kind).Frameless = bare.IsChecked;
			M.C.Save();
			M.Sync();
		};
		menu.Items.Insert(1, bare);
	}

	private void ClockDesign(StackPanel parent)
	{
		WidgetStyle widgetStyle = StyleFor("clock");
		clock = new TextBlock
		{
			FontSize = widgetStyle.ClockSize,
			FontWeight = FontWeights.Light,
			Foreground = base.TextBrush
		};
		date = new TextBlock
		{
			FontSize = 13.0,
			Margin = new Thickness(2.0, 4.0, 0.0, 0.0),
			Foreground = base.MutedBrush
		};
		if (!string.IsNullOrEmpty(widgetStyle.Font))
		{
			clock.FontFamily = new FontFamily(widgetStyle.Font);
		}
		StackPanel stackPanel = new StackPanel
		{
			Cursor = Cursors.SizeAll
		};
		stackPanel.Children.Add(clock);
		stackPanel.Children.Add(date);
		date.Visibility = ((!widgetStyle.ShowDate) ? Visibility.Collapsed : Visibility.Visible);
		stackPanel.MouseLeftButtonDown += delegate(object a, MouseButtonEventArgs e)
		{
			if (e.ClickCount != 1 || M.C.Prefs.Widgets.Locked)
			{
				return;
			}
			e.Handled = true;
			try
			{
				DragMove();
				SnapToNeighbors();
				Rect rect = DesktopWindowPolicy.WorkArea(this);
				base.Left = FeatureRules.Position(base.Left, rect.Left, rect.Width, base.Width);
				base.Top = FeatureRules.Position(base.Top, rect.Top, rect.Height, base.Height);
				WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
				widgetPanelPosition.Left = base.Left;
				widgetPanelPosition.Top = base.Top;
				M.C.Save();
				Backdrop(LightTheme);
				ApplyWidgetGlass();
			}
			catch (InvalidOperationException)
			{
			}
		};
		parent.Children.Add(stackPanel);
	}
	private static bool SetText(TextBlock text, string value)
	{
		if (text == null || text.Text == value)
		{
			return false;
		}
		text.Text = value;
		return true;
	}

	private void ClockTick()
	{
		WidgetStyle widgetStyle = StyleFor("clock");
		DateTime now = DateTime.Now;
		CultureInfo cultureInfo = CultureInfo.GetCultureInfo("en-US");
		SetText(clock, now.ToString(widgetStyle.Hour24 ? "HH:mm" : "hh:mm tt", cultureInfo));
		SetText(date, now.ToString("M月d日  dddd"));
	}
	private FrameworkElement WeatherSymbol(string icon)
	{
		string text = icon switch
		{
			"cloud" => "M 3,15 C -1,8 5,5 7,7 C 9,0 17,3 16,8 C 22,8 22,15 17,15 Z", 
			"fog" => "M 2,6 L 18,6 M 0,10 L 20,10 M 3,14 L 17,14 M 1,18 L 19,18", 
			"storm" => "M 3,12 C -1,6 5,4 7,6 C 9,0 17,3 16,7 C 22,7 22,12 17,12 M 11,10 L 7,16 L 12,16 L 9,22", 
			"snow" => "M 10,1 L 10,19 M 2,5 L 18,15 M 2,15 L 18,5 M 7,3 L 10,6 L 13,3 M 7,17 L 10,14 L 13,17", 
			"rain" => "M 3,13 C -1,6 5,4 7,6 C 9,0 17,3 16,7 C 22,7 22,13 17,13 Z M 6,16 L 4,20 M 12,16 L 10,20 M 18,16 L 16,20", 
			_ => null, 
		};
		if (text == null)
		{
			return Polish.Icon((icon == "sun") ? "sun" : "calendar", StyleInk("weather"), 44.0);
		}
		return new System.Windows.Shapes.Path
		{
			Data = Geometry.Parse(text),
			Stroke = StyleInk("weather"),
			StrokeThickness = 1.5,
			Fill = Brushes.Transparent,
			StrokeLineJoin = PenLineJoin.Round,
			Width = 44.0,
			Height = 44.0,
			Stretch = Stretch.Uniform
		};
	}

	public WidgetWindow(WidgetManager manager, int id)
		: base(29.0)
	{
		M = manager;
		PanelId = id;
		InitializeBareDragging();
		base.Title = "GlassBoard 组件 · " + ((id >= 0 && id < WidgetRules.Names.Length) ? WidgetRules.Names[id] : "组件");
		base.ShowActivated = false;
		moveGrip = new Thumb
		{
			Width = 48.0,
			Height = 5.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 9.0, 0.0, 0.0),
			Cursor = Cursors.SizeAll,
			ToolTip = "拖动组件面板"
		};
		moveGrip.Template = GlassWindow.GripTemplate();
		Root.Children.Add(moveGrip);
		moveGrip.DragDelta += delegate(object s, DragDeltaEventArgs e)
		{
			if (!M.C.Prefs.Widgets.Locked)
			{
				Rect rect = DesktopWindowPolicy.WorkArea(this, pointer: true);
				base.Left = FeatureRules.Position(base.Left + e.HorizontalChange, rect.Left, rect.Width, base.Width);
				base.Top = FeatureRules.Position(base.Top + e.VerticalChange, rect.Top, rect.Height, base.Height);
			}
		};
		moveGrip.DragCompleted += delegate
		{
			SnapToNeighbors();
			WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
			widgetPanelPosition.Left = base.Left;
			widgetPanelPosition.Top = base.Top;
			if (PanelId == 0)
			{
				M.C.Prefs.Placement.WidgetLeft = base.Left;
				M.C.Prefs.Placement.WidgetTop = base.Top;
			}
			M.C.Save();
			Backdrop(LightTheme);
			ApplyWidgetGlass();
		};
		resizeGrip = new Thumb
		{
			Width = 20.0,
			Height = 20.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0.0, 0.0, 7.0, 7.0),
			Cursor = Cursors.SizeNWSE,
			ToolTip = "拖动调整组件大小；内容自动适配，最小宽度 240，最小高度按内容计算"
		};
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
		frameworkElementFactory2.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 4,16 L 16,4 M 10,16 L 16,10"));
		frameworkElementFactory2.SetValue(Shape.StrokeProperty, Polish.Muted(light: false));
		frameworkElementFactory2.SetValue(Shape.StrokeThicknessProperty, 1.5);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		resizeGrip.Template = new ControlTemplate(typeof(Thumb))
		{
			VisualTree = frameworkElementFactory
		};
		Root.Children.Add(resizeGrip);
		resizeGrip.DragDelta += delegate(object s, DragDeltaEventArgs e)
		{
			if (!M.C.Prefs.Widgets.Locked)
			{
				Rect rect = DesktopWindowPolicy.WorkArea(this);
				WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
				widgetPanelPosition.Width = Math.Max(Math.Min(240.0, rect.Width - 30.0), Math.Min(rect.Width - 30.0, base.Width + e.HorizontalChange));
				widgetPanelPosition.Height = Math.Max(Math.Min(minimumWidgetHeight, rect.Height - 30.0), Math.Min(rect.Height - 30.0, base.Height + e.VerticalChange));
				base.Width = widgetPanelPosition.Width.Value;
				FitContent();
				base.Left = FeatureRules.Position(base.Left, rect.Left, rect.Width, base.Width);
				base.Top = FeatureRules.Position(base.Top, rect.Top, rect.Height, base.Height);
			}
		};
		resizeGrip.DragCompleted += delegate
		{
			WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
			widgetPanelPosition.Left = base.Left;
			widgetPanelPosition.Top = base.Top;
			M.C.Save();
			Backdrop(LightTheme);
			ApplyWidgetGlass();
		};
		base.Closed += delegate
		{
			bool disable = !stopped;
			stopped = true;
			M.Closed(this, disable);
		};
	}

	public void Stop()
	{
		if (!stopped)
		{
			stopped = true;
			Close();
		}
	}

	public void UpdateOptions(List<string> kinds)
	{
		WidgetOptions widgets = M.C.Prefs.Widgets;
		Kinds = kinds.ToList();
		bool flag = signature != string.Join(",", Kinds) + "/" + M.C.Prefs.Light + "/" + ControlsHidden + "/" + AppearanceSignature;
		LightTheme = M.C.Prefs.Light;
		MotionEnabled = M.C.Prefs.Dock.MotionEnabled;
		MotionLevel = M.C.Prefs.Dock.MotionLevel;
		ConfigureGlass(M.C.Prefs.Dock, widgets.Material);
		GlassPercent = (BareShell ? 0.0 : WidgetSizing.Background(widgets, widgets.Position(PanelId)));
		base.Topmost = widgets.Topmost;
		moveGrip.Visibility = ((BareShell || widgets.Locked || Kinds.SequenceEqual(new string[1] { "clock" })) ? Visibility.Collapsed : Visibility.Visible);
		resizeGrip.Visibility = ((BareShell || widgets.Locked) ? Visibility.Collapsed : Visibility.Visible);
		Polish.Apply(this, LightTheme);
		Rect rect = DesktopWindowPolicy.WorkArea(this);
		base.Width = Math.Max(Math.Min(240.0, rect.Width - 30.0), Math.Min(widgets.Position(PanelId).Width ?? Math.Min(450.0, rect.Width * 0.32), rect.Width - 30.0));
		if (flag)
		{
			Build();
			signature = string.Join(",", Kinds) + "/" + LightTheme + "/" + ControlsHidden + "/" + AppearanceSignature;
		}
		Tick();
		UpdateWeather();
		FitContent();
		WidgetPanelPosition widgetPanelPosition = widgets.Position(PanelId);
		double? num = widgetPanelPosition.Left;
		double? num2 = widgetPanelPosition.Top;
		if (PanelId == 0)
		{
			num = num ?? M.C.Prefs.Placement.WidgetLeft;
			num2 = num2 ?? M.C.Prefs.Placement.WidgetTop;
		}
		base.Left = FeatureRules.Position(num ?? (rect.Right - base.Width - 18.0), rect.Left, rect.Width, base.Width);
		base.Top = FeatureRules.Position(num2 ?? (rect.Top + 18.0 + (double)(PanelId * 260)), rect.Top, rect.Height, base.Height);
		Backdrop(LightTheme);
		ApplyWidgetGlass();
		RootWidgetMenu();
		Tick();
		UpdateWeather();
	}

	private void FitContent()
	{
		if (stack == null || contentScale == null || building || resizing)
		{
			return;
		}
		resizing = true;
		try
		{
			Rect rect = DesktopWindowPolicy.WorkArea(this);
			double num = Math.Max(100.0, base.Width - 28.0);
			stack.Width = 390.0;
			stack.Measure(new Size(422.0, double.PositiveInfinity));
			double num2 = Math.Ceiling(stack.DesiredSize.Height);
			minimumWidgetHeight = WidgetSizing.MinimumHeight(num2, rect.Height - 30.0);
			double num3 = num2 * (num / 422.0) + 28.0;
			WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
			double num4 = (BareShell ? Math.Min(rect.Height - 30.0, Math.Max(72.0, num3)) : Math.Max(minimumWidgetHeight, Math.Min(rect.Height - 30.0, widgetPanelPosition.Height ?? num3)));
			contentScale.Width = num;
			contentScale.Height = Math.Max(BareShell ? 44 : 100, num4 - 28.0);
			viewport.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
			viewport.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
			if (!BareShell && widgetPanelPosition.Height.HasValue)
			{
				widgetPanelPosition.Height = num4;
			}
			if (Math.Abs(base.Height - num4) > 0.5 || double.IsNaN(base.Height))
			{
				base.Height = num4;
				if (base.IsVisible)
				{
					base.Top = FeatureRules.Position(base.Top, rect.Top, rect.Height, base.Height);
					Backdrop(LightTheme);
					ApplyWidgetGlass();
				}
			}
		}
		finally
		{
			resizing = false;
		}
	}

	private void ApplyWidgetGlass()
	{
		SetGlassOpacity(BareShell ? 0.0 : WidgetSizing.Background(M.C.Prefs.Widgets, M.C.Prefs.Widgets.Position(PanelId)));
		Surface.BorderBrush = ((GlassPercent == 0.0) ? Brushes.Transparent : Polish.Line(LightTheme));
		ApplyCardAppearance();
	}

	private Button Small(string text, Action action, double width = 30.0)
	{
		Button button = Button(text, delegate
		{
			action();
		}, width);
		button.Height = 28.0;
		button.Margin = new Thickness(2.0, 0.0, 2.0, 0.0);
		button.FontSize = 12.0;
		return button;
	}

	private Button Glyph(string icon, Action action, double size = 34.0, bool primary = false)
	{
		Button button = Button("", delegate
		{
			action();
		}, size);
		button.Height = size;
		button.Margin = new Thickness(3.0);
		button.Content = Polish.Icon(icon, primary ? Polish.Ink(light: true) : base.TextBrush, (size >= 48.0) ? 22 : 17);
		if (primary)
		{
			button.Style = Polish.ButtonStyle(light: true);
			button.Background = new LinearGradientBrush(Color.FromRgb(224, 234, byte.MaxValue), Colors.White, 90.0);
			button.BorderBrush = Brushes.Transparent;
			FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
			frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(size / 2.0));
			frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
			{
				RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
			});
			FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
			frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
			frameworkElementFactory.AppendChild(frameworkElementFactory2);
			button.Template = new ControlTemplate(typeof(Button))
			{
				VisualTree = frameworkElementFactory
			};
		}
		return button;
	}

	private void Build()
	{
		building = true;
		try
		{
			ContentGrid.Children.Clear();
			clock = null;
			date = null;
			weatherTemperature = null;
			weatherPlace = null;
			weatherDetails = null;
			weatherStatus = null;
			stack = new StackPanel
			{
				Margin = (BareShell ? new Thickness(2.0) : new Thickness(16.0, Kinds.SequenceEqual(new string[1] { "clock" }) ? 16 : 25, 16.0, 16.0))
			};
			contentScale = new Viewbox
			{
				Child = stack,
				Stretch = Stretch.Uniform,
				StretchDirection = StretchDirection.Both,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Top
			};
			viewport = new ScrollViewer
			{
				Content = contentScale,
				VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
			};
			ContentGrid.Children.Add(viewport);
			foreach (string kind in Kinds)
			{
				StackPanel stackPanel = new StackPanel();
				Border border = new Border
				{
					Background = Polish.Card(LightTheme),
					BorderBrush = Polish.Line(LightTheme),
					BorderThickness = new Thickness(1.0),
					CornerRadius = new CornerRadius(22.0),
					Tag = kind,
					Padding = (StyleFor(kind).Frameless ? new Thickness(4.0) : new Thickness(18.0)),
					Margin = new Thickness(0.0, 0.0, 0.0, 14.0),
					Child = stackPanel
				};
				stack.Children.Add(border);
				if (!StyleFor(kind).Frameless)
				{
					Header(stackPanel, kind);
				}
				switch (kind)
				{
				case "clock":
					ClockDesign(stackPanel);
					break;
				default:
					Weather(stackPanel);
					break;
				}
				PaintCard(border, kind);
			}
			if (stack.Children.Count > 0)
			{
				((Border)stack.Children[stack.Children.Count - 1]).Margin = new Thickness(0.0);
			}
		}
		finally
		{
			building = false;
		}
	}

	private ContextMenu WidgetMenu(string kind)
	{
		ContextMenu contextMenu = MenuTheme.Create(LightTheme);
		MenuItem lockPosition = new MenuItem
		{
			Header = "锁定组件位置和尺寸",
			IsCheckable = true,
			IsChecked = M.C.Prefs.Widgets.Locked
		};
		lockPosition.Click += delegate
		{
			M.C.Prefs.Widgets.Locked = lockPosition.IsChecked;
			M.C.Save();
			M.Sync();
		};
		contextMenu.Items.Add(lockPosition);
		MenuItem menuItem9 = new MenuItem
		{
			Header = "恢复自动尺寸"
		};
		menuItem9.Click += delegate
		{
			WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
			widgetPanelPosition.Width = (widgetPanelPosition.Height = null);
			M.C.Save();
			M.Sync();
		};
		contextMenu.Items.Add(menuItem9);
		MenuItem newItem = new MenuItem
		{
			Header = "背景透明度（100% 为全透明）",
			IsEnabled = false
		};
		contextMenu.Items.Add(newItem);
		Slider transparency = new Slider
		{
			Width = 190.0,
			Minimum = 0.0,
			Maximum = 100.0,
			Value = 100.0 - WidgetSizing.Background(M.C.Prefs.Widgets, M.C.Prefs.Widgets.Position(PanelId)),
			IsSnapToTickEnabled = true,
			TickFrequency = 5.0
		};
		transparency.ValueChanged += delegate
		{
			M.C.Prefs.Widgets.Position(PanelId).OpacityPercent = 100.0 - transparency.Value;
			ApplyWidgetGlass();
		};
		transparency.PreviewMouseLeftButtonUp += delegate
		{
			M.C.Save();
		};
		transparency.KeyUp += delegate
		{
			M.C.Save();
		};
		contextMenu.Items.Add(new MenuItem
		{
			Header = transparency,
			StaysOpenOnClick = true
		});
		contextMenu.Closed += delegate
		{
			M.C.Save();
		};
		MenuItem menuItem10 = new MenuItem
		{
			Header = "组件设置"
		};
		menuItem10.Click += delegate
		{
			M.C.ConfigureWidgets();
		};
		contextMenu.Items.Add(menuItem10);
		MenuItem menuItem11 = new MenuItem
		{
			Header = (ControlsHidden ? "显示右上角关闭和更多按钮" : "隐藏右上角关闭和更多按钮")
		};
		menuItem11.Click += delegate
		{
			M.C.Prefs.Widgets.Position(PanelId).HideControls = !ControlsHidden;
			M.C.Save();
			M.Sync();
		};
		contextMenu.Items.Insert(0, menuItem11);
		AddAppearanceMenu(contextMenu, kind);
		return contextMenu;
	}

	private void RootWidgetMenu()
	{
		ContextMenu contextMenu = MenuTheme.Create(LightTheme);
		if (BareShell)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = "恢复组件边框（退出无框模式）"
			};
			menuItem.Click += delegate
			{
				foreach (string kind in Kinds)
				{
					StyleFor(kind).Frameless = false;
				}
				M.C.Save();
				M.Sync();
			};
			contextMenu.Items.Add(menuItem);
		}
		MenuItem menuItem2 = new MenuItem
		{
			Header = (ControlsHidden ? "显示右上角关闭和更多按钮" : "隐藏右上角关闭和更多按钮")
		};
		menuItem2.Click += delegate
		{
			M.C.Prefs.Widgets.Position(PanelId).HideControls = !ControlsHidden;
			M.C.Save();
			M.Sync();
		};
		contextMenu.Items.Add(menuItem2);
		contextMenu.Closed += delegate
		{
			M.C.Save();
		};
		foreach (string kind2 in Kinds)
		{
			MenuItem menuItem3 = new MenuItem
			{
				Header = WidgetRules.Names[Array.IndexOf(WidgetRules.Kinds, kind2)]
			};
			ContextMenu contextMenu2 = WidgetMenu(kind2);
			while (contextMenu2.Items.Count > 0)
			{
				object newItem = contextMenu2.Items[0];
				contextMenu2.Items.RemoveAt(0);
				menuItem3.Items.Add(newItem);
			}
			contextMenu.Items.Add(menuItem3);
		}
		base.ContextMenu = contextMenu;
	}

	private void Header(StackPanel parent, string kind)
	{
		int num = Array.IndexOf(WidgetRules.Kinds, kind);
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		TextBlock textBlock = new TextBlock
		{
			Text = WidgetRules.Names[num],
			Foreground = base.MutedBrush,
			FontSize = 11.0,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Left,
			Cursor = Cursors.SizeAll,
			ToolTip = "拖动标题移动组件"
		};
		textBlock.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			if (!M.C.Prefs.Widgets.Locked && e.ButtonState == MouseButtonState.Pressed)
			{
				DragMove();
				SnapToNeighbors();
				WidgetPanelPosition widgetPanelPosition = M.C.Prefs.Widgets.Position(PanelId);
				Rect rect = DesktopWindowPolicy.WorkArea(this);
				base.Left = FeatureRules.Position(base.Left, rect.Left, rect.Width, base.Width);
				base.Top = FeatureRules.Position(base.Top, rect.Top, rect.Height, base.Height);
				widgetPanelPosition.Left = base.Left;
				widgetPanelPosition.Top = base.Top;
				M.C.Save();
				Backdrop(LightTheme);
				ApplyWidgetGlass();
			}
		};
		grid.Children.Add(textBlock);
		StackPanel stackPanel = new StackPanel
		{
			Tag = "widgetControls",
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button options = Small("•••", delegate
		{
		});
		options.ToolTip = "组件设置";
		options.Click += delegate
		{
			ContextMenu contextMenu = WidgetMenu(kind);
			options.ContextMenu = contextMenu;
			contextMenu.PlacementTarget = options;
			contextMenu.IsOpen = true;
		};
		stackPanel.Children.Add(options);
		Button button = Glyph("close", delegate
		{
			M.Enable(kind, value: false);
		}, 26.0);
		button.ToolTip = "关闭这个组件";
		stackPanel.Children.Add(button);
		stackPanel.Visibility = (ControlsHidden ? Visibility.Collapsed : Visibility.Visible);
		grid.Children.Add(stackPanel);
		parent.Children.Add(grid);
	}

	private void Weather(StackPanel parent)
	{
		weatherPlace = new TextBlock
		{
			Foreground = base.TextBrush,
			FontSize = 14.0,
			FontWeight = FontWeights.Medium,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		parent.Children.Add(weatherPlace);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel stackPanel = new StackPanel();
		grid.Children.Add(stackPanel);
		weatherTemperature = new TextBlock
		{
			Foreground = base.TextBrush,
			FontSize = 43.0,
			FontWeight = FontWeights.Light,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(weatherTemperature);
		weatherIcon = new ContentControl
		{
			Content = WeatherSymbol("cloud"),
			Width = 44.0,
			Height = 44.0
		};
		parent.Children.Add(grid);
		weatherDetails = new TextBlock
		{
			Foreground = base.TextBrush,
			FontSize = 12.0,
			Margin = new Thickness(0.0, 5.0, 0.0, 8.0)
		};
		parent.Children.Add(weatherDetails);
		Grid grid2 = new Grid();
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		weatherStatus = new TextBlock
		{
			Foreground = base.MutedBrush,
			FontSize = 10.0,
			VerticalAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			MaxHeight = 56.0,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		Grid.SetColumn(weatherStatus, 0);
		grid2.Children.Add(weatherStatus);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Grid.SetColumn(stackPanel2, 1);
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
		UIElementCollection children = stackPanel2.Children;
		Button button = Small("刷新", delegate
		{
			M.Weather.Update(force: true);
		}, 34.0);
		button.FontSize = 9.0;
		button.Height = 22.0;
		children.Add(button);
		Button button2 = Small("天气来源", delegate
		{
			Process.Start(new ProcessStartInfo((M.Weather.Snapshot != null && M.Weather.Snapshot.Provider == "wttr.in") ? "https://wttr.in/" : "https://open-meteo.com/")
			{
				UseShellExecute = true
			});
		}, 62.0);
		button2.FontSize = 9.0;
		button2.Height = 22.0;
		stackPanel2.Children.Add(button2);
		grid2.Children.Add(stackPanel2);
		parent.Children.Add(grid2);
	}

	public void Tick()
	{
		if (clock != null)
		{
			ClockTick();
		}
	}

	public void UpdateWeather()
	{
		if (weatherTemperature != null)
		{
			WeatherSnapshot snapshot = M.Weather.Snapshot;
			weatherTemperature.Text = ((snapshot == null) ? "—°" : (snapshot.Current.Temperature.ToString("0.#") + "°"));
			weatherPlace.Text = ((snapshot != null) ? WeatherPresentation.Place(M.C.Prefs.Widgets.City, snapshot.Place) : (string.IsNullOrWhiteSpace(M.C.Prefs.Widgets.City) ? "选择你的城市" : M.C.Prefs.Widgets.City));
			if (weatherIcon != null)
			{
				((ContentControl)weatherIcon).Content = WeatherSymbol((snapshot == null) ? "cloud" : WeatherPresentation.Icon(snapshot.Current.Code, snapshot.Current.Description));
			}
			weatherDetails.Text = ((snapshot == null) ? "" : (WeatherPresentation.Description(snapshot.Current.Code, snapshot.Current.Description) + "  ·  风速 " + snapshot.Current.Wind.ToString("0.#") + " km/h"));
			weatherStatus.Text = M.Weather.Message;
			weatherStatus.ToolTip = M.Weather.Message;
			weatherPlace.ToolTip = weatherPlace.Text;
			FitContent();
		}
	}

}

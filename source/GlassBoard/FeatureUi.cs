using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassBoard;

public static class FeatureUi
{
	private static readonly BoundedCache<string, ImageSource> customIconCache = new BoundedCache<string, ImageSource>(128, StringComparer.OrdinalIgnoreCase);

	internal static void ReleaseCustomIcons()
	{
		customIconCache.Clear();
	}

	public static string PickFolder(Window window, string description)
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = description,
			ShowNewFolderButton = false
		};
		NativeWindow nativeWindow = new NativeWindow();
		nativeWindow.AssignHandle(new WindowInteropHelper(window).EnsureHandle());
		try
		{
			return (folderBrowserDialog.ShowDialog(nativeWindow) == DialogResult.OK) ? folderBrowserDialog.SelectedPath : null;
		}
		finally
		{
			nativeWindow.ReleaseHandle();
		}
	}

	public static Window Dialog(string title, Window owner, double width = 650.0, double height = 560.0)
	{
		Window obj = new Window
		{
			Title = "GlassBoard · " + title,
			Owner = owner,
			Width = Math.Min(width, SystemParameters.WorkArea.Width - 40.0),
			Height = Math.Min(height, SystemParameters.WorkArea.Height - 50.0),
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = new SolidColorBrush(Color.FromRgb(30, 33, 42)),
			Foreground = Brushes.White,
			Topmost = true
		};
		Polish.Apply(obj, light: false);
		return obj;
	}

	public static System.Windows.Controls.Button Button(string text, Action action)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button();
		button.Content = text;
		button.Padding = new Thickness(12.0, 7.0, 12.0, 7.0);
		button.Margin = new Thickness(0.0, 0.0, 8.0, 8.0);
		button.Click += delegate
		{
			action();
		};
		return button;
	}

	public static string Ask(Window owner, string title, string value = "")
	{
		Window w = Dialog(title, owner, 440.0, 180.0);
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(22.0)
		};
		w.Content = stackPanel;
		System.Windows.Controls.TextBox text = new System.Windows.Controls.TextBox
		{
			Text = value,
			MinHeight = 38.0,
			FontSize = 14.0
		};
		stackPanel.Children.Add(text);
		string result = null;
		System.Windows.Controls.Button apply = Button("确定", delegate
		{
			if (!string.IsNullOrWhiteSpace(text.Text))
			{
				result = text.Text.Trim();
				w.DialogResult = true;
			}
		});
		apply.Margin = new Thickness(0.0, 18.0, 0.0, 0.0);
		stackPanel.Children.Add(apply);
		w.Loaded += delegate
		{
			text.Focus();
			text.SelectAll();
		};
		text.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
			}
		};
		w.ShowDialog();
		return result;
	}

	public static string StoreIcon(string path)
	{
		Directory.CreateDirectory(Path.Combine(Store.Folder, "icons"));
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.UriSource = new Uri(path);
		bitmapImage.DecodePixelWidth = 128;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapImage));
		string text = Path.Combine(Store.Folder, "icons", Guid.NewGuid().ToString("N") + ".png");
		using FileStream stream = File.Create(text);
		pngBitmapEncoder.Save(stream);
		return text;
	}

	public static ImageSource Icon(Entry entry, bool showShortcutOverlays = false, bool sharper = false)
	{
		if (!string.IsNullOrWhiteSpace(entry.IconPath) && File.Exists(entry.IconPath))
		{
			try
			{
				FileInfo fileInfo = new FileInfo(entry.IconPath);
				string key = fileInfo.FullName + "/" + fileInfo.Length + "/" + fileInfo.LastWriteTimeUtc.Ticks;
				if (customIconCache.TryGet(key, out var value))
				{
					return value;
				}
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.UriSource = new Uri(entry.IconPath);
				bitmapImage.DecodePixelWidth = 128;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				customIconCache.Put(key, bitmapImage);
				return bitmapImage;
			}
			catch
			{
			}
		}
		ImageSource imageSource = Native.Icon(entry.Path, showShortcutOverlays, sharper);
		if (imageSource != null)
		{
			return imageSource;
		}
		if (entry.IsFolder || Directory.Exists(entry.Path))
		{
			DrawingImage drawingImage = new DrawingImage(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(240, 191, 77)), new Pen(new SolidColorBrush(Color.FromRgb(204, 149, 45)), 1.0), Geometry.Parse("M 2,5 L 8,5 L 10,7 L 18,7 L 18,17 L 2,17 Z")));
			drawingImage.Freeze();
			return drawingImage;
		}
		return null;
	}
}

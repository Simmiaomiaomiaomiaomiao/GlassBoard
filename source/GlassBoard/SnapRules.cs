using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GlassBoard;

/// <summary>自由拖动时把窗口吸附到桌面中心线与相邻窗口的边缘/中心线。</summary>
public static class SnapRules
{
	/// <summary>吸附阈值（DIP）：离对齐线小于这个距离就贴上去。</summary>
	public const double Threshold = 10.0;

	private static readonly double[] Anchors = new double[3] { 0.0, 0.5, 1.0 };

	public static Point Snap(double left, double top, double width, double height, Rect workArea, IEnumerable<Rect> obstacles)
	{
		double num = SnapAxis(left, width, workArea.Left, workArea.Width, GuidesX(obstacles));
		double num2 = SnapAxis(top, height, workArea.Top, workArea.Height, GuidesY(obstacles));
		double val = Math.Max(workArea.Left, Math.Min(workArea.Right - width, num));
		double val2 = Math.Max(workArea.Top, Math.Min(workArea.Bottom - height, num2));
		return new Point(val, val2);
	}

	private static double SnapAxis(double start, double length, double origin, double extent, IEnumerable<double> guides)
	{
		double result = start;
		double num = Threshold;
		foreach (double item in guides.Concat(new double[1] { origin + extent / 2.0 }))
		{
			foreach (double anchor in Anchors)
			{
				double num2 = item - (start + length * anchor);
				double num3 = Math.Abs(num2);
				if (num3 < num)
				{
					num = num3;
					result = start + num2;
				}
			}
		}
		return result;
	}

	private static IEnumerable<double> GuidesX(IEnumerable<Rect> obstacles)
	{
		foreach (Rect obstacle in obstacles ?? Enumerable.Empty<Rect>())
		{
			yield return obstacle.Left;
			yield return obstacle.Left + obstacle.Width / 2.0;
			yield return obstacle.Right;
		}
	}

	private static IEnumerable<double> GuidesY(IEnumerable<Rect> obstacles)
	{
		foreach (Rect obstacle in obstacles ?? Enumerable.Empty<Rect>())
		{
			yield return obstacle.Top;
			yield return obstacle.Top + obstacle.Height / 2.0;
			yield return obstacle.Bottom;
		}
	}
}

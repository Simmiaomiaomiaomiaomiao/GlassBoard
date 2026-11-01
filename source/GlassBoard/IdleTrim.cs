using System;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace GlassBoard;

/// <summary>
/// 长时间无人操作时释放可重建的缓存并回收工作集。只在桌面空闲时执行，
/// 交互中绝不触发；游戏模式本身由 FullscreenState 的调用方处理，不会在这里重复。
/// </summary>
internal static class IdleTrim
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);

    private static readonly TimeSpan IdleThreshold = HasTestFlag() ? TimeSpan.FromSeconds(20) : TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMinutes(10);

    private static bool HasTestFlag()
    {
        string[] args = Environment.GetCommandLineArgs();
        return args != null && Array.IndexOf(args, "--idle-trim-test") >= 0;
    }

    private static DispatcherTimer timer;
    private static Action release;
    private static Func<bool> busy;
    private static DateTime lastRun = DateTime.MinValue;

    public static void Start(Action releaseCaches, Func<bool> isBusy)
    {
        release = releaseCaches;
        busy = isBusy;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += delegate { Tick(); };
        timer.Start();
    }

    public static void TrimNow()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            GC.WaitForPendingFinalizers();
            // 大对象堆通常只在完整压缩时才归还；空闲/关面板时做一次，避免私有提交长期虚高。
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
            lastRun = DateTime.UtcNow;
        }
        catch
        {
        }
    }

    private static void Tick()
    {
        if ((DateTime.UtcNow - lastRun) < RepeatInterval)
        {
            return;
        }
        try
        {
            if (busy != null && busy())
            {
                return;
            }
            LASTINPUTINFO info = default;
            info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info))
            {
                return;
            }
            uint idleMs = GetTickCount() - info.dwTime;
            if ((double)idleMs < IdleThreshold.TotalMilliseconds)
            {
                return;
            }
            release?.Invoke();
            TrimNow();
        }
        catch
        {
            // 后台整理失败不影响界面；下次空闲再试。
        }
    }
}

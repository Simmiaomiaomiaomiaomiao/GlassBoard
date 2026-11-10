using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GlassBoard
{
    internal enum PerformanceMode { Balanced, Eco, Protection }
    internal enum GlassRenderMode { RichGlassSnapshot, LightweightGlass, Solid }

    /// <summary>Central resource policy. Thresholds are conservative starting points for 16 GB systems.</summary>
    internal sealed class AdaptivePerformance : IDisposable
    {
        private readonly PerformanceCounter cpu;
        private PerformanceMode candidate = PerformanceMode.Balanced;
        private DateTime candidateSince = DateTime.MinValue;
        public PerformanceMode Mode { get; private set; } = PerformanceMode.Balanced;
        public GlassRenderMode RenderMode
        {
            get { return Mode == PerformanceMode.Protection ? GlassRenderMode.Solid :
                         Mode == PerformanceMode.Eco ? GlassRenderMode.LightweightGlass : GlassRenderMode.RichGlassSnapshot; }
        }
        public event Action<PerformanceMode> ModeChanged;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile;
            public ulong ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        public AdaptivePerformance()
        {
            try { cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total"); cpu.NextValue(); } catch { }
        }

        public void Sample(bool fullscreen, bool batterySaver)
        {
            if (fullscreen || batterySaver) { SetMode(PerformanceMode.Eco); return; }
            MEMORYSTATUSEX memory = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!GlobalMemoryStatusEx(ref memory)) return;
            double availableGb = memory.ullAvailPhys / 1073741824.0;
            PerformanceMode desired = availableGb < 1.5 ? PerformanceMode.Protection :
                                      availableGb < 2.5 ? PerformanceMode.Eco : PerformanceMode.Balanced;
            try { if (cpu != null && cpu.NextValue() > 85 && desired == PerformanceMode.Balanced) desired = PerformanceMode.Eco; } catch { }
            if (desired == Mode) { candidate = desired; candidateSince = DateTime.MinValue; return; }
            if (candidate != desired) { candidate = desired; candidateSince = DateTime.UtcNow; return; }
            if (candidateSince == DateTime.MinValue) candidateSince = DateTime.UtcNow;
            double hold = desired == PerformanceMode.Balanced ? 30.0 : 10.0;
            if ((DateTime.UtcNow - candidateSince).TotalSeconds >= hold) SetMode(desired);
        }

        private void SetMode(PerformanceMode value)
        {
            if (value == Mode) return;
            Mode = value; candidate = value; candidateSince = DateTime.MinValue;
            Action<PerformanceMode> handler = ModeChanged;
            if (handler != null) handler(value);
        }

        public void Dispose() { if (cpu != null) cpu.Dispose(); }
    }
}
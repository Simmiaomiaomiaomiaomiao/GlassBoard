using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace GlassBoard
{
    internal enum PerformanceMode { Balanced, Eco, Protection }
    internal enum GlassRenderMode { RichGlassSnapshot, LightweightGlass, Solid }

    /// <summary>Central resource policy. Thresholds are conservative starting points for 16 GB systems.</summary>
    internal sealed class AdaptivePerformance : IDisposable
    {
        private readonly PerformanceCounter cpu;
        private readonly List<PerformanceCounter> gpuCounters = new List<PerformanceCounter>();
        private DateTime nextGpuRefresh = DateTime.MinValue;
        public double LastGpu3DPercent { get; private set; } = -1.0;
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
            double cpuLoad = -1.0;
            try { if (cpu != null) cpuLoad = cpu.NextValue(); } catch { }
            double gpuLoad = ReadGpu3DUsage();
            if ((cpuLoad >= 85.0 || gpuLoad >= 85.0) && desired == PerformanceMode.Balanced)
                desired = PerformanceMode.Eco;
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

        private double ReadGpu3DUsage()
        {
            if (DateTime.UtcNow >= nextGpuRefresh)
            {
                nextGpuRefresh = DateTime.UtcNow.AddSeconds(30);
                foreach (PerformanceCounter counter in gpuCounters) counter.Dispose();
                gpuCounters.Clear();
                try
                {
                    if (PerformanceCounterCategory.Exists("GPU Engine"))
                    {
                        PerformanceCounterCategory category = new PerformanceCounterCategory("GPU Engine");
                        foreach (string instance in category.GetInstanceNames().Where(x => x.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0).Take(48))
                        {
                            try { gpuCounters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true)); }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            if (gpuCounters.Count == 0) { LastGpu3DPercent = -1.0; return -1.0; }
            double total = 0.0;
            int sampled = 0;
            foreach (PerformanceCounter counter in gpuCounters)
            {
                try { total += Math.Max(0.0, counter.NextValue()); sampled++; }
                catch { }
            }
            LastGpu3DPercent = sampled == 0 ? -1.0 : Math.Min(100.0, total);
            return LastGpu3DPercent;
        }

        public void Dispose()
        {
            if (cpu != null) cpu.Dispose();
            foreach (PerformanceCounter counter in gpuCounters) counter.Dispose();
            gpuCounters.Clear();
        }
    }
}
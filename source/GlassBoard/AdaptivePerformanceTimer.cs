using System;
using System.Windows.Threading;

namespace GlassBoard
{
    /// <summary>One shared sampler for the process; do not create one instance per window.</summary>
    internal sealed class AdaptivePerformanceTimer : IDisposable
    {
        private readonly AdaptivePerformance policy = new AdaptivePerformance();
        private readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);
        public AdaptivePerformance Policy { get { return policy; } }
        public AdaptivePerformanceTimer()
        {
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += delegate { policy.Sample(FullscreenState.Active(), false); };
            timer.Start();
        }
        public void Dispose() { timer.Stop(); policy.Dispose(); }
    }
}
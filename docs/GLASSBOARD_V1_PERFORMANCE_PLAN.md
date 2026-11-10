# GlassBoard v1.0 performance and UI foundation

## Scope
This branch is an engineering foundation, not a claim that the full concept mockup is already implemented. It keeps WPF/.NET Framework 4.8 and adds a central performance policy, Per-Monitor DPI manifest, and Windows installer pipeline.

## Source audit
- Main UI is constructed mostly in C# code-behind rather than XAML pages.
- GlassWindow.Backdrop captures a screen region with CopyFromScreen, downsamples it and applies WPF BlurEffect. This is a snapshot effect, not live DWM Acrylic.
- DockWindow has a 50 ms auto-hide timer and a 3-second running-window timer; hidden scans are already throttled to 10 seconds.
- BoardWindow already releases its hidden view after a delay.
- BoundedCache already provides capacity-bounded LRU caching.
- IdleTrim forces GC and trims the working set; actual performance impact must be measured.

## Adaptive policy
- 16 GB class defaults: Eco candidate below 2.5 GB available physical RAM; Protection candidate below 1.5 GB.
- Fullscreen switches to Eco. A dwell period prevents rapid oscillation.
- AdaptivePerformance is a shared policy primitive. Wire it into Controller lifetime before enabling per-window rendering changes; do not create one monitor per window.

## Visual acceptance
- Preserve recognizable app icons and crisp text.
- Keep translucency behind content only; avoid full-surface blur.
- Separate discovery/search from app management actions.
- Widgets are optional and must be independently pausable.
- Settings should group appearance, behavior, workspaces and diagnostics.
- Never ship hard-coded mock weather, time or music data.

## Validation
- Build and run --self-test on Windows.
- Test 100%, 125%, 150% and mixed-DPI dual monitors.
- Test Board open/close loops and a 30-minute soak.
- Record Private Bytes, Working Set, CPU time, GPU shared/dedicated memory.
- Confirm uninstaller shell restoration and preservation of LocalAppData user settings.
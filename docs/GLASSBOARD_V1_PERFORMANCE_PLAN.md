# GlassBoard v1.0 direction — performance foundation preview

## Current deliverable
This branch builds a preview based on the existing GlassBoard v0.9.17 codebase. It is not yet a completed visual 1.0 redesign. The application remains C# / WPF / .NET Framework 4.8 and keeps the existing Dock, app board, workspace, widget and backup features.

## Source audit findings
- The main UI is built mostly in C# code rather than separate XAML pages.
- GlassWindow.Backdrop uses CopyFromScreen, downsamples the captured area and applies WPF BlurEffect. That is a snapshot effect, not live DWM Acrylic.
- DockWindow has a 50 ms auto-hide timer and a 3-second running-window timer; running-window scans are already throttled to 10 seconds while hidden.
- BoardWindow already releases its hidden view after a delay.
- BoundedCache already supplies capacity-bounded LRU caching.
- IdleTrim explicitly collects and trims the working set. This affects Working Set reporting but is not proof that private committed memory has fallen.

## Implemented in this branch
- One shared AdaptivePerformance manager owned by Controller.
- Three coordinated render modes: RichGlassSnapshot (existing screenshot + blur material), LightweightGlass (no desktop capture and no WPF blur), and Solid (solid surface, no backdrop blur/shadow).
- Adaptive policy samples available physical memory, system CPU and (when Windows GPU Engine counters exist) aggregate 3D engine utilization. Fullscreen triggers Eco. The GPU counter is optional; unsupported or blocked counters fall back without stopping the app.
- Mode transitions use dwell periods to avoid rapid oscillation.
- Board header has direct Workspaces and Widgets entry points and a live adaptive material status indicator.
- Per-Monitor DPI manifest declaration and a Windows x64 build/installer pipeline.
- Per-user setup location and uninstall cleanup of the application's scheduled task / Run startup entry plus shell visibility restoration.

## Adaptive thresholds (initial values, not guarantees)
- Available physical RAM below 2.5 GB: Eco candidate.
- Available physical RAM below 1.5 GB: Protection candidate.
- CPU or GPU 3D utilization above 85%: Eco candidate when otherwise Balanced.
- Fullscreen: immediate Eco.
- Returning to Balanced waits 30 seconds; downgrading waits 10 seconds.
These values must be calibrated on real 8 GB / 16 GB / 32 GB systems and with design software/game workloads.

## What is not yet implemented or certified
- Dedicated/shared GPU memory telemetry.
- A true DWM live Acrylic/Mica material implementation. Balanced mode intentionally retains the existing snapshot-based glass until a native backdrop adapter can be implemented and validated.
- A new complete Settings / Widgets visual redesign matching every element of the concept image.
- Verified per-monitor mixed-DPI positioning across 100%, 125%, 150% and multi-monitor layouts.
- Empirical proof that total Private Bytes stays in the 250–400 MB target under the user's own app count and widgets.
- A 30–60 minute soak test on physical hardware.

## Required acceptance tests
1. Windows self-test must pass.
2. Install, launch, update, uninstall and verify startup cleanup.
3. Test 100%, 125%, 150% scaling plus mixed-DPI multi-monitor.
4. Open/close Board repeatedly and test 30-minute idle soak.
5. Record Private Bytes, Working Set, CPU time, GPU 3D utilization, GPU dedicated/shared memory, and frame/interaction responsiveness.
6. Confirm LocalAppData user configuration is preserved during ordinary upgrades and removed only when the user explicitly deletes it.

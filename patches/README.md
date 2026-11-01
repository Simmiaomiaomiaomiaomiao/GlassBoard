# GlassBoard v0.9.17 游戏模式判定补丁

> **完整交接文档见 `D:\GlassBoard\HANDOFF.md`**（项目背景、补丁链、重建流程、验证清单、踩坑记录）。本文件只记录补丁技术细节。

## 现状

- `D:\GlassBoard\GlassBoard.exe` = **源码构建版**（sha256 `b82d776f…`，2026-10-31，已删除创作者支持），当前在用。
- 补丁链：原版 → patch5 → patch6 → patch7b → patch9b → patch10 → patch11 → patch12 → patch13 → patch14（详见 `HANDOFF.md` 第 4、5 节）。patch14 是源码构建的基线，**不含** 5.2 的内存优化和 5.3 的创作者删除。
- 反编译源码快照：`patches\decompiled-src-20261031.zip`（152 个 `.cs`，解压后可直接 `dotnet build`）。
- 备份：`backups\GlassBoard_v0.9.17_original.exe`（官方原版，`ad0e504f…`）；`backups\GlassBoard_v0.9.17_preCreatorRemoval.exe`（上一版源码构建，`cbb9e888…`）；`backups\GlassBoard_v0.9.17_patch14.exe`（IL 补丁版，`1705d025…`）；`backups\GlassBoard_v0.9.17_patch13.exe`（`afa15662…`）。
- `patches\GlassBoard_v0.9.17_fixed.exe` 是**第一版补丁，已废弃**（会让独占全屏游戏的 Dock 弹出来，别用）。

## patch5 改了三处

**1. 全屏判定改用屏幕中心探针**、**2. 排除带标题栏的窗口** —— 见下一节（与 patch4 相同）。

**3. Dock 滚轮切换灵敏度**（`DockWheelState.Scroll`）

原逻辑：`if (now - last < 280ms) { accumulated = 0; return false; }`，切换后 280 毫秒内的滚轮事件全部丢弃**并清零累积**，快速滚动时大部分格子不生效，表现为"滚很多下才动一次"。

改动：节流 280ms → **100ms**，且节流期间**不再清零累积**（只返回 false）。快速滚动时累积值保留，节流一过立即生效。

注意：切换本身要重新扫描运行窗口并重建整条 Dock，节流不能完全去掉，否则连续滚动会反复重建导致卡顿。如果 100ms 手感仍不好可以再调（改 `patch5.cs` 里的 100 重新生成）。

## patch4 的两个改动（已含在 patch5 里）

**1. 全屏判定改用屏幕中心探针**（修"Win+D 后组件误弹"）

`FullscreenState.Active()` 开头：

```
hwnd = GetAncestor(WindowFromPoint(((cy/2)<<32)|(cx/2)), GA_ROOT)
```

原版用 `GetForegroundWindow()`；Win+D 时焦点会被 GlassBoard 自己的 Dock 抢走，原版看到"焦点是自己"就直接判"不在全屏"，于是 Dock 和组件弹到游戏画面上。

**2. 排除带标题栏的窗口**（修"最大化窗口导致 Dock 消失"）

在类名过滤之后、矩形比较之前插入：

```
if ((GetWindowLongW(hwnd, GWL_STYLE) & WS_CAPTION) != 0) return false;
```

原因：最大化普通窗口时 `GetWindowRect` 会带 7px 不可见边框（实测记事本最大化 = `-7,-7,1714,1074`，屏幕 1707×1067），四条边全部"覆盖"显示器，被误判成全屏。实测原版和 patch3 在记事本最大化时 `Active()=True`、Dock 直接消失。无边框全屏游戏没有标题栏，不受影响。

新增 4 个 P/Invoke（都在 `FullscreenState` 内）：`GetSystemMetrics`、`WindowFromPoint(long 打包 POINT)`、`GetAncestor`、`GetWindowLongW`。可见性全部 private，不改动程序其它类。

## 验证（全部实测）

| 场景 | 原版 | patch4 |
| --- | --- | --- |
| 全屏窗口 + 焦点被抢（Win+D 场景） | False → 弹组件 | **True → 不弹** |
| 记事本最大化 | True → Dock 消失 | **False → Dock 正常** |
| 无边框全屏窗口 | True | True |
| 没有全屏窗口 / 普通非最大化窗口 | False | False |
| 端到端：最大化→全屏→抢焦点→关闭 | 误判 | 1/True、2/False、3/False、4/True 全对 |

启动日志完整，无 .NET 异常事件。

## 已知但尚未修的问题（审查发现，风险从高到低）

1. **副屏全屏不生效**：探针固定用主屏中心（`GetSystemMetrics` 只描述主显示器）。游戏在副屏全屏时会退回原 bug。当前是单屏，暂不影响；将来接副屏要改成逐显示器探针。
2. **屏幕中心被置顶小窗遮挡**：输入法候选窗、通知气泡、或把 Dock 拖到屏幕中心时，探针命中这些小窗 → 判定非全屏。游戏模式下 Dock/组件本来隐藏，日常风险低。
3. **独占全屏（DirectX 隐藏渲染窗口）**：`WindowFromPoint` 会跳过不可见窗口，可能返回桌面 → 判定非全屏。需要真独占游戏实测。
4. **无滞回**：350ms 单次采样，状态翻转就重建整个 Dock/组件，理论上会出现闪一下；实际未观察到。
5. **手动 + 自动同时开启**：`GameModeRules.Suppressed = manual || (auto && fullscreen)`，手动开启时自动探测完全无效（这是用户实际踩到的第一个坑）。建议 UI 上做成互斥。

## 回滚

```
copy D:\GlassBoard\backups\GlassBoard_v0.9.17_original.exe D:\GlassBoard\GlassBoard.exe
```

先退出程序再替换。回滚后 `SHA256SUMS.txt` 重新匹配。

## 注意

- 在线"设置 → 通用 → 立即检查 → 下载并更新"会覆盖本补丁。
- 打补丁脚本 `patch4.cs` 现在会严格校验目标方法（第一条必须是 `call FullscreenState::GetForegroundWindow`、第二条 `stloc.0`），不匹配直接报错退出，避免打错版本产生非法 IL。

## 【重要】Win+D 兜底组件 windfix.exe

**为什么需要它**：实测《异环》（HTGame.exe / UnrealWindow）这类 UE 游戏，窗口是 `WS_POPUP` 全屏无标题栏。此时 Windows 自己的 Win+D（以及发给 Shell_TrayWnd 的显示桌面命令）对它**完全无效**——游戏画面不消失，前台一直是游戏。但 `ShowWindow(hwnd, SW_MINIMIZE)` 对它是**有效的**（实测 iconic 0→1）。也就是说游戏拒绝的是"shell 的显示桌面"，不是"被最小化"。

**它做什么**：`D:\GlassBoard\windfix.exe` 常驻后台，安装一个低级键盘钩子（WH_KEYBOARD_LL），只在检测到 **Win+D / Win+R+D** 组合时，把当前前台窗口 `ShowWindow(SW_MINIMIZE)`。游戏被最小化后，GlassBoard 的定时器在 0.35 秒内检测到前台变成桌面，自动退出游戏模式并恢复 Dock/组件。

**它不是热键工具**：没有注册任何新的快捷键，没有占用任何组合键，只监听系统原有的 Win+D 并在系统失效时补一刀。

**实测结果（异环）**：

```
异环到前台                       → Dock/控制中心隐藏（进入游戏模式）
按 Win+D                         → 游戏 iconic=1（被最小化），前台变 Progman
0.6 秒内                         → Dock、控制中心、时钟天气全部恢复
windfix 日志：Win+D -> minimized 197562 class=UnrealWindow
```

**自启**：已注册计划任务 `GlassBoard-WinDFix`（登录时启动，State=Ready）。

**风险与边界**：
- 钩子里只认 D 键 + Win 按下状态，其它键一律透传，不拦截。
- 400ms 去抖，连续按不会重复最小化。
- 如果某个游戏把 Win 键整个吞掉（连低级钩子都收不到），这个兜底也无能为力——那种情况只能把游戏改成窗口模式。
- 想停用：`schtasks /Delete /TN GlassBoard-WinDFix /F` 并结束 windfix 进程。

## 【patch9/10】去水印、删支持创作者页、游戏模式省内存

- **水印**：`BoardWindow.Build`（面板底部「抖音 森霖Sama 制作」）、`BoardWindow.MoreMenu`、`Controller.ShowCreatorImage`、`Controller.BuildCreatorSupport` 四处界面文字清空。**更新用的 9 个 `SENLIN-SAMA/win-.-` URL 必须保留**（批量按 "Sama" 清会连更新地址一起清掉，检查更新会坏）。
- **支持创作者 Tab**：在 `Controller.Settings` 里把 `creatorPage = makeTab("支持创作者")` 的调用换成 `null`，Tab 根本不会创建；同时把恢复上次选中页的 `Math.Min(5, …)` 改成 4（注意 `Settings` 开头另有一处 `Min(5, …)` 是窗口尺寸计算，**不能改**）。验证：设置页只剩 5 个 Tab。
- **游戏模式省内存**：`ApplyGameMode` 进入游戏模式（`Board.Hide()` 之后）调用 `SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1)`，让系统回收物理页。实测工作集 **123MB → 33MB**，退出游戏模式和所有窗口恢复正常。
- 性能测量结论：`FullscreenState.Active()` 平均 **0.05ms**、`RunningWindows.Scan` 平均 **0.23ms**，都不是卡顿来源；UI 卡顿来自 WPF 渲染效果（玻璃模糊 BlurRadius、悬停放大动效、145 个图标的位图解码），要降只能调这些观感参数。

## 【patch11】桌面层窗口的 z-order 修正

**现象**：主屏开着窗口化的小窗（游戏或普通程序）时，点一下窗口外的桌面，Dock、控制中心、组件面板（以及打开着的大面板）会立刻平移到最上层，盖住那个小窗。

**根因**：`GlassBoard.DesktopPersistence.RefreshOrder` 在前台窗口变化（`EVENT_SYSTEM_FOREGROUND` / `EVENT_OBJECT_REORDER`）时重排所有已"桌面化"的窗口。原逻辑是：

```
hWndInsertAfter = Desktop(foreground) ? HWND_TOP : HWND_BOTTOM
```

前台是桌面（Progman / WorkerW / ShellWindow）时用 `HWND_TOP`，而 `HWND_TOP` 是**所有非 topmost 窗口的最顶层**，不是"桌面层内最顶"。于是点桌面 → 前台变 Progman → 玻璃窗口全部被提到普通窗口之上。

**修复**（`patch11.cs`）：把该分支的 `HWND_TOP` 常量换成 `HWND_BOTTOM`，与另一支一致。窗口 owner 本来就是 shell window，压在底部仍位于桌面之上，不会沉到壁纸下面。

IL 层面是替换 `SetWindowPos` 前那条 `ldsfld System.IntPtr::Zero`，改成 `ldc.i4.1; newobj IntPtr::.ctor`，并把 `brtrue.s` 的分支目标重定向到新指令。

**验证（实测）**：

| 时刻 | 前台 | GlassBoard 三窗口 z-order 位次 |
| --- | --- | --- |
| 修复前，点击桌面 | Progman | 5–7（在 ZCode 之上，压住小窗） |
| 修复后，点击桌面 | Progman | 12–14（在 ZCode 之下，与点击前一致） |

组件在桌面上照常可见；启动日志 START / LOAD / WIDGET SYNC / WIDGET SHOW 齐全；近 10 分钟无 GlassBoard 相关事件日志。

**已知边界**：`DesktopPersistence` 每 100ms 用定时器合并重排，前台快速来回切时窗口会重新 Own 一次（原本就这样），未观察到闪烁。

## 【patch12】逐显示器全屏探针

**问题**：全屏探针固定取主屏中心（`GetSystemMetrics` 只描述主显示器）。判定矩形本身用 `MonitorFromWindow` + `GetMonitorInfo` 跟随窗口所在显示器，所以只有探针点是单屏的——副屏全屏游戏时取不到窗口，游戏模式不触发。

**修复**：重写 `FullscreenState.Active()` 方法体：
- `Screen.AllScreens` 遍历每个显示器，取 `Bounds` 中心；
- 打包 `POINT` 为 `((long)cy << 32) | (uint)cx`（`cx` 先 `conv.u4` 再 `conv.u8`，副屏 X 为负时也正确）；
- `GetAncestor(WindowFromPoint(...), GA_ROOT)` 取窗口，其余过滤（pid / 类名黑名单 / `WS_CAPTION`）、`GetWindowRect` + `GetMonitorInfo` 覆盖判定、前台是桌面时最小化的兜底逻辑原样保留。

**两个坑**：
1. `RECT` / `MONITORINFO` 在程序里有同名嵌套类型（例如 `TaskbarAutoHide+RECT`），必须从 `Active` 的局部变量类型里取，按名字全局找会拿到别的私有类型，运行时报 `FieldAccessException`。
2. pid 过滤是"等于本进程就跳过"，IL 必须用 `beq`；写成 `bne.un` 会跳过所有其它进程窗口，`Active()` 永远返回 false。

**验证**：反射调用时全屏窗口存在 → `True`，不存在 → `False`；真实运行下全屏出现三窗口隐藏、工作集 123MB→23MB，关闭后 1 秒内恢复。

## 【patch13】游戏模式开关互斥提示

在 `Controller.BuildGameSettings` 末尾的说明文本追加：
「提示：手动开关与自动检测请二选一，同时开启时手动开关会让 Dock 一直隐藏。」

原因：`GameModeRules.Suppressed = manual || (auto && fullscreen)`，两个开关同时打开时手动开关会让 Dock 永久隐藏。只替换一条 `ldstr`，无结构改动。验证：设置 → 通用 Tab 的 UIA 文本中已出现该句。

## 【patch14】天气卡片排版

`WidgetWindow.Weather` 三处调整：

1. **删除右侧天气符号**：温度行原来的两列（自动 + 60px）中，第二列是 `weatherIcon`（云/雾/雨/雪/雷的矢量符号）。删掉 60px 列定义，并让 `weatherIcon` 不再加入 Grid——对象仍创建、`UpdateWeather` 仍更新它，只是不显示。
2. **按钮并到状态行**：底部 Grid 两列都设为 `Auto`，`wttr.in · 更新于 xx:xx` 放第 0 列，刷新/天气来源按钮行放第 1 列、垂直居中、左侧留 5px——按钮紧跟状态文本，中间约一个空格。
3. **按钮缩小**：刷新 45→34 宽、字号 12→9、高 28→22；天气来源 88→62 宽、字号 10→9、高 28→22。

验证：组件窗口高度 423→385；UIA 中状态文本 x=49..181，刷新按钮 x=194、天气来源 x=247，同一行紧凑排列；截图中符号已消失。

> 踩坑：`Weather` 的局部变量用 `stloc.2` / `stloc.3` 短路存储，Cecil 里操作数为 null，不能按 `Stloc + VariableDefinition` 匹配。

## 工具

- `fgtrace.exe`（D:\download\gbpatch）= 前台窗口状态记录器，日志写到 `patches\fg_trace.log`。
- `patch4.cs` / `ildump.cs` / `Mono.Cecil.dll`（D:\download\gbpatch）= 打补丁与读 IL 的源码。

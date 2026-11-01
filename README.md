# GlassBoard v0.9.17

## 安装
 1. 将 `GlassBoard_v0.9.17_User.zip` 完整解压到一个新文件夹，运行其中的 `GlassBoard.exe` 或 `Start.cmd`。不要只复制 EXE。
   
 2. 如果不会手动安装也可以复制项目地址https://github.com/xinxiao1011/GlassBoard
  让agent安装。

## 发布文件

- `User.zip`：用户完整运行包。
- `Source.zip`：对应源码和构建／验证记录。
- `update.json`：与本版用户包对应，发布时上传用户包并替换仓库配套索引

## 从源码构建

源码在 `source/` 目录（ILSpy 反编译重建，152 个 `.cs`），需要 .NET SDK 8+：

```bat
cd source
dotnet build GlassBoard.csproj -c Release
```

产物为 `source\bin\Release\net48\GlassBoard.exe`。替换运行时，把该 exe 覆盖到仓库根目录的 `GlassBoard.exe` 即可；不要只复制 exe 到空文件夹——程序还需要同目录的 `Interop.UIAutomationClient.dll`、`data\`、`lib\`、`GlassBoard.Updater.exe` 等文件。`source\bin` 与 `source\obj` 不纳入版本控制。

## 授权

本项目（反编译重建的源码、IL 补丁、维护文档与构建产物）按 **GNU General Public License v3.0** 发布，许可证全文见 [LICENSE](LICENSE)。

第三方组件保持各自许可：NPSMLib（MIT，© 2021 ADeltaX）与 now-playing-service（MIT，© 2024 Widdit），许可证原文见 `licenses/`，与本项目的 GPL 授权并行有效。完整声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

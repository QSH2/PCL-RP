# PCL 下载限制补丁工具（PCL-CE-RegionPatch）

> **独立的第三方工具**，与 PCL-Community、龙腾猫跃（LTCat）无隶属、无授权、无背书关系。
> 请勿向官方仓库反馈由本工具产生的问题。
>
> 制作人：**清水Shimizu** · GitHub [@QSH36](https://github.com/QSH36) · 邮箱 shimizu-sk@qq.com · 如有侵权请联系删除

**本仓库不包含任何 PCL 的源代码、二进制或密钥，也不提供任何成品下载。** 它只提供本地工具
（命令行版与 Windows 图形界面版）：在**你自己的电脑上**，对**你自己合法获取**的官方 exe
做受控修改，移除「中国大陆地区不可用」的判断。

上游致谢：

- **Plain Craft Launcher 2 (PCL)** · 作者 **龙腾猫跃 (LTCat)** · [仓库](https://github.com/Meloong-Git/PCL) · [赞助链接](https://afdian.com/a/LTCat)
- **PCL Community Edition (PCL CE)** · 维护 **PCL-Community** · [仓库](https://github.com/PCL-Community/PCL-CE)

---

## 支持范围

| 产品 | 已测试版本 | 恢复内容 | 状态 |
| --- | --- | --- | --- |
| **PCL CE（社区版，.NET 单文件）** | 2.15.1 x64 | 解除中国大陆地区限制：MC 下载、安装、补全、重置、整合包安装、启动时补全 | ✅ 完整恢复 |
| **PCL 官方版（.NET Framework）** | 2.99.0.0 | 下载页的 **Minecraft 版本列表**（可正常下载/安装原版与加载器）、实例修改页的 Minecraft 卡片 | ✅ 已恢复 |
| PCL 官方版 · 整合包安装入口 | 2.99.0.0 | 整合包在线安装 | ⚠️ 暂不恢复（见下方说明） |

工具会自动识别输入的是哪种产品，并应用对应的补丁。

## 背景

- **PCL CE**：2026-10-20 发布的 v2.15.1 通过 #3673 / #3676 在中国大陆地区禁用了 Minecraft 的
  下载、安装、补全与整合包安装。上游仓库中有一个**尚未合并**的预留回滚 PR #3677。
- **PCL 官方版**：2026-10-20 发布的 2.99.0.0 在更新说明中表示「为遵守相关法律法规，PCL 不再
  提供 MC 下载功能」。其实现方式是新增编译期常量 `ModSecret.RestrictMcDownload = True`，
  并在下载页、整合包安装等入口加上判断。由于是**编译期常量**，发布版中「未受限」的分支已被
  编译器剪除，因此不能靠改一个开关恢复，需要重建相应入口的方法体。

## 图形界面版（GUI，推荐给不想用命令行的用户）

![GUI 截图](docs/screenshot.png)

`src/PclCeRegionPatch.Gui` 提供 Windows 图形界面（WinForms / .NET 10）：

- 选择或**拖拽**官方 exe → 自动识别产品（PCL CE / PCL 官方版）、版本、**限制状态**；
  状态行会按产品区分：PCL CE 显示「地区限制状态」，PCL 官方版显示「下载功能状态」；
- 一键生成 `.patched.exe`，可自定义输出路径、打补丁后直接打开输出位置；
- 打补丁后的状态：

![打补丁成功](docs/screenshot-success.png)

从 Release 下载 `pcl-ce-regionpatch-gui.exe`，或自行构建：

```powershell
dotnet publish src/PclCeRegionPatch.Gui/PclCeRegionPatch.Gui.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o dist
```

> 默认是**框架依赖**单文件（约 0.8 MB），需要 .NET 10 Desktop Runtime（PCL CE 本身也依赖它，
> 通常已安装）。如果要在未安装 .NET 10 的电脑上使用，把 `--self-contained false` 换成
> `--self-contained true`（体积会大很多）。

## 命令行版

```powershell
# 构建
dotnet build PclCeRegionPatch.slnx -c Release

# 用法（PCL CE 与 PCL 官方版相同）
pcl-ce-regionpatch PCL2_CE_Release_x64.exe            # PCL CE
pcl-ce-regionpatch "Plain Craft Launcher 2.exe"       # PCL 官方版
pcl-ce-regionpatch --check <输入.exe>                  # 仅检查是否已打补丁（0=已打）
pcl-ce-regionpatch -o <输出.exe> <输入.exe>            # 指定输出路径
```

PCL 官方版补丁的实际输出示例：

```text
input     : ...\Plain Craft Launcher 2.exe (5,980,160 bytes)
target    : PCL 官方版（.NET Framework） 2.99.0.0
info      : 目标程序集：Plain Craft Launcher 2 2.99.0.0
info      : 限制方式：编译期常量 ModSecret.RestrictMcDownload = True
info      : 可恢复入口：下载页 Minecraft 版本列表、实例修改页的 Minecraft 卡片
output    : ...\Plain Craft Launcher 2.patched.exe (5,975,040 bytes)
step      : 从 PCL.Resources 中提取 14 个内嵌依赖程序集用于元数据解析（仅临时目录，完成后删除）
step      : 重建 PageDownloadInstall.LoaderInit：改为加载真实的 Minecraft 版本列表（DlClientListLoader）
step      : 恢复 PageDownloadModify.LoadInstanceFinished 中 Minecraft 卡片的可见性（1 处）
verify    : LoaderInit：已使用 DlClientListLoader 加载真实的 Minecraft 版本列表
verify    : LoadInstanceFinished：Minecraft 卡片可见性 = Visible
```

## 原理

### PCL CE（.NET 单文件）

PCL CE 使用 .NET 单文件发布：exe = apphost PE + 追加的 bundle，主程序集
`Plain Craft Launcher 2.dll` 内嵌其中。工具用元数据字符串定位该程序集（校验 PE、要求 IL-only），
再把 `PCL.RestrictedFeaturePolicy` 的两个方法体原地改成 3 字节的 tiny IL：

- `Check(string, bool)` → `ldc.i4.1; ret`（永远放行）
- `get_IsRestricted()` → `ldc.i4.0; ret`（永远不受限）

文件大小不变，仅 6 字节差异；官方内嵌密钥保持原样。

### PCL 官方版（.NET Framework）

1. 识别 `ModSecret.RestrictMcDownload` 编译期常量，确认是受限的官方版本；
2. 从启动器自身的 `PCL.Resources` 中临时提取 14 个内嵌依赖程序集（PCLCS、MeloongCore 等），
   供 Mono.Cecil 解析元数据（处理完立即删除）；
3. 重建 `PageDownloadInstall.LoaderInit`：改为调用真实的 `DlClientListLoader` 加载版本列表，
   并注册 `LoadMinecraft_OnFinish` 回调；
4. 修正 `PageDownloadModify.LoadInstanceFinished` 中 Minecraft 卡片的可见性常量；
5. 用 Cecil 重写程序集并做 IL 校验。输出文件大小会略有变化，属正常现象。

> 为什么整合包安装不恢复：上游用编译期常量把相关代码整体剪掉了（例如 `ModpackInstall`
> 分发逻辑在 Release 中已不存在），不是简单的开关，重建这部分等于重写功能，超出补丁工具范围。
> 下载页的 Mod / 整合包 / 资源包等浏览功能不受影响，只是 PCL 内不再提供「一键安装整合包」。

## 已验证

- **PCL CE 2.15.1**：补丁输出与 2.15.1 的预期结果逐字节一致；GUI 与 CLI 输出 SHA256 相同。
- **PCL 官方版 2.99.0.0**：GUI/CLI 打补丁后启动正常，下载页成功加载出在线版本列表
  （26.3、26.4-snapshot-5 及正式版/预览版分类），见下图：

![PCL 官方版补丁后下载页](docs/official-download-page.png)

- GUI 全流程已用自动化测试：载入官方 exe → 点击打补丁 → 输出文件验证通过。

## 已知限制

- **PCL 官方版只测试了 2.99.0.0**；如果后续版本改了类型/方法结构，工具会明确报错而不是产出坏文件，
  需要更新适配。
- **PCL 官方版的整合包安装入口暂不恢复**（原因见上）。
- PCL 官方版补丁会重写程序集，输出文件大小与签名（sigstore）都会变化，属预期行为。
- PCL CE 只适用于内嵌主程序集为 IL-only 的单文件构建。
- 修改后的文件属于上游软件的修改副本，**请勿再分发**。

## 安全提示

- 只从官方 Release / 官方渠道获取原始 exe；
- 不要使用、也不要传播任何第三方提供的「免限制版 / 破解版」exe，它们无法验证是否被植入内容；
- 不要把你处理后的文件或官方 exe 提交到本仓库的 Issue / PR 中。

## 环境要求

- Windows 10 1809 或更高
- 官方 PCL CE **单文件** exe，或官方 PCL 官方版 exe（.NET Framework 4.8）
- .NET 10 SDK（构建工具时需要）
- .NET 10 Runtime（运行命令行版）/ .NET 10 Desktop Runtime（运行 GUI 版）

## 仓库结构

```text
src/PclCeRegionPatch.Core/   # 核心库：PCL CE IL 补丁 + PCL 官方版补丁
src/PclCeRegionPatch/        # 命令行版（跨平台，net10.0）
src/PclCeRegionPatch.Gui/    # Windows 图形界面版（WinForms，net10.0-windows，含 app.ico）
scripts/                     # 杂项脚本（make-icon.ps1：生成多尺寸应用图标）
docs/                        # 截图与图标预览
```

## 许可与责任

- 本仓库的**工具源代码**使用 [MIT License](LICENSE)。
- 本仓库**不包含** PCL / PCL CE 的任何代码或二进制；仅在互操作意义上引用公开可见的命名空间与
  类型名。PCL 与 PCL CE 的版权归其各自权利人所有。
- 上游《PCL 分发有限许可》只授权分发「原样副本」，因此**修改后的二进制不在授权范围内**，
  请不要分发本工具的输出文件。
- 本工具与上游无隶属关系；使用本工具修改软件、以及之后如何使用修改后的软件，均由你自行负责，
  并应遵守当地法律法规与上游许可。

## 制作人

- 制作人：**清水Shimizu**
- GitHub：[@QSH36](https://github.com/QSH36)
- 联系邮箱：shimizu-sk@qq.com
- 如有侵权请联系删除（同上邮箱）。

## 更新日志

- **v1.2.2**：去掉图标上的绿色对勾徽章，改为扁平的蓝色下载图标（无渐变、无徽章，
  16–256 多尺寸），观感更接近系统原生。
- **v1.2.1**：重绘应用图标（蓝色下载箭头 + 绿色对勾）；状态文案按产品区分：PCL CE 显示
  「地区限制状态」，PCL 官方版显示「下载功能状态」（官方版并非按地区限制，而是整体移除下载功能）。
- **v1.2.0**：新增 PCL 官方版 2.99.0.0 支持（重建下载页版本列表加载、恢复修改页 MC 卡片）；
  GUI/CLI 自动识别产品并显示对应信息；新增示意图。
- **v1.1.0**：新增 WinForms GUI、应用图标、制作人署名。
- **v1.0.0**：PCL CE 2.15.1 补丁工具（命令行）。

## 免责声明

使用前请阅读 [DISCLAIMER.md](DISCLAIMER.md)。本项目按「现状」提供，不附带任何担保。

## 相关链接

- PCL 官方版 2.99.0.0 发布说明：<https://github.com/Meloong-Git/PCL/releases/tag/2.99.0.0>
- PCL CE：<https://github.com/PCL-Community/PCL-CE>
- 上游预留回滚 PR：#3677 <https://github.com/PCL-Community/PCL-CE/pull/3677>
- 限制引入的 PR：#3673 <https://github.com/PCL-Community/PCL-CE/pull/3673>、#3676 <https://github.com/PCL-Community/PCL-CE/pull/3676>
- 另一位作者的同类工具（思路参考）：<https://github.com/zhicheng233/PCL2-RegionPatch>

---

## English summary

An independent, third-party CLI + GUI tool that removes the mainland-China download restriction
from **user-owned** official PCL builds:

- **PCL CE** (community edition, .NET single-file): rewrites two tiny IL method bodies
  (`Check` → `ldc.i4.1; ret`, `get_IsRestricted` → `ldc.i4.0; ret`), changing 6 bytes in total.
- **PCL official** (.NET Framework, 2.99.0.0): upstream implemented the restriction as a
  compile-time constant, so the unrestricted code paths were removed by the compiler. This tool
  rebuilds `PageDownloadInstall.LoaderInit` (real version list loader) and restores the
  `PageDownloadModify` Minecraft card visibility. Modpack installation entry points are not
  restored (their code no longer exists in the release build).

This repository contains **no** PCL code, binaries or credentials, and provides **no** pre-patched
downloads. Not affiliated with PCL or PCL CE. Use only on copies you obtained legally; do not
redistribute patched binaries; you are responsible for complying with local laws and upstream
licenses.

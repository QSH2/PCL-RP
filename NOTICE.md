# NOTICE — 第三方项目与许可说明

本仓库（PCL-CE-RegionPatch）是一个**独立的第三方补丁工具**，不包含、不修改、也不重新分发任何
PCL / Plain Craft Launcher 2 / PCL CE 的源代码或二进制文件。本工具只在用户本机上，对用户
自行合法获取的官方 PCL CE 单文件构建做受控的二进制修改。

## 上游项目与署名

### Plain Craft Launcher 2 (PCL)

- 作者：**龙腾猫跃 (LTCat)**
- 仓库：https://github.com/Meloong-Git/PCL
- 赞助链接：https://afdian.com/a/LTCat
- 版权与许可：见上游仓库中的《PCL 分发有限许可》与《PCL 存储库合理使用指南》
  - 《PCL 分发有限许可》仅授予「原样副本」的存储、镜像、备份、上传、下载、向公众提供、
    传播和再分发权；「原样副本」指未经任何方式改变的软件副本。
  - 因此：**修改后的二进制（包括本工具的输出）不在上述授权范围内，不应被再分发。**

### PCL Community Edition (PCL CE)

- 维护：PCL-Community（与龙腾猫跃无从属关系）
- 仓库：https://github.com/PCL-Community/PCL-CE
- 许可：`Plain Craft Launcher 2/` 目录使用《PCL 分发有限许可》相关自定义许可；其余目录使用
  Apache License 2.0。详见上游仓库 README 与 LICENSE。

## 本工具的立场

1. 本工具**不含** PCL / PCL CE 的任何代码、二进制、资源或凭据（包括
   `MS_CLIENT_ID`、`CURSEFORGE_API_KEY` 等官方编译期注入的密钥）。
2. 本工具**不提供**任何修改后的成品下载；用户必须自行获取官方构建并自行处理。
3. 本工具仅在互操作意义上引用公开可见的命名空间/类型/方法名称
   （`PCL.RestrictedFeaturePolicy` 及其成员），用于定位需要修改的方法体。
4. 本工具与 PCL、PCL CE 的维护者无隶属、无授权、无背书关系；请勿向上述官方仓库反馈由本工具
   产生的问题。
5. 本工具的 README 首位给出 PCL 原作者龙腾猫跃的署名与赞助链接，以尊重上游
   《PCL 存储库合理使用指南》的署名精神。
6. 本工具没有 Minecraft 启动/下载/管理功能，因此不涉及上游「重度使用」条款中关于
   `McLaunchPrecheck` 正版购买劝导与赞助劝导的实现要求；也没有与 PCL 赞助解锁类似的界面表现。
7. PCL、Plain Craft Launcher 等名称及相关标识归其权利人所有。本仓库不以任何方式暗示由官方
   提供或与官方关联。

## 使用方法与责任

修改、运行修改后的软件属于使用者自己的行为。请在使用前确认你拥有该软件的合法副本、你的用途
符合当地法律法规，并遵守上游许可。详见 [DISCLAIMER.md](DISCLAIMER.md)。

# 铁路模拟驾驶 — 1.0.4 恢复工程

## 重要位置
- `Source/UnityProject`：普通文件夹形式的完整 Unity 源码。以此为权威源码，不依赖磁盘映像保存。
- `Delivery`：安装包、独立导入地图、源码 ZIP（完成构建验收后生成）。
- `Reports`：修改差异、检查结果、构建日志和文件哈希。
- `Baseline`：早期源码 ZIP 与对照脚本，保留原始内容。
- `Tools`：构建与恢复记录工具。
- `Work/BuildCache.sparseimage`：仅编译工具、APFS 工作副本和可再生成缓存，挂载名 RailRestoreWork。不是唯一源码。

## 工程来源与恢复范围
以 KINGSTON 的早期 iOS 源码 ZIP 为基线，合并保存的 ImportFix、VehicleFix、Fix103 和 StartupFix。
恢复文件选择器、完整驾驶/站停/交路逻辑、可调车辆性能、天气/画质、触控与警惕系统、手动存档、地图导入和管理。
恢复 1.0.3 的分区加载预算、菜单返回触控布局以及 1.0.4 的延后地图扫描和启动渲染配置。
保留原始驾驶室 FBX 和道路车辆 FBX，重新生成独立预制体、URP 材质、场景及体积光/云渲染配置。
手机版不内置地图。Delivery 中的 iOS .railmap 是已有地图的完整副本，不是重新命名的桌面地图。

## 打开与编译
使用 Unity 6000.6.4f1（Apple Silicon），安装 iOS Build Support。Xcode 26.3 及其设备 SDK 为本机已有工具。
在 Unity Hub 中选择 Source/UnityProject。建议将 Unity 的 Library/编译工作副本放在 APFS；KINGSTON 为 exFAT，所以本次自动构建在同目录映像的 APFS 工作区执行，再把所有实际源码和生成资源同步回普通文件夹。
自动构建入口为 Tools/build.sh。默认复用本次 Work 缓存中的 Editor；更换 Editor 时可设置 UNITY 环境变量。
只需阅读或继续修改代码，不需要挂载 Work 映像。缓存损坏/删除不会再使普通源码消失；重新安装对应 Editor 和 iOS 模块后可重建缓存。
独立地图中的城市几何为已编译分区资源；游戏源码不等于整套地图的原始 Blender 作者工程。

## 验收边界
以 Reports 中实际执行的检查与构建结果为准。没有连接手机执行本次安装和长距离驾驶，不保证历史黑屏/闪退已在真机完全消除。
安装新包优先用原应用标识覆盖签名安装，保留导入地图和设置；SideStore 负责重签名。

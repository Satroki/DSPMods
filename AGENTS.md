# AGENTS.md

本文件是给 AI 编码代理的仓库级规则，不是用户文档。README/CHANGELOG 的写作规则见下文。

## 仓库

- 这是《戴森球计划》BepInEx 模组集合，目标框架 `netstandard2.1`。
- 解决方案：`DSPMods.sln`。
- 游戏程序集与 BepInEx 引用统一在 `DSPMods.Shared/DSPMods.Shared.props`。
- 不修改游戏安装目录；构建产物部署到 r2modman profile。
- `SourceCode/` 是反编译输出，已被忽略，不要提交。

## 项目

| 项目 | 类型 |
| --- | --- |
| `AutoPlaceMiner/` | 大型采矿机自动摆放 |
| `AutoConnectPower/` | 自动连接电网 |
| `DSPMultiplyStackSize/` | 堆叠数量编辑（原作者 Appun） |
| `DSPTools/` | 储罐、成就、分辨率等工具 |
| `DSPMods.Shared/` | 共享构建配置，不单独发布 |

## 构建

```powershell
dotnet build .\DSPMods.sln -c Release
```

- 构建必须为 **0 警告、0 错误**。
- 修改共享引用或目标框架后，构建整个解决方案，不只构建单个项目。
- 构建后确认 DLL 已复制到 profile；部署路径由各 `csproj` 的 `DeployPluginDir` 控制。

## 版本与发布

- 本仓库自有模组的版本必须三处一致：
  1. `manifest.json` 的 `version_number`
  2. 插件代码中的 `PluginVersion`
  3. `CHANGELOG.md` 最新条目标题
- **未提交的连续改动视为同一个未发布版本**：只在提交/发布前确定一次版本号，不要在每一轮未提交修改中连续升 patch。
- 已提交或已发布后，才开始下一个版本；新功能升 minor，修复升 patch。
- 未发布的多个改动可以合并为一个版本条目。
- 上传 Thunderstore 时遵循 [Updating a Package](https://wiki.thunderstore.io/mods/updating-a-package)：
  - 不修改 `manifest.json` 的 `name`
  - 只提高 `version_number`
  - README/CHANGELOG 随包更新
- 发布版本的版本号不可覆盖；已上传后必须继续升版本。

## README

- 根目录 `README.md`：解决方案总览、模组列表、构建、目录结构。
- 各模组 `README.md`：只写用户可见的功能、安装、使用、配置和注意事项。
- 模组 README 不写算法、内部实现、线程/物理细节或性能实现；这些放在 `DEVELOPMENT.md`。
- 自有模组的 README 保持中英双语；中文在前，英文在后。
- 自有模组 README 开头统一附上 AI 开发说明：
  - 中文：`> 本 MOD 完全由 AI 开发，代码、配置和默认值都可以按你的需要自行修改。`
  - English: `> This mod is fully AI-developed. You are free to modify its code, configuration, and defaults.`
- 第三方作者原有 README 保持原样，除非用户明确要求修改。
- 不把 README 写成 AGENTS 指令或版本发布记录。

## CHANGELOG

- 每个自有可发布模组维护 `CHANGELOG.md`。
- 所有自有模组的 CHANGELOG 统一使用中文。
- 使用简洁格式：

```markdown
## v1.2.1

- 优化自动摆放的搜索与校验性能。
- 新增详细日志开关 `General.VerboseLog`。
```

- 每个版本最多 3 条，写用户能感知的变化，不写内部实现细节。
- 新条目放在最上面。
- 版本标题统一用 `## vX.Y.Z`。
- 详细日志、调试过程、实现说明不要放进 CHANGELOG。

## 代码与测试

- 使用 BepInEx 5.4.x / HarmonyX 与游戏 `Assembly-CSharp.dll` 的实际 API。
- 不提交 `bin/`、`obj/`、`SourceCode/` 或本地配置。
- 修改后至少：
  1. 构建整个解决方案；
  2. 检查版本号三处一致；
  3. 如能进入游戏，验证启动日志和关键交互；
  4. 更新 README/CHANGELOG。
- 涉及 Unity 对象、物理或工厂状态时，注意主线程限制；后台线程只处理纯数据。

## 提交

- 提交信息使用简洁中文或英文，说明模组和变更。
- 不还原或覆盖用户已有改动。
- 提交前检查 `git diff`，确认没有混入构建产物或无关删除。

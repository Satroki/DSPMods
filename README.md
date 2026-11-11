# DSPMods

《戴森球计划》（Dyson Sphere Program）BepInEx 模组集合。

## 模组

| 项目 | 说明 |
| --- | --- |
| [AutoPlaceMiner](AutoPlaceMiner/) | 建造大型采矿机时按 `Q`，自动搜索最佳位置与角度并放置，最大化矿脉覆盖。 |
| [DSPMultiplyStackSize](DSPMultiplyStackSize/) | 修改全部物品的堆叠数量，支持逐物品编辑。 |
| [DSPTools](DSPTools/) | 储罐链式输送、装 mod 解锁成就、自定义分辨率等工具，各功能均可在配置文件里开关。 |
| [DSPMods.Shared](DSPMods.Shared/) | 共享构建配置与游戏程序集引用，不单独发布。 |

> [DSPMultiplyStackSize](DSPMultiplyStackSize/) 的原作者：[Appun](https://github.com/appuns/DSPMultiplyStackSize)。

各模组的使用说明见对应目录下的 `README.md`。

## 环境

- 游戏：Dyson Sphere Program 0.10.x
- 框架：BepInEx 5.4.x（含 HarmonyX）
- 构建：.NET SDK，目标框架 `netstandard2.1`

## 构建

```powershell
dotnet build .\DSPMods.sln -c Release
```

各模组的 `csproj` 会在构建后自动复制 DLL 到 r2modman 的 `Default` profile；部署路径可通过 `DeployPluginDir` 修改。

游戏程序集和 BepInEx 的引用路径统一配置在 [DSPMods.Shared.props](DSPMods.Shared/DSPMods.Shared.props)。

## 反编译游戏源码

写补丁前想查游戏 API，可以把 `Assembly-CSharp.dll` 反编译到 `SourceCode/`（该目录已被 `.gitignore` 忽略）：

```powershell
pwsh -File .\decompile.ps1 -Force
```

脚本会自动探测游戏目录（也可用 `-GameDir` 指定），缺少 `ilspycmd` 时通过 `dotnet tool` 安装；输出是 `SourceCode/Assembly-CSharp.csproj` 加按类型拆分的 `.cs` 文件，可以直接用 IDE 打开跳转。

## 结构

```text
DSPMods/
├─ AutoPlaceMiner/         大型采矿机自动摆放
├─ DSPMultiplyStackSize/   堆叠数量编辑
├─ DSPTools/               储罐、成就解锁、自定义分辨率
├─ DSPMods.Shared/         共享构建配置
├─ decompile.ps1           反编译游戏源码到 SourceCode/
└─ DSPMods.sln
```

# DSPMods

《戴森球计划》（Dyson Sphere Program）BepInEx 模组集合。

## 模组

| 项目 | 说明 |
| --- | --- |
| [AutoPlaceMiner](AutoPlaceMiner/) | 建造大型采矿机时按 `Q`，自动搜索最佳位置与角度并放置，最大化矿脉覆盖。 |
| [DSPMultiplyStackSize](DSPMultiplyStackSize/) | 修改全部物品的堆叠数量，支持逐物品编辑。 |
| [SmartTankS](DspSmartTank/) | 储罐与资源相关的便利功能。 |
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

## 结构

```text
DSPMods/
├─ AutoPlaceMiner/         大型采矿机自动摆放
├─ DSPMultiplyStackSize/   堆叠数量编辑
├─ DspSmartTank/           SmartTankS
├─ DSPMods.Shared/         共享构建配置
└─ DSPMods.sln
```

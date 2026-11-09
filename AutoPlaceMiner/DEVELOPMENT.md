# AutoPlaceMiner 开发说明

面向开发与构建，不包含在游戏的 mod 介绍中。

## 构建

需要 .NET SDK、BepInEx 5.4.x 和游戏 `Assembly-CSharp.dll`。项目引用路径在 `..\DSPMods.Shared\DSPMods.Shared.props` 中配置。

```powershell
dotnet build .\AutoPlaceMiner\AutoPlaceMiner.csproj -c Release
```

构建后会自动复制到 r2modman 的 `Default` profile（可在 csproj 的 `DeployPluginDir` 中修改）。

## 实现要点

- 仅对 `PrefabDesc.isVeinCollector` 的大型采矿机生效。
- 根据鼠标附近矿脉群计算中心与半径，在该区域内搜索位置和角度。
- 用原生 `BuildTool_Click.CheckBuildConditions` 做最终合法性校验。
- 校验期间临时隐藏当前手持建筑的预览模型，避免把玩家自己的预览当成碰撞体。
- 通过后调用原生 `CreatePrebuilds` 放置。

## 性能

- 如果矿脉群中心附近已经能覆盖当前矿脉群的全部矿脉，会直接使用快速候选，不再做完整网格搜索。
- 完整搜索只在需要比较多个未满覆盖布局时执行。
- 覆盖判断使用预归一化的矿点坐标和点积，不再对每个候选反复调用完整的范围函数。
- 粗扫时如果已经出现满覆盖候选，会跳过后续精扫。
- 如果首轮搜索没有覆盖满，会围绕一批空间上分散的高分候选再做一次更宽的搜索。
- 日志会显示 `候选最大覆盖 X/Y`：X 是搜索到的最大覆盖数，Y 是矿脉群总矿脉数。

## 日志排查

- `目标矿脉群 ... 共 N 条 ... 候选最大覆盖 X/N`：搜索阶段。
- `当前预览条件 ... 隐藏预览模型 ...`：原生校验前的状态。
- `所有候选位置均被原生规则拒绝：...`：查看具体拒绝原因（Collide、OutOfReach、NeedResource 等）。
- `已放置大型采矿机：... 矿脉群共 N 条，本机覆盖当前群 X 条 ...`：最终结果。

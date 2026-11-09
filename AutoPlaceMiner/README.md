# AutoPlaceMiner

戴森球计划（Dyson Sphere Program）模组：建造**大型采矿机**时，按 `Q` 自动寻找覆盖矿脉最多的位置和角度，并直接放下矿机。

## 功能

- 仅在建造大型采矿机（Advanced Mining Machine，`PrefabDesc.isVeinCollector`）时生效。
- 先根据鼠标附近“当前矿脉群”的范围估算搜索区，再同时搜索**摆放位置**与**朝向**。
- 先按 2m / 10° 粗扫，再在最优候选附近按 0.5m / 0.5° 精扫。
- 优先覆盖当前矿脉群的矿脉；同类型相邻矿脉群也会计入总覆盖数。
- 用游戏原生的 `BuildTool_Click.CheckBuildConditions` 校验：
  - 建筑碰撞、地形、水面、坡度；
  - 建造范围、垂直建造高度；
  - 大型采矿机之间的最小间距；
  - 是否混入其他矿物类型。
- 校验期间会临时隐藏当前手持建筑的预览模型，避免把玩家自己的半透明预览当成碰撞体；校验结束后恢复。
- 通过校验后调用原生 `CreatePrebuilds` 放置，消耗背包/手中的大型采矿机。
- 找不到无碰撞的位置/角度时不会强行放置。

## 使用

1. 手持大型采矿机，进入建造模式。
2. 把建造预览移动到矿脉群附近。
3. 按 `Q`。

默认会拦截 `Q` 的相机横滚，避免自动摆放时镜头同时滚动；可在 BepInEx 配置中关闭 `SuppressCameraRoll`，或把 `Hotkey` 改成其他键。

## 配置

配置文件：`BepInEx/config/xyl29.dsp.AutoPlaceMiner.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General.Hotkey` | `Q` | 自动摆放快捷键 |
| `General.SuppressCameraRoll` | `true` | 手持大型采矿机时拦截 Q 的相机横滚 |
| `General.ShowResult` | `true` | 显示结果提示 |

## 安装

将 `AutoPlaceMiner.dll` 放到：

```
BepInEx/plugins/AutoPlaceMiner/
```

r2modman 用户可直接把该目录放到对应 profile 的 `BepInEx/plugins/` 下。

## 构建

需要 .NET SDK、BepInEx 5.4.x 和游戏 `Assembly-CSharp.dll`。项目引用路径在 `..\DSPMods.Shared\DSPMods.Shared.props` 中配置。

```powershell
dotnet build .\AutoPlaceMiner\AutoPlaceMiner.csproj -c Release
```

构建后会自动复制到 r2modman 的 `Default` profile（可在 csproj 的 `DeployPluginDir` 中修改）。

## 性能

- 如果矿脉群中心附近已经能覆盖当前矿脉群的全部矿脉，会直接使用快速候选，不再做完整网格搜索。
- 完整搜索只在需要比较多个未满覆盖布局时执行。
- 覆盖判断使用预归一化的矿点坐标和点积，不再对每个候选反复调用完整的范围函数。
- 粗扫时如果已经出现满覆盖候选，会跳过后续精扫。
- 如果首轮搜索没有覆盖满，会围绕一批空间上分散的高分候选再做一次更宽的搜索。
- 日志会显示 `候选最大覆盖 X/Y`：X 是搜索到的最大覆盖数，Y 是矿脉群总矿脉数。

## 说明

- 会自动移动建造位置到搜索到的最优中心，不再只调整当前预览位置的角度。
- 搜索范围以当前矿脉群中心为基准；如果鼠标离矿脉群太远，先把预览移近一些。
- 大型采矿机一次只能开采一种固体矿；覆盖区内出现另一种固体矿时，该角度会被原生规则拒绝。
- 油井不参与大型采矿机覆盖计算。

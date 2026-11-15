# AutoConnectPower 开发说明

面向开发与构建，不包含在游戏的 mod 介绍中。

## 构建

需要 .NET SDK、BepInEx 5.4.x 和游戏 `Assembly-CSharp.dll`。引用路径在 `..\DSPMods.Shared\DSPMods.Shared.props` 中配置。

```powershell
dotnet build .\AutoConnectPower\AutoConnectPower.csproj -c Release
```

构建后会自动复制到 r2modman 的 `Default` profile（可在 csproj 的 `DeployPluginDir` 中修改）。

## 默认行为

- `PlacementMode = Ghost`：默认只生成虚影，不扣电力塔材料。
- `AutoFoundation = true`：默认只给落点在水面的塔铺 3×3 地基，立即消耗地基物品（1131）和沙土；地面塔不铺。
- `TowerMode = Alternate`；快捷键 `Alt+C`（`C + LeftAlt`）。
- 没有建造菜单 UI，不使用 Harmony。

## 实现要点

### 数据来源

- `PlanetFactory.powerSystem`（0.10.x 的新电力系统）：`nodePool` 是电力节点，`genPool` 是发电机，`consumerPool` 是用电设备。
- 节点的 `networkId` 就是电网编号；`consumerPool[i].networkId == 0` 表示该设备没有被任何电力塔覆盖。
- 节点的连接/覆盖距离直接取 `PrefabDesc.powerConnectDistance` / `powerCoverRadius`，不写死数值。
- 节点位置统一换算成单位球方向，距离用 `(realRadius + 0.2) × |dirA - dirB|` 的弦长，与游戏内部的 `Node.x/y/z` 判定一致。

### 规划流程

1. **扫描**：按 `networkId` 把节点分组，选节点最多的电网作为主电网，其余作为待连接组件；同时收集 `networkId == 0` 的用电设备。
2. **连接电网**：用 Prim 式最小生成树在组件之间找最近节点对。每对节点之间沿大圆弦长按 `max(两端连接距离) - 1.8 m` 的间距放塔，直到两端进入连接距离。
3. **覆盖设备**：对每个未覆盖的用电设备，在“朝现有电网方向 + 覆盖半径内”的多个候选点里评分（覆盖设备数优先，其次少放连接塔、离电网近），选最优位置；必要时从该塔继续向电网铺塔。
4. **最少设备**：连接链按实际连接距离尽量拉长；覆盖塔会一次覆盖尽可能多的设备；规划完成后还会把能被新塔直接连上的其他电网合并掉，避免重复铺线。
5. **落成**：
   - `Direct`：按计划顺序从背包/手上扣 1 个塔，`AddPrebuildDataWithComponents` + `BuildFinally` 立即建成；整批用 `BeginFlattenTerrain/EndFlattenTerrain` + `PlanetFactory.batchBuild`，避免逐座刷新地形。材料不足的塔退化为 `itemRequired = 1` 的虚影。
   - `Ghost`：一律 `itemRequired = 1`，不扣材料，交给建造无人机/玩家补材料。

### 虚影建筑检查

- `ScanPrebuilds` 会遍历 `factory.prebuildPool`：
  - `isPowerNode` 的虚影（电力塔、发电机、蓄电器、能量枢纽等）作为 `IsGhost = true` 的 `ConnNode` 加入规划；同时计入 3.5 m 间距和虚影发电机数量。
  - `isPowerConsumer` 的虚影设备会作为待供电目标；如果已经落在已建成节点或虚影节点的覆盖范围内就跳过。
- 虚影节点按实际连接距离聚成“虚影电网”组件；如果组件已经落在某个已建成电网的连接范围内，就视为建成后会自动并网，不再额外铺线；否则作为待连接组件进入 MST。
- `_connected` 里的虚影节点会参与覆盖判断：已经能被虚影塔覆盖的用电设备不会重复建塔。
- 虚影只是预建造对象，未建成前不会真正导电；直接建造只会补连接塔，虚影本身仍要建成后才生效。

### 自动铺地基（`General.AutoFoundation`）

- 开启后规划阶段允许液体表面落点；建造前先跑 `PaveFoundations`，只对**落点在水面/熔岩等液体表面**的计划塔尝试铺 3×3 格地基；地面塔直接跳过，不铺地基。
- 液体判定不能只看 `waterHeight`：部分海洋星球的 `waterHeight` 可能为 0，实际水体由 `waterItemId` 标记；`waterItemId > 0` 是水/硫酸等液体，`< 0` 是熔岩等特殊海洋球。代码同时检查 `EPlanetType.Ocean`、`EPlanetType.Vocano`、`waterItemId != 0` 和 `|waterHeight|`。
- 实现路径与原生改造笔刷一致：
  - `PlanetAuxData.ReformSnap(pos, 0, 3, 1, 0, ...)` 取格子坐标和中心。
  - `PlanetFactory.ComputeFlattenTerrainReform` 计算沙土消耗。
  - `PlanetFactory.FlattenTerrainReform(center, 0.990946×3, 3, false)` 整平地形。
  - `PlatformSystem.SetReformType(index, 1)` / `SetReformColor(index, 0)` 标记地基，并立即 `reformDataBuffer.SetData` 推送显示。
  - 从手/背包扣 1131 地基物品，`player.SetSandCount` 扣沙土。
- 已经是地基的格子直接复用、不重复扣材料。
- 水面塔如果地基物品或沙土不足，会从计划中移除并在结果里报告；地面塔不铺地基，由 `BuildFinally` 的原生地形整平负责。

### 放置合法性

`TryPlaceAt` 会做这些检查：

- 网格吸附（`PlanetAuxData.Snap`）后取 `realRadius + 0.2` 的高度。
- 水面：`waterHeight > 0` 且 `QueryHeight` 低于水平面时拒绝。
- 电力塔最小间距：3.6 m（游戏原生 3.5 m），用 8 m 网格分桶做快速查询。
- 建筑/传送带/虚影碰撞：与 `BuildTool_Click` 相同的 `Physics.OverlapBoxNonAlloc` + `PlanetPhysics.GetColliderData`（掩码 `395264`，`isForBuild`）；`EObjectType.Prebuild` 一律视为障碍，只跳过带 `BuildPreviewModel` 的 layer 18 建造预览（玩家当前手上的预览）。
- 被挡住时围绕目标点左右偏转 20°～180° 重试；仍不行则缩小步长或放弃该条线路。

### 快捷键

- 入口只有快捷键：`General.Hotkey`（默认 `C + LeftAlt`，即 Alt+C；清空/`None` 关闭）。
- 快捷键在 `AutoConnectPowerPlugin.Update` 中处理；使用 `KeyboardShortcut` 保存主键和修饰键，但只检查这些键是否按下，不阻止玩家同时按着移动键。会避开输入文字（`VFInput.inputing`）和已打开的 GUI（`VFInput.onGUI`）。
- 不再注入任何建造菜单 UI，也不使用 Harmony。

## 日志排查

- `自动连接电网完成：...`：计划/建成/虚影数量、合并前后电网数、无供电设备数、虚影电网数、耗时。
- `无法连接电网 X：最近距离 ... 被水面或建筑阻挡`：某条线路找不到合法落点。

## 已知限制

- 不跨星球；只处理当前已加载工厂的星球。
- 水面/断崖/密集建筑会阻断直线路径，目前不会做全局绕障寻路。
- 虚影节点参与规划，但未建成前不会真正导电；虚影用电设备也只按规划处理，不会立刻出现在 `consumerPool`。
- 极端大的工厂（数千电力节点）规划可能耗时数百毫秒到数秒。

## AI 开发说明

本 MOD **完全由 AI 开发**。代码、默认值、配置和功能都可以自行调整或修改，不需要额外授权；修改后按上面的构建命令验证即可。遇到 bug 可附 BepInEx 日志关键字（如 `自动连接电网完成`、`无法连接电网`、`铺地基失败`、`自动铺地基未成功`）与复现步骤反馈。

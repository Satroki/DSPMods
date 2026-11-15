# AutoConnectPower

**中文** | [English](#english)

> **说明：本 MOD 完全由 AI 开发。代码、默认值、配置和功能都可以按你的需要自行调整或修改。**

## 中文

一键把**当前星球**上互不相连的电网和没有供电的用电设备连接起来。使用快捷键触发（默认 `Alt+C`），不占用建造菜单。

### 功能

- 扫描当前星球的所有电力节点（发电机、电力塔、蓄电器、能量枢纽等）和用电设备，并会检查**虚影建筑**（未建造的电力节点/用电设备）。
- 把所有独立的电网自动连接成一个电网。
- 为没有落在任何电力塔覆盖范围内的用电设备补建电力塔。
- 优先选择能一次覆盖多个用电设备、并且离现有电网最近的位置，尽量减少连接设备数量。
- 连接塔的间距按两种塔的实际连接距离自动计算，不会多放。
- 支持两种连接塔策略：只使用**电力感应塔**，或长距离连接时**交替使用无线输电塔和电力感应塔**。
- 支持两种落成方式：**仅生成虚影**（默认，不扣塔材料，交给建造无人机/玩家后续建造）或**直接建造**（从背包扣材料并立即建成）。
- **默认开启自动铺地基（仅水面/熔岩塔）**：只有落点在水面/熔岩等液体表面的连接塔会自动铺 3×3 格地基，消耗地基物品和沙土；地面上的塔不铺地基。已经是地基的格不会重复消耗，水面/熔岩塔在地基材料不足时会跳过并报告。
- 规划时会检查水面/熔岩、建筑/传送带碰撞和 3.5 m 的电力塔最小间距；放不下的位置会自动尝试旁边或换一条路线。

### 使用

在星球上直接按 `Alt+C`（可在配置中修改）。不需要打开建造菜单，但不要在输入文字或打开其他窗口时按。按住移动键等其它游戏按键不影响触发。

执行后会弹出结果提示。默认 `PlacementMode = Ghost`，只生成虚影，不扣电力塔材料；切换为 `Direct` 后才会从背包扣塔并立即建成，材料不足的位置保留为虚影。`AutoFoundation` 默认开启，但只给水面/熔岩塔铺地基，会立即消耗地基/沙土；地面塔不铺。

提示会显示：建成/虚影数量、合并前后的电网数量、无供电设备数量、虚影电网数量，以及未能连接的线路/设备数量。

### 配置

配置文件：`BepInEx/config/Satroki.dsp.AutoConnectPower.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General.Hotkey` | `C + LeftAlt`（Alt+C） | 自动连接电网快捷键；清空或设为 `None` 关闭。BepInEx 的组合键格式为主键 + 修饰键，例如 `C + LeftAlt`、`F8`。 |
| `General.TowerMode` | `Alternate` | `TeslaOnly` = 只使用电力感应塔；`Alternate` = 长距离连接时交替使用无线输电塔和电力感应塔。 |
| `General.PlacementMode` | `Ghost` | `Ghost` = 只生成未建造虚影（默认）；`Direct` = 直接建造（消耗背包材料）。 |
| `General.AutoFoundation` | `true` | 只给落点在水面/熔岩等液体表面的连接塔自动铺 3×3 格地基（消耗地基物品和沙土）；地面塔不铺。液体表面塔在地基材料/沙土不足时会跳过。默认开启。 |
| `General.ShowResult` | `true` | 显示执行结果提示。 |

### 注意

- 只处理**当前星球**，不会跨星球连接。
- 会检查**虚影建筑**：未建造的电力塔/发电机/蓄电器等虚影电力节点会参与电网规划；虚影用电设备也会作为待供电目标。虚影不是实际节点，真正并网要等它们建成。
- 连接塔默认只能放在陆地上。开启 `General.AutoFoundation` 后，只有落点在水面/熔岩等液体表面的连接塔会自动铺 3×3 格地基；地面上的塔不铺地基。液体表面塔在地基物品或沙土不足时会被跳过并在提示里报告。被断崖或密集建筑完全隔开的线路仍会放弃。
- “直接建造”会按顺序从背包（及手上）扣除电力塔；材料不足的部分自动变成虚影。“仅生成虚影”（默认）不扣塔材料，虚影仍需要材料才能建造，但不会立即改变现有电网，建成后才会真正合并。两种模式都不检查伊卡洛斯的建造范围，会在全星球范围内直接落成/生成虚影；若不希望这样，请放弃本次操作后手动处理。
- 虚影本身会占用建造位置，未建完前重复执行可能找不到落点；建议建完后再重新扫描（或先拆掉不满意的虚影）。
- 自动铺地基默认开启，但只对水面/熔岩塔生效，且会**立即**消耗地基物品和沙土（即使使用“仅生成虚影”模式），因为地形改造没有虚影形态。地面塔不铺地基；已经是地基的格不会重复消耗。
- 沙盒模式开启「无限沙土」后，不再检查/扣除沙土，沙土消耗按 0 处理；地基物品仍按正常规则检查。
- 只使用 `电力感应塔` 时，长距离连接需要的塔会明显多于交替模式；这是两种塔连接距离不同造成的正常结果。
- 建议在单机存档使用。多人模式下由主机/存档拥有者执行，避免不同步。

### 反馈与自行调整

本 MOD 完全由 AI 开发，你可以直接修改代码、配置和默认值，按自己的玩法调整。遇到问题可附上 BepInEx 日志关键字（如 `自动连接电网完成`、`无法连接电网`、`铺地基失败`、`自动铺地基未成功`）和复现步骤反馈。

> **Note: This mod is fully AI-developed. You are free to adjust or modify its code, defaults, configuration, and behavior for your own playstyle.**

## English

Connects every separate power network and every unpowered consumer on the **current planet** with a hotkey (default `Alt+C`). It does not add anything to the build menu.

### Features

- Scans all power nodes (generators, power towers, accumulators, energy exchangers, ...) and consumers, including **unbuilt ghost buildings** (ghost power nodes and ghost consumers).
- Merges all separate power networks into a single grid.
- Builds additional power towers to cover consumers that are outside every tower's coverage radius.
- Picks tower positions that cover as many consumers as possible and stay close to the existing grid, minimizing the number of towers.
- Computes chain spacing from the actual `powerConnectDistance` of each tower, so no redundant tower is placed.
- Two strategies: **Tesla Tower only**, or **alternate Wireless Power Tower / Tesla Tower** for long-distance links.
- Two build modes: **Ghost** (default; create unbuilt prebuilds and let construction drones / the player build them later) or **Direct** (consume items and build immediately).
- **Auto foundation is on by default (water/lava towers only)**: only towers planned on water or lava get a 3×3 foundation, consuming foundation items and soil; towers on land are not given foundations. Already-founded cells are not charged again. Liquid-surface towers are skipped and reported when foundation materials run out.
- Placement validation checks water/lava, building/belt collision, and the native 3.5 m minimum spacing between power towers; blocked spots trigger nearby retries or a different route.

### Usage

Press `Alt+C` (configurable) while on a planet. The build menu does not need to be open; do not press it while typing or while another window is focused. Holding movement or other game keys does not block it.

Read the result message. `PlacementMode` defaults to `Ghost` (no tower items consumed); switch to `Direct` to consume tower items and build immediately, with missing items left as ghosts. `AutoFoundation` defaults to `true`, but only water/lava towers are paved; land towers are left alone.

### Configuration

Config file: `BepInEx/config/Satroki.dsp.AutoConnectPower.cfg`

| Setting | Default | Description |
| --- | --- | --- |
| `General.Hotkey` | `C + LeftAlt` (Alt+C) | Hotkey; clear or set to `None` to disable. BepInEx combo format is main key + modifiers, e.g. `C + LeftAlt` or `F8`. |
| `General.TowerMode` | `Alternate` | `TeslaOnly` or `Alternate` (Wireless / Tesla alternating on long links). |
| `General.PlacementMode` | `Ghost` | `Ghost` (default; unbuilt prebuilds only) or `Direct` (consume items and build). |
| `General.AutoFoundation` | `true` | Automatically lay a 3×3 foundation under water/lava towers only (consumes foundation items and soil); land towers are not paved. Liquid-surface towers are skipped if materials/soil are insufficient. Enabled by default. |
| `General.ShowResult` | `true` | Show result messages. |

### Notes

- Only the **current planet** is processed; no interplanetary connections.
- **Ghost buildings are considered**: unbuilt ghost power nodes (towers, generators, accumulators, ...) participate in grid planning, and ghost consumers are treated as targets. Ghosts are not live nodes; they join the grid only after being built.
- Towers can only be built on land by default. With `General.AutoFoundation`, only towers on water or lava get a 3×3 foundation patch; land towers are not paved. Liquid-surface towers whose foundation items or soil are insufficient are skipped and reported. Routes completely blocked by cliffs or dense buildings are still skipped.
- Direct mode consumes tower items from the inventory (or hand) in order; missing items become ghosts. Ghost mode (default) does not consume tower items; ghosts become real networks only after they are built. Ghosts occupy build positions, so running again before they are built may find no free spots; build them first (or dismantle unwanted ghosts) and scan again. Neither mode checks the mecha build range: placements are made planet-wide.
- Auto foundation is enabled by default but applies to water/lava towers only; it consumes foundation items and soil **immediately**, even in Ghost mode, because terrain reformation has no ghost form. Land towers are not paved; already-founded cells are not charged again.
- With sandbox **Infinite Sand** enabled, the sand check and sand cost are skipped (treated as zero); foundation items are still required normally.
- With `TeslaOnly`, long links need many more towers than `Alternate`; this is expected because the two towers have different connection ranges.
- Recommended for single-player saves. In multiplayer, let the host/save owner run it to avoid desync.

### Feedback and customization

This mod is fully AI-developed; you can directly modify its code, configuration, and defaults to suit your own playstyle. For bugs, include BepInEx log keywords (e.g. `自动连接电网完成`, `无法连接电网`, `铺地基失败`, `自动铺地基未成功`) and reproduction steps.

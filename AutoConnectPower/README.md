# AutoConnectPower

当前版本：**1.0.1**

> 本 MOD 完全由 AI 开发，代码、配置和默认值都可以按你的需要自行修改。

一键连接**当前星球**的电网：扫描已建成/虚影的发电与用电设备，用尽量少的电力感应塔/无线输电塔把独立电网和无供电设备连接起来。快捷键为 `Alt+C`。

## 功能

- 合并所有独立电网，并为没有电力塔覆盖的用电设备补建电力塔。
- 支持只使用**电力感应塔**，或长距离连接时**交替使用无线输电塔/电力感应塔**。
- 默认 `Ghost`：只生成未建造虚影，不扣电力塔材料；可切换 `Direct` 直接建造。
- 默认自动为**水面/熔岩塔**铺 3×3 地基，地面塔不铺。
- 规划时检查建筑/传送带碰撞、3.5 m 电力塔间距，并考虑未建造的虚影建筑。

## 使用

在星球上按 `Alt+C`（可在配置中修改）。执行后会提示建成/虚影数量、电网合并、无供电设备、地基消耗和失败数量。

- 默认 `PlacementMode = Ghost`，不扣电力塔材料；虚影建成后才真正并网。
- 默认 `AutoFoundation = true`，水面/熔岩塔会**立即**消耗地基物品和沙土。
- 两种模式都不检查伊卡洛斯建造范围，会在全星球范围内放置。

## 配置

文件：`BepInEx/config/Satroki.dsp.AutoConnectPower.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General.Hotkey` | `C + LeftAlt`（Alt+C） | 快捷键；清空或设为 `None` 关闭。 |
| `General.TowerMode` | `Alternate` | `TeslaOnly` 只用电力感应塔；`Alternate` 长距离交替使用两种塔。 |
| `General.PlacementMode` | `Ghost` | `Ghost` 只生成虚影；`Direct` 扣材料直接建成。 |
| `General.AutoFoundation` | `true` | 只给水面/熔岩塔铺 3×3 地基；地面塔不铺。 |
| `General.ShowResult` | `true` | 显示结果提示。 |

## 注意

- 虚影电力节点和虚影用电设备会参与规划，但未建成前不会真正导电。
- 地基只对水面/熔岩塔生效；材料/沙土不足时会跳过该塔并报告。已经是地基的格不会重复消耗。
- 建议单机使用；多人模式由主机/存档拥有者执行。

## 更新日志

见 [CHANGELOG](CHANGELOG.md)。

## English

Current version: **1.0.1**

> This mod is fully AI-developed. You are free to modify its code, configuration, and defaults.

Connects the **current planet's** power grid with `Alt+C`: scans built and ghost power generation/consumer devices, then connects separate grids and uncovered consumers using as few Tesla Towers / Wireless Power Towers as possible.

### Features

- Merges all separate power grids and builds towers to cover unpowered consumers.
- Supports **Tesla Tower only**, or **alternating Wireless Power Tower / Tesla Tower** for long links.
- Defaults to `Ghost`: creates unbuilt ghosts without consuming tower items; `Direct` is available.
- Automatically lays a 3×3 foundation for **water/lava towers only**.
- Checks building/belt collision, 3.5 m tower spacing, and unbuilt ghost buildings during planning.

### Usage

Press `Alt+C` on a planet (configurable). The result message reports built/ghost towers, grid merging, unpowered consumers, foundation usage, and failures.

- `PlacementMode = Ghost` by default: no tower items are consumed; ghosts join the grid only after being built.
- `AutoFoundation = true` by default: water/lava towers **immediately** consume foundation items and soil; land towers are not paved.
- Neither mode checks the mecha build range; placements are made planet-wide.

### Configuration

File: `BepInEx/config/Satroki.dsp.AutoConnectPower.cfg`

| Setting | Default | Description |
| --- | --- | --- |
| `General.Hotkey` | `C + LeftAlt` (Alt+C) | Hotkey; clear or set to `None` to disable. |
| `General.TowerMode` | `Alternate` | `TeslaOnly` for Tesla only; `Alternate` alternates both towers on long links. |
| `General.PlacementMode` | `Ghost` | `Ghost` creates unbuilt ghosts; `Direct` consumes items and builds. |
| `General.AutoFoundation` | `true` | Lays a 3×3 foundation for water/lava towers only; land towers are not paved. |
| `General.ShowResult` | `true` | Show result messages. |

### Notes

- Ghost power nodes and ghost consumers participate in planning, but they do not conduct power until built.
- Auto foundation applies only to water/lava towers; if materials/soil are insufficient, that tower is skipped and reported. Already-founded cells are not charged again.
- Recommended for single-player; in multiplayer, let the host/save owner run it.

### Changelog

See [CHANGELOG](CHANGELOG.md).

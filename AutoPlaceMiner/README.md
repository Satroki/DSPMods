# AutoPlaceMiner

> 本 MOD 完全由 AI 开发，代码、配置和默认值都可以按你的需要自行修改。

**中文** | [English](#english)

## 中文

建造**大型采矿机**时，按 `Q` 自动寻找能覆盖最多矿脉的位置和角度，并直接放下矿机。

### 功能

- 自动搜索大型采矿机的最佳摆放位置和朝向。
- `Alt+Q` 可为当前矿脉群以及当前星球其它同类型矿脉群批量放置大型采矿机；超出建造范围的目标只放置白色虚影，批量时同样会尝试自动地基。
- 优先覆盖鼠标附近的“当前矿脉群”；相邻的同类型矿脉也会计入总覆盖数。
- 默认开启自动地基：没有更好的陆地位置时，会为水中矿脉的落点铺设地基，消耗地基物品和沙土。
- 默认开启掩埋矿脉恢复：会把当前矿脉群中被完全掩埋在地下的矿脉恢复到地表。
- 找不到合法位置时不会强行放置。
- 默认拦截 `Q` 的相机横滚，避免自动摆放时镜头同时滚动；快捷键可配置。

### 使用

1. 手持大型采矿机，进入建造模式。
2. 把建造预览移动到矿脉群附近。
3. 按 `Q` 放置当前矿脉群；按 `Alt+Q` 为当前及其它同类型矿脉群批量放置。

### 配置

配置文件：`BepInEx/config/Satroki.dsp.AutoPlaceMiner.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General.Hotkey` | `Q` | 自动摆放快捷键 |
| `General.SuppressCameraRoll` | `true` | 手持大型采矿机时拦截 Q 的相机横滚 |
| `General.AutoFoundation` | `true` | 水中矿脉自动铺地基，消耗地基物品和沙土 |
| `General.RestoreBuriedVeins` | `true` | 搜索前恢复当前矿脉群中被完全掩埋的矿脉 |
| `General.ShowResult` | `true` | 显示结果提示 |
| `General.VerboseLog` | `false` | 输出搜索、候选校验、地基等详细日志 |

### 注意

- 只对大型采矿机生效。
- `Alt+Q` 以当前预览附近矿脉群为类型依据，包含当前矿脉群；已有矿机或虚影的矿脉群会跳过，每次最多处理 32 个。
- 摆放仍受建造范围、碰撞和地形限制；如果满覆盖位置被矿脉本身挡住，会退而选择较低覆盖的位置。
- 自动地基只在没有更好的陆地位置时尝试，最多尝试少数几个高覆盖水面候选；地基和沙土不足时会跳过并记录日志。
- 掩埋矿脉恢复会移动矿脉、碰撞体和模型；已有建筑/虚影/矿机占用时会跳过。关闭 `RestoreBuriedVeins` 后只使用原本可见的矿脉。
- 已经是地基的格子不会重复消耗材料。铺地基会立即改变地形，即使后续建造失败也不会恢复。

版本历史见 [CHANGELOG](CHANGELOG.md)。

## English

> This mod is fully AI-developed. You are free to modify its code, configuration, and defaults.

While building an **Advanced Mining Machine**, press `Q` to automatically search for the position and angle that cover the most veins, then place the miner.

### Features

- Searches for the best placement position and rotation of the Advanced Mining Machine.
- `Alt+Q` batch-places miners on the current and other same-type vein groups on the current planet; out-of-range targets are placed as white ghosts, and automatic foundation is also applied in batch mode.
- Prioritizes the current vein group near the cursor; adjacent groups of the same ore type also count toward total coverage.
- Auto foundation is enabled by default: when no better land position exists, it lays a foundation at a water position for underwater veins, consuming foundation items and soil.
- Buried-vein restoration is enabled by default: fully buried veins in the current group are restored to the surface.
- Does not force placement when no valid position exists.
- Suppresses the camera roll bound to `Q` by default while holding an Advanced Mining Machine; the hotkey is configurable.

### Usage

1. Hold an Advanced Mining Machine and enter build mode.
2. Move the build preview near a vein group.
3. Press `Q` for the current group, or `Alt+Q` for the current and other groups of the same ore type.

### Configuration

Config file: `BepInEx/config/Satroki.dsp.AutoPlaceMiner.cfg`

| Setting | Default | Description |
| --- | --- | --- |
| `General.Hotkey` | `Q` | Hotkey for automatic placement |
| `General.SuppressCameraRoll` | `true` | Suppress the `Q` camera roll while holding an Advanced Mining Machine |
| `General.AutoFoundation` | `true` | Lay foundation for underwater vein placements, consuming foundation items and soil |
| `General.RestoreBuriedVeins` | `true` | Restore fully buried veins in the current group before searching |
| `General.ShowResult` | `true` | Show result messages |
| `General.VerboseLog` | `false` | Output detailed search, validation, and foundation logs |

### Notes

- Only works with the Advanced Mining Machine.
- `Alt+Q` uses the vein type of the group near the current preview, including the current group; groups that already have a miner or ghost are skipped, up to 32 groups per press.
- Placement is still limited by build range, collision, and terrain; if a full-coverage position is blocked by the veins themselves, a lower-coverage position is used.
- Auto foundation is tried only when there is no better land position, and only for a few high-coverage water candidates. It is skipped and logged if foundation items or soil are insufficient.
- Buried-vein restoration moves the vein, collider, and model; veins occupied by buildings, ghosts, or existing miners are skipped. Disable `RestoreBuriedVeins` to use only originally visible veins.
- Already-founded cells are not charged again. Paving changes terrain immediately and is not rolled back if the later build check fails.

See [CHANGELOG](CHANGELOG.md) for version history.

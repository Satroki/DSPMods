# AutoPlaceMiner

**中文** | [English](#english)

## 中文

建造**大型采矿机**时，按 `Q` 自动寻找能覆盖最多矿脉的位置和角度，并直接放下矿机。

### 功能

- 同时搜索大型采矿机的**摆放位置**和**朝向**，不再只旋转当前预览。
- 优先覆盖鼠标附近的“当前矿脉群”；相邻的同类型矿脉也会计入总覆盖数。
- 使用游戏原生建造规则检查碰撞、地形、建造范围、矿机间距和矿物类型。
- 找不到合法位置时不会强行放置。
- 默认拦截 `Q` 的相机横滚，避免自动摆放时镜头同时滚动；快捷键可配置。

### 使用

1. 手持大型采矿机，进入建造模式。
2. 把建造预览移动到矿脉群附近。
3. 按 `Q`。

### 配置

配置文件：`BepInEx/config/Satroki.dsp.AutoPlaceMiner.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General.Hotkey` | `Q` | 自动摆放快捷键 |
| `General.SuppressCameraRoll` | `true` | 手持大型采矿机时拦截 Q 的相机横滚 |
| `General.ShowResult` | `true` | 显示结果提示 |

### 注意

- 只对大型采矿机生效。
- 摆放仍受建造范围、碰撞和地形限制；覆盖数可能小于矿脉群总数。
- 大型采矿机一次只能开采一种固体矿；覆盖区内混入其他固体矿时，该位置会被原生规则拒绝。
- 油井不参与大型采矿机覆盖计算。

## English

While building an **Advanced Mining Machine**, press `Q` to automatically search for the position and angle that cover the most veins, then place the miner.

### Features

- Searches both the **placement position** and the **rotation** of the Advanced Mining Machine.
- Prioritizes the current vein group near the cursor; adjacent groups of the same ore type also count toward total coverage.
- Uses the game's native build rules for collision, terrain, build range, miner spacing, and ore type checks.
- Does not force placement when no valid position exists.
- Suppresses the camera roll bound to `Q` by default while holding an Advanced Mining Machine; the hotkey is configurable.

### Usage

1. Hold an Advanced Mining Machine and enter build mode.
2. Move the build preview near a vein group.
3. Press `Q`.

### Configuration

Config file: `BepInEx/config/Satroki.dsp.AutoPlaceMiner.cfg`

| Setting | Default | Description |
| --- | --- | --- |
| `General.Hotkey` | `Q` | Hotkey for automatic placement |
| `General.SuppressCameraRoll` | `true` | Suppress the `Q` camera roll while holding an Advanced Mining Machine |
| `General.ShowResult` | `true` | Show result messages |

### Notes

- Only works with the Advanced Mining Machine.
- Placement is still limited by build range, collision, and terrain; coverage may be lower than the total number of veins in the group.
- An Advanced Mining Machine can mine only one solid ore type at a time. A position that covers another solid ore type is rejected by the native build rules.
- Oil veins are not included in Advanced Mining Machine coverage.

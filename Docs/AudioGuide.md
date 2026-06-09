# Lilith 音乐与音效添加指南

这份文档面向不写代码的协作者，目标是让你能安全地把音乐和音效放进项目，并把它们交给程序接入游戏。

当前音频系统已经支持音乐、音效、淡入淡出、音量设置、Addressables 加载，以及基于 `AudioCue` 资产的分类限流、优先级、冷却、并发上限和世界位置音效。也就是说，你可以先完成资源导入、Addressable 地址配置和 `AudioCue` 资产配置；具体在什么场景、按钮、敌人或技能上播放，仍需要程序把对应 `AudioCue` 接到触发点。

## 你可以做什么

| 你要做的事 | 是否需要写代码 | 说明 |
| --- | --- | --- |
| 把音乐或音效导入 Unity | 不需要 | 直接拖进 Project 面板 |
| 设置 AudioClip 的导入选项 | 不需要 | 在 Inspector 里改 |
| 把 AudioClip 标记为 Addressable | 不需要 | 勾选 Addressable 并填写地址 |
| 调整游戏里的主音量 / 音乐 / 音效 | 不需要 | 在 Options 里调，点击 Apply |
| 指定某段音乐在哪里播放 | 不写代码，但需要交给程序 | 提供 `AudioCue` 资产和触发时机 |
| 指定某个音效绑定到按钮 / 技能 / 命中 | 不写代码，但需要交给程序 | 提供 `AudioCue` 资产和触发时机 |

## 推荐文件位置

如果项目里还没有这些文件夹，可以在 `Assets` 下创建：

| 类型 | 推荐路径 | 例子 |
| --- | --- | --- |
| 背景音乐 | `Assets/Audio/Music` | `main_menu_loop.ogg` |
| 环境音乐 / 氛围 | `Assets/Audio/Ambience` | `start_room_ambience_loop.ogg` |
| UI 音效 | `Assets/Audio/Sfx/UI` | `ui_confirm.wav` |
| 战斗音效 | `Assets/Audio/Sfx/Combat` | `player_hit.wav` |
| 敌人音效 | `Assets/Audio/Sfx/Enemy` | `boss_phase_change.wav` |

文件名建议只用英文、小写、数字和下划线。

推荐：

```text
main_menu_loop.ogg
start_room_loop.ogg
ui_confirm.wav
ui_cancel.wav
enemy_hit_light.wav
boss_phase_change.wav
```

不推荐：

```text
主菜单最终版!!!!.mp3
new sound 3.wav
Boss音效(修改版).wav
```

## 音频格式建议

| 类型 | 推荐格式 | 建议 |
| --- | --- | --- |
| 音乐 | `.ogg` 或 `.wav` | 长音乐优先 `.ogg`；需要无缝循环时，导出前确认开头和结尾没有空白 |
| UI / 战斗音效 | `.wav` | 短音效优先 `.wav`，方便保留瞬态和剪辑 |
| 临时占位 | `.wav` / `.ogg` 都可以 | 文件名加 `_temp`，避免误当正式资源 |

制作音频时请尽量做到：

| 检查项 | 标准 |
| --- | --- |
| 不爆音 | 波形不要顶满，听起来不要刺耳或破音 |
| 不留无意义空白 | 音效开头不要有明显延迟，循环音乐结尾不要多出静音 |
| 音量相对一致 | 同类 UI 音效响度接近，不要一个特别大一个特别小 |
| 循环可用 | 需要循环的音乐，文件名建议带 `_loop` |

## Unity 导入步骤

1. 打开 Unity。
2. 在 Project 面板里找到或创建推荐文件夹，例如 `Assets/Audio/Music`。
3. 把音频文件拖进该文件夹。
4. 点击刚导入的音频文件。
5. 在 Inspector 里确认它是 `AudioClip`。
6. 按下面的建议设置导入选项。
7. 点击 `Apply`。

### 音乐导入建议

| Inspector 项 | 建议值 |
| --- | --- |
| Force To Mono | 不勾选，除非这段音乐本来就应该是单声道 |
| Load In Background | 勾选 |
| Load Type | `Streaming` |
| Compression Format | `Vorbis` |
| Quality | 70 到 90 |

### 短音效导入建议

| Inspector 项 | 建议值 |
| --- | --- |
| Force To Mono | UI / 战斗短音效通常可以勾选 |
| Load In Background | 不需要勾选 |
| Load Type | `Decompress On Load` |
| Compression Format | `PCM` 或 `ADPCM` |

如果你不确定，就先使用 Unity 默认设置导入，并在提交说明里写“导入设置未确认”。程序或音频负责人可以之后统一调整。

## 设置 Addressable 地址

音频系统通过 Addressable 地址加载音乐和音效。地址就是程序之后要用来播放这段声音的名字。

### 标记为 Addressable

1. 选中音频文件。
2. 在 Inspector 顶部勾选 `Addressable`。
3. 如果弹出分组选择，不确定时放在 `Default Local Group`。
4. 把 Address 改成稳定、易读、没有扩展名的形式。

推荐地址：

```text
Assets/Audio/Music/main_menu_loop
Assets/Audio/Music/start_room_loop
Assets/Audio/Sfx/UI/ui_confirm
Assets/Audio/Sfx/Combat/player_hit
Assets/Audio/Sfx/Enemy/boss_phase_change
```

地址可以和文件路径相似，但不要依赖 Unity 自动生成的奇怪名字。以后给程序的就是这个 Address。

## 创建 AudioCue 资产

推荐每个可复用音乐或音效都配一个 `AudioCue` 资产。创建方式：

1. 在 Project 面板里右键。
2. 选择 `Create > Vocalith > Audio > Audio Cue`。
3. 把资产放在清晰目录里，例如 `Assets/Data/AudioCues/UI` 或 `Assets/Data/AudioCues/Combat`。
4. 如果音频会常驻或低频播放，可以直接把 `AudioClip` 拖到 `Clip`。
5. 如果音频希望按需加载或预加载，填写稳定的 Addressable `Address`。
6. 同时有 `Clip` 和 `Address` 时，系统优先使用 `Clip`。

常用字段建议：

| 字段 | 用途 |
| --- | --- |
| Kind | `Music` 用于背景音乐，`Sfx` 用于短音效 |
| Category | 用于限流；UI 用 `Ui`，战斗命中 / 攻击 / 技能用 `Combat`，环境循环或氛围用 `Ambience` |
| Priority | 越高越不容易被抢占；Boss、玩家受击、关键反馈应高于普通小怪或重复命中 |
| Volume Scale | 这条 cue 自身音量倍率 |
| Pitch Min / Max | 每次播放随机音高范围；普通 UI 可固定 `1 / 1` |
| Cooldown Seconds | 同一 cue 的最小重复间隔，用来防止大量同类命中音叠在一起 |
| Max Simultaneous Self | 同一 cue 可同时播放几份；普通短反馈建议 1，连续技能可适当提高 |
| Spatial Mode | `TwoDimensional` 为普通 2D 音效；`WorldPosition` 会在触发位置播放并启用距离衰减 |
| Min / Max Distance | 世界位置音效的距离衰减范围 |

### 不要随便改已交付地址

如果一个地址已经交给程序接入过，后面不要直接改名。需要改名时，请同步告诉程序：

| 原地址 | 新地址 | 为什么改 |
| --- | --- | --- |
| `Assets/Audio/Music/main_menu_loop` | `Assets/Audio/Music/title_loop` | 标题音乐改名 |

## 给程序的交付格式

新增音乐或音效后，请把下面的信息发给程序或写进任务说明。

| 字段 | 示例 |
| --- | --- |
| 用途 | 主菜单背景音乐 |
| 文件路径 | `Assets/Audio/Music/main_menu_loop.ogg` |
| AudioCue 资产 | `Assets/Data/AudioCues/Music/main_menu_loop_cue.asset` |
| Addressable 地址 | `Assets/Audio/Music/main_menu_loop` |
| 类型 | 音乐 |
| 是否循环 | 是 |
| 希望什么时候播放 | 进入 StartUp 主菜单后播放 |
| 希望什么时候停止 | 开始游戏进入 Main 前淡出 |
| 特殊说明 | 默认 0.5 秒淡入淡出即可 |

音效示例：

| 字段 | 示例 |
| --- | --- |
| 用途 | UI 确认按钮音效 |
| 文件路径 | `Assets/Audio/Sfx/UI/ui_confirm.wav` |
| AudioCue 资产 | `Assets/Data/AudioCues/UI/ui_confirm_cue.asset` |
| Addressable 地址 | `Assets/Audio/Sfx/UI/ui_confirm` |
| 类型 | 音效 |
| 是否循环 | 否 |
| 希望什么时候播放 | 点击 Options 的 Apply / 主菜单开始按钮 |
| 特殊说明 | 音量可以比普通点击略明显 |

## 游戏内音量设置

Options 里已经有三项音频设置：

| 设置项 | 影响 |
| --- | --- |
| 主音量 | 同时影响音乐和音效 |
| 音乐音量 | 只影响背景音乐 |
| 音效音量 | 只影响短音效 |

最终听到的音乐音量大致等于：

```text
主音量 x 音乐音量
```

最终听到的音效音量大致等于：

```text
主音量 x 音效音量 x 单次播放音量
```

你在 Options 里修改后，需要点击 `Apply` 才会生效。点击 `Cancel` 或关闭后选择丢弃，不会改变当前运行时音量。

## 自查清单

提交前请确认：

| 检查项 | 通过标准 |
| --- | --- |
| 文件放在了合适目录 | 例如 `Assets/Audio/Music` 或 `Assets/Audio/Sfx/UI` |
| 文件名清晰 | 英文、小写、下划线，没有“最终版2”这类名字 |
| AudioClip 能在 Inspector 里播放 | 点击预览能听到声音 |
| 已勾选 Addressable | Inspector 顶部显示 Addressable |
| 地址稳定且无扩展名 | 例如 `Assets/Audio/Sfx/UI/ui_confirm` |
| 交付说明完整 | 写清楚用途、`AudioCue` 资产、地址、播放时机和是否循环 |
| 不爆音、不拖尾 | 听起来没有破音，短音效没有多余静音 |

## 常见问题

### 我导入了音乐，为什么游戏里没有响？

当前不会自动扫描并播放新音乐。你需要把 `AudioCue` 资产和播放时机交给程序接入。

### 我改了 Options 里的音乐音量，为什么没听出变化？

如果当前没有正在播放的音乐，就听不出变化。音量系统已经接入，但是否有音乐播放取决于具体触发点有没有接。

### Console 里提示找不到 address 怎么办？

通常是 Addressable 地址填错、没有勾选 Addressable，或者改名后没有同步给程序。检查 Inspector 里的 Address 是否和交付说明完全一致。

### 循环音乐有明显断点怎么办？

检查音频文件开头和结尾是否有空白，或者结尾是否有混响尾巴。需要无缝循环的音乐，导出前最好在音频软件里循环试听。

### 我能直接改脚本来播放声音吗？

不要。非程序协作者只需要导入资源、设置 Addressable、写清楚触发时机。播放逻辑交给程序接入。

## 不要动的东西

| 不要动 | 原因 |
| --- | --- |
| `Assets/Scripts/Vocalith/Audio/AudioManager.cs` | 通用音频系统脚本 |
| `Assets/Scripts/Vocalith/Audio/AudioCue.cs` | 通用音频 Cue 资产定义 |
| `Assets/Scripts/Kernel/Audio/LilithAudioSettings.cs` | Lilith 的音量设置桥接 |
| `Assets/Data/UI/OptionsCatalog.json` 里的音频 key | Options 读取这些 key 保存音量 |
| 已经接入过的 Addressable 地址 | 改名会导致程序加载不到资源 |

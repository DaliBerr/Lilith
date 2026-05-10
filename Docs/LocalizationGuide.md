# Lilith 本地化使用指南

这份文档面向策划、翻译、叙事、美术和测试协作者。你不需要写代码，只需要知道哪些文件可以改、哪些字段会显示给玩家、改完以后怎样检查。

## 一句话说明

Lilith 现在把玩家能看到的文字分成两类：

| 类型 | 改哪里 | 例子 |
| --- | --- | --- |
| 固定界面文字 | `Assets/Data/Localization/StringTables` | 按钮、弹窗、设置提示、升级界面固定标签 |
| 数据内容文字 | `Assets/Data/Localization/JsonPatches` | 剧情、任务、设置项名称、Hint、结算、永久升级 |

原始中文内容会继续保留，作为兜底文本。翻译时优先改本地化文件，不要直接删除中文原文。

## 现在已经支持什么

当前首批语言：

| 语言 | 用途 |
| --- | --- |
| `zh-Hans-CN` | 简体中文 |
| `en-US` | 英文 |

当前已经接入本地化的范围：

| 系统 | 支持方式 |
| --- | --- |
| 通用按钮和提示 | 字符串表 key |
| 设置界面 Options | JSON patch |
| Hint / 指南 | JSON patch |
| 结算界面 | 字符串表 + JSON patch |
| 任务文本 | JSON patch |
| 剧情 / 对话 | JSON patch |
| 永久升级 | JSON patch |
| Token 展示名和说明 | Inspector key + 字符串表 |
| Enemy 展示名和说明 | Inspector key + 字符串表 |

不属于首版本地化范围：

| 不翻译 | 原因 |
| --- | --- |
| 日志和报错细节 | 主要给开发看 |
| Editor 工具文字 | 主要给开发 / 制作者看 |
| GameObject 名字 | Unity 场景内部引用 |
| Addressables 地址 | 资源加载用，不能当文案改 |
| InputAction 名字 | 输入系统内部标识 |
| Token / Enemy 单字 glyph | 默认作为玩法视觉符号，不强制翻译 |

## 最安全的工作方式

1. 先确认你要改的是“固定界面文字”还是“数据内容文字”。
2. 只改目标语言文件，比如英文就改 `en-US`。
3. 不要改 key、id、domain、language，除非程序同学确认。
4. 改完保存，回到 Unity 等导入完成。
5. 从 `StartUp` 场景进入游戏检查显示。
6. 如果 Console 出现红色错误，先撤回最近一次 JSON 改动，再找程序同学看。

## 固定界面文字怎么改

固定界面文字在：

`Assets/Data/Localization/StringTables`

英文文件：

`Assets/Data/Localization/StringTables/ui.en-US.json`

中文文件：

`Assets/Data/Localization/StringTables/ui.zh-Hans-CN.json`

格式长这样：

```json
{
  "language": "en-US",
  "entries": {
    "ui.common.close": "Close",
    "ui.common.confirm": "OK"
  }
}
```

你可以改右边的显示文字，不要改左边的 key。

| 可以改 | 不要改 |
| --- | --- |
| `"Close"` | `"ui.common.close"` |
| `"OK"` | `"ui.common.confirm"` |

如果文字里有 `{0}`、`{1}` 这类占位符，必须原样保留。

示例：

```json
"ui.upgrade.cost": "Cost {0} Remnants"
```

可以改成：

```json
"ui.upgrade.cost": "{0} Remnants required"
```

不要改成：

```json
"ui.upgrade.cost": "Remnants required"
```

因为 `{0}` 是游戏运行时填进去的数字。

## 数据内容文字怎么改

数据内容文字在：

`Assets/Data/Localization/JsonPatches`

英文补丁目录：

`Assets/Data/Localization/JsonPatches/en-US`

每个文件负责一个系统：

| 文件 | 负责内容 |
| --- | --- |
| `OptionsCatalog.en-US.json` | 设置界面分类和条目名称 |
| `HintCatalog.en-US.json` | Hint / 指南 |
| `SettlementPresentationCatalog.en-US.json` | 结算文案 |
| `QuestCatalog.en-US.json` | 任务目标 |
| `PermanentUpgradeCatalog.en-US.json` | 永久升级分类和升级项 |
| `StorySequence.en-US.json` | 剧情和对话 |

JSON patch 的基本格式：

```json
{
  "language": "en-US",
  "domain": "QuestCatalog",
  "patches": {
    "tutorial_open_backpack": {
      "text": "Open the backpack and check your situation"
    }
  }
}
```

你通常只改最里面的文字：

```json
"text": "Open the backpack and check your situation"
```

不要改这些字段：

| 字段 | 为什么别改 |
| --- | --- |
| `language` | 用来判断这个文件属于哪种语言 |
| `domain` | 用来判断这个补丁属于哪个系统 |
| `patches` 下面的 ID | 用来找到原始数据 |

## 任务文本示例

文件：

`Assets/Data/Localization/JsonPatches/en-US/QuestCatalog.en-US.json`

示例：

```json
"tutorial_enter_teleporter": {
  "text": "Step into the book portal"
}
```

可以改 `text`，不要改 `tutorial_enter_teleporter`。

## 剧情和对话示例

文件：

`Assets/Data/Localization/JsonPatches/en-US/StorySequence.en-US.json`

剧情按原始资源分组：

| ID | 对应内容 |
| --- | --- |
| `Assets/Data/Story/Introduction` | 新档开场叙事 |
| `Assets/Data/Story/DialogTest` | Main 场景开场对话 |

示例：

```json
{
  "speakerId": "book",
  "displayName": "Book",
  "displayMode": "replace",
  "text": "Use me well."
}
```

可以改：

| 字段 | 含义 |
| --- | --- |
| `displayName` | 说话人显示名 |
| `text` | 对话正文 |

不要改：

| 字段 | 原因 |
| --- | --- |
| `speakerId` | 内部说话人 ID |
| `displayMode` | 显示方式，通常由程序定 |

## Hint / 指南示例

文件：

`Assets/Data/Localization/JsonPatches/en-US/HintCatalog.en-US.json`

可翻译字段通常是：

| 字段 | 含义 |
| --- | --- |
| `title` | 分类名或条目名 |
| `content` | 正文 |

如果正文需要换行，写 `\n`，不要在引号中直接回车。

示例：

```json
"content": "Move with WASD.\nPress E to interact."
```

## 结算文案示例

文件：

`Assets/Data/Localization/JsonPatches/en-US/SettlementPresentationCatalog.en-US.json`

结算文案里可能有 `{waves}` 和 `{bosses}`：

```json
"victoryResultTemplate": "You defeated {waves} enemy waves and {bosses} bosses."
```

这些占位符必须保留，因为游戏会把它们替换成真实数字。

## 永久升级示例

文件：

`Assets/Data/Localization/JsonPatches/en-US/PermanentUpgradeCatalog.en-US.json`

常见可改字段：

| 字段 | 含义 |
| --- | --- |
| `title` | 分类标题或升级项名称 |
| `description` | 升级说明，如果原始数据里有这个字段 |

不要改升级项 ID、消耗、等级、效果类型，除非你正在做数值配置并且知道这些字段含义。

## Token 和 Enemy 怎么本地化

Token 和 Enemy 是 Unity Inspector 里的 `.asset` 资源，不主要靠 JSON patch。

### Token

路径：

`Assets/Data/BulletTokens`

现在 Token 资产里可以填这些 key 字段：

| 字段 | 用途 |
| --- | --- |
| `Display Text Key` | Token 展示名 / 选择界面显示 |
| `Description Key` | Token 说明 |
| `Pickup Display Text Key` | Linked Token 掉落物显示文字 |

填法示例：

```text
token.fire_core.display
token.fire_core.description
```

然后去字符串表里加对应翻译：

```json
"token.fire_core.display": "Fire",
"token.fire_core.description": "A blazing core that burns enemies."
```

如果 key 没填，游戏会继续使用资产里原本的中文字段。

### Enemy

路径：

`Assets/Data/Enemies`

现在 Enemy 资产里可以填这些 key 字段：

| 字段 | 用途 |
| --- | --- |
| `Display Name Key` | 敌人显示名 |
| `Description Key` | 图鉴说明 |

填法示例：

```text
enemy.swarm.display
enemy.swarm.description
```

然后去字符串表里加：

```json
"enemy.swarm.display": "Swarm",
"enemy.swarm.description": "A weak enemy that wins through numbers."
```

## 新增一个语言怎么做

假设要新增日文 `ja-JP`：

1. 复制 `Assets/Data/Localization/StringTables/ui.en-US.json`。
2. 改名成 `ui.ja-JP.json`。
3. 把文件里的 `"language": "en-US"` 改成 `"language": "ja-JP"`。
4. 翻译 `entries` 里的右侧文本。
5. 在 `Assets/Data/Localization/JsonPatches` 下复制一份 `en-US` 文件夹。
6. 改名成 `ja-JP`。
7. 把里面每个文件的 `.en-US.json` 改成 `.ja-JP.json`。
8. 把每个文件里的 `"language": "en-US"` 改成 `"language": "ja-JP"`。
9. 翻译每个 patch 里的显示文字。
10. 请程序同学确认这些新文件已经被 Addressables 标记：
    - 字符串表需要 label `Localization`
    - JSON patch 需要 label `LocalizationJson`

第 10 步很重要；没有 Addressables label，游戏运行时就读不到新语言。

## JSON 编辑检查表

每次提交前检查：

| 检查项 | 正确 |
| --- | --- |
| 引号 | 使用英文双引号 `"` |
| 逗号 | 每一项之间有英文逗号，最后一项后面没有逗号 |
| 换行 | 字符串内部换行写 `\n` |
| 占位符 | `{0}`、`{1}`、`{waves}`、`{bosses}` 都保留 |
| key / id | 左侧 key 和 patch ID 没有被翻译 |
| 文件语言 | 文件名和 `"language"` 一致 |

## 改完怎么验收

最小验收流程：

1. 保存文件。
2. 回到 Unity，等导入完成。
3. 看 Console 是否有红色错误。
4. 从 `StartUp` 场景进入游戏。
5. 检查你改过的界面或剧情。
6. 请测试或程序同学切换到目标语言再看一遍。

重点检查：

| 场景 | 看什么 |
| --- | --- |
| 主菜单 / Profile | 按钮、错误提示、时间显示 |
| Options | 分类、设置项、按键绑定提示 |
| Hint | 分类、条目、正文、敌人图鉴 |
| Token Select | Token 类型和说明 |
| Upgrade | 标题、等级、花费、购买提示 |
| Story / Dialog | 说话人和正文 |
| Settlement | 胜败标题、统计、收益 |

## 常见问题

### 我改了英文文件，游戏还是显示中文

常见原因：

| 原因 | 处理 |
| --- | --- |
| 当前语言不是 `en-US` | 请测试或程序同学切换语言 |
| 文件没有 Addressables label | 请程序同学检查 `Localization` / `LocalizationJson` label |
| key 写错了 | 对照原文件里的 key 或资产里的 key 字段 |
| JSON 格式坏了 | 看 Unity Console 红色错误 |

### 我看到一个中文，但不知道该改哪里

先按这个顺序找：

1. 如果是按钮、弹窗、固定 UI，找 `StringTables/ui.en-US.json`。
2. 如果是剧情、任务、Hint、结算、升级，找 `JsonPatches/en-US`。
3. 如果是 Token 或 Enemy 的名称 / 说明，找对应 `.asset` 的 key 字段，再去字符串表补 key。
4. 如果还是找不到，截图给程序同学确认它是不是还没接入本地化。

### 我可以直接改原始中文 JSON 吗

可以改中文原文内容，但不要把它当作英文翻译入口。原始中文是 fallback，也是中文版本的内容真源之一。英文、日文等其他语言优先走 `Assets/Data/Localization`。

### Token 的单个汉字需要翻译吗

默认不强制。Token / Enemy 的单字 glyph 可以继续作为玩法视觉符号。需要翻译的是展示名、说明、按钮、提示、剧情、任务、结算这类玩家阅读文本。

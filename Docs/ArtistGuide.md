# Lilith 美术资源替换指南

这份文档面向美术协作者，目标是把游戏里的临时美术资源逐步替换成正式资产，同时尽量不破坏当前可玩流程。

如果你只想先开始做，请优先看“替换优先级”和“替换时不要动的东西”。

## 当前游戏系统总览

| 系统 | 玩家看到什么 | 美术主要负责什么 |
| --- | --- | --- |
| 启动与开场 | `StartUp` 场景进入菜单、开场 storyteller、存档栏位 | 启动菜单、存档选择、开场叙事 UI 的视觉 |
| 起始房间 | `Main` 场景里的 `StartRoomMapRoot`，玩家出生、书、传送门、指南 | 起始房间地面、墙、书、传送门、引导物件 |
| 战斗地图 | `Main` 场景里的 `CombatMapRoot`，进入战斗后生成地图和障碍 | 地面、墙、障碍、战斗空间氛围 |
| 玩家 | `Player`，当前主体是文字 / 符文表现 | 玩家字形、底座、阴影、粒子 |
| 敌人 | `CharEnemy` prefab，加上不同 `EnemyDefinition` 数据 | 敌人字形、符文底座、阴影、受击反馈、Boss 表现 |
| 子弹与 Token | 文字子弹、掉落 token、背包里的词元 | 子弹符文、尾迹、掉落物表现、词元图形 |
| UI | 主界面、背包、暂停、提示、选择、结算、升级 | UI 皮肤、按钮、面板、图标、排版 |
| 特效 | 玩家粒子、子弹尾迹、爆炸预警、Boss 阶段提示 | 粒子、警示圈、命中特效、状态反馈 |

## Unity 中确认到的主层级

当前 Unity Editor 打开的主场景是 `Assets/Scenes/Main.unity`。

主场景里和美术最相关的对象：

| 场景对象 | 当前作用 | 美术替换建议 |
| --- | --- | --- |
| `StartRoomMapRoot` | 起始房间地图根节点 | 替换起始房间里的地面、墙、书、传送门、装饰 |
| `StartRoomMapRoot/Guide` | 新手引导视觉组 | 可替换为更清晰的引导标识或场景提示物 |
| `StartRoomMapRoot/Book` | 永久升级入口，带交互碰撞 | 可替换书模型，但保留交互组件和 Collider |
| `StartRoomMapRoot/Teleporter` | 进入战斗的传送门 | 可替换传送门模型 / 特效，但保留 `StartRoomBattleTeleporter` |
| `StartRoomMapRoot/GeneratedContent` | 起始房间格子内容 | 谨慎替换，优先通过地图 prefab 统一改 |
| `CombatMapRoot` | 战斗地图根节点 | 替换战斗地图整体地面、墙、障碍视觉 |
| `CombatMapRoot/GeneratedContent` | 战斗地图格子内容 | 谨慎替换，优先通过 `Cell3D.prefab` 统一改 |
| `Player` | 玩家根节点 | 可改视觉子物体，勿删除移动、碰撞、血量组件 |
| `UIRoot` | UI 总根节点 | 一般不直接改场景对象，优先改 UI prefab |

## 替换优先级

建议按这个顺序替换，收益最大，也最不容易踩坑：

1. `Assets/Prefabs/Map/Cell3D.prefab`
2. `Assets/Prefabs/Enemy/CharEnemy.prefab`
3. `Assets/Prefabs/Bullet/CharBullet.prefab`
4. `Assets/Prefabs/Bullet/BulletTokenPickup.prefab`
5. `Assets/Data/BulletVisuals/CharBulletVisualLibrary.asset`
6. `Assets/Prefabs/UI/*.prefab`
7. `Assets/Scenes/Main.unity` 中的 `StartRoomMapRoot/Book` 和 `StartRoomMapRoot/Teleporter`

## 地图资源

### 地图格子

主要 prefab：

| 路径 | 用途 |
| --- | --- |
| `Assets/Prefabs/Map/Cell3D.prefab` | 3D 地图格子，当前包含地面和墙模型 |
| `Assets/Prefabs/Map/Cell.prefab` | 旧 / 基础格子对象 |

`Cell3D.prefab` 当前层级：

| 子物体 | 当前组件 | 替换说明 |
| --- | --- | --- |
| `Cell3D` | `BoxCollider`、`Rigidbody`、`CellData` | 根节点组件不要删 |
| `Cell3D/Model/ground` | 地面模型 | 可替换正式地砖 / 地面模型 |
| `Cell3D/Model/wall` | 墙模型 | 可替换正式墙体 / 障碍模型 |

地面和墙体可以换模型、材质、缩放，但建议保留 `Model/ground` 和 `Model/wall` 这两个名字。项目里有自动绑定逻辑会按这些名字找对象。

### 起始房间和战斗地图

| 场景根节点 | 用途 | 美术关注点 |
| --- | --- | --- |
| `StartRoomMapRoot` | 起始房间，位置大约在 X=3200 附近 | 做成安全、可读、能看懂“书”和“传送门”的空间 |
| `CombatMapRoot` | 战斗区域，运行时会生成布局 | 做成更适合战斗辨识的地面 / 墙 / 障碍 |

当前墙体对象使用 `Wall` Tag 和 `Wall` Layer。墙体会参与相机遮挡淡出，所以正式墙体必须能被射线打到。

墙体替换注意：

| 必须保留 | 原因 |
| --- | --- |
| `Wall` Tag | 相机遮挡系统用它判断墙体 |
| `Wall` Layer | 瞄准、碰撞和遮挡逻辑会用 |
| 非 Trigger Collider | 没有 Collider 时，相机遮挡淡出不会命中墙体 |

如果新墙体没有 Collider，可以使用菜单工具：

`Tools/Lilith/Wall Collider/Add Missing Colliders In Open Scenes`

## 角色和敌人

### 玩家

当前场景对象：`Player`

玩家根节点上的移动、刚体、碰撞、攻击、血量、背包等组件不要删除。可替换的主要是视觉子物体：

| 子物体 | 用途 | 可怎么改 |
| --- | --- | --- |
| `Player/GroundShadow` | 玩家脚下阴影 | 换 Sprite、颜色、大小 |
| `Player/Text/Glyph` | 玩家字形显示 | 改字体、颜色、字号、材质 |
| `Player/FloatingParticles` | 玩家周围粒子 | 换粒子材质、颜色、形状 |

### 敌人

主要 prefab：

`Assets/Prefabs/Enemy/CharEnemy.prefab`

当前层级：

| 子物体 | 用途 | 替换说明 |
| --- | --- | --- |
| `CharEnemy/Text/Glyph` | 敌人字形 | 可改 TMP 字体、材质、颜色 |
| `CharEnemy/RuneBaseCore` | 敌人符文底座 | 可换 Sprite |
| `CharEnemy/GroundShadow` | 敌人阴影 | 可换 Sprite |
| `CharEnemy/Collider` | 敌人碰撞 | 不要删；尺寸需要跟视觉对齐 |

敌人外观也可以在每个敌人定义里单独配置：

`Assets/Data/Enemies/*.asset`

常用美术字段：

| 字段 | 含义 |
| --- | --- |
| `Visual / Glyph Text` | 敌人显示的字 |
| `Visual / Glyph Scale Multiplier` | 字形缩放 |
| `Visual / Glyph Color` | 字形颜色 |
| `Visual / Rune Base Sprite` | 符文底座 Sprite |
| `Visual / Rune Base Tint` | 符文底座颜色 |
| `Visual / Ground Shadow Sprite` | 阴影 Sprite |
| `Visual / Ground Shadow Tint` | 阴影颜色 |

当前敌人定义：

| 资产 | 角色定位 |
| --- | --- |
| `群.asset` | 普通群体敌人 |
| `迅.asset` | 快速 / 冲刺敌人 |
| `甲.asset` | 装甲 / 减伤敌人 |
| `召.asset` | 召唤敌人 |
| `爆.asset` | 自爆敌人 |
| `弦.asset` | 远程 / 风筝敌人 |
| `锁.asset` | 控制相关敌人 |
| `愈.asset` | 治疗相关敌人 |
| `Boss_Phase1.asset` | Boss 一阶段 |
| `Boss_Phase2.asset` | Boss 二阶段 |

## 子弹、词元和掉落物

### 子弹

主要 prefab：

`Assets/Prefabs/Bullet/CharBullet.prefab`

当前层级：

| 子物体 | 用途 | 替换说明 |
| --- | --- | --- |
| `CharBullet/Text/Glyph` | 子弹主字形 | 可改字体、材质、颜色 |
| `CharBullet/Text/GlyphShadow` | 字形阴影 | 可改颜色和偏移 |
| `CharBullet/RuneBaseCore` | 核心词底座 | 可换 Sprite |
| `CharBullet/RuneBaseResult` | 结果词叠加图 | 可换 Sprite |
| `CharBullet/Trail` | 子弹尾迹 | 可换材质、宽度、渐变 |
| `CharBullet/Collider` | 命中碰撞 | 不要删 |

子弹视觉库：

`Assets/Data/BulletVisuals/CharBulletVisualLibrary.asset`

它负责按词元类型决定子弹底图、颜色、尾迹和叠加图。

| 配置 | 控制什么 |
| --- | --- |
| `Core Visuals` | Fire / Ice / Thunder / Edge 等核心词的底座、颜色、尾迹 |
| `Result Visuals` | DirectDamage / Explosion / Split / Healing 等结果词的覆盖图、旋转、脉冲 |

### 掉落物

主要 prefab：

`Assets/Prefabs/Bullet/BulletTokenPickup.prefab`

当前层级：

| 子物体 | 用途 |
| --- | --- |
| `BulletTokenPickup/Glyph` | 掉落词元主字 |
| `BulletTokenPickup/Shadow` | 掉落词元阴影 |

根节点的 `SphereCollider` 和 `BulletTokenPickup` 组件不要删除。

## UI 资源

主要 UI prefab 在：

`Assets/Prefabs/UI`

当前重要 UI：

| Prefab | 玩家看到的界面 |
| --- | --- |
| `StartUp UI Prefab.prefab` | 启动菜单 |
| `Profile Popup.prefab` | 存档选择弹窗 |
| `Storyteller Panel.prefab` | 开场滚动叙事 |
| `Dialog UI.prefab` | Main 场景开场对话 |
| `MainUI.prefab` | 主战斗界面、血条、任务提示 |
| `BackPackUI.prefab` | 背包、构筑、攻击预览 |
| `Token Select Panel.prefab` | 进入战斗或波后选择 token |
| `Hint UI.prefab` | 指南 / 帮助 / 敌人图鉴 |
| `PauseUI.prefab` | 暂停菜单 |
| `Settlement UI Screen.prefab` | 战斗结算 |
| `Update UI Screen.prefab` | 永久升级 |
| `Boss Info UI.prefab` | Boss 名称和血条 |

UI 替换规则：

| 可以改 | 不要随便改 |
| --- | --- |
| Image 的 Sprite、Color、材质 | 根节点上的 `Kernel.UI.*UIScreen` 组件 |
| TMP 文本的字体、字号、颜色 | Button、Grid、Layout 组件的引用关系 |
| 面板背景、边框、按钮视觉 | 脚本引用字段里已经拖好的对象 |
| 层级下新增纯视觉装饰 | 删除已有关键节点 |

项目规则：新增文字组件时使用 TMP，不要新增 `UnityEngine.UI.Text`。

## 替换时不要动的东西

这些内容是“可玩逻辑的骨架”，美术替换时请尽量保留：

| 类型 | 不要删 / 不要乱改的例子 |
| --- | --- |
| 脚本组件 | `PlayerPlaneMovement`、`EnemyDefinitionBinder`、`CharBullet`、`BackPackUIScreen` |
| Collider | 玩家、敌人、子弹、掉落物、墙体、传送门、书的 Collider |
| Rigidbody | 玩家、敌人、子弹、地图格子根节点 |
| Tag / Layer | `Player_Object`、`Enemy_Object`、`Wall`、`Ground` 等 |
| 引用字段 | Inspector 里已经拖好的 prefab、Transform、TMP、Image 引用 |
| 地图根节点 | `StartRoomMapRoot`、`CombatMapRoot` |
| UI 根节点 | `UIRoot`、UI prefab 根节点 |

如果需要大改层级，建议先复制 prefab 做备份，再让程序同事确认脚本引用是否还在。

## 美术交付检查清单

每次替换完一个资源，至少检查：

| 检查项 | 通过标准 |
| --- | --- |
| Prefab 能打开 | Inspector 没有 Missing Script |
| 引用没丢 | 关键脚本字段没有变成 None |
| 角色能碰撞 | 玩家 / 敌人 / 子弹 Collider 还在 |
| 墙体能遮挡淡出 | 墙体有 `Wall` Tag、`Wall` Layer、非 Trigger Collider |
| 字能显示 | TMP 文本没有字体缺失 |
| UI 能点击 | Button 还可点，面板能打开关闭 |
| 画面清晰 | 16:9 下主要 UI 不被黑边或边缘裁切 |

## 建议命名

为了后续好找，建议新资源按用途命名：

| 类型 | 命名例子 |
| --- | --- |
| 地面 | `Floor_StartRoom_Stone_A` |
| 墙体 | `Wall_Combat_Ruin_A` |
| 传送门 | `Portal_StartRoom_BookGate_A` |
| 敌人底座 | `EnemyRune_Base_Red_A` |
| 子弹核心 | `BulletCore_Fire_A` |
| UI 背景 | `UI_Panel_Backpack_BG_A` |

## 当前已知坑

| 问题 | 原因 | 处理 |
| --- | --- | --- |
| 墙体挡住玩家但不变透明 | 墙体没有非 Trigger Collider 或没设 `Wall` Tag | 给墙体补 Collider，并保持 `Wall` Tag / Layer |
| 子弹瞄准贴墙异常 | 墙体 Collider 会参与射线 | 当前代码已优先选非墙命中；替换墙后仍要复测 |
| 拖了引用后又消失 | 某些组件有自动绑定逻辑 | 不要随意改关键子物体名字，尤其 `Model/ground`、`Model/wall` |
| UI 文本乱码或不显示 | TMP 字体资产不支持字符 | 换支持中文的 TMP Font Asset |


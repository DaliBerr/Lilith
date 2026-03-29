# Scribe 系统使用说明

`Lonize.Scribe` 是通用序列化基础层，只提供五类能力：

- `Scribe_Values`：基础值类型
- `Scribe_Deep` / `Scribe_Collections.LookDeep<T>`：`IExposable`
- `Scribe_Refs`：交叉引用
- `Scribe_Polymorph`：`List<ISaveItem>`
- `Scribe_Collections.LookJsonList<T>`：普通 JSON DTO 列表

硬规则：

- `Lonize.Scribe` 不允许反向引用 `Kernel.*`
- 业务 DTO 必须留在 `Kernel` 或更上层模块
- 需要业务语义时，在业务层写 adapter，不要给 `Lonize.Scribe` 增加专用重载

## 初始化

基础 codec 由 `ScribeBootstrap.InitializeDefaults()` 注册：

- `bool`
- `int`
- `float`
- `string`
- `long`

业务层只负责额外注册：

- `ISaveItem` 白名单
- Unity/游戏侧 codec，例如 `DictStrEnumInt32Codec<KeyCode>`

## 何时使用哪种 API

### 1. 基础值

```cs
public void ExposeData()
{
    Scribe_Values.Look("hp", ref HP, 100);
    Scribe_Values.Look("name", ref Name, string.Empty);
}
```

### 2. Deep 对象

适用于实现了 `IExposable` 的嵌套对象。

```cs
public class SavePlayerStats : IExposable
{
    public int Level;

    public void ExposeData()
    {
        Scribe_Values.Look("level", ref Level, 1);
    }
}

public void ExposeData()
{
    Scribe_Deep.Look("stats", ref Stats);
}
```

### 3. JSON DTO 列表

适用于普通 DTO，不要求实现 `IExposable`。

```cs
public class ItemSnapshot
{
    public string Id;
    public int Count;
}

public void ExposeData()
{
    Scribe_Collections.LookJsonList("items", ref Items);
}
```

### 4. 多态存档条目

根存档通常通过 `ISaveItem` 清单管理。

```cs
public class SavePlayerInfo : ISaveItem
{
    public string TypeId => "PlayerInfo";
    public int HP;

    public void ExposeData()
    {
        Scribe_Values.Look("hp", ref HP, 100);
    }
}
```

别忘记在业务层注册：

```cs
PolymorphRegistry.Register<SavePlayerInfo>("PlayerInfo");
```

## 常见注意事项

- `TypeId` 必须稳定且唯一，否则读取时会丢失条目
- 保存前先清理或重建 `PolySaveData.Items`，避免重复写入旧条目
- `LookDeep<T>` 只给 `IExposable` 用；普通 DTO 列表请用 `LookJsonList<T>`
- `Lonize.Scribe` 不直接支持 Unity `Vector3` 这类类型；要么拆成基础值，要么在业务层注册 codec
- 当前项目开发期不兼容旧存档；字段类型或保存版本变化后，旧档应直接拒绝读取

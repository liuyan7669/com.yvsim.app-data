# App Data

轻量的全局运行数据容器，提供数据存入、取出、修改、重置和 JSON 转换。数据跨场景保留，JSON 的保存与传输由具体项目自行处理。

## 功能

- `Set<T>` 存入数据，或用新对象整体替换同类型数据。
- `Get<T>` 取出当前对象，直接修改字段；不存在时返回 `null`。
- `Contains<T>` 查询是否已经存入某类数据。
- `Reset<T>` 重置一类已有数据，`ResetAll` 重置全部已存入类型。
- `ToJson` 同步导出、`ToJsonAsync` 协程分帧导出全部数据，`FromJson` 同步还原、`FromJsonAsync` 协程分帧还原，失败时不部分覆盖。
- 支持普通数据类中的可序列化字段、一维数组、`List<T>`，并保留空值。
- 使用 Inspector Groups 统一显示数据、类型说明和添加/移除操作，支持编辑器撤销。
- 用 `[AppDataMenu]` 明确选择可从 Inspector 添加的类，不影响代码存取。

## 安装

当前源码版本为 `1.0.0`，最低 Unity 2021.3，与 Inspector Groups 的最低版本一致。

### 本地源码

先安装 `com.yvsim.inspector-groups` `1.0.0`，再在 **Window > Package Manager > + > Add package from disk...** 中选择本包的 `package.json`。
也可将两个完整包目录（含 `.meta`）放入项目 `Packages/`，作为 embedded package 使用；本包目录名为 `com.yvsim.app-data`。

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| `com.yvsim.inspector-groups` | `1.0.0` | 统一 Inspector 绘制、分组和隐藏 Script 行 |
| `com.unity.modules.jsonserialize` | `1.0.0` | JSON 转换 |
| `com.unity.modules.imgui` | `1.0.0` | 编辑器菜单与面板使用的 IMGUI 类型 |

基础示例另外需要 Unity UI（`com.unity.ugui`）；核心运行数据功能不依赖 Unity UI。

### Git 安装

GitHub 仓库为 `liuyan7669/com.yvsim.app-data`。可在 **Add package from git URL...** 中使用：

```text
https://github.com/liuyan7669/com.yvsim.app-data.git#main
```

先通过本地源码或已配置的 Registry 安装 Inspector Groups。固定版本安装应在对应标签创建后使用 `#v1.0.0`，不要把 `main` 当作不可变版本。

### OpenUPM

OpenUPM 安装须在完成登记与发布后使用，上传 Git 源码不代表已经完成 OpenUPM 发布。`.github/workflows/openupm.yml` 与其他公开 Yvsim 包一致，仅支持手动运行，且要求在已有发布标签上执行。登记与发布需另行完成，本地整理不会自动发布。

## 创建数据容器

通过菜单栏或 Hierarchy 右键的 `Yvsim/框架/创建 AppData` 创建专用根物体。
场景中只保留一个 AppData；业务脚本放在其他物体上，不需要绑定容器引用，数据类也不需要挂到物体上。

AppData 在 `Awake` 初始化。业务从 `Start` 或之后在 Unity 主线程使用，本类不额外检测线程。尚未初始化时调用会抛出异常。

## 基本用法

下面是普通业务脚本的完整用法，场景中需要先放置 AppData：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Yvsim.AppData;

[Serializable]
[AppDataMenu] // 仅在需要从 Inspector 添加时使用，可以省略。
public sealed class Progress
{
    [Tooltip("已经完成的步骤数量。")]
    public int completedSteps;

    [Tooltip("每次操作的得分。")]
    public List<int> scores = new List<int>();
}

public sealed class AppDataExample : MonoBehaviour
{
    private void Start()
    {
        AppData.Set(new Progress());

        Progress progress = AppData.Get<Progress>();
        if (progress != null)
        {
            progress.completedSteps = 3;
            progress.scores.Add(10);
        }

        string json = AppData.ToJson();
        AppData.Reset<Progress>();
        AppData.FromJson(json);

        Debug.Log(AppData.Get<Progress>().completedSteps); // 3
    }
}
```

| API | 用途与边界 |
| --- | --- |
| `Set<T>(value)` | 保管传入对象本身，不复制；同类型再次 Set 会整体替换 |
| `Get<T>()` | 返回当前对象，可直接修改字段，不需要再次 Set；不存在时返回 null |
| `Contains<T>()` | 返回该具体类型是否存在，不创建数据 |
| `Reset<T>()` | 用 `new T()` 重置一类已有数据；未存入时跳过 |
| `ResetAll()` | 重置全部已有类型；全部新对象创建成功后才一起替换 |
| `ToJson()` | 返回包含全部当前数据的 JSON，不写入文件 |
| `ToJsonAsync(frameInterval = 1, chunkSize = 32)` | 分批导出，返回可等待、可读取进度和错误的操作对象 |
| `FromJson(json)` | 同步还原已知类型；全部检查成功后才合并替换 |
| `FromJsonAsync(json, frameInterval = 1, chunkSize = 32)` | 分批解析与还原，返回可等待的操作对象，成功后才整体提交 |

要整体换成新对象时使用 `AppData.Set(new Progress { completedSteps = 5 })`。
Set 替换、重置和 FromJson 后，请重新 Get；旧引用仍指向原来的对象，不会自动指向新对象。
重置使用数据类的字段初始值和公开无参构造函数，不是读档，也不会恢复 Inspector 中手动填写过的值。

## 支持的数据类型

Set 接收带 `[Serializable]` 的具体、非泛型普通数据类，泛型 `T` 必须与对象实际类型一致。
不能直接存入 `null`、字符串、数组、List/Dictionary 等泛型对象、委托，以及 GameObject、Component、ScriptableObject 等 Unity 对象。值类型不能直接作为 `T`，可放在数据类字段中。

这不限制数据类里的 List 字段。以下方式支持：

```csharp
[Serializable]
public sealed class EquipmentData
{
    public List<int> numbers = new List<int>();
    public List<string> names = new List<string>();
    public List<Progress> progress = new List<Progress>();
}
```

存入的是 `AppData.Set(new EquipmentData())`，不是 `AppData.Set(new List<int>())`。列表元素仍须符合序列化要求。
Inspector 有简要类型说明；Set 的参数异常会列出泛型 T、实际类型、具体原因和支持范围，便于定位错误。

## Inspector 添加数据菜单

数据类需要同时带 `[Serializable]` 和 `[AppDataMenu]`，并且公开、非抽象、非泛型，具有公开无参构造函数，才会出现在“添加数据”菜单中。
已添加的类型在菜单中保持可见但禁用。移除操作支持撤销；编辑模式修改后请保存场景或 Prefab。

AppDataMenu 只控制菜单显示，不是 Set、Get 或 JSON 转换的前提，也不要求数据类继承基类或实现接口。
子类需要单独添加该标记；移除标记不会删除已经存入的数据。

## 分帧导出 JSON

数据少时使用 `ToJson()`：在当前帧检查并直接导出全部数据，不再还原一次进行比较。
数据多时使用 `ToJsonAsync()`：由 AppData 自己运行协程，不使用线程或 async/await，适用于 WebGL、PC 和安卓。

```csharp
using System.Collections;
using UnityEngine;
using Yvsim.AppData;

// 写在业务 MonoBehaviour 中，通过 StartCoroutine(Export()) 调用。
private IEnumerator Export()
{
    var operation = AppData.ToJsonAsync(frameInterval: 1, chunkSize: 32);
    yield return operation;
    if (operation.Error != null)
    {
        Debug.LogException(operation.Error);
        yield break;
    }
    string json = operation.Json; // 交给项目自己的保存或发送逻辑。
}
```

| 参数 | 含义 |
| --- | --- |
| `frameInterval` | 两批处理之间相隔多少帧，至少为 1，默认 1。1 表示下一帧继续，2 表示隔两帧继续；不是秒数，不受 timeScale 影响 |
| `chunkSize` | 每批最多执行多少份小工作，至少为 1，默认 32。字段、容器元素遍历和每段最多 128 字符的文本写入都参与计数；同一个元素可能需要多份工作。不是字节数、根数据类数量或毫秒预算 |

参数小于 1、AppData 未初始化或未启用时，调用会直接抛出异常。数据处理失败会结束操作并记录 `Error`，不会把半份 JSON 当成结果。

操作对象提供 `IsDone`、`Progress`、`Json` 和 `Error`，也能直接 `yield return` 等待。
要显示进度，可在 `while (!operation.IsDone)` 中读取 `operation.Progress`，每轮 `yield return null`。
Progress 按已处理完的数据类型推进，最后完成字符串时达到 1；单个大类型处理中可能暂时不变，它不是耗时百分比，也不代表导出成功。结束后必须检查 Error。

块越小，单批负担通常越轻；间隔越大，总耗时越长，但不能降低某一批本身的耗时。
默认值偏保守，仍需根据实际数据与目标设备调整，不能保证所有设备完全无卡顿。
大 List、嵌套字段和长字符串都会参与分帧；类型检查及最终完整字符串的分配仍是同步步骤。分帧不会消除 JSON 本身的内存占用或 GC 开销。

导出从下一帧开始，期间不要修改字段或列表，也不要 Set、Reset、ResetAll、FromJson 或 FromJsonAsync，否则结果可能混入不同时刻的数据。
不要重复启动多个导出来维持流畅度；组件停用、物体停用或销毁会结束尚未完成的操作，Error 为 OperationCanceledException。
同步与分帧导出共用同一套字段写入逻辑，结果均为紧凑 JSON，可交给 FromJson，也兼容此前带缩进的导出格式。
只转换字段，不读取属性或执行自定义序列化回调；数据请直接放在受支持的字段中。

## 分帧还原 JSON

数据少时使用 `FromJson(json)`，当前帧完成；数据多时使用 `FromJsonAsync(json, frameInterval: 1, chunkSize: 32)`。
两个入口共用同一套解析、填充与检查规则。分帧入口不会把整份数据交给 JsonUtility.FromJson 一次解析；外层 JSON、内层数据、长文本、列表元素、字段填充、空值恢复和最终检查都分批执行。

```csharp
// 写在业务 MonoBehaviour 的协程中。
var operation = AppData.FromJsonAsync(json, frameInterval: 1, chunkSize: 32);
yield return operation;
if (operation.Error != null)
{
    Debug.LogException(operation.Error);
    yield break;
}
Progress restored = AppData.Get<Progress>(); // 成功后重新取得新对象。
```

`frameInterval` 与 ToJsonAsync 相同：两批相隔的帧数，默认 1、至少为 1，不是秒数，不受 timeScale 影响。
`chunkSize` 同样默认 32、至少为 1：每批最多执行的小工作数量，字符读取每段最多 128 字符，解析/填充一个元素可能需要多份工作；不是字节数、根数据类数量或毫秒预算。

返回相同的 AppDataJsonOperation，可读取 IsDone、Progress 和 Error；导入不生成字符串，Json 始终为 null。
导入进度结合字符读取与字段处理估算，部分检查阶段可能暂时不变；它不是耗时百分比，结束时达到 1 也不代表成功。
无效 JSON（包括 null/空字符串）、未知类型、重复字段/类型、数字范围错误、空值路径错误等通过 Error 返回。两个调度参数小于 1、容器未初始化或未启用时直接抛出异常。

先 Set 各类初始数据，导入仅匹配已知类型；JSON 缺少的类型保留原对象。
全部解析与检查成功后才一起替换。完成前 Get 仍返回原对象，失败、组件/物体停用或销毁时保留原数据，不提交半份结果。
导入期间业务不要修改、Set、重置数据，也不要同时导入或导出；成功后重新 Get。

解析会使用临时结构与新数据对象，峰值内存会包含原数据、JSON 文本、临时解析结果和新对象。
字符串/数组分配、容器扩容、类型检查和创建单个对象仍有同步开销，分帧不能消除 GC 或保证所有设备无卡顿。
为保留缺失字段的既有初始化行为，创建普通对象时只交给 Unity 固定的空 JSON；实际数据不由原生解析器整批读取。无参构造函数不是导入的前提，但存在时仍可能执行其初始化逻辑，避免在其中安排大量工作或修改外部状态。
同步和分帧解析均严格检查 JSON 语法，单份 JSON 最多解析 128 层嵌套，拒绝重复字段；格式版本保持 1，原有紧凑或带缩进的导出均可导入。

## JSON 转换与还原

ToJson 导出全部已存入数据，无需注册存档类型。JSON 包含格式版本、程序集简单名称、完整类型名、各类字段 JSON 与空值位置，不包含文件路径、存档槽位或存储配置。

FromJson 前先由业务 Set 各类初始数据。导入只匹配这些已知类型，不根据外部类型名任意创建对象。
JSON 中有的类型替换当前对象，缺少的类型保留当前数据；未知类型、格式错误或不支持的数据会抛出异常，原容器不被部分覆盖。

JSON 字段支持可序列化的基础类型、枚举、普通嵌套数据对象、一维数组和 List。以下情况不支持：

- 字典、多维数组、直接嵌套的数组/List；需要嵌套集合时用普通数据类包裹。
- Unity 对象引用、object/接口字段、委托、多态实例或带 SerializeReference 的嵌套字段。
- 循环引用、运行时指针、NaN 和 Infinity，以及其他不在支持范围内的字段类型。

这里的 JSON 限制独立于 Unity 用 SerializeReference 保存 Inspector 数据的能力。
程序集名、类名、字段名或结构改变后，旧 JSON 的迁移由业务自行处理。
保存文件、浏览器存储、服务器接口、备份或加密均由项目接收 JSON 后自行实现，本包不提供持久化脚本。

## 示例

安装 Unity UI，在 Package Manager 导入 **Basic Usage**，打开其中的 `AppDataDemo.unity`，运行即可，不需要生成菜单或其他 Yvsim 业务包。

10 个按钮对应 Set 初始存入、Contains、Get 修改、Set 整体替换、Reset<T>、ResetAll、ToJson、FromJson、ToJsonAsync、FromJsonAsync。
示例的 EquipmentData 包含 `List<int>`、`List<string>`、`List<QuizData>`；Get 按钮向每类列表添加一项，画面直接显示内容。

建议按「Get 修改 → ToJson → ResetAll → FromJson」操作，观察列表和元素字段一起还原；也可换成 ToJsonAsync 与 FromJsonAsync，观察分帧流程。
JSON 只放在示例内存中，退出运行后消失。场景使用 Unity 内置字体，不携带字体资源或 TMP 依赖。
详细说明见 [示例 README](Samples~/Basic%20Usage/README.md)。

## 包结构

```text
Runtime/              数据容器、菜单标记和 JSON 转换
Editor/               Groups 面板声明和创建菜单
Samples~/Basic Usage/ 可直接运行的场景与三个简单示例脚本
.github/workflows/    手动 OpenUPM 工作流模板
```

运行程序集不引用 UnityEditor，不依赖其他 Yvsim 业务包；Inspector Groups 是明确声明的面板依赖。

## 许可证

本包代码与示例采用 [MIT License](LICENSE.md)。

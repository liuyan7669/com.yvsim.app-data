# AppData 基础示例

## 打开就能运行

1. 项目需安装 Unity UI（com.unity.ugui），示例用普通 uGUI 按钮和 Unity 内置字体，不需要 TMP 或额外字体资源。AppData 本身不依赖 Unity UI。
2. 在 Package Manager 导入 Basic Usage。
3. 双击同目录的 AppDataDemo.unity，点击运行。

左边点按钮，右边看两类数据和 JSON。启动时自动存入 QuizData 和 EquipmentData；每个按钮对应 AppDataBasicUsage.cs 中一个直接调用 API 的方法。

| 按钮 | 看到的效果 |
| --- | --- |
| 1. Set：存入初始数据 | 两类数据都换成初始对象 |
| 2. Contains：检查是否存在 | 显示两类数据是否已经存入 |
| 3. Get：取出并修改字段 | 答对题数加一、设备检查改为 True，并向三种 List 各添加一项 |
| 4. Set：整体换成新对象 | 答题数据换成总数 20、答对 10；设备数据不变 |
| 5. `Reset<T>`：只重置答题数据 | 答题恢复 10/0；设备数据不变 |
| 6. ResetAll：重置全部数据 | 两类数据都恢复初始值 |
| 7. ToJson：导出当前数据 | 右边显示当前数据的 JSON 字符串 |
| 8. FromJson：还原 JSON | 恢复为上一次导出 JSON 时的数据 |
| 9. ToJsonAsync：分帧导出 | 显示进度，完成后显示 JSON；期间按钮暂时禁用 |
| 10. FromJsonAsync：分帧还原 | 等待全部还原成功后更新数据；期间按钮暂时禁用 |

建议按「4 → 7 → 6 → 8」点击：先换新数据，导出 JSON，重置，再还原。没有导出时点击 FromJson 会提示先生成 JSON。
也可以用「9. ToJsonAsync」代替「7. ToJson」。
JSON 只放在内存中，退出运行后消失；这个示例不保存文件。

## 看代码只看这几处

- AppDataBasicUsage.cs：Start 主动 Set 两类数据；10 个公开方法分别对应 10 个按钮。末尾 ShowState 只更新画面文字。
- QuizData.cs、EquipmentData.cs：普通 Serializable 数据类，不是组件，不需要挂到物体上。
- 场景里的 AppData 物体只放容器；AppDataDemo 物体放业务脚本。Canvas 的按钮已在 Inspector 中绑定公开方法，不需要创建菜单或生成脚本。

Get 返回当前对象，修改字段不需要再次 Set；Set 负责存入或整体替换。
未存入时 Get 返回 null、Contains 返回 false、Reset<T> 跳过。实际业务读取可能不存在的数据时先判空。
FromJson 只还原已经注入的类型，成功后重新 Get；保存和传输 JSON 由项目自己处理。

两类示例数据都带 `[Serializable]` 和 `[AppDataMenu]`。后者只让类出现在 Inspector 的“添加数据”菜单中，不是 Set 的前提；通过代码使用的其他数据类可以不加它。子类需单独标记。

## 数据类里的三种 List

EquipmentData 内直接声明 `List<int>`、`List<string>` 和 `List<QuizData>`，初始分别有 2、2、1 项。QuizData 是带 Serializable 的普通数据类，列表中的每项都能展开编辑。

```csharp
EquipmentData data = AppData.Get<EquipmentData>();
data.listint.Add(3);                              // 添加一个整数
data.liststring.Add("新设备");                   // 添加一段文字
data.listQuizData.Add(new QuizData());             // 添加一个普通数据对象
data.listQuizData[0].correctAnswerCount = 2;        // 修改列表中某个对象的字段
```

不需要再次 Set。运行场景点击「3. Get」，画面和 AppData Inspector 会同时看到列表变化。
再点「7. ToJson → 6. ResetAll → 8. FromJson」，可以检查三个列表和每项字段一起导出、重置、还原。
「5. `Reset<QuizData>`」只重置单独存入的 QuizData，不会改动 EquipmentData 列表里的 QuizData 对象；重置整个 EquipmentData 才会重置它的列表。
不要直接 `AppData.Set(new List<int>())`：存入的是 EquipmentData，List 是它的字段。List 的元素仍须为支持的类型，不直接嵌套 List，也不放 GameObject。

## 协程分帧导出

点击「9. ToJsonAsync」，示例使用 `frameInterval: 1, chunkSize: 32`：每批最多 32 份小工作，两批相隔 1 帧。
字段、列表元素的遍历和每段最多 128 字符的文本写入都计入工作量，一个元素可能需要多份工作。参数不是秒数和字节数；间隔和处理量都至少为 1。

`ExportInFrames()` 展示读取 Progress、等待 IsDone、检查 Error、取得 Json 的完整过程。不显示进度时可直接 `yield return operation`。
示例数据很少，完成会很快；Progress 按完成的数据类型推进，大类型内部处理时可能暂时不变。
完成后使用同一个 FromJson 按钮还原即可。分帧 JSON 为紧凑格式，与同步导出的格式兼容。

导出期间按钮暂时禁用，避免修改数据或重复启动导出；这是示例的交互处理，不是 AppData 自动锁住了业务数据。
业务也应在完成前保持数据不变。块越小通常越流畅、间隔越大总耗时越长；最终字符串分配仍可能有耗时，不能保证所有设备完全无感。

## 协程分帧还原

点击「10. FromJsonAsync」，使用与导出相同的 `frameInterval: 1, chunkSize: 32`。
`ImportInFrames()` 直接 `yield return operation` 等待，结束后检查 Error；成功后 ShowState 重新 Get 更新画面。需要进度时，可像导出一样循环读取 Progress。

先点「3 → 9 → 6 → 10」：修改数据、分帧导出、重置、分帧还原。也可以用同步导出的 JSON 分帧还原，反过来也可以。
还原期间按钮暂时禁用，Get 仍返回原数据；只有全部成功才替换，失败或 AppData 停用/销毁时保留原数据。
参数的单位、默认值与导出一致；字符读取每段最多 128 字符，解析、填充、恢复空值和检查都参与分批。
操作对象的 Json 在导入时始终为 null，导入结果由成功后重新 Get 获得。
分帧不会消除临时内存、字符串/数组分配、容器扩容或单个对象创建的开销，实际参数应按目标设备调整。

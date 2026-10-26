# Sedan01 — 参考图独立建模资产

以 `Art/References/Vehicles/Sedan01_Reference.png` 为唯一车辆造型参考，在 Blender 5.2.3 LTS 中建立。没有读取、复制或修改项目已有占位车辆。原图没有精确尺寸标注，以下尺寸为依据外观比例制定的游戏尺度。

## 交付文件

- `Sedan01.blend`：可编辑建模源文件。`SEDAN01_SOURCE` 中保留车身曲面、门窗、灯具、内饰和轮胎等独立部件；`STUDIO_NOT_EXPORTED` 为摄影棚和相机。
- `Assets/Art/Vehicles/Sedan01/Models/Sedan01_LOD0.fbx`、`LOD1.fbx`、`LOD2.fbx`：三档游戏模型。
- `Assets/Art/Vehicles/Sedan01/Materials/`：11 个 URP/Lit 材质，覆盖银色车漆、橡胶、深灰金属、玻璃、轮毂合金、镀铬、透明灯面、红灯、转向灯、内饰和牌照。
- `Assets/Art/Vehicles/Sedan01/Prefabs/Sedan01.prefab`：已绑定材质和 LODGroup，可拖入 Unity 场景。
- `Renders/`：Blender 正前侧、后侧与侧面预览；Unity URP 预览由编辑器工具生成。
- `build_sedan01.py`：可复现建模、LOD 导出和摄影棚渲染的 Blender 脚本。
- `validate_sedan01.py`、`blender_validation_report.json`、`unity_import_report.json`：资产定向检查及检查记录。

## 造型和细节

银色四门三厢车、五辐轮毂、低车身、前唇、侧裙、后尾翼。车身轮拱具有真实开口，前保险杠进气口和雾灯口由曲面拓扑构成；补充发动机盖压线、车门/油箱盖接缝、门把手、后视镜、独立投射灯、灯具内部光学结构、格栅网、排气管、雨刷及后窗除雾线。四轮含圆润胎肩、胎纹、轮缘、五辐轮毂、轮毂螺母、打孔刹车盘和卡钳；内饰包含前排座椅、后排座椅、头枕、仪表台、方向盘、中控和换挡杆。

## Unity 使用

单位为米；预制体根节点缩放 `(1,1,1)`，原点位于地面，Unity `+Z` 朝前、`+Y` 朝上。尺寸约长 **4.676 m**、含后视镜宽 **2.138 m**、高 **1.476 m**；轴距 **2.700 m**；轮胎半径 **0.325 m**。

| LOD | Unity 导入后三角面 | Unity 顶点（含材质/法线/UV 分割） | MeshRenderer | 材质槽合计 | 屏幕相对高度阈值 |
| --- | ---: | ---: | ---: | ---: | ---: |
| LOD0 | 122,641 | 116,363 | 6 | 27 | 0.60 |
| LOD1 | 48,797 | 55,274 | 6 | 27 | 0.28 |
| LOD2 | 14,096 | 22,322 | 6 | 27 | 0.065 |

低于最后阈值时剔除显示。近景模型适合玩家车辆或少量展示车辆；多车交通应按实际镜头与性能预算调整 LOD 阈值。材质槽合计不等同于最终帧的 draw call 数量，阴影、额外灯光和渲染器设置会影响实际开销。本次没有进行场景帧率基准测试。

每档模型包含 `Body`、`Glass`、`Wheel_FL`、`Wheel_FR`、`Wheel_RL`、`Wheel_RR`。车轮具有独立中心轴，可绕本地 X 轴滚动；接入车辆控制器时应同步三个 LOD 内同名车轮的姿态。下表为预制体根节点下的 Unity 坐标：

| 节点 | X | Y | Z |
| --- | ---: | ---: | ---: |
| Wheel_FL | -0.781 | 0.325 | 1.365 |
| Wheel_FR | 0.781 | 0.325 | 1.365 |
| Wheel_RL | -0.781 | 0.325 | -1.335 |
| Wheel_RR | 0.781 | 0.325 | -1.335 |

车漆使用金属度/光滑度参数；玻璃采用双面 Alpha 混合、关闭深度写入和阴影投射。材质采用几何细节与常量 PBR 参数，没有外部纹理依赖。已有 UV0 为可重复的平面投影，并非独占烘焙图集；后续添加车贴或烘焙 AO/法线时，应单独制作对应 UV。未添加光照贴图 UV，车辆应按动态物体使用。

此预制体为美术资产；驾驶控制器、WheelCollider、碰撞体、车门开合和灯光控制需要在接入具体车辆系统时配置。

## 游戏接入

`RaceGameController` 通过 `RaceCarVisual` 为玩家和对手实例化 `Assets/Resources/Vehicles/RaceSedan.prefab`。这个比赛用预制体是原始 `Sedan01.prefab` 的变体，保留模型、材质和三档 LOD 的引用，运行时加载及游戏构建均可使用；原始美术预制体仍可独立重建。比赛变体将最后一档 LOD 的剔除阈值设为 `0.003`，使两车在 400 米赛道上拉开距离时仍能显示。

玩家使用蓝色、对手使用红色车漆。颜色通过各 LOD 车身的车漆材质槽属性覆盖，不修改共享银色材质或玻璃、轮胎和灯具。模型保持米制尺寸、地面原点和 `+Z` 朝向，车道中心仍为 `X=-2.4` 和 `X=2.4`。全部 12 个车轮节点根据各自车辆的行驶距离和 `0.325 m` 半径滚动，重开比赛时复位；车辆实例跟随控制器销毁。比赛继续使用现有数值模拟，不依赖 WheelCollider。

接入后在 Unity 6000.6.7f1 中执行 `RaceGamePlayModeTests`：完整比赛与重开、模型位移与车轮复位两项通过；场景与材质检查的颜色断言加入浮点容差后，定向复测通过。三项检查覆盖双方模型、三档 LOD、独立车漆、接地尺寸、车轮轴向、资源加载和实例销毁。本次未运行无关 Runtime 测试，也未打包游戏。

## 重建与验证

在项目根目录的 PowerShell 中运行：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python 'Art/Models/Vehicles/Sedan01/build_sedan01.py'
```

随后在 Unity 中使用 `Tools > Art > Build Sedan01 Reference Model` 重建专属材质、FBX 材质映射和预制体；此工具只处理 Sedan01 新资产，保留既有资源 GUID。使用 `Tools > Art > Render Sedan01 URP Previews` 生成 Unity 预览。

美术制作阶段检查记录：Blender 源网格与有效材质节点、三档 FBX 回读、有限顶点坐标、单位/包围尺寸、四轮世界轴心、UV0、六网格结构和递减 LOD 面数检查通过；Unity 6000.6.7f1 下模型导入、URP 材质映射、车头朝向、尺寸、车轮轴心、LOD 配置和预制体生成检查通过；已生成 Unity URP 前后视角与 LOD2 渲染，确认材质正常显示。该美术制作阶段没有运行 Runtime/PlayMode 游戏逻辑测试；后续游戏接入测试见上节。

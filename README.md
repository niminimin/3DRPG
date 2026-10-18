# 3DRPG

Unity 3D RPG 学习工程（第三人称点击移动 + 战斗动画 + 遮挡描边）。

## 环境

| 项 | 版本 |
|---|---|
| Unity | 2022.3.62f3c1 |
| 渲染管线 | URP 14.0.12（`Assets/Settings/URP-HighFidelity`） |
| 输入 | 旧版 Input Manager（`Input.GetMouseButtonDown`） |

工程内嵌了两个包（不走 Package Manager 在线解析）：

- `Packages/com.unity.progrids` (3.0.3-preview.6) —— 网格吸附
- `Packages/com.unity.settings-manager` (1.0.3) —— ProGrids 的依赖

## 目录结构

```
Assets/
├── Art Assets/          商店素材（角色、树木、地面）
├── Scripts/
│   ├── Managers/        MouseManager（单例、鼠标射线、点击事件）
│   └── Player/          PlayerController（订阅点击事件，驱动 NavMeshAgent）
├── Material/            URP 材质
│   └── Shader Graph/    Occlusion Shader（角色被遮挡时的轮廓）
├── Prefabs/             自制预制体
├── Scenes/              SampleScene（主场景）
└── Settings/            URP 资产与 Renderer 配置
```

## 当前进度

- 地形与植被布置（ProBuilder / Polybrush）
- 点击地面移动：`MouseManager` 射线检测 + `OnMouseClicked` 事件 → `PlayerController` 设置 `NavMeshAgent.destination`
- 角色被遮挡时显示轮廓：`Occlusion Shader`（Unlit，Depth Test = Greater）+ `URP-HighFidelity-Renderer` 上的 `Render Objects` 特性（Event = After Rendering Opaques，Layer = Player，Override Material）
- 角色使用素材自带的 Humanoid 骨骼与动画片段

## 操作

| 操作 | 说明 |
|---|---|
| 鼠标左键点地面 | 角色移动到点击位置（仅 `Ground` 标签的碰撞体响应） |

## 打开方式

1. `git clone` 本仓库
2. Unity Hub → Add → 选择工程目录 → 用 **2022.3.62f3c1** 打开
3. 首次打开会重建 `Library/`（需几分钟），属正常现象

## 说明

- `Art Assets` 内含商店付费素材（Dungeon Mason / JustCreate），**本仓库保持私有**，请勿公开分发。
- 仓库已配置 `UnityYAMLMerge` 作为 `.unity`/`.prefab`/`.asset` 的合并驱动；换行策略为 YAML 用 LF、C# 用 CRLF。

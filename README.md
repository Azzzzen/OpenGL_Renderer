# OpenGL Renderer

一个基于 OpenGL 4.1 的实时渲染器，实现了前向渲染（Phong 光照）与延迟渲染（SSAO）两条渲染管线，使用 C++11 构建，依赖 GLFW、GLAD、ASSIMP、GLM 等主流图形开发库。

---

## 目录

- [功能特性](#功能特性)
- [项目结构](#项目结构)
- [代码架构](#代码架构)
- [渲染管线](#渲染管线)
- [依赖与环境](#依赖与环境)
- [构建方法](#构建方法)
- [使用说明](#使用说明)
- [资源说明](#资源说明)

---

## 功能特性

### 已实现

- **Phong 光照模型** — 逐片元的环境光 + 漫反射 + 镜面反射计算
- **延迟渲染（Deferred Rendering）** — 将几何信息写入 G-Buffer，分离几何与光照计算
- **SSAO（屏幕空间环境光遮蔽）** — 64 样本半球采样，结合法线贴图噪声随机化，提升接触遮蔽的真实感
- **SSAO 模糊** — 4×4 Box Filter 后处理，消除 SSAO 采样噪点
- **模型加载** — 通过 ASSIMP 支持 OBJ / FBX / DAE 等格式，自动计算切线/副切线
- **多通道纹理映射** — 漫反射贴图、镜面贴图、法线贴图、高度贴图
- **FPS 摄像机** — 支持键盘移动、鼠标观察及滚轮缩放

### 基础设施已就绪（尚未完整接入渲染流程）

- IBL（基于图像的光照）— HDR 环境贴图与辐照度贴图已准备
- 骨骼动画 — 顶点结构中预留了骨骼 ID 与权重字段

---

## 项目结构

```
OpenGL_Renderer/
├── opengl.cpp              # 前向渲染示例（Phong 光照 + 模型加载）
├── ssao.cpp                # 延迟渲染示例（G-Buffer + SSAO）
├── shader.h                # Shader 封装类
├── camera.h                # FPS 摄像机控制器
├── mesh.h                  # 网格数据结构与绘制
├── model.h                 # 模型加载器（基于 ASSIMP）
├── stb_image.h             # 图像加载库（单头文件）
├── shader.vs / shader.fs   # 前向渲染 GLSL 着色器
├── ssao_geometry.vs/.fs    # 几何阶段着色器（写入 G-Buffer）
├── ssao.vs / ssao.fs       # SSAO 计算着色器
├── ssao_blur.fs            # SSAO 模糊着色器
├── ssao_lighting.fs        # 延迟光照着色器
├── CMakeLists.txt          # CMake 构建配置
├── scenes/                 # 3D 模型与环境资源
│   ├── nanosuit/           # Nanosuit 角色模型及纹理
│   ├── diablo3_pose/       # Diablo III 角色模型及纹理
│   ├── envmaps/            # HDR 环境贴图（IBL 用）
│   ├── space-ship.obj      # 太空飞船模型（SSAO 示例使用）
│   └── landingpad.obj      # 着陆台模型
└── .vscode/                # VSCode 编辑器配置
```

---

## 代码架构

### Shader 类（`shader.h`）

负责 GLSL 着色器程序的加载、编译与管理。

```cpp
Shader shader("shader.vs", "shader.fs");
shader.use();
shader.set("lightColor", glm::vec3(1.0f));
shader.set("model", modelMatrix);
```

- 构造函数从文件读取顶点/片元着色器源码并完成编译链接
- 提供模板方法 `set(name, value)` 统一设置各类型 Uniform（`bool`、`int`、`float`、`vec2~4`、`mat2~4`）
- 编译/链接错误会输出详细日志

---

### Camera 类（`camera.h`）

FPS 风格摄像机，基于 Euler 角（偏航/俯仰）维护视角方向。

| 操作 | 映射 |
|------|------|
| 前/后/左/右移动 | `W` / `S` / `A` / `D` |
| 上/下移动 | `Space` / `Ctrl` |
| 鼠标拖动 | 自由环视（偏航 + 俯仰，俯仰限幅 ±89°）|
| 滚轮 | 视野角缩放（FOV 1° ~ 45°）|

关键方法：
- `GetViewMatrix()` — 返回由 `glm::lookAt` 构建的视图矩阵
- `ProcessKeyboard(direction, deltaTime)` — 帧率无关的位移更新
- `ProcessMouseMovement(xoffset, yoffset)` — 偏航/俯仰更新
- `ProcessMouseScroll(yoffset)` — FOV 缩放

---

### Mesh 类（`mesh.h`）

封装单个网格的顶点数据与 OpenGL 缓冲区对象（VAO / VBO / EBO）。

顶点结构体包含：

```cpp
struct Vertex {
    glm::vec3 Position;
    glm::vec3 Normal;
    glm::vec2 TexCoords;
    glm::vec3 Tangent;
    glm::vec3 Bitangent;
    int       BoneIDs[MAX_BONE_INFLUENCE];   // 骨骼索引（预留）
    float     Weights[MAX_BONE_INFLUENCE];   // 骨骼权重（预留）
};
```

- `setupMesh()` — 初始化并绑定 VAO / VBO / EBO，配置顶点属性指针
- `Draw(Shader &shader)` — 绑定纹理（漫反射/镜面/法线/高度），调用 `glDrawElements` 完成绘制

---

### Model 类（`model.h`）

基于 ASSIMP 的模型加载器，支持 OBJ / FBX / DAE 等主流格式。

加载流程：

```
loadModel()
  └─ ASSIMP importer.ReadFile()
       └─ processNode()  ← 递归处理节点树
            └─ processMesh()  ← 将 ASSIMP 网格转换为内部 Mesh
                 └─ loadMaterialTextures()  ← 加载并缓存纹理
```

特性：
- 自动生成切线/副切线（`aiProcess_CalcTangentSpace`）
- 纹理缓存去重，避免重复加载同一张图像
- `Draw(Shader &shader)` 遍历所有子网格并调用 `Mesh::Draw`

---

## 渲染管线

### 管线一：前向渲染（`opengl.cpp`）

```
顶点属性 (Position, Normal, TexCoords)
    │
    ▼ shader.vs
Transform to world space
    │
    ▼ shader.fs
Phong = Ambient + Diffuse + Specular
× DiffuseTexture
    │
    ▼ 帧缓冲
最终图像
```

使用 Phong 光照模型，单点光源，直接输出到默认帧缓冲。

---

### 管线二：延迟渲染 + SSAO（`ssao.cpp`）

```
Pass 1 — 几何阶段 (ssao_geometry.vs/fs)
    写入 G-Buffer:
    ├── gPosition  (视空间位置)
    ├── gNormal    (视空间法线)
    └── gAlbedo    (漫反射颜色)

Pass 2 — SSAO 计算 (ssao.vs/fs)
    ├── 64 样本半球核心
    ├── 切线空间法线映射 + 噪声纹理随机化
    ├── 遮蔽因子计算（深度比较 + 范围衰减）
    └── 输出单通道遮蔽值

Pass 3 — SSAO 模糊 (ssao_blur.fs)
    └── 4×4 Box Filter 平滑遮蔽图

Pass 4 — 延迟光照 (ssao_lighting.fs)
    ├── 从 G-Buffer 重建光照
    ├── 漫反射 + 镜面反射 + 距离衰减
    └── 环境光 × (1 - SSAO遮蔽) → 最终颜色
```

---

## 依赖与环境

| 库 | 用途 | 管理方式 |
|----|------|---------|
| **GLFW 3** | 窗口创建与输入处理 | vcpkg |
| **GLAD** | OpenGL 函数加载器（目标 OpenGL 4.1 Core）| vcpkg |
| **GLM** | 数学库（向量、矩阵、变换）| vcpkg（仅头文件）|
| **ASSIMP** | 3D 模型导入 | vcpkg |
| **stb_image** | 图像加载（PNG / JPG / BMP / HDR 等）| 单头文件，已内嵌 |

> **操作系统**：Windows（CMakeLists.txt 中 vcpkg 路径指向 Windows 默认安装位置）  
> **编译器**：MSVC（Visual Studio 2022）  
> **OpenGL 版本**：4.1 Core Profile

---

## 构建方法

### 前置条件

1. 安装 [Visual Studio 2022](https://visualstudio.microsoft.com/)（含 C++ 桌面开发工作负载）
2. 安装 [vcpkg](https://github.com/microsoft/vcpkg)，并记录其安装路径（如 `C:/vcpkg` 或 `C:/Users/<用户名>/vcpkg`）
3. 根据你的实际 vcpkg 安装路径，修改 `CMakeLists.txt` 中对应的路径变量，然后安装依赖：

```bash
vcpkg install glfw3:x64-windows
vcpkg install glad:x64-windows
vcpkg install glm:x64-windows
vcpkg install assimp:x64-windows
```

### CMake 构建

```bash
# 在项目根目录
cmake -B build -S . -A x64
cmake --build build --config Release
```

### VSCode 构建（可选）

打开项目文件夹后，使用 `Ctrl+Shift+B` 触发预配置的 MSVC 编译任务（`.vscode/tasks.json`）。

---

## 使用说明

构建完成后运行 `OpenGLProject.exe`，将弹出 800×600 的 OpenGL 渲染窗口。

### 摄像机控制

| 按键 / 操作 | 功能 |
|------------|------|
| `W` / `S` | 前进 / 后退 |
| `A` / `D` | 左移 / 右移 |
| `Space` / `Ctrl` | 上升 / 下降 |
| 鼠标移动 | 环视 |
| 滚轮 | 缩放视野（FOV） |
| `Esc` | 退出程序 |

---

## 资源说明

| 资源 | 路径 | 说明 |
|------|------|------|
| Nanosuit 模型 | `scenes/nanosuit/` | OBJ + 漫反射/法线/镜面纹理，见 `LICENSE.txt` |
| Diablo III 模型 | `scenes/diablo3_pose/` | OBJ + 漫反射/法线/镜面/自发光纹理，见 `readme.txt` |
| 太空飞船 | `scenes/space-ship.obj` | 用于 SSAO 示例 |
| 着陆台 | `scenes/landingpad.obj` | 用于 SSAO 示例 |
| HDR 环境贴图 | `scenes/envmaps/` | IBL 用途，含辐照度图与多级镜面反射预计算图 |

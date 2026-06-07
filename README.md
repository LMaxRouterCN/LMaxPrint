
# LMaxPrint

一款基于 .NET 8 和 WinForms 构建的桌面应用程序

## 速览

<details>

<summary>点击展开图集</summary>

![alt text](<README/屏幕截图 2026-06-07 140517.png>)
![alt text](<README/屏幕截图 2026-06-07 140605.png>)
![alt text](<README/屏幕截图 2026-06-07 140726.png>)
</details>

## 🌟 核心特性

- **🎯 精准物理映射 (HUD)**
  抛弃视觉错觉，状态栏实时显示图像在纸张上的真实物理尺寸（精确到 0.1 厘米）。所见即所印，尺子量出来的尺寸与屏幕显示分毫不差。
- **🗺️ CAD 级双坐标系架构**
  彻底解耦“逻辑世界”与“视图相机”。支持画布无限平移缩放（`Ctrl+滚轮` / `中键拖拽`），图像如同印在纸上一般完美跟随，告别视觉撕裂。
- **✂️ 锋利裁剪引擎**
  以蓝色虚线框为绝对物理边界。超出边界的像素在内存渲染阶段被物理切断，彻底杜绝老式打印机驱动因矩阵越界导致的“灰块崩溃”或“全页拉伸”Bug。
- **🛡️ 毒图片防御系统**
  内置图像洗白管线：自动读取并纠正手机照片的 EXIF 旋转标签；强制转换为纯净的 32bpp ARGB 格式。完美规避 GDI+ 解析 CMYK 或损坏 TIFF 时引发的 `OutOfMemoryException`。
- **🎨 动态主题与持久化**
  内置可视化主题配置面板，支持自定义窗口、按钮、纸张及虚线框的颜色与粗细。窗口尺寸与主题配置自动序列化为 `theme.json`，下次启动完美恢复。
- **📦 傻瓜式单文件部署**
  采用极限压缩技术（EnableCompressionInSingleFile），将完整的 .NET 8 运行时打包进约 68MB 的单文件中。无需安装任何环境，双击即用。

## ⌨️ 操作指南

| 操作 | 快捷键 / 鼠标动作 | 说明 |
| :--- | :--- | :--- |
| **导入图片** | `Ctrl + O` 或 拖拽文件 | 支持 JPG/PNG/BMP/TIFF 等主流格式 |
| **移动图像** | `鼠标左键` 拖拽 | 在虚线框（物理纸张）内移动图像 |
| **缩放图像** | `鼠标滚轮` | 以鼠标光标为锚点缩放图像物理尺寸 |
| **旋转图像** | `←` / `→` 方向键 | 每次旋转 90 度 |
| **缩放画布** | `Ctrl` + `鼠标滚轮` | 缩放视图相机，方便查看极限比例纸张 |
| **平移画布** | `鼠标中键` 或 `右键` 拖拽 | 移动整个工作台视图 |
| **适应纸张** | `R` 键 | 图像瞬间居中并最大化适应虚线框 |
| **打印** | `Ctrl + P` | 触发物理裁剪并发送至打印机 |

## 🛠️ 技术栈与构建

- **语言与框架**: C# 12 / .NET 8.0 / Windows Forms
- **渲染引擎**: GDI+ (System.Drawing)
- **架构模式**: 世界坐标 (World Space) 与 视图坐标 (View Space) 隔离模型

### 本地开发
```bash
dotnet build
```

### 发布单文件独立版 (Release)
使用以下命令可生成带环境压缩的单文件 EXE：
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

## 📖 开发手记 (Technical Highlights)

本项目的开发过程是一场与 Windows GDI+ 底层缺陷的艰苦博弈：
1. **降伏 HP P1108 驱动**：发现老式 GDI 驱动在处理高倍率嵌套矩阵时会发生 32767 像素溢出。通过放弃嵌套矩阵，改用“预渲染 1:1 物理矩形映射”彻底解决。
2. **GDI+ 矩阵乘法陷阱**：深刻验证了 GDI+ 行向量右乘（`P' = P * M`）的特性。摒弃了极易产生浮点漂移的 `MatrixOrder.Append`，回归最稳定的 `Prepend` 反向书写铁律。
3. **PageSetupDialog 数据丢失 Bug**：发现该原生对话框在切换横纵向时，不会将新数据同步至 `PrintDocument`。通过强制拦截 `Dialog.PageSettings` 成功绕过此系统级 Bug。

## 📄 许可证

本项目仅供学习与个人工具使用。你可以自由修改和分发，但请保留此 README 文件。

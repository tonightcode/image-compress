## Purpose

为 Windows 用户提供本地、直接的批量图片压缩流程：从文件或文件夹导入图片，选择输出格式，并在不覆盖原图的前提下保存压缩结果。

## ADDED Requirements

### Requirement: Unified file and folder import
应用 SHALL 在主界面提供一个统一导入区，接受拖入的文件、文件夹及其组合；点击导入区 SHALL 允许用户选择导入文件或文件夹。文件夹 SHALL 递归查找支持的静态图片，同一路径在当前队列中 SHALL 只出现一次。

#### Scenario: Drop a folder containing nested images
- **WHEN** 用户拖入一个含子文件夹和图片的文件夹
- **THEN** 应用将支持的图片加入队列，并保留每张图片的原始路径

#### Scenario: Select files from the unified area
- **WHEN** 用户点击统一导入区并选择文件导入
- **THEN** 应用打开 Windows 文件选择器，并将所选支持的图片加入队列

#### Scenario: Duplicate input path
- **WHEN** 同一图片通过文件和文件夹两种方式重复导入
- **THEN** 当前队列只保留该图片一次

### Requirement: Local batch compression and output format
应用 SHALL 在本机处理静态 JPEG、PNG、WebP 图片，不上传图片内容。用户 SHALL 能在主界面选择保持原格式或统一输出为 JPEG、PNG、WebP。应用 SHALL 展示逐张处理状态和结果，单张失败 SHALL 不阻断其他图片。

#### Scenario: Keep source format
- **WHEN** 用户选择 `Original` 并压缩一张 JPEG 图片
- **THEN** 输出保持 JPEG 格式，列表显示该图片的处理结果

#### Scenario: Convert selected format
- **WHEN** 用户选择 WebP 并压缩一张 PNG 图片
- **THEN** 输出使用 `.webp` 扩展名并保存在设置指定的位置

#### Scenario: Unsupported or damaged input
- **WHEN** 队列中存在不支持或无法解码的文件
- **THEN** 应用标明该文件未处理及原因，并继续处理其他有效图片

### Requirement: Safe output defaults and settings
应用 SHALL 默认把结果保存到每张原图所在目录，在扩展名前追加 `_compressed`，且 SHALL 保留原图。右上角设置入口 SHALL 允许修改保存位置和文件名后缀；主界面 SHALL 不展示保存规则文案。遇到同名输出文件时，应用 SHALL 生成不冲突的名称，不覆盖已有文件。

#### Scenario: Default output beside original
- **WHEN** `D:\Photos\Vacation.jpg` 采用默认设置完成压缩
- **THEN** 生成 `D:\Photos\Vacation_compressed.jpg`，原文件保持不变

#### Scenario: Existing output name
- **WHEN** 目标目录已有 `Vacation_compressed.jpg`
- **THEN** 应用为新结果选择不冲突的文件名，且不修改已有文件

#### Scenario: Change output settings
- **WHEN** 用户在右上角设置窗口修改保存位置或后缀并保存
- **THEN** 后续压缩任务按新设置输出，设置窗口显示示例文件名

### Requirement: Focused Windows main window
应用 SHALL 以英文显示 `letsmall` 品牌；主界面 SHALL 只保留统一导入区、图片队列、输出格式、右上角设置入口和压缩操作，不显示常驻免费次数或购买入口。

#### Scenario: Ready to compress
- **WHEN** 用户导入三张支持的图片
- **THEN** 主界面显示三张图片及 `Compress 3 images` 操作，设置入口位于右上角

### Requirement: Compact Windows distribution
Windows 10 22H2 / Windows 11 x64 安装包 SHALL 不超过 30 MiB，应用自身安装文件 SHALL 不超过 50 MiB。发布前 SHALL 用实际构建产物测量；应用 SHALL 不要求安装 WebView2。

#### Scenario: Release size check
- **WHEN** 准备发布 Windows x64 安装包
- **THEN** 安装包及应用自身安装文件均满足体积上限，并记录测量结果

# Spec Delta

## Purpose

为 letsmall Windows 用户提供应用内更新能力，在启动或进入更新界面时发现新版本，由用户主动下载安装并确认重启；在保留授权和本地设置的同时，确保更新失败不妨碍原有图片压缩功能。

## ADDED Requirements

### Requirement: Event driven version checks
应用 SHALL 仅在每次启动和每次打开更新界面时异步检查最新稳定版本，不阻塞主界面，不定时轮询，不增加后台常驻或退出后的通知。重叠检查 SHALL 合并为一个进行中的请求。

#### Scenario: Startup check
- **WHEN** 用户启动应用且服务发布了更高版本
- **THEN** 主界面正常打开并在检查成功后显示非阻塞更新提示

#### Scenario: Open update view
- **WHEN** 用户从设置入口打开更新界面
- **THEN** 应用检查一次并展示当前版本、最新版本、更新说明和可用操作

#### Scenario: Remain open or exit
- **WHEN** 软件持续打开且用户没有进入更新界面，或软件已经退出
- **THEN** 不产生新的自动检查或后台更新通知

### Requirement: Consistent version comparison
应用、安装包与公开版本信息 SHALL 使用一致的产品版本。应用 SHALL 按数字比较稳定版本的主、次、补丁号，只有远端版本更高时提供更新，不自动降级。

#### Scenario: Equal or older release
- **WHEN** 本地版本为 1.2.0，远端为 1.2.0 或 1.1.9
- **THEN** 更新界面显示已是最新版本且不提供下载更新

#### Scenario: Numeric ordering
- **WHEN** 本地版本为 1.9.0，远端为 1.10.0
- **THEN** 判定存在新版本并提供更新操作

### Requirement: Non disruptive update notification
启动检查发现新版本时应用 SHALL 提供打开更新界面的英文非阻塞提示；用户 SHALL 可以选择稍后更新，当前会话不反复弹出同版本提示。更新功能 SHALL 对免费及已激活用户均可用。

#### Scenario: Defer update
- **WHEN** 用户选择稍后更新
- **THEN** 应用保持当前图片队列，用户能继续压缩并可再次从设置进入更新界面

### Requirement: User initiated verified download
应用 SHALL 在用户点击更新后才下载安装包，展示进度，允许取消和失败后重试。执行安装前 SHALL 验证发布信息的真实性及安装包的大小、摘要和对应版本；无法验证的文件 MUST NOT 执行。

#### Scenario: Download completes
- **WHEN** 用户点击更新且安装包下载并通过验证
- **THEN** 显示重启并更新操作，尚不退出或安装

#### Scenario: Invalid release or package
- **WHEN** 发布信息签名无效、版本格式错误、下载地址不受信任，或安装包长度或摘要不匹配
- **THEN** 停止更新并报告失败，不执行文件、不关闭当前应用

#### Scenario: Cancel or interrupted download
- **WHEN** 用户取消下载或网络中断
- **THEN** 不将不完整文件用于安装，提供重新下载入口，压缩功能继续可用

### Requirement: Explicit and safe installation handoff
应用 SHALL 仅在用户点击重启并更新且没有压缩任务执行时启动安装交接。安装 SHALL 使用当前安装位置和当前用户权限，等待相关旧实例退出后覆盖；不能安全替换时 SHALL 明确失败而非报告成功，并仅在成功安装后重启应用。

#### Scenario: Compression active
- **WHEN** 安装包已就绪但应用正在压缩图片
- **THEN** 禁止重启安装并提示先等待任务完成，任务结束后允许用户再次点击

#### Scenario: Successful upgrade
- **WHEN** 用户在空闲时点击重启并更新且安装成功
- **THEN** 旧进程退出，新版本覆盖原安装位置并重新打开，显示新版本号

#### Scenario: Installation cannot proceed
- **WHEN** 另一个旧实例占用程序文件或安装目录无法写入
- **THEN** 安装程序明确报告失败，不启动混合版本程序，不显示更新成功

### Requirement: Preserve local user state
覆盖升级 SHALL 保留许可证、设备标识规则、输出设置和当天已用额度，不修改用户原图及已输出文件，不将升级视为重新购买或重新获取免费额度。

#### Scenario: Activated user upgrades
- **WHEN** 已激活用户带有自定义输出设置和当天额度记录完成升级
- **THEN** 授权继续有效，设置和额度记录保持，用户图片文件不变

### Requirement: Public and safely published release information
系统 SHALL 在不要求付款或激活的情况下通过 HTTPS 提供最新稳定版本信息，包含版本、发布日期、纯文本更新说明、版本化安装包地址、长度、SHA-256 及可信签名。发布 SHALL 先确保安装包可获取且校验一致，再公开对应版本信息。

#### Scenario: Payment unavailable
- **WHEN** 支付未启用或支付配置不完整但更新资产可用
- **THEN** 用户仍能检查版本和下载更新

#### Scenario: Release not ready
- **WHEN** 新安装包尚未上传完成或校验不一致
- **THEN** 不将其公布为最新版本

### Requirement: Bounded and recoverable network failures
更新请求 SHALL 有超时和响应大小限制。启动检查失败 SHALL 静默结束；更新界面检查失败 SHALL 明确显示无法检查，提供重新打开界面以重试的提示，不将失败表示为已是最新版本。

#### Scenario: Offline startup
- **WHEN** 用户离线启动软件
- **THEN** 应用正常打开和压缩，不因检查失败弹出阻塞错误

#### Scenario: Unavailable metadata
- **WHEN** 打开更新界面时服务超时或返回无效数据
- **THEN** 显示检查失败而不是已是最新版本，用户可关闭并重新打开界面重试

### Requirement: Recognizable application branding
应用 SHALL 使用与现有橙色外框、白色内框和薄荷绿中心一致的品牌图标，覆盖程序文件、运行窗口、任务栏及安装和卸载程序；图标 SHALL 提供适用于不同 Windows 显示尺寸的资源，不显示默认程序图标。

#### Scenario: Launch installed application
- **WHEN** 用户运行安装后的软件
- **THEN** 程序文件、任务栏及应用窗口显示同一品牌图形

#### Scenario: Installer and uninstaller
- **WHEN** 用户查看安装包或运行卸载程序
- **THEN** 显示相同品牌图标，多种尺寸下保持可辨识

### Requirement: Clear and consistent shared interaction feedback
桌面共享控件 SHALL 区分悬停、按下、禁用和键盘焦点，保持文字可读。导入区 SHALL 支持点击或 Enter/Space 直接打开支持多选的 Windows 文件选择框并反馈拖入状态，不展示导入类型菜单；成功和失败 SHALL 同时提供文字。菜单阴影 SHALL 完整可见，交互优化 SHALL 保留现有布局和业务行为。

#### Scenario: Button interaction
- **WHEN** 用户悬停、按下并松开按钮，或使用键盘聚焦按钮
- **THEN** 操作状态清楚、可恢复，文字不随整个控件变淡，键盘焦点可辨认

#### Scenario: Import and floating menus
- **WHEN** 用户从导入区按 Enter/Space 或展开格式下拉框
- **THEN** 导入区直接打开支持多选的 Windows 文件选择框；格式下拉浮层的边界和阴影不被裁切

### Requirement: Restrained and optional motion
应用 SHALL 在内容出现、菜单展开和设置项显示时采用短促过渡，不阻碍点击或改变业务时序；Windows 关闭客户端区域动画时 SHALL 直接呈现最终状态。动效 SHALL 不因下载进度频繁刷新而重复播放。

#### Scenario: Expand custom output folder
- **WHEN** 用户切换至自定义保存目录，再切回原图目录
- **THEN** 路径输入区域正确显示和隐藏，其余控件保持可用、布局无裁切

#### Scenario: System animation disabled
- **WHEN** Windows 客户端区域动画已关闭
- **THEN** 内容和菜单直接显示，按钮不播放过渡，所有功能仍可操作

### Requirement: Understandable update presentation
更新界面 SHALL 明确区分检查中、已是最新版、可更新、下载中、已就绪和失败，结合文字、状态区及操作表达，不只依赖颜色。检查中 SHALL 有等待反馈，下载中 SHALL 禁止重复下载并保留取消操作，安装就绪后 SHALL 明确显示重启更新入口。

#### Scenario: Download progress changes
- **WHEN** 下载进度从一个百分比变为下一个百分比
- **THEN** 更新进度且不重播整块状态区的入场动画，下载按钮不可重复触发

#### Scenario: Failure or ready state
- **WHEN** 下载失败或安装包验证完成
- **THEN** 分别显示明确失败及重试操作，或显示重启并更新操作

### Requirement: Aligned and responsive update actions
更新窗口的可见操作按钮 SHALL 等高，同一行的上、下边缘和文字垂直位置 SHALL 对齐，并保持清晰间隔。按钮切换文案、显示或隐藏、窗口缩放时 SHALL 保持一致；空间不足时 SHALL 整体换行，不裁切文字或要求不同按钮等宽。

#### Scenario: Download and later actions
- **WHEN** 显示 Download update 和 Later
- **THEN** 两个按钮等高且上下边缘对齐，Later 不因边距差异被拉高

#### Scenario: Cancel retry and restart actions
- **WHEN** 分别进入下载中、失败重试或待重启状态
- **THEN** 对应的主操作、Cancel download 和 Later 保持等高；隐藏操作不留下空白按钮位

#### Scenario: Narrow or scaled window
- **WHEN** 更新窗口处于支持的窄宽度或较高显示缩放
- **THEN** 操作可换行，同一行保持对齐且按钮文字完整可见

## Purpose

为 letsmall 的 Windows 首版提供每天三张的免费使用方式，以及清晰、一次性付费的解锁流程，同时让正常压缩界面保持简洁。

## ADDED Requirements

### Requirement: Three successful images per day for free
未解锁的安装实例 SHALL 每个本地日历日最多成功输出三张图片。失败、取消或没有产生输出的图片 SHALL 不计入使用量；次日本地日期开始时 SHALL 恢复三张额度。

#### Scenario: Three successful outputs
- **WHEN** 未解锁用户在同一天成功压缩三张图片
- **THEN** 当天剩余额度为零，原图不受影响

#### Scenario: Failed output does not consume quota
- **WHEN** 某张图片压缩失败或用户在输出完成前取消
- **THEN** 该图片不消耗免费额度

### Requirement: Prompt when selection exceeds available quota
当新导入的图片使队列中的待处理数量超过当天剩余额度时，应用 SHALL 立即显示超额提示，不悄悄加入超额图片。提示 SHALL 提供保留额度内图片与进入一次性购买流程的选择；正常主界面 SHALL 不常驻显示免费次数。

#### Scenario: Fourth image on an unused free day
- **WHEN** 未解锁用户的队列已有三张图片并尝试加入第四张
- **THEN** 应用显示免费额度提示，第四张图片不进入队列，用户可保留前三张或选择解锁

#### Scenario: Only one free use remains
- **WHEN** 用户当天已成功压缩两张图片，随后尝试加入两张图片
- **THEN** 应用按剩余一张额度提示超额，并允许用户只保留一张待处理图片

### Requirement: One-time purchase unlock
超额提示 SHALL 以美元显示一次性价格 `$6.90`，购买按钮 SHALL 启动支付流程。只有支付成功且授权完成后，应用 SHALL 移除每日张数限制，并在重新启动后保持解锁状态；支付失败或取消 SHALL 不解锁。

#### Scenario: Successful purchase
- **WHEN** 用户完成 `$6.90` 一次性购买并取得有效授权
- **THEN** 应用解除图片数量限制，重新启动后仍识别该授权

#### Scenario: Abandoned checkout
- **WHEN** 用户关闭或取消支付页面
- **THEN** 应用保持未解锁状态，已有免费队列不丢失

### Requirement: One order activates one computer
支付服务确认订单后，网页 SHALL 以短时一次性领取凭证唤起应用；应用 SHALL 通过授权服务将该订单绑定到首次激活的电脑，并取得该电脑专属的签名许可证。同一凭证 SHALL 不可重复激活，另一台电脑 SHALL 不可复用已绑定订单；换电脑需要重新购买。应用 SHALL 在本地验签并核对本机标识，已激活电脑的日常使用 SHALL 不要求联网。

#### Scenario: First activation from paid checkout
- **WHEN** 支付已确认，网页唤起应用，且该订单尚未绑定电脑
- **THEN** 授权服务绑定当前电脑并签发许可证，应用解除每日额度

#### Scenario: Reused token or different computer
- **WHEN** 已消费的领取凭证被再次提交，或已绑定订单尝试在另一台电脑上激活
- **THEN** 授权服务拒绝激活，另一台电脑保持免费状态

#### Scenario: Offline use after activation
- **WHEN** 已激活电脑在没有网络的情况下重新启动应用
- **THEN** 应用通过本地许可证验签保持解锁，不发起在线授权检查

### Requirement: Preserve local license across application reinstall
应用 SHALL 将签名许可证保存在安装目录之外的本机用户数据目录，卸载应用时 SHALL 保留该文件。同一用户在原电脑重装应用后，若许可证仍存在且有效，应用 SHALL 本地验签并保持解锁。许可证文件被清理或用户数据丢失后，服务端 SHALL 不凭旧订单补发许可证；用户须重新购买。购买说明 SHALL 告知用户一笔订单只用于一台电脑、授权不可转移，以及许可证丢失后的重新购买规则。

#### Scenario: Reinstall application with license retained
- **WHEN** 用户在原电脑卸载并重装应用，且本地许可证仍存在
- **THEN** 应用离线验签后保持解锁，不要求再次购买

#### Scenario: Local license removed
- **WHEN** 用户清理了本地许可证或重装系统导致其丢失
- **THEN** 应用保持免费状态，旧订单不提供补发，重新解锁需要购买新授权

#### Scenario: New computer
- **WHEN** 用户在不同电脑上使用原电脑的许可证或再次提交已消费的激活凭证
- **THEN** 应用或授权服务拒绝解锁，并提示需要购买新授权

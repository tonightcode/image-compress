## Purpose

为 letsmall 提供可以部署的云端付款确认和设备绑定授权能力，连接 Paddle 订单与 Windows 客户端；首次领取需要联网，日常使用依靠本地许可证，不上传任何图片内容。

## ADDED Requirements

### Requirement: Trusted one-time payment confirmation

服务 SHALL 仅在验证 Paddle 通知签名、时间有效性及最终完成状态后确认付款，并核对商品价格标识、数量一、美元币种及单价 690 美分。买家税费 SHALL 按 Paddle 的结账结果处理，不以含税总额必须等于 690 美分为条件。浏览器返回参数 SHALL 不能代替可信付款通知；重复事件和重复订单 SHALL 不产生第二份授权。

#### Scenario: Valid completed transaction
- **WHEN** 服务收到签名有效的最终完成交易，商品、数量、币种和单价均符合配置
- **THEN** 记录该订单已付款，并允许对应购买会话领取一次授权

#### Scenario: Forged or mismatched payment
- **WHEN** 通知签名无效、时间过期、金额或商品不符，或交易尚未完成
- **THEN** 不创建可领取的已付款订单，不解锁软件

#### Scenario: Duplicate notification
- **WHEN** 同一事件或同一已付款订单再次到达
- **THEN** 不生成新的领取凭证，不清除设备绑定，不重置已经消费的授权

### Requirement: Purchase-session-controlled activation handoff

服务 SHALL 将付款交易与发起购买的浏览器会话关联，只向持有该会话凭据的浏览器提供短时领取凭证。领取凭证 SHALL 有至少 256 位随机熵，默认在付款确认后十分钟到期；服务持久化记录 SHALL 仅保存其摘要。订单号本身 SHALL 不能用于获取或补发凭证。

#### Scenario: Confirmed purchase in originating browser
- **WHEN** 已付款且未激活的购买会话持有有效的会话凭据和领取凭据
- **THEN** 付款结果接口返回该会话的激活入口

#### Scenario: Other browser knows transaction identifier
- **WHEN** 请求者只知道交易编号或购买会话编号，未持有对应会话凭据
- **THEN** 不能获取激活凭证或许可证

### Requirement: Atomic first-device activation

HTTPS `POST /activate` SHALL 接受现有客户端的 `token` 和 `deviceId`，仅对已付款、未到期、未消费的凭证返回包含 `payload` 和 `signature` 的许可证。设备绑定与凭证消费 SHALL 作为一次不可分割的持久化操作，保证并发请求中只有一个成功；签名失败 SHALL 不消费凭证。

#### Scenario: First matching activation
- **WHEN** 有效凭证与合法设备标识首次提交
- **THEN** 订单绑定该设备，凭证被消费，返回与 C# 客户端兼容的签名许可证

#### Scenario: Concurrent activation
- **WHEN** 两台设备同时领取同一个订单
- **THEN** 只有一个请求成功，其余请求被拒绝，订单始终只有一个设备绑定

#### Scenario: Expired unpaid or consumed token
- **WHEN** 凭证不存在、未付款、已到期或已消费
- **THEN** 返回受控拒绝结果，不签发可用许可证

### Requirement: Desktop-compatible offline license

许可证 SHALL 保持现有版本一、产品 `letsmall`、订单及设备声明格式，并使用与现有客户端相同的签名字节规则。签名私钥 SHALL 仅存于云端秘密配置，桌面仅嵌入验签公钥。服务 SHALL 不要求已激活用户日常联网，也 SHALL 不接收图片或图片路径。

#### Scenario: Worker-issued license verified by desktop
- **WHEN** Windows 客户端收到有效且属于本机的云端签名许可证
- **THEN** 本地验签成功，并在后续离线启动时保持解锁

### Requirement: Persistent non-transferable orders

订单消费及设备绑定 SHALL 在服务重启、重新部署后保留。服务 SHALL 不提供旧订单补发、换机、解绑或授权转移入口；本地许可证被删除后，旧订单 SHALL 不能再次解锁。

#### Scenario: Redeploy after activation
- **WHEN** 服务重新部署后收到已使用订单的旧凭证
- **THEN** 仍拒绝领取，不重置设备绑定

#### Scenario: Deleted local license
- **WHEN** 用户删除本地许可证，并尝试使用旧订单或旧凭证恢复
- **THEN** 不补发许可证，重新解锁需要新订单

### Requirement: Configuration and environment isolation

服务 SHALL 通过显式配置区分 Paddle 沙箱与生产，并使用独立订单存储及密钥配置。商户密钥、通知秘密和签名私钥 SHALL 不进入源码、日志或安装包。未配置付款渠道时 SHALL 不提供实际可付款结账，配置不完整时 SHALL 不签发许可证；部署 SHALL 优先使用免费套餐，不自动升级付费套餐。

#### Scenario: Sandbox order presented to production
- **WHEN** 沙箱订单或沙箱凭证被提交到生产环境
- **THEN** 不能解锁生产客户端

#### Scenario: Missing required secrets
- **WHEN** 签发或付款所需配置缺失
- **THEN** 返回明确的不可用状态，不以模拟成功结果代替真实付款或签发

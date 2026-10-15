## Purpose

为 letsmall 提供简洁的英文产品网站和付款后激活页面，让用户了解真实的 Windows 软件、下载并购买一次性授权，也提供用于支付平台审核的产品及交易说明。

## ADDED Requirements

### Requirement: Focused English product website

网站 SHALL 展示 letsmall 名称、真实软件截图、支持的 Windows 版本、批量本地压缩功能、每天三张成功输出的免费规则、有效下载入口及美元 6.90 一次性价格。网站 SHALL 明确一单一机、不可转移、卸载重装保留本地许可证、许可证被清理后需重新购买；不得宣传未实现的 macOS 版。

#### Scenario: User visits product page
- **WHEN** 用户打开产品网站
- **THEN** 可以直接了解软件、下载 Windows 安装包及查看购买价格和授权限制

### Requirement: Reviewed commercial information

网站 SHALL 提供隐私、交易及退款说明和可用联系入口。正式发布前这些信息 SHALL 由用户确认，不能把占位邮箱、未确认的退款承诺或无效下载地址当作完整上线内容。隐私说明 SHALL 区分本地图片处理与付款、授权产生的数据。

#### Scenario: Incomplete public configuration
- **WHEN** 正式域名、联系信息、下载地址或交易说明尚未准备完整
- **THEN** 保持预览或待配置状态，不宣称正式销售已上线

### Requirement: One-time hosted checkout

购买入口 SHALL 发起可信购买会话并使用 Paddle 托管支付界面，以美元一次性商品销售，非订阅。页面 SHALL 说明结账时展示的适用税费，并 SHALL 不收集银行卡资料。Paddle 尚未配置时 SHALL 禁用实际购买入口并提示尚未开放。

#### Scenario: Purchase before merchant setup
- **WHEN** 用户访问网站且支付配置尚未完成
- **THEN** 可以了解并下载软件，但不能进入伪造或未配置的付款流程

### Requirement: Verified activation result page

结果页 SHALL 根据服务端确认状态显示等待、可激活、已领取、失败或到期，而不因客户端宣称付款成功就展示授权。仅已确认且会话有效时 SHALL 提供 `Open letsmall` 按钮及同一短时激活链接；链接 SHALL 只包含一次性凭证，不包含永久许可证。

#### Scenario: Waiting for payment notification
- **WHEN** 用户已返回结果页，但服务端尚未收到可信付款完成通知
- **THEN** 显示等待确认，不提供可用激活链接

#### Scenario: Paid and available
- **WHEN** 服务已确认付款且该购买会话尚未激活、凭证未到期
- **THEN** 用户可通过按钮唤起 letsmall，也可重新点击同一有效入口，不重新签发另一凭证

#### Scenario: Expired or consumed link
- **WHEN** 原凭证已到期或已消费
- **THEN** 显示相应状态，不自动重新签发，不提供旧订单补发入口

### Requirement: Public production access

正式产品网页、付款通知和激活接口 SHALL 不要求用户或 Paddle 先登录 Cloudflare 控制台。预览地址可以只允许项目成员访问，正式业务接口 SHALL 通过各自的付款签名、购买会话和激活凭证验证身份。

#### Scenario: Production payment callback
- **WHEN** Paddle 向正式通知接口提交有效付款通知
- **THEN** 请求进入业务验签处理，不被 Cloudflare 账户登录页面阻断

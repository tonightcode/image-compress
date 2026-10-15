# Design

## Context

动机见 `proposal.md`。现有服务为 Python + SQLite，含原子领取与 RSA 签名；C# `ActivationClient` 需要 HTTPS 地址，并接受 `payload`/`signature` 格式。客户端本地验签和卸载保留许可证已验证，正常构建尚未嵌入正式公钥或服务配置。

2026-10-08 从用户已登录的 Cloudflare 控制台核对：Worker `wandering-hat-a5a4` 已部署；D1 `letsmall_data` 已绑定，代码绑定名也为 `letsmall_data`；未设置自定义域名。平台部署成功不等于授权业务已上线，公网业务代码与数据表还需实现阶段核验。

## Goals / Non-Goals

首页正式视觉参考：用户已选择第一版 [website-home-v1.png](assets/website-home-v1.png)。采用英文、白色与薄荷绿分区、橘色操作按钮，居中展示品牌、下载/购买入口及软件窗口，下方保留功能概览、价格规则和常见问题。后续首页实现以此稿为准。该图是视觉稿，不代表网页或支付功能已经实现；图中软件窗口基于现有截图生成，实际网站应嵌入正式版本的真实截图。付款结果页及移动端布局仍需在实现阶段覆盖。

**Goals:** 用一个小型服务完成付款通知、购买会话与授权签发，兼容已有 Windows 客户端；保留订单记录，优先免费资源，不维护 VPS。

首页未采用的备选稿：[website-home-v2.png](assets/website-home-v2.png) 为白底左对齐排版，[website-home-v3.png](assets/website-home-v3.png) 为炭黑首屏与白色内容区的高对比排版。两稿仅保留供参考，不作为实现依据。

**Non-Goals:** 不新增桌面框架或图片上传，不做用户账号系统、订阅、管理后台、许可证找回或换机；本提案不执行注册、域名购买、收费升级或生产部署。

## Decisions

### 一个 Worker、一个 D1、同源网站

计划在 `service/cloudflare/` 使用 TypeScript Worker、轻量静态 HTML/CSS 和 SQL 迁移，通过 `env.letsmall_data` 访问已绑定 D1。相对于运行 Python WSGI 的 VPS，此方案避免系统和进程运维。Python 服务保留作兼容基准，不把其 `BEGIN IMMEDIATE` 直接照搬到 D1。

网站使用现有品牌色及真实应用截图，不引入前端大型框架。网页只含产品、下载、价格、交易说明和激活状态；不会在桌面中嵌入网页运行时。

### 付款会话与凭证交付

接口计划：`POST /checkout/start`、`POST /webhooks/paddle`、`GET /purchase/status`、`POST /activate`、`GET /health`。网站提供产品页与 `/purchase/result`。

发起购买时创建随机会话和随机领取凭证，将秘密会话凭据和领取凭证保存在同源 `Secure`、`HttpOnly`、`SameSite=Lax` Cookie 中；D1 仅保存摘要。交易附带不可猜测的公开会话关联标识。服务端创建限定商品和数量的 Paddle 交易，客户端不能自行指定价格或付款状态。所有状态读取需校验浏览器会话，不能仅凭交易号领取。

Paddle 完成通知验签后把交易绑定到会话，设置付款状态及十分钟领取期限；结果页通过状态接口取得属于该会话的同一个激活链接。浏览器返回和页面参数不授予付款状态。刷新结果页只能再次显示尚未消费的同一凭证，不能生成新的凭证。浏览器清理购买 Cookie 后，不提供旧订单找回入口，网站需在购买前明确提示在原浏览器及时完成激活。

### 可信 Paddle 事件

使用 Paddle Billing 的 `transaction.completed`，以原始请求体验证 `Paddle-Signature` 并检查时间容差。核对沙箱/生产配置、允许的 price ID、USD、数量一和单价 690 美分；税费以 Paddle 结账为准，不把含税总额硬编码为 690。事件 ID 和交易 ID 唯一，重复通知不能重新创建授权或重置领取状态。

收到有效事件后，事件记录和订单变更作为 D1 批量事务提交；处理失败返回可重试错误，不把失败事件提前标记为已处理。未配置的产品、无效通知和无对应会话的交易不能产生可领取授权。

### 最小数据模型与原子领取

`purchase_sessions` 存储会话摘要、关联标识、凭证摘要及购买时间；`orders` 存储唯一 Paddle 交易号、商品、币种、单价、付款状态、有效期及设备绑定；`webhook_events` 存储唯一事件号。设置必要的唯一约束和查询索引，避免扫描整库。

领取先读取已付款且可领取的订单，使用 Web Crypto 计算许可证签名，然后执行包含付款状态、凭证摘要、有效期及 `device_id IS NULL` 条件的单条原子更新。只有更新实际改变一行时才返回许可证；并发失败者丢弃已计算的签名。时间有效性也在更新条件中重新判断，不能只依赖先前读取。签名失败发生在消费前，不锁死订单。

该流程保持现有严格一次性规则：绑定成功后不重复返回许可证，即使客户端响应丢失或本地保存失败，也不能自动补发。实现和上线文案必须显式记录这个已有规则带来的交付风险，不悄悄增加恢复接口。

### 签名和秘密配置

继续使用 RSA 2048 位以上、SHA-256、PKCS#1 v1.5；签名字节为 UTF-8 `letsmall-license-v1\n` 加上原始 JSON payload 字节，字段为 `v`、`product`、`orderId`、`deviceId`。返回 Base64 payload 与 signature，公钥转成现有 C# 需要的 XML 格式。

签名私钥采用 PKCS#8，通过 Workers Secret 配置；Paddle API 密钥和通知秘密同样只在云端保存。不在日志中记录完整通知请求、Cookie、领取凭证或许可证。限请求体大小、校验字段、按来源适度限流；未配置密钥时拒绝签发。

沙箱采用本地 D1 或独立测试 D1 和独立秘密配置，不能写入 `letsmall_data` 的生产订单。正式密钥须保持稳定；回滚不清空订单或更换签名密钥，否则旧订单和已分发客户端可能不兼容。

### 两份提案的任务归属

本变更负责云端实现、网站、Paddle 通知及部署验证。原 `build-letsmall-windows-image-compressor` 的 4.3 保留为桌面付款入口、公钥及服务配置与真实流程验收，依赖本变更；5.1 保留为最终安装包复测。不把尚未完成的任务标记完成，也不在两份清单重复实现同一个云端服务。

## Risks / Trade-offs

- 商户审核和正式域名未完成：先完成本地服务和公开产品介绍，购买入口保持未配置状态；真实收款验收等待平台审核。
- 免费套餐的执行与用量限制：测量通知验签及 RSA 签发成本，保持小请求和索引；不自动升级收费，超限明确报告。
- 严格一次性领取存在网络丢包和保存失败风险：先签名再原子消费，前端明确激活状态；不承诺旧订单可补发或绕过现有产品规则。
- 退款后离线许可证无法即时撤销：未激活退款订单停止领取；不声称已发出的离线许可证能远程立刻失效，不新增每次联网检查。
- 公共接口被 Access 阻断：成员保护仅用于预览，正式接口使用业务验签和凭据校验。

## Migration Plan

先实现 TypeScript 与 D1 迁移，使用临时密钥完成与 C# 的协议及签名互通；再完成网页和 Paddle 沙箱流程。经用户确认后部署产品介绍和服务，保留现有 Worker 名与绑定。正式域名、下载链接、联系及交易说明准备完整，Paddle 审核通过后配置正式商品和秘密，最后由原桌面提案完成公钥、URL 嵌入及安装包验收。

生产迁移前保留 D1 数据恢复点，迁移不删除历史订单；代码回滚到前一部署版本，保留 D1、已消费记录和原签名密钥。付款入口在依赖不可用时停止新交易，不把本地图片压缩置为不可用。

## Open Questions

- 正式域名、公开联系邮箱、下载地址和经用户确认的交易/退款文案。
- Paddle 审核后提供的正式与沙箱商品 ID、API 配置和通知目的地秘密。
- 现有随机 Worker 名是否保留：默认保留，不以改名作为实现前置条件。

## Reference Sources

- [Paddle 通知验签](https://developer.paddle.com/webhooks/about/signature-verification/)
- [Paddle transaction.completed](https://developer.paddle.com/webhooks/transactions/transaction-completed/)
- [D1 预编译语句及变更行数](https://developers.cloudflare.com/d1/worker-api/prepared-statements/)
- [Workers Web Crypto](https://developers.cloudflare.com/workers/runtime-apis/web-crypto/)

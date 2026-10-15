# Proposal

## Why

letsmall 的 Windows 压缩功能及本地许可证验证已有实现，但项目里的 Python 授权核心尚未部署，不能直接接入用户已创建的 Cloudflare Workers 与 D1。需要把产品网站、真实付款与云端签发授权独立规划，避免继续混在桌面软件提案里。

## What Changes

- 新增 Cloudflare Workers + D1 云端服务，优先使用免费套餐，复用已有 Worker `wandering-hat-a5a4` 和数据库绑定 `letsmall_data`。
- 迁移现有一次性激活与签名规则，保持现有 C# 请求、许可证格式和离线验签兼容；一单一机、不可转移、许可证丢失不补发的规则不变。
- 接入 Paddle Billing 的 `$6.90` 美元一次性购买、服务端付款通知验签及重复通知处理。
- 新增简洁英文产品介绍、下载、购买说明和付款结果页面；付款确认后，通过 `letsmall://activate?token=...` 唤起软件领取授权。
- 新增 D1 建表迁移、云端密钥配置及部署说明；签名私钥和 Paddle 商户密钥不进入桌面安装包或仓库。
- 原桌面变更继续负责客户端配置与最终安装包验收；本变更负责网站、付款、订单存储与线上授权服务。

## Capabilities

### New Capabilities

- `cloud-payment-licensing`: 可信付款确认、一次性领取、设备绑定签发、持久化订单和云端部署。
- `product-purchase-website`: 英文产品展示、下载与购买入口、交易说明及付款后的软件激活页面。

### Modified Capabilities

无。现有 `free-usage-and-license` 仍在未归档的桌面变更中，本变更补齐其云端依赖，不改变产品授权规则。

## Impact

- 计划新增 `service/cloudflare/` 下的 TypeScript Worker、D1 迁移和静态网站资源；不增加桌面运行时依赖。
- `service/authorization.py` 和 `service/http_api.py` 作为已验证行为的参考，迁移期间保留；Cloudflare 版本通过协议兼容验证后成为部署入口。
- 外部依赖为 Cloudflare Workers/D1、Paddle 商户及沙箱账号、正式域名、有效下载链接和联系信息。
- Paddle 审核、正式密钥及域名尚未确认；这些是上线依赖，不能用模拟付款代替正式验收。

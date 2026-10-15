## Why

目前项目只有 OpenSpec 配置，尚无可用的图片压缩软件。需要将已确认的 letsmall 桌面界面和关键行为整理为可实施的 Windows 首版范围，让批量压缩保持“导入、选格式、压缩”三步完成。

## What Changes

- 新建英文界面的 Windows 桌面应用 letsmall，支持拖入文件或文件夹，以及点击统一导入区选择文件或文件夹。
- 支持批量压缩静态 JPG、PNG、WebP 图片，并在主界面选择保持原格式或指定输出格式；逐张展示处理结果。
- 默认在每张原图所在目录生成 `原名_compressed.扩展名`，保留原图；保存位置和后缀仅在右上角设置窗口中配置。
- 免费用户每天可成功压缩 3 张图片；超过可用额度时弹出提示，并提供一次性支付 $6.90 的解锁入口。主界面不常驻显示额度或购买入口。
- 支付确认后由网页唤起 letsmall 自动激活：每笔订单只授权一台电脑，换电脑需重新购买；本地许可证在卸载应用时保留，重装应用后可继续使用，若许可证被清除则需重新购买；激活后可离线使用。
- 将主界面、超额支付提醒、设置窗口的最终设计图保存到本变更的 `assets/` 目录，作为实现时的视觉参考。
- Windows 首版以小体积和简单安装为优先，安装包目标不超过 30 MiB；开发初期用实际安装包验证，AVIF 暂不进入首版。

## Capabilities

### New Capabilities

- `desktop-image-compression`: Windows 桌面端的文件和文件夹导入、批量压缩、格式选择、输出命名与设置。
- `free-usage-and-license`: 每日免费额度、超额提醒、一次性购买与授权状态。

### Modified Capabilities

无。项目目前没有已有能力规范。

## Impact

云端付款与授权的实现规划已独立为 [add-letsmall-cloudflare-payment-and-licensing](../add-letsmall-cloudflare-payment-and-licensing/proposal.md)，采用 Cloudflare Workers + D1 和 Paddle。桌面提案继续负责原有产品行为、客户端联调及安装包验收，不重复承接新提案的云端实现任务。

- 新增 C# WPF Windows 应用、JPG/PNG/WebP 图片处理和安装包；使用目标 Windows 已包含的 .NET Framework 4.8，不依赖 WebView2。
- 新增本地偏好设置与免费使用记录；购买和授权需要外部支付渠道及可验证的授权服务，并支持一次性激活凭证、设备绑定和本地验签，具体服务商在实施前确认。
- 首版只交付 Windows 10 22H2 / Windows 11 x64；macOS 兼容性与打包不作为验收条件。

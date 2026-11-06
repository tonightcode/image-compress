# 应用更新与发布

## 用户行为

每次启动软件、每次打开 Settings → Software updates 时异步检查一次。不定时检查、不后台常驻、不在退出后通知。启动失败静默，更新界面明确显示失败，关闭后重新进入可重试。

发现新版本后用户点击 Update 下载；下载可取消和重试，关闭更新窗口不取消下载，退出软件取消。下载校验后点击 Restart and update，确认退出会清空导入队列，再运行安装包。压缩中不可安装。授权、输出设置、额度及用户图片不被安装脚本修改。

## 配置及构建

版本唯一来源为仓库根目录 `version.props`，使用三段数字稳定版本，每段最多四位。首次支持更新版本设为 1.0.0；发布前确认与实际产品版本安排一致。

使用独立的更新密钥，不使用许可证密钥。下列命令在 `service/cloudflare` 下执行，输出路径必须放在仓库、网站资产目录和公开同步目录之外：

```powershell
node scripts/update-key.mjs https://your-release-origin C:\private\update-key.pem C:\private\update-public.json
```

脚本生成 RSA 私钥及只含 origin、公钥 XML 的客户端配置。保存私钥并限制文件访问权限，不能丢失或提交；客户端只嵌入 public.json 内容，不嵌入私钥。首次生成后不要每版重建密钥。正式 origin 使用目前网站 HTTPS 源，地址无尾部斜杠；安装包必须同源。

在仓库根目录构建（需 Visual Studio Build Tools 和 NSIS；MakeNsis 可传便携版路径）：

```powershell
.\scripts\build-release.ps1 -UpdateConfigFile C:\private\update-public.json -LicensePublicKeyFile C:\private\license-public.xml -ServiceConfigFile C:\private\service.json -MakeNsis C:\tools\nsis\Bin\makensis.exe
```

许可证和支付配置沿用项目原有格式。缺少更新配置的开发构建仍可压缩，但显式检查更新会失败，不能执行未经信任的安装包。构建脚本传递产品版本给 NSIS，并检查 EXE、安装包元数据和安装包大小。安装产物位于 artifacts/letsmall-setup.exe。

## 两阶段发布

1. 修改 version.props、编写纯文本更新说明，使用同一公钥构建安装包。先完成本地两版升级验收。
2. 在 service/cloudflare 执行 package 阶段，准备版本化包，不改变最新版本信息：

```powershell
node scripts/prepare-update.mjs package https://your-release-origin C:\private\release-notes.txt C:\private\update-key.pem
```

3. 部署网站资产以先提供 `/downloads/letsmall-<version>-win-x64.exe`；保留既有版本资产。不覆盖同版本不同内容的包。部署需要明确的生产发布授权。
4. 执行 manifest 阶段，它会实际下载远程包并验证长度、摘要后才生成签名 latest：

```powershell
node scripts/prepare-update.mjs manifest https://your-release-origin C:\private\release-notes.txt C:\private\update-key.pem
```

5. 再部署包含 public/updates/latest.json 的资产。latest 不缓存，版本化包使用不可变缓存。不得手动修改签名后的 payload。现有 prepare-assets 仍提供固定官网包，不能替代这两个阶段。
6. 保留签名 latest 的上一份副本。新机器发布工作区应先恢复历史安装包资产，再部署，避免把历史包删掉。私钥不得放到 public 或作为运行时 Worker 秘密。

`/updates/latest.json` 与版本化下载只允许 GET/HEAD，在支付配置校验前处理，更新不需要账户或激活。签名协议 payload 含 schema=1、product=letsmall、platform=win-x64、version、publishedAt、notes、url、size、sha256；签名覆盖原始 UTF-8 payload 字节。响应使用 Base64 payload/signature，算法 RSA-SHA256/PKCS#1 v1.5。客户端先验签再解析，拒绝跨源地址、重定向和不匹配安装包。

## 安装及恢复

更新调用安装器 `/UPDATEPID=<进程号> /D=<原安装目录>`，`/D` 必须最后且不加引号，遵循 [NSIS 命令行约定](https://nsis.sourceforge.io/Which_command_line_parameters_can_be_used_to_configure_installers)。更新模式验证已注册安装位置；便携启动的程序应使用官网完整安装包正常安装后再更新。

安装器设置安装中标记，等待旧实例退出，最多 30 秒；其他实例仍运行则失败，不强杀。为安全起见，同一 Windows 会话内的 letsmall 实例均需关闭，包括其他安装位置的实例；这不限制平时多实例运行。成功后更新程序版本并启动新版。不能安全覆盖、目录不能写入或写入失败会提示失败，不启动不完整版本。更新仍显示安装进度界面，不静默强制安装。

更新取消或失败可重试。更新完成后临时安装包可能保留在系统临时目录，由系统临时文件清理机制回收；未下载完整的文件由客户端主动清理。安装中断没有自动事务回滚，用官网下载完整包安装到原位置修复，用户数据保存在 `%LOCALAPPDATA%\letsmall`，不能删除该目录。撤回发布可恢复上一份签名 latest，已升级客户端不自动降级，需要发布更高修复版本。

## 验证记录

执行结果和未完成项见 `artifacts/app-update-verification.md`。任务中的十项验收矩阵定义预期，不代表已实际通过；只有有验证证据时才勾选任务。不支持更新的历史客户端需要先手动安装一次新版。

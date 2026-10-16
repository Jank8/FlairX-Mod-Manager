# FlairX 自定义版更新指南

这套流程用于长期保留以下自定义功能：

- GameBanana 按角色筛选。
- 按 Liked 和 Downloads 排序。
- 使用较高清晰度的列表缩略图。

源码保存在 GitHub 仓库 `Dyendless/FlairX-Mod-Manager` 的
`custom/gamebanana-enhancements` 分支。`origin` 始终指向官方仓库，脚本只向
`fork` 推送自定义分支。

## 前置条件

- Windows PowerShell 或 PowerShell 7。
- Git 和已经登录的 GitHub CLI（`gh auth status` 可检查）。
- .NET 10 SDK。脚本会从源码目录逐级向上寻找 `dotnet-sdk\dotnet.exe`，也会检查系统 `dotnet`。
- 已安装的 FlairX，默认位置为 `E:\flairx`。
- Git 工作区必须干净，并处于 `custom/gamebanana-enhancements` 分支。

只有 .NET Runtime 不够；`dotnet --list-sdks` 必须至少返回一个 SDK。

## 日常更新

先执行预演：

```powershell
pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1 -WhatIf
```

预演只检查安装目录、分支、remote、GitHub 登录和 SDK，并列出计划阶段。它不会拉取、合并、构建、停止程序、备份、部署或推送。

确认预演无误后执行真实更新：

```powershell
pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1
```

其他安装位置可显式指定：

```powershell
pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1 -InstallPath 'D:\Apps\flairx'
```

脚本会依次：

1. 获取 FlairX 官方最新稳定 Release。
2. 将对应标签合并到长期分支。
3. 运行完整测试和 x64 Release 发布。
4. 下载官方发布包，在临时目录叠加自定义构建。
5. 备份现有安装。
6. 部署并启动 FlairX 验证。
7. 将验证后的长期分支推送到 Fork。

## 备份位置

每次实际部署前都会创建：

```text
E:\flairx\backups\yyyyMMdd-HHmmss-custom-update
```

备份时会排除 `backups` 自身，避免递归复制。历史备份不会自动删除。

## 更新冲突

如果官方源码与自定义改动冲突，脚本会中止合并并停止。此时不会测试、构建或修改安装目录。

保留终端中的冲突信息并进行人工合并。不要将旧版 DLL 直接复制到新版 FlairX，因为新版依赖或接口可能已经改变。

解决冲突并完成测试后，再重新运行预演和真实更新命令。

## 手动回滚

脚本在部署后启动失败时会尝试自动恢复。需要手动回滚时：

1. 关闭 FlairX 和 FlairX Launcher。
2. 在 `E:\flairx\backups` 中选择更新前最新的 `*-custom-update` 目录。
3. 将当前 `E:\flairx\app` 重命名留存，不要直接删除。
4. 将备份中的 `app` 目录复制回 `E:\flairx`。
5. 将备份根目录中的启动器文件复制回 `E:\flairx`。
6. 运行 `E:\flairx\FlairX Mod Manager Launcher.exe`。

## 检查远端备份

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
gh api repos/Dyendless/FlairX-Mod-Manager/branches/custom/gamebanana-enhancements --jq .commit.sha
```

最后两个 SHA 相同，表示本地长期分支已经安全保存到 GitHub Fork。

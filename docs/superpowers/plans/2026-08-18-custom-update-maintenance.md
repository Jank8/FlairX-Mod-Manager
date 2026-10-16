# FlairX Custom Update Maintenance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Build a safe one-command workflow that syncs the custom FlairX branch with the latest official release, validates it, backs up E:\flairx, deploys it, and preserves the source on the user's GitHub Fork.

**Architecture:** Keep official history on origin and the durable customization on fork/custom/gamebanana-enhancements. Put pure validation and stage-gating logic in a PowerShell module, while a thin script owns network, Git, build, backup, deployment, rollback, and push side effects. Deploy by overlaying the custom publish output onto a staged official Release ZIP.

**Tech Stack:** PowerShell, Pester 3.4-compatible tests, Git/GitHub CLI, .NET 10 SDK, dotnet test/publish, robocopy.

---

## File map

- Create tools/FlairX.CustomUpdate.psm1 for pure safety and orchestration helpers.
- Create tools/tests/FlairX.CustomUpdate.Tests.ps1 for Pester regression tests.
- Create tools/Update-CustomFlairX.ps1 for side-effecting orchestration and rollback.
- Create docs/CUSTOM_UPDATE_ZH-CN.md for user instructions.
- Modify README.md only to link the guide.

### Task 1: Safety and release-selection helpers

**Files:**
- Create: tools/FlairX.CustomUpdate.psm1
- Create: tools/tests/FlairX.CustomUpdate.Tests.ps1

- [ ] **Step 1: Write failing tests**

~~~powershell
$module = Join-Path $PSScriptRoot '..\FlairX.CustomUpdate.psm1'
Import-Module $module -Force

Describe 'Resolve-FlairXInstallRoot' {
    It 'accepts a root containing the main executable' {
        $root = Join-Path $TestDrive 'flairx'
        New-Item -ItemType Directory -Path (Join-Path $root 'app') | Out-Null
        New-Item -ItemType File -Path (Join-Path $root 'app\FlairX Mod Manager.exe') | Out-Null
        Resolve-FlairXInstallRoot $root | Should Be ([IO.Path]::GetFullPath($root))
    }
    It 'rejects a root without the executable' {
        { Resolve-FlairXInstallRoot $TestDrive } | Should Throw
    }
    It 'rejects a drive root' {
        { Resolve-FlairXInstallRoot ([IO.Path]::GetPathRoot($TestDrive)) } | Should Throw
    }
}

Describe 'ConvertFrom-FlairXReleaseJson' {
    It 'returns the stable tag and zip URL' {
        $json = '{"tag_name":"4.1.0","prerelease":false,"assets":[{"name":"FlairX.Mod.Manager.zip","browser_download_url":"https://example/app.zip"}]}'
        $result = ConvertFrom-FlairXReleaseJson $json
        $result.Tag | Should Be '4.1.0'
        $result.DownloadUrl | Should Be 'https://example/app.zip'
    }
    It 'rejects prereleases' {
        { ConvertFrom-FlairXReleaseJson '{"tag_name":"beta","prerelease":true,"assets":[]}' } | Should Throw
    }
}

Describe 'Assert-FlairXPushTarget' {
    It 'only allows the fork maintenance branch' {
        { Assert-FlairXPushTarget fork 'custom/gamebanana-enhancements' } | Should Not Throw
        { Assert-FlairXPushTarget origin 'custom/gamebanana-enhancements' } | Should Throw
        { Assert-FlairXPushTarget fork main } | Should Throw
    }
}
~~~

- [ ] **Step 2: Run Pester and observe RED**

Run:

~~~powershell
powershell.exe -NoProfile -Command "Invoke-Pester '.\tools\tests\FlairX.CustomUpdate.Tests.ps1'"
~~~

Expected: FAIL because the module/functions are missing.

- [ ] **Step 3: Implement the minimal helpers**

~~~powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FlairXInstallRoot {
    param([Parameter(Mandatory)][string]$InstallPath)
    $resolved = [IO.Path]::GetFullPath($InstallPath).TrimEnd('\')
    if ($resolved -eq [IO.Path]::GetPathRoot($resolved).TrimEnd('\')) { throw "Drive roots are unsafe: $resolved" }
    $exe = Join-Path $resolved 'app\FlairX Mod Manager.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing FlairX executable: $exe" }
    $resolved
}

function ConvertFrom-FlairXReleaseJson {
    param([Parameter(Mandatory)][string]$Json)
    $release = $Json | ConvertFrom-Json
    if ($release.prerelease -or [string]::IsNullOrWhiteSpace($release.tag_name)) { throw 'Not a stable release.' }
    $asset = $release.assets | Where-Object name -Like '*.zip' | Select-Object -First 1
    if ($null -eq $asset -or [string]::IsNullOrWhiteSpace($asset.browser_download_url)) { throw 'Release ZIP missing.' }
    [pscustomobject]@{ Tag = [string]$release.tag_name; DownloadUrl = [string]$asset.browser_download_url }
}

function Assert-FlairXPushTarget {
    param([string]$Remote, [string]$Branch)
    if ($Remote -ne 'fork' -or $Branch -ne 'custom/gamebanana-enhancements') { throw "Unsafe push target: $Remote/$Branch" }
}

Export-ModuleMember -Function Resolve-FlairXInstallRoot,ConvertFrom-FlairXReleaseJson,Assert-FlairXPushTarget
~~~

- [ ] **Step 4: Re-run Pester and verify GREEN**

Expected: all Task 1 tests pass.

- [ ] **Step 5: Commit**

~~~powershell
git add tools/FlairX.CustomUpdate.psm1 tools/tests/FlairX.CustomUpdate.Tests.ps1
git commit -m "feat: add FlairX update safety helpers"
~~~

### Task 2: Testable stage gates and backup exclusion

**Files:**
- Modify: tools/FlairX.CustomUpdate.psm1
- Modify: tools/tests/FlairX.CustomUpdate.Tests.ps1

- [ ] **Step 1: Add failing stage tests**

~~~powershell
Describe 'Invoke-FlairXUpdatePipeline' {
    It 'stops before deployment when a validation stage fails' {
        $calls = New-Object System.Collections.ArrayList
        $steps = [ordered]@{
            Sync = { [void]$calls.Add('Sync') }
            Test = { [void]$calls.Add('Test'); throw 'failed' }
            Deploy = { [void]$calls.Add('Deploy') }
        }
        { Invoke-FlairXUpdatePipeline $steps } | Should Throw
        ($calls -join ',') | Should Be 'Sync,Test'
    }
    It 'runs no stage in preview mode' {
        $calls = New-Object System.Collections.ArrayList
        $steps = [ordered]@{ Sync = { [void]$calls.Add('Sync') }; Deploy = { [void]$calls.Add('Deploy') } }
        $names = Invoke-FlairXUpdatePipeline $steps -Preview
        $calls.Count | Should Be 0
        ($names -join ',') | Should Be 'Sync,Deploy'
    }
}

Describe 'Get-FlairXBackupArguments' {
    It 'excludes the backup directory' {
        (Get-FlairXBackupArguments 'E:\flairx' 'E:\flairx\backups') -join ' ' | Should Match '/XD E:\\flairx\\backups'
    }
}
~~~

- [ ] **Step 2: Run Pester and observe RED for the two missing functions**

- [ ] **Step 3: Add minimal implementations**

~~~powershell
function Invoke-FlairXUpdatePipeline {
    param([System.Collections.IDictionary]$Steps, [switch]$Preview)
    if ($Preview) { return @($Steps.Keys) }
    foreach ($name in $Steps.Keys) { Write-Host "==> $name"; & $Steps[$name] }
}

function Get-FlairXBackupArguments {
    param([string]$InstallRoot, [string]$BackupRoot)
    @($InstallRoot, '__DESTINATION__', '/E', '/COPY:DAT', '/DCOPY:DAT', '/R:2', '/W:1', '/XD', $BackupRoot)
}

Export-ModuleMember -Function Resolve-FlairXInstallRoot,ConvertFrom-FlairXReleaseJson,Assert-FlairXPushTarget,Invoke-FlairXUpdatePipeline,Get-FlairXBackupArguments
~~~

- [ ] **Step 4: Re-run Pester and verify all tests pass**

- [ ] **Step 5: Commit**

~~~powershell
git add tools/FlairX.CustomUpdate.psm1 tools/tests/FlairX.CustomUpdate.Tests.ps1
git commit -m "feat: gate custom update deployment stages"
~~~

### Task 3: One-command updater

**Files:**
- Create: tools/Update-CustomFlairX.ps1
- Modify: tools/tests/FlairX.CustomUpdate.Tests.ps1

- [ ] **Step 1: Add failing static contract tests**

~~~powershell
Describe 'Update-CustomFlairX safety contract' {
    $text = Get-Content (Join-Path $PSScriptRoot '..\Update-CustomFlairX.ps1') -Raw -ErrorAction SilentlyContinue
    It 'uses only the fork maintenance branch' {
        $text | Should Match 'Assert-FlairXPushTarget'
        $text | Should Match 'custom/gamebanana-enhancements'
    }
    It 'orders validation before deployment' {
        $text.IndexOf('Test =') | Should BeLessThan $text.IndexOf('Backup =')
        $text.IndexOf('Build =') | Should BeLessThan $text.IndexOf('Deploy =')
    }
    It 'supports side-effect-free preview' {
        $text | Should Match '\[switch\]\$WhatIf'
        $text | Should Match 'Invoke-FlairXUpdatePipeline.+Preview'
    }
}
~~~

- [ ] **Step 2: Run Pester and observe RED because the script is absent**

- [ ] **Step 3: Implement parameters, path checks, clean-tree check, branch/remote checks, and SDK discovery**

~~~powershell
[CmdletBinding()]
param([string]$InstallPath='E:\flairx',[string]$DotNetPath,[switch]$WhatIf)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'FlairX.CustomUpdate.psm1') -Force
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$branch = 'custom/gamebanana-enhancements'
$installRoot = Resolve-FlairXInstallRoot $InstallPath
Assert-FlairXPushTarget fork $branch
if (@(git -C $repoRoot status --porcelain).Count -ne 0) { throw 'Git worktree must be clean.' }
if ((git -C $repoRoot branch --show-current) -ne $branch) { throw "Check out $branch first." }
git -C $repoRoot remote get-url origin | Out-Null
git -C $repoRoot remote get-url fork | Out-Null
if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $portable = [IO.Path]::GetFullPath((Join-Path $repoRoot '..\dotnet-sdk\dotnet.exe'))
    $DotNetPath = if (Test-Path $portable) { $portable } else { (Get-Command dotnet -ErrorAction Stop).Source }
}
if (-not @(& $DotNetPath --list-sdks)) { throw 'A .NET SDK is required; the installed runtime alone is insufficient.' }
~~~

- [ ] **Step 4: Implement the ordered stages**

Use an ordered hashtable named $steps. Its stages and exact behaviors are:

~~~powershell
$steps = [ordered]@{
    Sync = {
        $state.Release = ConvertFrom-FlairXReleaseJson (gh api repos/Jank8/FlairX-Mod-Manager/releases/latest)
        git -C $repoRoot fetch origin "refs/tags/$($state.Release.Tag):refs/tags/$($state.Release.Tag)"
        if ($LASTEXITCODE) { throw 'Fetch failed.' }
        git -C $repoRoot merge-base --is-ancestor $state.Release.Tag HEAD
        if ($LASTEXITCODE) {
            git -C $repoRoot merge --no-edit $state.Release.Tag
            if ($LASTEXITCODE) { git -C $repoRoot merge --abort; throw 'Merge conflict; installation unchanged.' }
        }
    }
    Test = {
        & $DotNetPath test (Join-Path $repoRoot 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj') -c Release -p:Platform=x64
        if ($LASTEXITCODE) { throw 'Tests failed; deployment cancelled.' }
    }
    Build = {
        $state.Temp = Join-Path ([IO.Path]::GetTempPath()) ('FlairX-CustomUpdate-' + [guid]::NewGuid().ToString('N'))
        $state.Publish = Join-Path $state.Temp 'publish'
        New-Item -ItemType Directory $state.Publish | Out-Null
        & $DotNetPath publish (Join-Path $repoRoot 'FlairX-Mod-Manager\FlairX-Mod-Manager.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $state.Publish
        if ($LASTEXITCODE) { throw 'Publish failed; deployment cancelled.' }
    }
    Stage = {
        $zip = Join-Path $state.Temp 'official.zip'
        $extract = Join-Path $state.Temp 'official'
        Invoke-WebRequest $state.Release.DownloadUrl -OutFile $zip
        Expand-Archive $zip $extract
        $state.StageRoot = Get-ChildItem $extract -Directory | Select-Object -First 1 -ExpandProperty FullName
        Get-ChildItem $state.Publish -Force | Copy-Item -Destination (Join-Path $state.StageRoot 'app') -Recurse -Force
        if (-not (Test-Path (Join-Path $state.StageRoot 'FlairX Mod Manager Launcher.exe'))) { throw 'Staged launcher missing.' }
        if (-not (Test-Path (Join-Path $state.StageRoot 'app\FlairX Mod Manager.exe'))) { throw 'Staged app missing.' }
    }
    Backup = {
        $backupRoot = Join-Path $installRoot 'backups'
        New-Item -ItemType Directory $backupRoot -Force | Out-Null
        $state.Backup = Join-Path $backupRoot ((Get-Date -Format yyyyMMdd-HHmmss) + '-custom-update')
        New-Item -ItemType Directory $state.Backup | Out-Null
        $args = Get-FlairXBackupArguments $installRoot $backupRoot; $args[1] = $state.Backup
        & robocopy @args | Out-Host
        if ($LASTEXITCODE -ge 8) { throw 'Backup failed; deployment cancelled.' }
    }
    Deploy = {
        Get-Process 'FlairX Mod Manager','FlairX Mod Manager Launcher' -ErrorAction SilentlyContinue | Stop-Process -Force
        & robocopy $state.StageRoot $installRoot /E /IS /IT /R:2 /W:1 /XD (Join-Path $installRoot 'backups') | Out-Host
        if ($LASTEXITCODE -ge 8) { throw 'Deployment failed.' }
    }
    Verify = {
        $launcher = Join-Path $installRoot 'FlairX Mod Manager Launcher.exe'
        if (-not (Test-Path $launcher)) { throw 'Installed launcher missing.' }
        Start-Process $launcher -WorkingDirectory $installRoot
        Start-Sleep -Seconds 3
        if (-not (Get-Process 'FlairX Mod Manager' -ErrorAction SilentlyContinue)) { throw 'FlairX did not stay running.' }
    }
    Push = {
        Assert-FlairXPushTarget fork $branch
        git -C $repoRoot push fork "HEAD:refs/heads/$branch"
        if ($LASTEXITCODE) { throw 'Deployment succeeded but fork push failed.' }
    }
}
Invoke-FlairXUpdatePipeline $steps -Preview:$WhatIf
~~~

- [ ] **Step 5: Add rollback and guarded cleanup**

Wrap execution in try/catch/finally. Record whether Deploy began. On failure after that point, move only the validated installRoot\app to installRoot\failed-app-yyyyMMdd-HHmmss and restore Backup\app plus root launcher files. In finally, delete state.Temp only after confirming its full path starts with the system temp path plus FlairX-CustomUpdate-. Never use git reset --hard or robocopy /MIR against the install root.

- [ ] **Step 6: Run Pester and verify all contract tests pass**

- [ ] **Step 7: Commit**

~~~powershell
git add tools/Update-CustomFlairX.ps1 tools/tests/FlairX.CustomUpdate.Tests.ps1
git commit -m "feat: add safe custom FlairX updater"
~~~

### Task 4: Durable GitHub Fork and maintenance branch

**Files:** none.

- [ ] **Step 1: Create the Fork**

~~~powershell
gh repo fork Jank8/FlairX-Mod-Manager --clone=false
~~~

Expected: Dyendless/FlairX-Mod-Manager exists. An already-exists response is acceptable.

- [ ] **Step 2: Add and verify the fork remote**

~~~powershell
git remote add fork https://github.com/Dyendless/FlairX-Mod-Manager.git
git remote -v
~~~

Expected: origin points to Jank8 and fork points to Dyendless.

- [ ] **Step 3: Create and publish the durable branch**

~~~powershell
git switch -c custom/gamebanana-enhancements
git push -u fork custom/gamebanana-enhancements
~~~

- [ ] **Step 4: Verify remote and local SHA equality**

~~~powershell
gh api repos/Dyendless/FlairX-Mod-Manager/branches/custom/gamebanana-enhancements --jq .commit.sha
git rev-parse HEAD
~~~

Expected: identical SHAs.

### Task 5: Chinese operations guide

**Files:**
- Create: docs/CUSTOM_UPDATE_ZH-CN.md
- Modify: README.md

- [ ] **Step 1: Write the guide with exact daily commands**

~~~markdown
# FlairX 自定义版更新指南

## 日常更新

pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1 -WhatIf
pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1

默认安装目录是 E:\flairx；其他位置使用 -InstallPath。
冲突时脚本不修改安装目录，请保留错误信息并进行人工合并。
手动回滚时关闭 FlairX，从 E:\flairx\backups 中选择最新的 *-custom-update，
将当前 app 重命名留存，再复制备份中的 app 和根目录启动器文件。
~~~

Also document prerequisites, portable SDK lookup, WhatIf semantics, automatic backup location, conflict handling, and how to verify fork/custom/gamebanana-enhancements.

- [ ] **Step 2: Add a README link**

~~~markdown
## Custom GameBanana maintenance

See [docs/CUSTOM_UPDATE_ZH-CN.md](docs/CUSTOM_UPDATE_ZH-CN.md) for the safe custom update workflow.
~~~

- [ ] **Step 3: Validate and commit**

~~~powershell
rg -n "Update-CustomFlairX|custom/gamebanana-enhancements|手动回滚" docs/CUSTOM_UPDATE_ZH-CN.md README.md
git diff --check
git add docs/CUSTOM_UPDATE_ZH-CN.md README.md
git commit -m "docs: explain custom FlairX updates"
~~~

### Task 6: End-to-end verification and first deployment

**Files:** none unless a defect is found; any defect requires a failing regression test first.

- [ ] **Step 1: Run all PowerShell tests**

~~~powershell
powershell.exe -NoProfile -Command "$r=Invoke-Pester '.\tools\tests\FlairX.CustomUpdate.Tests.ps1' -PassThru; if($r.FailedCount){exit 1}"
~~~

Expected: zero failures.

- [ ] **Step 2: Run all application tests**

~~~powershell
& '..\dotnet-sdk\dotnet.exe' test '.\FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release -p:Platform=x64 --no-restore
~~~

Expected: at least 13 pass, zero fail.

- [ ] **Step 3: Run Release publish**

~~~powershell
& '..\dotnet-sdk\dotnet.exe' publish '.\FlairX-Mod-Manager\FlairX-Mod-Manager.csproj' -c Release -p:Platform=x64 -r win-x64 --self-contained true
~~~

Expected: exit 0 and a publish/FlairX Mod Manager.exe.

- [ ] **Step 4: Prove preview has no side effects**

Record HEAD, newest backup, installed DLL hash, remote SHA, and processes; run:

~~~powershell
pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1 -WhatIf
~~~

Expected: stage names print, but recorded values are unchanged.

- [ ] **Step 5: Run the real workflow**

~~~powershell
pwsh -NoProfile -File .\tools\Update-CustomFlairX.ps1
~~~

Expected: tests/build precede a new backup; the app deploys and launches.

- [ ] **Step 6: Verify artifact and branch identity**

~~~powershell
Get-FileHash '.\FlairX-Mod-Manager\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\FlairX Mod Manager.dll'
Get-FileHash 'E:\flairx\app\FlairX Mod Manager.dll'
gh api repos/Dyendless/FlairX-Mod-Manager/branches/custom/gamebanana-enhancements --jq .commit.sha
git rev-parse HEAD
git status --short
~~~

Expected: DLL hashes match, remote SHA equals local HEAD, worktree is clean.

- [ ] **Step 7: UI acceptance**

Open GameBanana browser and verify the character selector filters cards, Liked and Downloads sorts exist, and list thumbnails are sharper than the 220-pixel previews. If any check fails, write a failing regression test before changing code.

---

## Self-review result

- Spec coverage: every design requirement maps to a task.
- Placeholder scan: no TBD/TODO/deferred implementation remains.
- Type consistency: helper names, remotes, branch name, install layout, and commands match throughout.


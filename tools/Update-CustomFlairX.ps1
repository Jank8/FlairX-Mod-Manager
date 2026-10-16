[CmdletBinding()]
param(
    [string]$InstallPath = 'E:\flairx',
    [string]$DotNetPath,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'FlairX.CustomUpdate.psm1') -Force

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$branch = 'custom/gamebanana-enhancements'
$installRoot = Resolve-FlairXInstallRoot $InstallPath
$state = @{
    Temp = $null
    Backup = $null
    DeployStarted = $false
    DeploymentVerified = $false
}

Assert-FlairXPushTarget fork $branch

$dirtyPaths = @(git -C $repoRoot status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the Git worktree.'
}
if ($dirtyPaths.Count -ne 0) {
    throw 'The Git worktree must be clean before updating.'
}

$currentBranch = (git -C $repoRoot branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or $currentBranch -ne $branch) {
    throw "Check out $branch before updating."
}

$originUrl = (git -C $repoRoot remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
    throw 'The official origin remote is missing.'
}
$forkUrl = (git -C $repoRoot remote get-url fork).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($forkUrl)) {
    throw 'The user fork remote is missing.'
}

Get-Command gh -ErrorAction Stop | Out-Null
gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI is not authenticated.'
}

if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $sdkCandidates = New-Object System.Collections.Generic.List[string]
    $cursor = [IO.DirectoryInfo]$repoRoot
    while ($null -ne $cursor) {
        $sdkCandidates.Add((Join-Path $cursor.FullName 'dotnet-sdk\dotnet.exe'))
        $cursor = $cursor.Parent
    }

    $systemDotNet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $systemDotNet) {
        $sdkCandidates.Add($systemDotNet.Source)
    }

    foreach ($candidate in $sdkCandidates | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }
        $sdkList = @(& $candidate --list-sdks 2>$null)
        if ($LASTEXITCODE -eq 0 -and $sdkList.Count -gt 0) {
            $DotNetPath = $candidate
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($DotNetPath) -or
    -not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) {
    throw 'A .NET SDK is required. Pass its dotnet.exe with -DotNetPath.'
}
$availableSdks = @(& $DotNetPath --list-sdks)
if ($LASTEXITCODE -ne 0 -or $availableSdks.Count -eq 0) {
    throw 'The selected dotnet executable has runtimes only; an SDK is required.'
}

$steps = [ordered]@{
    Sync = {
        $releaseJson = gh api repos/Jank8/FlairX-Mod-Manager/releases/latest | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to query the latest official release.'
        }
        $state.Release = ConvertFrom-FlairXReleaseJson $releaseJson

        git -C $repoRoot fetch origin "refs/tags/$($state.Release.Tag):refs/tags/$($state.Release.Tag)"
        if ($LASTEXITCODE -ne 0) {
            throw "Fetching official release $($state.Release.Tag) failed."
        }

        git -C $repoRoot merge-base --is-ancestor $state.Release.Tag HEAD
        if ($LASTEXITCODE -ne 0) {
            git -C $repoRoot merge --no-edit $state.Release.Tag
            if ($LASTEXITCODE -ne 0) {
                git -C $repoRoot merge --abort
                throw 'The official release conflicts with the custom branch. The installation was not changed.'
            }
        }
    }

    Test = {
        & $DotNetPath test `
            (Join-Path $repoRoot 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj') `
            -c Release `
            -p:Platform=x64
        if ($LASTEXITCODE -ne 0) {
            throw 'Tests failed. Deployment was cancelled.'
        }
    }

    Build = {
        $state.Temp = Join-Path `
            ([IO.Path]::GetTempPath()) `
            ('FlairX-CustomUpdate-' + [guid]::NewGuid().ToString('N'))
        if (-not (Test-FlairXTemporaryPath $state.Temp)) {
            throw "Unsafe temporary path: $($state.Temp)"
        }

        $state.Publish = Join-Path $state.Temp 'publish'
        New-Item -ItemType Directory -Path $state.Publish | Out-Null

        & $DotNetPath publish `
            (Join-Path $repoRoot 'FlairX-Mod-Manager\FlairX-Mod-Manager.csproj') `
            -c Release `
            -p:Platform=x64 `
            -r win-x64 `
            --self-contained true `
            -o $state.Publish
        if ($LASTEXITCODE -ne 0) {
            throw 'Release publish failed. Deployment was cancelled.'
        }
    }

    Stage = {
        $zipPath = Join-Path $state.Temp 'official.zip'
        $extractPath = Join-Path $state.Temp 'official'
        Invoke-WebRequest -Uri $state.Release.DownloadUrl -OutFile $zipPath
        Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath

        $releaseRoots = @(Get-ChildItem -LiteralPath $extractPath -Directory)
        if ($releaseRoots.Count -ne 1) {
            throw 'The official release archive has an unexpected root layout.'
        }
        $state.StageRoot = $releaseRoots[0].FullName
        $stageApp = Join-Path $state.StageRoot 'app'
        Get-ChildItem -LiteralPath $state.Publish -Force |
            Copy-Item -Destination $stageApp -Recurse -Force

        if (-not (Test-Path -LiteralPath (Join-Path $state.StageRoot 'FlairX Mod Manager Launcher.exe') -PathType Leaf)) {
            throw 'The staged launcher is missing.'
        }
        if (-not (Test-Path -LiteralPath (Join-Path $stageApp 'FlairX Mod Manager.exe') -PathType Leaf)) {
            throw 'The staged main executable is missing.'
        }
    }

    Backup = {
        $backupRoot = Join-Path $installRoot 'backups'
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        $state.Backup = Join-Path `
            $backupRoot `
            ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-custom-update')
        New-Item -ItemType Directory -Path $state.Backup | Out-Null

        $backupArguments = Get-FlairXBackupArguments $installRoot $backupRoot
        $backupArguments[1] = $state.Backup
        & robocopy @backupArguments | Out-Host
        if ($LASTEXITCODE -ge 8) {
            throw 'Backing up the current installation failed. Deployment was cancelled.'
        }
    }

    Deploy = {
        $state.DeployStarted = $true
        Get-Process 'FlairX Mod Manager', 'FlairX Mod Manager Launcher' -ErrorAction SilentlyContinue |
            Stop-Process -Force

        $backupRoot = Join-Path $installRoot 'backups'
        & robocopy `
            $state.StageRoot `
            $installRoot `
            /E /IS /IT /R:2 /W:1 `
            /XD $backupRoot `
            /XF *.log *.tmp |
            Out-Host
        if ($LASTEXITCODE -ge 8) {
            throw 'Copying the staged release into the installation failed.'
        }
    }

    Verify = {
        $launcher = Join-Path $installRoot 'FlairX Mod Manager Launcher.exe'
        $mainExe = Join-Path $installRoot 'app\FlairX Mod Manager.exe'
        if (-not (Test-Path -LiteralPath $launcher -PathType Leaf) -or
            -not (Test-Path -LiteralPath $mainExe -PathType Leaf)) {
            throw 'Installed FlairX executables are missing.'
        }

        Start-Process -FilePath $launcher -WorkingDirectory $installRoot
        Start-Sleep -Seconds 5
        if ($null -eq (Get-Process 'FlairX Mod Manager' -ErrorAction SilentlyContinue)) {
            throw 'FlairX did not stay running after deployment.'
        }
        $state.DeploymentVerified = $true
    }

    Push = {
        Assert-FlairXPushTarget fork $branch
        git -C $repoRoot push fork "HEAD:refs/heads/$branch"
        if ($LASTEXITCODE -ne 0) {
            throw 'Deployment succeeded, but pushing the maintenance branch failed.'
        }
    }
}

try {
    if ($WhatIf) {
        $previewStages = Invoke-FlairXUpdatePipeline $steps -Preview
        Write-Host 'Preview only. No update stage was executed.'
        $previewStages | ForEach-Object { Write-Host "  - $_" }
    } else {
        Invoke-FlairXUpdatePipeline $steps
    }
} catch {
    if ($state.DeployStarted -and
        -not $state.DeploymentVerified -and
        -not [string]::IsNullOrWhiteSpace([string]$state.Backup) -and
        (Test-Path -LiteralPath $state.Backup -PathType Container)) {
        Write-Warning 'Deployment verification failed. Restoring the previous installation.'
        $backupRoot = Join-Path $installRoot 'backups'
        $installedApp = Join-Path $installRoot 'app'
        $failedApp = Join-Path `
            $backupRoot `
            ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-failed-deployment-app')

        if (Test-Path -LiteralPath $installedApp -PathType Container) {
            Move-Item -LiteralPath $installedApp -Destination $failedApp
        }
        Copy-Item `
            -LiteralPath (Join-Path $state.Backup 'app') `
            -Destination $installRoot `
            -Recurse `
            -Force
        Get-ChildItem -LiteralPath $state.Backup -File -Force |
            Copy-Item -Destination $installRoot -Force
    }
    throw
} finally {
    if (-not [string]::IsNullOrWhiteSpace([string]$state.Temp) -and
        (Test-FlairXTemporaryPath $state.Temp) -and
        (Test-Path -LiteralPath $state.Temp -PathType Container)) {
        Remove-Item -LiteralPath $state.Temp -Recurse -Force
    }
}

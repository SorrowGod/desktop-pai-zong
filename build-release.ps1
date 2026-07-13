param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$Version = '1.0.0',
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'artifacts\release')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$workspace = (Resolve-Path $PSScriptRoot).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $outputPath.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output must stay inside the workspace: $outputPath"
}

if (Test-Path -LiteralPath $outputPath) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputPath).Path
    if (-not $resolvedOutput.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove release output outside workspace: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

$publishDirectory = Join-Path $outputPath 'publish'
$installerDirectory = Join-Path $outputPath 'installer'
$testResultsDirectory = Join-Path $outputPath 'test-results'
New-Item -ItemType Directory -Force -Path $publishDirectory, $installerDirectory, $testResultsDirectory | Out-Null

$env:DOTNET_CLI_HOME = Join-Path $workspace '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspace '.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$logPath = Join-Path $outputPath 'build-release.log'
Start-Transcript -LiteralPath $logPath -Force | Out-Null

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [scriptblock]$Command
    )
    Write-Output "[$([DateTimeOffset]::Now.ToString('O'))] START $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
    Write-Output "[$([DateTimeOffset]::Now.ToString('O'))] PASS  $Name"
}

try {
    Push-Location $workspace
    try {
        Invoke-Checked 'restore' {
            dotnet restore .\DesktopPet.sln -r $RuntimeIdentifier
        }

        Invoke-Checked 'test' {
            dotnet test .\tests\DesktopPet.Tests\DesktopPet.Tests.csproj `
                -c $Configuration `
                --no-restore `
                --results-directory $testResultsDirectory `
                --logger 'trx;LogFileName=desktop-pet-release-tests.trx'
        }

        Invoke-Checked 'publish' {
            dotnet publish .\app\DesktopPet.App\DesktopPet.App.csproj `
                -c $Configuration `
                -r $RuntimeIdentifier `
                --self-contained true `
                --no-restore `
                -o $publishDirectory `
                -p:PublishSingleFile=true `
                -p:IncludeNativeLibrariesForSelfExtract=true `
                -p:PublishTrimmed=false `
                -p:DebugType=None `
                -p:DebugSymbols=false
        }

        $publishedExecutable = Join-Path $publishDirectory 'DesktopPet.exe'
        if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
            throw "Publish completed without DesktopPet.exe: $publishedExecutable"
        }

        $isccCandidates = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        )
        $isccPath = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($isccPath)) {
            throw 'Inno Setup 6 was not found. Install Inno Setup 6 before building the installer.'
        }

        Invoke-Checked 'installer' {
            & $isccPath `
                "/DSourceDir=$publishDirectory" `
                "/DOutputDir=$installerDirectory" `
                "/DAppVersion=$Version" `
                '.\installer\DesktopPet.iss'
        }

        $installerPath = Join-Path $installerDirectory "DesktopPet-Setup-$Version-win-x64.exe"
        if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
            throw "Inno Setup completed without the expected installer: $installerPath"
        }

        $hashes = @(
            Get-FileHash -LiteralPath $publishedExecutable -Algorithm SHA256
            Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
        )
        $checksumPath = Join-Path $outputPath 'checksums.sha256'
        $hashes | ForEach-Object { "$($_.Hash.ToLowerInvariant()) *$($_.Path)" } |
            Set-Content -LiteralPath $checksumPath -Encoding ASCII

        Write-Output "Release build completed."
        Write-Output "EXE:       $publishedExecutable"
        Write-Output "Installer: $installerPath"
        Write-Output "Tests:     $(Join-Path $testResultsDirectory 'desktop-pet-release-tests.trx')"
        Write-Output "Checksums: $checksumPath"
        Write-Output "Log:       $logPath"
    }
    finally {
        Pop-Location
    }
}
finally {
    Stop-Transcript | Out-Null
}

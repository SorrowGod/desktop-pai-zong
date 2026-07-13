param(
    [string]$Executable = (Join-Path $PSScriptRoot '..\app\DesktopPet.App\bin\Debug\net8.0-windows\win-x64\DesktopPet.exe'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\gui-validation'),
    [int]$DurationSeconds = 65
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$executablePath = (Resolve-Path $Executable).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$dataPath = Join-Path $workspace '.gui-test-data'
if (-not $outputPath.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must stay inside the workspace: $outputPath"
}
if (-not $dataPath.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Data directory must stay inside the workspace: $dataPath"
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
if (Test-Path -LiteralPath $dataPath) {
    $resolvedDataPath = (Resolve-Path -LiteralPath $dataPath).Path
    if (-not $resolvedDataPath.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove test data outside workspace: $resolvedDataPath"
    }
    Remove-Item -LiteralPath $resolvedDataPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $dataPath | Out-Null

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class GuiNative
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rectangle);
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")]
    public static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [DllImport("gdi32.dll")]
    public static extern int GetRgnBox(IntPtr region, out RECT rectangle);
    [DllImport("gdi32.dll")]
    public static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr handle);

    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
    public const uint RightDown = 0x0008;
    public const uint RightUp = 0x0010;
    public const uint Move = 0x0001;
    public const uint KeyUp = 0x0002;
    public const byte Escape = 0x1B;
    public const uint Close = 0x0010;

    public static List<IntPtr> WindowsForProcess(uint processId)
    {
        var result = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var candidate);
            if (candidate == processId && IsWindowVisible(hwnd)) result.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static string Title(IntPtr hwnd)
    {
        var text = new StringBuilder(512);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }
}
'@

function Wait-Until {
    param([scriptblock]$Condition, [int]$TimeoutMilliseconds = 5000, [string]$Description = 'condition')
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        [System.Windows.Forms.Application]::DoEvents()
        $result = & $Condition
        if ($result) { return $result }
        Start-Sleep -Milliseconds 50
    }
    throw "Timed out waiting for $Description"
}

function Get-WindowRecord {
    param([IntPtr]$Handle)
    $rectangle = New-Object GuiNative+RECT
    [void][GuiNative]::GetWindowRect($Handle, [ref]$rectangle)
    [pscustomobject]@{
        Handle = $Handle
        HandleValue = $Handle.ToInt64()
        Title = [GuiNative]::Title($Handle)
        Left = $rectangle.Left
        Top = $rectangle.Top
        Right = $rectangle.Right
        Bottom = $rectangle.Bottom
        Width = $rectangle.Right - $rectangle.Left
        Height = $rectangle.Bottom - $rectangle.Top
    }
}

function Get-ProcessWindows {
    param([int]$ProcessId)
    @([GuiNative]::WindowsForProcess([uint32]$ProcessId) | ForEach-Object { Get-WindowRecord $_ })
}

function Invoke-MouseClick {
    param([int]$X, [int]$Y, [switch]$Right)
    [void][GuiNative]::SetCursorPos($X, $Y)
    Start-Sleep -Milliseconds 60
    if ($Right) {
        [GuiNative]::mouse_event([GuiNative]::RightDown, 0, 0, 0, [UIntPtr]::Zero)
        [GuiNative]::mouse_event([GuiNative]::RightUp, 0, 0, 0, [UIntPtr]::Zero)
    } else {
        [GuiNative]::mouse_event([GuiNative]::LeftDown, 0, 0, 0, [UIntPtr]::Zero)
        [GuiNative]::mouse_event([GuiNative]::LeftUp, 0, 0, 0, [UIntPtr]::Zero)
    }
}

function Invoke-MouseDrag {
    param([int]$StartX, [int]$StartY, [int]$DeltaX, [int]$DeltaY)
    [void][GuiNative]::SetCursorPos($StartX, $StartY)
    [GuiNative]::mouse_event([GuiNative]::LeftDown, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 8; $step++) {
        [void][GuiNative]::SetCursorPos(
            $StartX + [int]($DeltaX * $step / 8),
            $StartY + [int]($DeltaY * $step / 8))
        [GuiNative]::mouse_event([GuiNative]::Move, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 30
    }
    [GuiNative]::mouse_event([GuiNative]::LeftUp, 0, 0, 0, [UIntPtr]::Zero)
}

function Save-Screenshot {
    param([string]$Name, [System.Drawing.Rectangle]$Rectangle)
    $bitmap = New-Object System.Drawing.Bitmap($Rectangle.Width, $Rectangle.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($Rectangle.Left, $Rectangle.Top, 0, 0, $Rectangle.Size)
        } finally {
            $graphics.Dispose()
        }
        $path = Join-Path $outputPath $Name
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        return $path
    } finally {
        $bitmap.Dispose()
    }
}

function Start-SecondaryCommand {
    param([string]$Command)
    $environment = @{ DESKTOPPET_DATA_ROOT = $dataPath }
    $secondary = Start-Process -FilePath $executablePath -ArgumentList "--$Command" -PassThru -Environment $environment
    if (-not $secondary.WaitForExit(5000)) {
        throw "Secondary instance did not exit for command --$Command"
    }
    return $secondary.Id
}

$environment = @{ DESKTOPPET_DATA_ROOT = $dataPath }
$primary = $null
$restart = $null
$underlay = $null
$results = [ordered]@{
    executable = $executablePath
    startedAt = [DateTimeOffset]::Now.ToString('O')
    durationSeconds = $DurationSeconds
}
$stage = 'initialized'

function Set-ValidationStage {
    param([string]$Name)
    $script:stage = $Name
    $script:results.stage = $Name
    $script:results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputPath 'gui-validation.partial.json') -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $outputPath 'stage.log') -Value $Name -Encoding UTF8
    Write-Output "GUI validation stage: $Name"
}

try {
    Set-ValidationStage 'starting-primary'
    $primary = Start-Process -FilePath $executablePath -PassThru -Environment $environment
    $results.primaryPid = $primary.Id
    $pet = Wait-Until -Description 'PetWindow' -Condition {
        Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    }
    Start-Sleep -Milliseconds 1000
    $pet = Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    Set-ValidationStage 'pet-window-found'
    $results.petInitialBounds = $pet
    $results.petDpi = [GuiNative]::GetDpiForWindow($pet.Handle)

    $regionEvidence = Wait-Until -Description 'non-empty alpha window region' -TimeoutMilliseconds 5000 -Condition {
        $region = [GuiNative]::CreateRectRgn(0, 0, 0, 0)
        try {
            $regionType = [GuiNative]::GetWindowRgn($pet.Handle, $region)
            $regionBox = New-Object GuiNative+RECT
            [void][GuiNative]::GetRgnBox($region, [ref]$regionBox)
            $centerX = [int]($pet.Width / 2)
            $centerY = [int]($pet.Height / 2)
            $evidence = [ordered]@{
                type = $regionType
                box = [ordered]@{ left=$regionBox.Left; top=$regionBox.Top; right=$regionBox.Right; bottom=$regionBox.Bottom }
                cornerInside = [GuiNative]::PtInRegion($region, 1, 1)
                centerInside = [GuiNative]::PtInRegion($region, $centerX, $centerY)
            }
            if ($regionType -ne 0 -and $evidence.centerInside) { return $evidence }
            return $null
        } finally {
            [void][GuiNative]::DeleteObject($region)
        }
    }
    $results.region = $regionEvidence
    Set-ValidationStage 'alpha-region-inspected'

    $script:underlayClicks = 0
    $underlay = New-Object System.Windows.Forms.Form
    $underlay.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $underlay.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $underlay.Location = [System.Drawing.Point]::new($pet.Left, $pet.Top)
    $underlay.Size = [System.Drawing.Size]::new($pet.Width, $pet.Height)
    $underlay.BackColor = [System.Drawing.Color]::FromArgb(65, 89, 110)
    $underlay.Text = 'DesktopPet validation underlay'
    $underlay.add_MouseDown({ $script:underlayClicks++ })
    $underlay.Show()
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 200
    $underlayHandle = $underlay.Handle
    Set-ValidationStage 'underlay-ready'

    $cornerScreenX = $pet.Left + 2
    $cornerScreenY = $pet.Top + 2
    $centerScreenX = $pet.Left + [int]($pet.Width / 2)
    $centerScreenY = $pet.Top + [int]($pet.Height / 2)
    $cornerPoint = New-Object GuiNative+POINT -ArgumentList $cornerScreenX, $cornerScreenY
    $centerPoint = New-Object GuiNative+POINT -ArgumentList $centerScreenX, $centerScreenY
    $cornerHit = [GuiNative]::WindowFromPoint($cornerPoint)
    $centerHit = [GuiNative]::WindowFromPoint($centerPoint)
    Invoke-MouseClick $cornerScreenX $cornerScreenY
    Start-Sleep -Milliseconds 150
    [System.Windows.Forms.Application]::DoEvents()
    $clicksAfterCorner = $script:underlayClicks
    $foregroundBeforePet = [GuiNative]::GetForegroundWindow()
    Invoke-MouseClick $centerScreenX $centerScreenY
    Start-Sleep -Milliseconds 700
    [System.Windows.Forms.Application]::DoEvents()
    $clicksAfterCenter = $script:underlayClicks
    $foregroundAfterPet = [GuiNative]::GetForegroundWindow()
    $results.hitTesting = [ordered]@{
        underlayWindow = $underlayHandle.ToInt64()
        cornerWindow = $cornerHit.ToInt64()
        centerWindow = $centerHit.ToInt64()
        petWindow = $pet.HandleValue
        underlayClicksAfterCorner = $clicksAfterCorner
        underlayClicksAfterCenter = $clicksAfterCenter
        transparentCornerPassedThrough = ($cornerHit -eq $underlayHandle)
        visibleCenterDidNotPassThrough = ($centerHit -eq $pet.Handle)
        underlayClickEventObserved = ($clicksAfterCorner -gt 0)
        foregroundBeforePetClick = $foregroundBeforePet.ToInt64()
        foregroundAfterPetClick = $foregroundAfterPet.ToInt64()
        petDidNotActivate = ($foregroundAfterPet -ne $pet.Handle)
    }
    $bubble = Get-ProcessWindows $primary.Id | Where-Object { $_.HandleValue -ne $pet.HandleValue -and [string]::IsNullOrEmpty($_.Title) } | Select-Object -First 1
    $results.bubble = if ($null -ne $bubble) {
        [ordered]@{
            window = $bubble
            independentFromPetWindow = ($bubble.HandleValue -ne $pet.HandleValue)
            avoidsScreenEdge = ($bubble.Left -ge [System.Windows.Forms.SystemInformation]::VirtualScreen.Left -and $bubble.Right -le [System.Windows.Forms.SystemInformation]::VirtualScreen.Right)
        }
    } else { $null }
    Set-ValidationStage 'hit-testing-complete'
    $petCapture = [System.Drawing.Rectangle]::new($pet.Left - 30, $pet.Top - 30, $pet.Width + 60, $pet.Height + 60)
    $results.petScreenshot = Save-Screenshot 'pet-alpha.png' $petCapture
    if ($null -ne $bubble) {
        $unionLeft = [Math]::Min($pet.Left, $bubble.Left) - 20
        $unionTop = [Math]::Min($pet.Top, $bubble.Top) - 20
        $unionRight = [Math]::Max($pet.Right, $bubble.Right) + 20
        $unionBottom = [Math]::Max($pet.Bottom, $bubble.Bottom) + 20
        $results.bubbleScreenshot = Save-Screenshot 'bubble-window.png' ([System.Drawing.Rectangle]::new($unionLeft, $unionTop, $unionRight - $unionLeft, $unionBottom - $unionTop))
    }
    $underlay.Hide()
    Start-Sleep -Milliseconds 300
    $results.petNormalScreenshot = Save-Screenshot 'pet-normal.png' $petCapture

    Invoke-MouseDrag $centerScreenX $centerScreenY -80 -40
    Start-Sleep -Milliseconds 500
    $petAfterDrag = Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    $results.drag = [ordered]@{
        before = $pet
        after = $petAfterDrag
        moved = ($petAfterDrag.Left -ne $pet.Left -or $petAfterDrag.Top -ne $pet.Top)
        remainedVisible = $null -ne $petAfterDrag
    }
    Set-ValidationStage 'drag-complete'

    $dragCenterX = $petAfterDrag.Left + [int]($petAfterDrag.Width / 2)
    $dragCenterY = $petAfterDrag.Top + [int]($petAfterDrag.Height / 2)
    $windowsBeforeMenu = @(Get-ProcessWindows $primary.Id | ForEach-Object { $_.HandleValue })
    Invoke-MouseClick -X $dragCenterX -Y $dragCenterY -Right
    Start-Sleep -Milliseconds 300
    $newMenuWindow = Get-ProcessWindows $primary.Id | Where-Object { $windowsBeforeMenu -notcontains $_.HandleValue } | Select-Object -First 1
    $results.rightClick = [ordered]@{
        contextWindow = if ($null -ne $newMenuWindow) { $newMenuWindow.HandleValue } else { 0 }
        appeared = ($null -ne $newMenuWindow)
    }
    [GuiNative]::keybd_event([GuiNative]::Escape, 0, 0, [UIntPtr]::Zero)
    [GuiNative]::keybd_event([GuiNative]::Escape, 0, [GuiNative]::KeyUp, [UIntPtr]::Zero)
    Set-ValidationStage 'context-menu-complete'

    Invoke-MouseClick $dragCenterX $dragCenterY
    Start-Sleep -Milliseconds 80
    Invoke-MouseClick $dragCenterX $dragCenterY
    $chat = Wait-Until -Description 'ChatWindow' -Condition {
        Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq '和猫咪聊天' } | Select-Object -First 1
    }
    Start-Sleep -Milliseconds 600
    $secondaryChatPid = Start-SecondaryCommand 'chat'
    $results.chat = [ordered]@{
        secondaryPid = $secondaryChatPid
        window = $chat
        singlePrimaryRemains = ((Get-Process -Id $primary.Id -ErrorAction SilentlyContinue) -ne $null)
        secondDesktopPetProcessCount = @(Get-Process DesktopPet -ErrorAction SilentlyContinue).Count
    }
    $results.chatScreenshot = Save-Screenshot 'chat-window.png' ([System.Drawing.Rectangle]::new($chat.Left, $chat.Top, $chat.Width, $chat.Height))
    [void][GuiNative]::PostMessage($chat.Handle, [GuiNative]::Close, [IntPtr]::Zero, [IntPtr]::Zero)
    [void](Wait-Until -Description 'closed ChatWindow' -Condition {
        -not (Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq '和猫咪聊天' })
    })
    $results.chat.closedWithoutExitingPet = -not $primary.HasExited
    Set-ValidationStage 'chat-window-complete'

    $secondarySettingsPid = Start-SecondaryCommand 'settings'
    $settings = Wait-Until -Description 'SettingsWindow' -Condition {
        Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq '桌面猫咪设置' } | Select-Object -First 1
    }
    Start-Sleep -Milliseconds 600
    $results.settings = [ordered]@{ secondaryPid = $secondarySettingsPid; window = $settings }
    $results.settingsScreenshot = Save-Screenshot 'settings-window.png' ([System.Drawing.Rectangle]::new($settings.Left, $settings.Top, $settings.Width, $settings.Height))
    [void][GuiNative]::PostMessage($settings.Handle, [GuiNative]::Close, [IntPtr]::Zero, [IntPtr]::Zero)
    [void](Wait-Until -Description 'closed SettingsWindow' -Condition {
        -not (Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq '桌面猫咪设置' })
    })
    $results.settings.closedWithoutExitingPet = -not $primary.HasExited
    Set-ValidationStage 'settings-window-complete'

    $secondaryHidePid = Start-SecondaryCommand 'hide'
    Start-Sleep -Milliseconds 300
    $hidden = -not (Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' })
    $secondaryShowPid = Start-SecondaryCommand 'show'
    $shownPet = Wait-Until -Description 'restored PetWindow' -Condition {
        Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    }
    $results.visibility = [ordered]@{
        hideSecondaryPid = $secondaryHidePid
        showSecondaryPid = $secondaryShowPid
        hidden = $hidden
        restored = ($null -ne $shownPet)
    }
    Set-ValidationStage 'visibility-complete'

    Set-ValidationStage 'stability-loop'
    $loopStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $iterations = 0
    while ($loopStopwatch.Elapsed.TotalSeconds -lt $DurationSeconds) {
        if ($primary.HasExited) { throw 'DesktopPet exited during stability validation.' }
        $command = switch ($iterations % 4) { 0 {'hide'} 1 {'show'} 2 {'chat'} default {'settings'} }
        [void](Start-SecondaryCommand $command)
        if (($iterations % 4) -eq 1) {
            $loopPet = Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
            if ($null -ne $loopPet) {
                $direction = if (($iterations % 8) -eq 1) { 24 } else { -24 }
                Invoke-MouseDrag ($loopPet.Left + [int]($loopPet.Width / 2)) ($loopPet.Top + [int]($loopPet.Height / 2)) $direction 12
            }
        }
        $iterations++
        Start-Sleep -Milliseconds 700
        [System.Windows.Forms.Application]::DoEvents()
    }
    $results.stability = [ordered]@{
        elapsedSeconds = [Math]::Round($loopStopwatch.Elapsed.TotalSeconds, 2)
        iterations = $iterations
        primaryAlive = (-not $primary.HasExited)
        desktopPetProcessCount = @(Get-Process DesktopPet -ErrorAction SilentlyContinue).Count
    }
    $petBeforeExit = Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    if ($null -eq $petBeforeExit) {
        [void](Start-SecondaryCommand 'show')
        $petBeforeExit = Wait-Until -Description 'PetWindow before exit' -Condition {
            Get-ProcessWindows $primary.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
        }
    }
    Set-ValidationStage 'stability-complete'

    [void](Start-SecondaryCommand 'exit')
    $results.cleanExit = $primary.WaitForExit(10000)
    $results.primaryExitCode = if ($primary.HasExited) { $primary.ExitCode } else { $null }
    $restart = Start-Process -FilePath $executablePath -PassThru -Environment $environment
    $restoredPet = Wait-Until -Description 'restarted PetWindow' -Condition {
        Get-ProcessWindows $restart.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    }
    Start-Sleep -Milliseconds 1000
    $restoredPet = Get-ProcessWindows $restart.Id | Where-Object { $_.Title -eq 'DesktopPet' } | Select-Object -First 1
    $results.positionRestore = [ordered]@{
        expectedLeft = $petBeforeExit.Left
        expectedTop = $petBeforeExit.Top
        actualLeft = $restoredPet.Left
        actualTop = $restoredPet.Top
        restored = ($restoredPet.Left -eq $petBeforeExit.Left -and $restoredPet.Top -eq $petBeforeExit.Top)
    }
    [void](Start-SecondaryCommand 'exit')
    $results.restartCleanExit = $restart.WaitForExit(10000)
    $results.completedAt = [DateTimeOffset]::Now.ToString('O')
    Set-ValidationStage 'complete'
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputPath 'gui-validation.json') -Encoding UTF8
    $results | ConvertTo-Json -Depth 8
} catch {
    $results.failedAt = $stage
    $results.failure = $_.Exception.ToString()
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputPath 'gui-validation.failed.json') -Encoding UTF8
    throw
} finally {
    if ($underlay -ne $null) {
        $underlay.Close()
        $underlay.Dispose()
    }
    if ($primary -ne $null -and -not $primary.HasExited) {
        try { [void](Start-SecondaryCommand 'exit') } catch { Stop-Process -Id $primary.Id -Force -ErrorAction SilentlyContinue }
    }
    if ($restart -ne $null -and -not $restart.HasExited) {
        Stop-Process -Id $restart.Id -Force -ErrorAction SilentlyContinue
    }
}

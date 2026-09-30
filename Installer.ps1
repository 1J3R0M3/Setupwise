<#
    Winget Installer
    Grafische Oberfläche (WPF, Windows-11-Stil) zum Installieren und Aktualisieren
    von Programmen über winget.

    Start über Installer.bat – diese umgeht die Ausführungsrichtlinie nur für
    diesen einen Aufruf, am System wird dabei nichts dauerhaft geändert.

    Hinweis: Diese Datei muss als "UTF-8 mit BOM" gespeichert bleiben, sonst
    zeigt Windows PowerShell 5.1 Umlaute falsch an.
#>
param(
    [switch]$Elevated,   # intern: Skript wurde bereits mit Adminrechten neu gestartet
    [string]$LogDir      # intern: Desktop des ursprünglichen Benutzers für das Protokoll
)

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

# ==================================================
# 1) Adminrechte und STA-Modus sicherstellen
# ==================================================
if (-not $LogDir) { $LogDir = [Environment]::GetFolderPath('Desktop') }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin   = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$isSta     = [Threading.Thread]::CurrentThread.GetApartmentState() -eq 'STA'

if ($PSCommandPath -and ((-not $isAdmin -and -not $Elevated) -or -not $isSta)) {
    # Neustart mit Adminrechten. -Wait sorgt dafür, dass die Batch wartet,
    # bis das Fenster geschlossen wurde, und den echten Exitcode bekommt.
    $argLine   = '-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "{0}" -Elevated -LogDir "{1}"' -f $PSCommandPath, $LogDir
    $startArgs = @{ FilePath = 'powershell.exe'; ArgumentList = $argLine; Wait = $true; PassThru = $true }
    if (-not $isAdmin) { $startArgs.Verb = 'RunAs' }
    try {
        $proc = Start-Process @startArgs -ErrorAction Stop
        if ($null -eq $proc.ExitCode) { exit 0 }
        exit $proc.ExitCode
    }
    catch {
        # UAC-Abfrage wurde abgelehnt
        if (-not $isSta) { exit 1223 }
        $answer = [System.Windows.MessageBox]::Show(
            "Die Administratorrechte wurden nicht erteilt.`n`nOhne Adminrechte fortfahren? Einige Installationen fragen dann einzeln nach einer Freigabe oder schlagen fehl.",
            'Winget Installer',
            [System.Windows.MessageBoxButton]::YesNo,
            [System.Windows.MessageBoxImage]::Warning)
        if ($answer -ne [System.Windows.MessageBoxResult]::Yes) { exit 1223 }
    }
}

# ==================================================
# 2) Hilfstypen (Datenmodell mit Änderungsbenachrichtigung, DWM-Aufrufe)
# ==================================================
if (-not ('WingetGui.PackageItem' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WingetGui
{
    public class PackageItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }

        private string _name = "", _id = "", _version = "", _available = "", _action = "install";
        private string _status = "", _statusKind = "";
        private bool _isChecked;

        public string Name             { get { return _name; }       set { _name = value ?? ""; Raise("Name"); } }
        public string Id               { get { return _id; }         set { _id = value ?? ""; Raise("Id"); } }
        public string Version          { get { return _version; }    set { _version = value ?? ""; Raise("Version"); Raise("VersionText"); } }
        public string AvailableVersion { get { return _available; }  set { _available = value ?? ""; Raise("AvailableVersion"); Raise("VersionText"); } }
        public string Action           { get { return _action; }     set { _action = value ?? "install"; Raise("Action"); Raise("VersionText"); } }
        public string Status           { get { return _status; }     set { _status = value ?? ""; Raise("Status"); } }
        public string StatusKind       { get { return _statusKind; } set { _statusKind = value ?? ""; Raise("StatusKind"); } }

        public bool IsChecked
        {
            get { return _isChecked; }
            set { if (_isChecked == value) return; _isChecked = value; Raise("IsChecked"); }
        }

        public string VersionText
        {
            get
            {
                if (_action == "upgrade" && _available.Length > 0) return _version + "  →  " + _available;
                return _version;
            }
        }
    }

    public static class Native
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static int SetWindowAttribute(IntPtr hwnd, int attribute, int value)
        {
            try { return DwmSetWindowAttribute(hwnd, attribute, ref value, 4); }
            catch { return -1; }
        }
    }
}
'@
}

# ==================================================
# 3) winget finden
# ==================================================
function Resolve-WingetPath {
    $cmd = Get-Command winget.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Path }

    # Fallback, z. B. wenn der Admin ein anderes Benutzerkonto ist
    $candidates = @(Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\winget.exe')
    $candidates += @(Resolve-Path "$env:ProgramFiles\WindowsApps\Microsoft.DesktopAppInstaller_*__8wekyb3d8bbwe\winget.exe" -ErrorAction SilentlyContinue |
                     ForEach-Object { $_.Path } | Sort-Object -Descending)
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    return $null
}

$wingetPath = Resolve-WingetPath
if (-not $wingetPath) {
    [void][System.Windows.MessageBox]::Show(
        "winget wurde nicht gefunden.`n`nBitte den „App-Installer“ aus dem Microsoft Store installieren oder aktualisieren und das Programm danach erneut starten.",
        'Winget Installer',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error)
    exit 2
}

$wingetVersion = try { [string](& $wingetPath --version 2>$null | Select-Object -First 1) } catch { '' }
$wingetVersion = $wingetVersion.Trim()

# Das offizielle PowerShell-Modul liefert saubere Objekte statt Text.
# Ist es nicht installiert, wird die Textausgabe von winget.exe ausgewertet.
$useModule = [bool](Get-Module -ListAvailable -Name Microsoft.WinGet.Client -ErrorAction SilentlyContinue)

$logFile = Join-Path $LogDir 'Installationsprotokoll.txt'

# ==================================================
# 4) Programmkatalog – hier Programme ergänzen oder entfernen
#    Anzeigename = winget-ID
# ==================================================
$catalog = [ordered]@{
    'Browser' = [ordered]@{
        'Opera Stable'                  = 'Opera.Opera'
        'Opera GX Stable'               = 'Opera.OperaGX'
        'Google Chrome'                 = 'Google.Chrome'
        'Mozilla Firefox (de)'          = 'Mozilla.Firefox.de'
    }
    'Mail' = [ordered]@{
        'Mozilla Thunderbird (de)'      = 'Mozilla.Thunderbird.de'
    }
    'Office' = [ordered]@{
        'LibreOffice'                   = 'TheDocumentFoundation.LibreOffice'
        'LibreOffice Help Pack'         = 'TheDocumentFoundation.LibreOffice.HelpPack'
        'Adobe Acrobat Reader (64-bit)' = 'Adobe.Acrobat.Reader.64-bit'
        'PDF24 Creator'                 = 'geeksoftwareGmbH.PDF24Creator'
        'Okular'                        = 'KDE.Okular'
    }
    'Gaming' = [ordered]@{
        'Epic Games Launcher'           = 'EpicGames.EpicGamesLauncher'
        'Steam'                         = 'Valve.Steam'
        'Ubisoft Connect'               = 'Ubisoft.Connect'
        'EA App'                        = 'ElectronicArts.EADesktop'
        'GOG GALAXY'                    = 'GOG.Galaxy'
    }
    'Sonstige' = [ordered]@{
        'Core Temp'                     = 'ALCPU.CoreTemp'
        'ImgBurn'                       = 'LIGHTNINGUK.ImgBurn'
        'Veeam Agent'                   = 'Veeam.VeeamAgent'
        'VLC media player'              = 'VideoLAN.VLC'
        'Nextcloud'                     = 'Nextcloud.NextcloudDesktop'
    }
}

# Symbole aus "Segoe Fluent Icons" (Windows 11) bzw. "Segoe MDL2 Assets" (Windows 10)
$categoryGlyphs = @{
    'Browser'  = 0xE774
    'Mail'     = 0xE715
    'Office'   = 0xE8A5
    'Gaming'   = 0xE7FC
    'Sonstige' = 0xE71D
}

# ==================================================
# 5) Hintergrund-Aufgaben (laufen in eigenem Runspace, damit die
#    Oberfläche während winget arbeitet nicht einfriert)
# ==================================================
$WorkerLib = @'
$ErrorActionPreference = 'Stop'
$CODE_NO_PACKAGES = -1978335212   # 0x8A150014: keine passenden Pakete gefunden

function Send-Msg([hashtable]$Message) { $sync.Queue.Enqueue($Message) }
function Write-UiLog([string]$Text) { Send-Msg @{ Type = 'log'; Text = $Text } }
function Format-ExitCode([int]$Code) { '0x{0:X8}' -f $Code }

# Fortschrittsbalken, Spinner und Download-Zähler von winget ausfiltern
function Test-NoiseLine([string]$Line) {
    $t = $Line.Trim()
    if ($t.Length -le 1) { return $true }
    if ($t -match '[▀-▟]') { return $true }
    if ($t -match '^[\d.,]+\s*[KMG]?B\s*/\s*[\d.,]+\s*[KMG]?B$') { return $true }
    if ($t -match '^\d{1,3}\s?%$') { return $true }
    return $false
}

function Invoke-Winget {
    param([string[]]$Arguments, [switch]$Stream)
    $psi = New-Object System.Diagnostics.ProcessStartInfo $WingetPath
    $psi.Arguments = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '').TrimEnd('\') + '"' } else { $_ }
    }) -join ' '
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.CreateNoWindow         = $true

    $p = [System.Diagnostics.Process]::Start($psi)
    $lines = New-Object System.Collections.Generic.List[string]
    while ($null -ne ($line = $p.StandardOutput.ReadLine())) {
        $line = ($line -replace '[\b]', '').TrimEnd()
        if (Test-NoiseLine $line) { continue }
        $lines.Add($line)
        if ($Stream) { Write-UiLog ('    ' + $line.Trim()) }
    }
    $p.WaitForExit()
    [PSCustomObject]@{ ExitCode = $p.ExitCode; Lines = $lines.ToArray() }
}

# Tabellen von winget anhand der Spaltenpositionen im Kopf zerlegen
# (Trennen an Doppel-Leerzeichen funktioniert bei gekürzten Namen nicht).
function ConvertFrom-WingetTable([string[]]$Lines, [int]$MinColumns) {
    $sep = -1
    for ($i = 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^-{10,}$') { $sep = $i; break }
    }
    if ($sep -lt 1) { return }

    $starts = @(0) + @([regex]::Matches($Lines[$sep - 1], '(?<=\s)\S') | ForEach-Object { $_.Index })
    if ($starts.Count -lt $MinColumns) { return }

    for ($i = $sep + 1; $i -lt $Lines.Count; $i++) {
        # Folgt eine weitere Tabelle, ist diese Zeile deren Kopf -> Ende
        if ($i + 1 -lt $Lines.Count -and $Lines[$i + 1] -match '^-{10,}$') { break }
        $line = $Lines[$i]
        $cols = New-Object string[] $starts.Count
        for ($c = 0; $c -lt $starts.Count; $c++) {
            $s = $starts[$c]
            if ($s -ge $line.Length) { $cols[$c] = ''; continue }
            $e = if ($c + 1 -lt $starts.Count) { [Math]::Min($starts[$c + 1], $line.Length) } else { $line.Length }
            $cols[$c] = $line.Substring($s, $e - $s).Trim()
        }
        if ($cols[1] -match '^\w[\w.+-]*$' -and $cols[$MinColumns - 1]) {
            [PSCustomObject]@{ Cols = $cols }
        }
    }
}

$KnownExitCodes = @{
    [int]-1978335189 = @{ Ok = $true;  Text = 'Bereits aktuell' }        # 0x8A15002B: kein Update anwendbar
    [int]-1978335135 = @{ Ok = $true;  Text = 'Bereits installiert' }    # 0x8A150061
    [int]-1978334967 = @{ Ok = $true;  Text = 'Neustart erforderlich' }  # 0x8A150109
    [int]-1978335212 = @{ Ok = $false; Text = 'Paket nicht gefunden' }   # 0x8A150014
}

function Resolve-ExitCode([int]$Code, [string]$Verb) {
    if ($Code -eq 0) {
        if ($Verb -eq 'upgrade') { return @{ Ok = $true; Text = 'Aktualisiert' } }
        return @{ Ok = $true; Text = 'Installiert' }
    }
    if ($KnownExitCodes.ContainsKey($Code)) { return $KnownExitCodes[$Code] }
    return @{ Ok = $false; Text = "Fehler $(Format-ExitCode $Code)" }
}
'@

$WorkerWrapper = @'
try {
#CODE#
}
catch {
    Send-Msg @{ Type = 'error'; Text = $_.Exception.Message }
}
'@

$SearchJob = @'
$term  = [string]$arg
$found = $null
if ($UseModule) {
    try {
        Import-Module Microsoft.WinGet.Client -ErrorAction Stop
        $found = @(Find-WinGetPackage -Query $term -Source winget -ErrorAction Stop | ForEach-Object {
            [PSCustomObject]@{ Name = $_.Name; Id = $_.Id; Version = [string]$_.Version; Available = '' }
        })
    }
    catch {
        Write-UiLog "WinGet-Modul nicht nutzbar, verwende winget.exe ($($_.Exception.Message))"
        $found = $null
    }
}
if ($null -eq $found) {
    $r = Invoke-Winget -Arguments @('search', '--query', $term, '--source', 'winget', '--accept-source-agreements')
    if ($r.ExitCode -eq $CODE_NO_PACKAGES) { $found = @() }
    elseif ($r.ExitCode -ne 0) { throw "winget search ist fehlgeschlagen ($(Format-ExitCode $r.ExitCode))." }
    else {
        $found = @(ConvertFrom-WingetTable -Lines $r.Lines -MinColumns 3 | ForEach-Object {
            [PSCustomObject]@{ Name = $_.Cols[0]; Id = $_.Cols[1]; Version = $_.Cols[2]; Available = '' }
        })
    }
}
Send-Msg @{ Type = 'search'; Data = $found; Term = $term }
'@

$UpdatesJob = @'
$found = $null
if ($UseModule) {
    try {
        Import-Module Microsoft.WinGet.Client -ErrorAction Stop
        $found = @(Get-WinGetPackage -Source winget -ErrorAction Stop | Where-Object { $_.IsUpdateAvailable } | ForEach-Object {
            [PSCustomObject]@{
                Name      = $_.Name
                Id        = $_.Id
                Version   = [string]$_.InstalledVersion
                Available = [string]($_.AvailableVersions | Select-Object -First 1)
            }
        })
    }
    catch {
        Write-UiLog "WinGet-Modul nicht nutzbar, verwende winget.exe ($($_.Exception.Message))"
        $found = $null
    }
}
if ($null -eq $found) {
    $r = Invoke-Winget -Arguments @('upgrade', '--source', 'winget', '--accept-source-agreements')
    if ($r.ExitCode -eq $CODE_NO_PACKAGES) { $found = @() }
    elseif ($r.ExitCode -ne 0) { throw "Update-Prüfung ist fehlgeschlagen ($(Format-ExitCode $r.ExitCode))." }
    else {
        $found = @(ConvertFrom-WingetTable -Lines $r.Lines -MinColumns 4 | ForEach-Object {
            [PSCustomObject]@{ Name = $_.Cols[0]; Id = $_.Cols[1]; Version = $_.Cols[2]; Available = $_.Cols[3] }
        })
    }
}
Send-Msg @{ Type = 'updates'; Data = $found }
'@

$InstallJob = @'
$items = @($arg)
$total = $items.Count
$done = 0; $okCount = 0; $failCount = 0
foreach ($it in $items) {
    if ($sync.Cancel) {
        Send-Msg @{ Type = 'status'; Id = $it.Id; Kind = ''; Text = '' }
        continue
    }
    $verb  = if ($it.Action -eq 'upgrade') { 'upgrade' } else { 'install' }
    $label = if ($verb -eq 'upgrade') { 'Wird aktualisiert …' } else { 'Wird installiert …' }
    Send-Msg @{ Type = 'status'; Id = $it.Id; Kind = 'busy'; Text = $label }
    Send-Msg @{ Type = 'busyText'; Text = "[$($done + 1)/$total] $($it.Name) – $label" }
    Write-UiLog "▶ $($it.Name) ($($it.Id)): $verb"

    try {
        $r   = Invoke-Winget -Stream -Arguments @($verb, '--id', $it.Id, '--exact', '--source', 'winget', '--silent',
                                                  '--accept-source-agreements', '--accept-package-agreements')
        $res = Resolve-ExitCode -Code $r.ExitCode -Verb $verb
        $code = $r.ExitCode
    }
    catch {
        $res  = @{ Ok = $false; Text = "Fehler: $($_.Exception.Message)" }
        $code = -1
    }

    if ($res.Ok) { $okCount++ } else { $failCount++ }
    $kind = if ($res.Ok) { 'ok' } else { 'fail' }
    Send-Msg @{ Type = 'status'; Id = $it.Id; Kind = $kind; Text = $res.Text }
    Send-Msg @{ Type = 'result'; Name = $it.Name; Id = $it.Id; Ok = $res.Ok; Text = $res.Text; Code = $code }
    $done++
    Send-Msg @{ Type = 'progress'; Value = [double]($done / $total * 100) }
}
Send-Msg @{ Type = 'installDone'; Ok = $okCount; Fail = $failCount; Skipped = $total - $done }
'@

# ==================================================
# 6) Farben (Windows 11 hell/dunkel, folgt der Systemeinstellung)
# ==================================================
$isDark = try {
    (Get-ItemPropertyValue 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name AppsUseLightTheme -ErrorAction Stop) -eq 0
} catch { $false }

$theme = if ($isDark) {
    @{
        WindowBg = '#202020'; Layer = '#272727'; LayerBorder = '#1C1C1C'
        Card = '#2D2D2D'; CardHover = '#343434'; CardBorder = '#1F1F1F'
        TextPrimary = '#FFFFFF'; TextSecondary = '#CFCFCF'; TextTertiary = '#9D9D9D'
        SubtleHover = '#0FFFFFFF'; SubtlePressed = '#0AFFFFFF'
        ControlBg = '#2D2D2D'; ControlHover = '#323232'; ControlPressed = '#272727'
        ControlBorder = '#3A3A3A'; ControlStrong = '#9D9D9D'; CheckBg = '#262626'
        Accent = '#60CDFF'; AccentHover = '#5BBAE6'; AccentPressed = '#56A7CE'; OnAccent = '#000000'; AccentSubtle = '#1F3747'
        InputBg = '#2D2D2D'; InputFocusBg = '#1F1F1F'; InputBottom = '#9D9D9D'
        Divider = '#333333'; ScrollThumb = '#9D9D9D'
        SuccessBg = '#393D1B'; SuccessFg = '#6CCB5F'
        CautionBg = '#433519'; CautionFg = '#FCE100'
        CriticalBg = '#442726'; CriticalFg = '#FF99A4'
        InfoBg = '#333333'
    }
} else {
    @{
        WindowBg = '#F3F3F3'; Layer = '#F9F9F9'; LayerBorder = '#E5E5E5'
        Card = '#FFFFFF'; CardHover = '#F6F6F6'; CardBorder = '#E5E5E5'
        TextPrimary = '#1B1B1B'; TextSecondary = '#5F5F5F'; TextTertiary = '#8B8B8B'
        SubtleHover = '#0A000000'; SubtlePressed = '#06000000'
        ControlBg = '#FBFBFB'; ControlHover = '#F6F6F6'; ControlPressed = '#F5F5F5'
        ControlBorder = '#E0E0E0'; ControlStrong = '#8A8A8A'; CheckBg = '#F5F5F5'
        Accent = '#005FB8'; AccentHover = '#196EBF'; AccentPressed = '#3183CA'; OnAccent = '#FFFFFF'; AccentSubtle = '#E0EEF9'
        InputBg = '#FBFBFB'; InputFocusBg = '#FFFFFF'; InputBottom = '#8A8A8A'
        Divider = '#E5E5E5'; ScrollThumb = '#8A8A8A'
        SuccessBg = '#DFF6DD'; SuccessFg = '#0F7B0F'
        CautionBg = '#FFF4CE'; CautionFg = '#9D5D00'
        CriticalBg = '#FDE7E9'; CriticalFg = '#C42B1C'
        InfoBg = '#EFEFEF'
    }
}

# ==================================================
# 7) Oberfläche (XAML)
# ==================================================
$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Winget Installer" Width="1080" Height="700" MinWidth="840" MinHeight="540"
        WindowStartupLocation="CenterScreen"
        Background="%%WindowBg%%" Foreground="%%TextPrimary%%"
        FontFamily="Segoe UI" FontSize="14"
        UseLayoutRounding="True" SnapsToDevicePixels="True">
  <Window.Resources>
%%BRUSHES%%
    <FontFamily x:Key="IconFont">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>

    <!-- Schmale, abgerundete Scrollbalken -->
    <Style TargetType="ScrollBar">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Width" Value="10"/>
      <Setter Property="MinWidth" Value="10"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ScrollBar">
            <Track x:Name="PART_Track" IsDirectionReversed="True">
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Border x:Name="T" Background="{StaticResource ScrollThumb}" CornerRadius="3" Width="4" HorizontalAlignment="Center"/>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="T" Property="Width" Value="6"/></Trigger>
                        <Trigger Property="IsDragging" Value="True"><Setter TargetName="T" Property="Width" Value="6"/></Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="Orientation" Value="Horizontal">
          <Setter Property="Width" Value="Auto"/>
          <Setter Property="MinWidth" Value="0"/>
          <Setter Property="Height" Value="10"/>
          <Setter Property="MinHeight" Value="10"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="ScrollBar">
                <Track x:Name="PART_Track">
                  <Track.Thumb>
                    <Thumb>
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Border Background="{StaticResource ScrollThumb}" CornerRadius="3" Height="4" VerticalAlignment="Center"/>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                </Track>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Trigger>
      </Style.Triggers>
    </Style>

    <!-- Standard-Schaltfläche -->
    <Style x:Key="Btn" TargetType="Button">
      <Setter Property="Background" Value="{StaticResource ControlBg}"/>
      <Setter Property="BorderBrush" Value="{StaticResource ControlBorder}"/>
      <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="14,5"/>
      <Setter Property="MinHeight" Value="32"/>
      <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Grid>
              <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4">
                <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              </Border>
              <Border x:Name="Focus" BorderBrush="{StaticResource TextPrimary}" BorderThickness="2" CornerRadius="6" Margin="-3" Visibility="Collapsed"/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Focus" Property="Visibility" Value="Visible"/></Trigger>
              <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Bd" Property="Opacity" Value="0.45"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="{StaticResource ControlHover}"/></Trigger>
        <Trigger Property="IsPressed" Value="True">
          <Setter Property="Background" Value="{StaticResource ControlPressed}"/>
          <Setter Property="Foreground" Value="{StaticResource TextSecondary}"/>
        </Trigger>
      </Style.Triggers>
    </Style>

    <!-- Akzent-Schaltfläche (Hauptaktion) -->
    <Style x:Key="AccentBtn" TargetType="Button" BasedOn="{StaticResource Btn}">
      <Setter Property="Background" Value="{StaticResource Accent}"/>
      <Setter Property="BorderBrush" Value="{StaticResource Accent}"/>
      <Setter Property="Foreground" Value="{StaticResource OnAccent}"/>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
          <Setter Property="Background" Value="{StaticResource AccentHover}"/>
          <Setter Property="BorderBrush" Value="{StaticResource AccentHover}"/>
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
          <Setter Property="Background" Value="{StaticResource AccentPressed}"/>
          <Setter Property="BorderBrush" Value="{StaticResource AccentPressed}"/>
          <Setter Property="Foreground" Value="{StaticResource OnAccent}"/>
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
          <Setter Property="Background" Value="{StaticResource ControlStrong}"/>
          <Setter Property="BorderBrush" Value="{StaticResource ControlStrong}"/>
        </Trigger>
      </Style.Triggers>
    </Style>

    <!-- Dezente Schaltfläche ohne Rahmen -->
    <Style x:Key="SubtleBtn" TargetType="Button" BasedOn="{StaticResource Btn}">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="BorderBrush" Value="Transparent"/>
      <Setter Property="Padding" Value="10,4"/>
      <Setter Property="MinHeight" Value="28"/>
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="{StaticResource SubtleHover}"/></Trigger>
        <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="{StaticResource SubtlePressed}"/></Trigger>
      </Style.Triggers>
    </Style>

    <!-- Windows-11-Kontrollkästchen -->
    <Style x:Key="Win11Check" TargetType="CheckBox">
      <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
      <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="CheckBox">
            <Grid Background="Transparent">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <Border x:Name="Box" Width="20" Height="20" CornerRadius="4" BorderThickness="1"
                      BorderBrush="{StaticResource ControlStrong}" Background="{StaticResource CheckBg}" VerticalAlignment="Center">
                <TextBlock x:Name="Mark" FontFamily="{StaticResource IconFont}" FontSize="12" Foreground="{StaticResource OnAccent}"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
              </Border>
              <ContentPresenter Grid.Column="1" Margin="8,0,0,0" VerticalAlignment="Center"/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Box" Property="Background" Value="{StaticResource ControlHover}"/></Trigger>
              <Trigger Property="IsChecked" Value="True">
                <Setter TargetName="Box" Property="Background" Value="{StaticResource Accent}"/>
                <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource Accent}"/>
                <Setter TargetName="Mark" Property="Text" Value="&#xE73E;"/>
              </Trigger>
              <Trigger Property="IsChecked" Value="{x:Null}">
                <Setter TargetName="Box" Property="Background" Value="{StaticResource Accent}"/>
                <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource Accent}"/>
                <Setter TargetName="Mark" Property="Text" Value="&#xE738;"/>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Paket-Karte: die ganze Karte ist ein Kontrollkästchen -->
    <Style x:Key="CardCheck" TargetType="CheckBox">
      <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
      <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
      <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="CheckBox">
            <Border x:Name="Card" Background="{StaticResource Card}" BorderBrush="{StaticResource CardBorder}"
                    BorderThickness="1" CornerRadius="6" Padding="16,10">
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="Auto"/>
                  <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Border x:Name="Box" Width="20" Height="20" CornerRadius="4" BorderThickness="1"
                        BorderBrush="{StaticResource ControlStrong}" Background="{StaticResource CheckBg}" VerticalAlignment="Center">
                  <TextBlock x:Name="Mark" FontFamily="{StaticResource IconFont}" FontSize="12" Foreground="{StaticResource OnAccent}"
                             HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ContentPresenter Grid.Column="1" Margin="16,0,0,0" VerticalAlignment="Center"
                                  HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"/>
              </Grid>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Card" Property="Background" Value="{StaticResource CardHover}"/></Trigger>
              <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Card" Property="BorderBrush" Value="{StaticResource TextPrimary}"/></Trigger>
              <Trigger Property="IsChecked" Value="True">
                <Setter TargetName="Box" Property="Background" Value="{StaticResource Accent}"/>
                <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource Accent}"/>
                <Setter TargetName="Mark" Property="Text" Value="&#xE73E;"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Suchfeld mit Akzentlinie bei Fokus -->
    <Style x:Key="SearchBox" TargetType="TextBox">
      <Setter Property="Background" Value="{StaticResource InputBg}"/>
      <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
      <Setter Property="BorderBrush" Value="{StaticResource ControlBorder}"/>
      <Setter Property="CaretBrush" Value="{StaticResource TextPrimary}"/>
      <Setter Property="SelectionBrush" Value="{StaticResource Accent}"/>
      <Setter Property="MinHeight" Value="32"/>
      <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="TextBox">
            <Grid>
              <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="4"/>
              <Border x:Name="Line" Height="1" VerticalAlignment="Bottom" Margin="4,0" Background="{StaticResource InputBottom}"/>
              <TextBlock x:Name="Hint" Text="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"
                         Foreground="{StaticResource TextTertiary}" Margin="12,0,36,0" VerticalAlignment="Center"
                         IsHitTestVisible="False" Visibility="Collapsed" TextTrimming="CharacterEllipsis"/>
              <ScrollViewer x:Name="PART_ContentHost" Margin="10,0,34,0" VerticalAlignment="Center" Focusable="False"
                            HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden"/>
              <TextBlock Text="&#xE721;" FontFamily="{StaticResource IconFont}" FontSize="14" Foreground="{StaticResource TextSecondary}"
                         HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0" IsHitTestVisible="False"/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property="Text" Value=""><Setter TargetName="Hint" Property="Visibility" Value="Visible"/></Trigger>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="{StaticResource ControlHover}"/></Trigger>
              <Trigger Property="IsKeyboardFocusWithin" Value="True">
                <Setter TargetName="Bd" Property="Background" Value="{StaticResource InputFocusBg}"/>
                <Setter TargetName="Line" Property="Height" Value="2"/>
                <Setter TargetName="Line" Property="Margin" Value="1,0"/>
                <Setter TargetName="Line" Property="Background" Value="{StaticResource Accent}"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Dünner Fortschrittsbalken -->
    <Style TargetType="ProgressBar">
      <Setter Property="Height" Value="4"/>
      <Setter Property="Foreground" Value="{StaticResource Accent}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ProgressBar">
            <Grid>
              <Border x:Name="PART_Track" Height="1" VerticalAlignment="Center" Background="{StaticResource ControlStrong}"/>
              <Border x:Name="PART_Indicator" Height="3" VerticalAlignment="Center" HorizontalAlignment="Left"
                      Background="{TemplateBinding Foreground}" CornerRadius="1.5"/>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Navigationseintrag links -->
    <Style x:Key="NavItem" TargetType="RadioButton">
      <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
      <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
      <Setter Property="Margin" Value="0,2"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="RadioButton">
            <Border x:Name="Bd" Background="Transparent" CornerRadius="4" Height="38">
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="Auto"/>
                  <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Border x:Name="Pill" Grid.ColumnSpan="2" Width="3" Height="16" CornerRadius="1.5" Background="{StaticResource Accent}"
                        HorizontalAlignment="Left" VerticalAlignment="Center" Visibility="Hidden"/>
                <TextBlock Margin="16,0,0,0" Text="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"
                           FontFamily="{StaticResource IconFont}" FontSize="16" VerticalAlignment="Center"/>
                <ContentPresenter Grid.Column="1" Margin="16,0,12,0" VerticalAlignment="Center"/>
              </Grid>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="{StaticResource SubtleHover}"/></Trigger>
              <Trigger Property="IsChecked" Value="True">
                <Setter TargetName="Bd" Property="Background" Value="{StaticResource SubtleHover}"/>
                <Setter TargetName="Pill" Property="Visibility" Value="Visible"/>
              </Trigger>
              <Trigger Property="IsPressed" Value="True"><Setter TargetName="Bd" Property="Background" Value="{StaticResource SubtlePressed}"/></Trigger>
              <Trigger Property="IsKeyboardFocused" Value="True">
                <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource TextPrimary}"/>
                <Setter TargetName="Bd" Property="BorderThickness" Value="1"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Darstellung eines Pakets in der Liste -->
    <DataTemplate x:Key="PackageTemplate">
      <CheckBox Style="{StaticResource CardCheck}" IsChecked="{Binding IsChecked, Mode=TwoWay}" Margin="0,0,0,4">
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
          </Grid.ColumnDefinitions>
          <StackPanel VerticalAlignment="Center">
            <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis"/>
            <TextBlock Text="{Binding Id}" FontSize="12" Foreground="{StaticResource TextSecondary}" TextTrimming="CharacterEllipsis" Margin="0,1,0,0"/>
          </StackPanel>
          <TextBlock Grid.Column="1" Text="{Binding VersionText}" FontSize="12" Foreground="{StaticResource TextSecondary}"
                     VerticalAlignment="Center" Margin="16,0,0,0"/>
          <Border x:Name="Pill" Grid.Column="2" CornerRadius="10" Padding="10,2" Margin="12,0,0,0" VerticalAlignment="Center"
                  Background="{StaticResource InfoBg}" Visibility="Collapsed">
            <TextBlock x:Name="PillText" Text="{Binding Status}" FontSize="12" Foreground="{StaticResource TextSecondary}"/>
          </Border>
        </Grid>
      </CheckBox>
      <DataTemplate.Triggers>
        <DataTrigger Binding="{Binding StatusKind}" Value="wait">
          <Setter TargetName="Pill" Property="Visibility" Value="Visible"/>
        </DataTrigger>
        <DataTrigger Binding="{Binding StatusKind}" Value="busy">
          <Setter TargetName="Pill" Property="Visibility" Value="Visible"/>
          <Setter TargetName="Pill" Property="Background" Value="{StaticResource AccentSubtle}"/>
          <Setter TargetName="PillText" Property="Foreground" Value="{StaticResource Accent}"/>
        </DataTrigger>
        <DataTrigger Binding="{Binding StatusKind}" Value="ok">
          <Setter TargetName="Pill" Property="Visibility" Value="Visible"/>
          <Setter TargetName="Pill" Property="Background" Value="{StaticResource SuccessBg}"/>
          <Setter TargetName="PillText" Property="Foreground" Value="{StaticResource SuccessFg}"/>
        </DataTrigger>
        <DataTrigger Binding="{Binding StatusKind}" Value="fail">
          <Setter TargetName="Pill" Property="Visibility" Value="Visible"/>
          <Setter TargetName="Pill" Property="Background" Value="{StaticResource CriticalBg}"/>
          <Setter TargetName="PillText" Property="Foreground" Value="{StaticResource CriticalFg}"/>
        </DataTrigger>
      </DataTemplate.Triggers>
    </DataTemplate>
  </Window.Resources>

  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="264"/>
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Navigation -->
    <DockPanel Grid.Column="0" Margin="8,12,8,12">
      <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="10,4,0,20">
        <Border Width="34" Height="34" CornerRadius="8" Background="{StaticResource Accent}">
          <TextBlock Text="&#xE896;" FontFamily="{StaticResource IconFont}" FontSize="16" Foreground="{StaticResource OnAccent}"
                     HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <StackPanel Margin="12,0,0,0" VerticalAlignment="Center">
          <TextBlock Text="Winget Installer" FontWeight="SemiBold"/>
          <TextBlock Text="Software installieren &amp; aktualisieren" FontSize="12" Foreground="{StaticResource TextSecondary}"/>
        </StackPanel>
      </StackPanel>
      <TextBlock x:Name="EnvText" DockPanel.Dock="Bottom" Margin="14,0,8,4" FontSize="12"
                 Foreground="{StaticResource TextTertiary}" TextWrapping="Wrap"/>
      <StackPanel x:Name="NavList"/>
    </DockPanel>

    <!-- Inhaltsbereich -->
    <Border Grid.Column="1" Background="{StaticResource Layer}" BorderBrush="{StaticResource LayerBorder}"
            BorderThickness="1,1,0,0" CornerRadius="8,0,0,0">
      <DockPanel>

        <!-- Aktionsleiste unten -->
        <Border DockPanel.Dock="Bottom" BorderBrush="{StaticResource Divider}" BorderThickness="0,1,0,0" Padding="32,14">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <StackPanel VerticalAlignment="Center" Margin="0,0,24,0">
              <TextBlock x:Name="SelectionText" FontWeight="SemiBold"/>
              <StackPanel Orientation="Horizontal" Margin="0,3,0,0">
                <Ellipse x:Name="Ring" Width="14" Height="14" Stroke="{StaticResource Accent}" StrokeThickness="2"
                         StrokeDashArray="6 100" StrokeDashCap="Round" RenderTransformOrigin="0.5,0.5"
                         Margin="0,0,8,0" VerticalAlignment="Center" Visibility="Collapsed">
                  <Ellipse.RenderTransform><RotateTransform/></Ellipse.RenderTransform>
                  <Ellipse.Triggers>
                    <EventTrigger RoutedEvent="FrameworkElement.Loaded">
                      <BeginStoryboard>
                        <Storyboard>
                          <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(RotateTransform.Angle)"
                                           From="0" To="360" Duration="0:0:0.9" RepeatBehavior="Forever"/>
                        </Storyboard>
                      </BeginStoryboard>
                    </EventTrigger>
                  </Ellipse.Triggers>
                </Ellipse>
                <TextBlock x:Name="StatusText" FontSize="12" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/>
              </StackPanel>
              <ProgressBar x:Name="Progress" Margin="0,8,0,0" Maximum="100" Visibility="Collapsed"/>
            </StackPanel>
            <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
              <Button x:Name="BtnLog" Style="{StaticResource Btn}" ToolTip="Protokoll ein-/ausblenden">
                <StackPanel Orientation="Horizontal">
                  <TextBlock Text="&#xE81C;" FontFamily="{StaticResource IconFont}" FontSize="14" VerticalAlignment="Center" Margin="0,0,8,0"/>
                  <TextBlock Text="Protokoll"/>
                </StackPanel>
              </Button>
              <Button x:Name="BtnCancel" Style="{StaticResource Btn}" Margin="8,0,0,0" Visibility="Collapsed" Content="Abbrechen"/>
              <Button x:Name="BtnInstall" Style="{StaticResource AccentBtn}" Margin="8,0,0,0" IsEnabled="False">
                <StackPanel Orientation="Horizontal">
                  <TextBlock Text="&#xE896;" FontFamily="{StaticResource IconFont}" FontSize="14" VerticalAlignment="Center" Margin="0,0,8,0"/>
                  <TextBlock Text="Installieren / Aktualisieren"/>
                </StackPanel>
              </Button>
            </StackPanel>
          </Grid>
        </Border>

        <Grid Margin="32,26,32,16">
          <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
          </Grid.RowDefinitions>

          <!-- Seitentitel -->
          <StackPanel Grid.Row="0" Margin="0,0,0,18">
            <TextBlock x:Name="PageTitle" FontSize="28" FontWeight="SemiBold"/>
            <TextBlock x:Name="PageSubtitle" Foreground="{StaticResource TextSecondary}" Margin="0,4,0,0" TextWrapping="Wrap"/>
          </StackPanel>

          <!-- Hinweisleiste (InfoBar) -->
          <Border x:Name="InfoBar" Grid.Row="1" CornerRadius="4" BorderThickness="1" BorderBrush="{StaticResource CardBorder}"
                  Padding="16,8,8,8" Margin="0,0,0,16" Visibility="Collapsed">
            <DockPanel>
              <TextBlock x:Name="InfoIcon" DockPanel.Dock="Left" FontFamily="{StaticResource IconFont}" FontSize="16"
                         VerticalAlignment="Center" Margin="0,0,14,0"/>
              <Button x:Name="InfoClose" DockPanel.Dock="Right" Style="{StaticResource SubtleBtn}" VerticalAlignment="Center" ToolTip="Schließen">
                <TextBlock Text="&#xE711;" FontFamily="{StaticResource IconFont}" FontSize="12"/>
              </Button>
              <Button x:Name="InfoAction" DockPanel.Dock="Right" Style="{StaticResource Btn}" Margin="12,0,4,0"
                      VerticalAlignment="Center" Visibility="Collapsed"/>
              <TextBlock VerticalAlignment="Center" TextWrapping="Wrap">
                <Run x:Name="InfoTitle" FontWeight="SemiBold"/><Run Text="   "/><Run x:Name="InfoText"/>
              </TextBlock>
            </DockPanel>
          </Border>

          <!-- Werkzeugleiste -->
          <Grid Grid.Row="2" Margin="0,0,0,12" MinHeight="32">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <StackPanel x:Name="SearchBar" Orientation="Horizontal" Visibility="Collapsed">
              <TextBox x:Name="SearchBox" Style="{StaticResource SearchBox}" Width="360"
                       Tag="Programm suchen, z. B. 7zip oder Notepad++"/>
              <Button x:Name="BtnSearch" Style="{StaticResource AccentBtn}" Margin="8,0,0,0" Content="Suchen"/>
            </StackPanel>
            <StackPanel x:Name="UpdateBar" Orientation="Horizontal" Visibility="Collapsed">
              <Button x:Name="BtnCheckUpdates" Style="{StaticResource AccentBtn}">
                <StackPanel Orientation="Horizontal">
                  <TextBlock Text="&#xE72C;" FontFamily="{StaticResource IconFont}" FontSize="14" VerticalAlignment="Center" Margin="0,0,8,0"/>
                  <TextBlock Text="Nach Updates suchen"/>
                </StackPanel>
              </Button>
            </StackPanel>
            <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
              <TextBlock x:Name="CountText" Foreground="{StaticResource TextSecondary}" VerticalAlignment="Center"/>
              <CheckBox x:Name="ChkAll" Style="{StaticResource Win11Check}" Content="Alle auswählen" Margin="20,0,0,0"/>
            </StackPanel>
          </Grid>

          <!-- Paketliste -->
          <ScrollViewer x:Name="ListScroll" Grid.Row="3" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled"
                        Margin="0,0,-14,0" Padding="0,0,14,0" Focusable="False">
            <ItemsControl x:Name="PackageList" ItemTemplate="{StaticResource PackageTemplate}" Focusable="False"/>
          </ScrollViewer>

          <StackPanel x:Name="EmptyState" Grid.Row="3" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed">
            <TextBlock x:Name="EmptyGlyph" FontFamily="{StaticResource IconFont}" FontSize="40"
                       Foreground="{StaticResource TextTertiary}" HorizontalAlignment="Center"/>
            <TextBlock x:Name="EmptyText" Foreground="{StaticResource TextSecondary}" Margin="0,14,0,0"
                       TextAlignment="Center" TextWrapping="Wrap" MaxWidth="380"/>
          </StackPanel>

          <!-- Protokoll -->
          <Border x:Name="LogPanel" Grid.Row="4" Height="190" Margin="0,12,0,0" CornerRadius="6" BorderThickness="1"
                  BorderBrush="{StaticResource CardBorder}" Background="{StaticResource Card}" Visibility="Collapsed">
            <DockPanel>
              <DockPanel DockPanel.Dock="Top" Margin="16,6,6,0">
                <Button x:Name="BtnClearLog" DockPanel.Dock="Right" Style="{StaticResource SubtleBtn}" Content="Leeren" FontSize="12"/>
                <Button x:Name="BtnOpenLog" DockPanel.Dock="Right" Style="{StaticResource SubtleBtn}" Content="Datei öffnen" FontSize="12"/>
                <TextBlock Text="Protokoll" FontWeight="SemiBold" VerticalAlignment="Center"/>
              </DockPanel>
              <TextBox x:Name="LogBox" IsReadOnly="True" BorderThickness="0" Background="Transparent"
                       Foreground="{StaticResource TextSecondary}" FontFamily="Cascadia Mono, Consolas" FontSize="12"
                       TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" Padding="12,4,12,8"/>
            </DockPanel>
          </Border>
        </Grid>
      </DockPanel>
    </Border>
  </Grid>
</Window>
'@

$brushXaml = ($theme.Keys | Sort-Object | ForEach-Object {
    '    <SolidColorBrush x:Key="{0}" Color="{1}"/>' -f $_, $theme[$_]
}) -join "`r`n"
$xaml = $xaml.Replace('%%BRUSHES%%', $brushXaml).Replace('%%WindowBg%%', $theme.WindowBg).Replace('%%TextPrimary%%', $theme.TextPrimary)

try {
    $window = [System.Windows.Markup.XamlReader]::Parse($xaml)
}
catch {
    [void][System.Windows.MessageBox]::Show("Die Oberfläche konnte nicht geladen werden:`n`n$($_.Exception.Message)", 'Winget Installer',
        [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
    exit 3
}

# Alle benannten Elemente in $ui sammeln
$ui = @{}
foreach ($m in [regex]::Matches($xaml, 'x:Name="(\w+)"')) {
    $name = $m.Groups[1].Value
    $el = $window.FindName($name)
    if ($el) { $ui[$name] = $el }
}

# Fenster an kleine Bildschirme anpassen
$workArea = [System.Windows.SystemParameters]::WorkArea
if ($window.Height -gt $workArea.Height - 40) { $window.Height = [Math]::Max($window.MinHeight, $workArea.Height - 40) }
if ($window.Width  -gt $workArea.Width  - 40) { $window.Width  = [Math]::Max($window.MinWidth,  $workArea.Width  - 40) }

# Titelleiste an das Farbschema anpassen (dunkel + gleiche Hintergrundfarbe wie das Fenster)
$bgHex = $theme.WindowBg.TrimStart('#')
$captionColor = [Convert]::ToInt32($bgHex.Substring(0, 2), 16) -bor
                ([Convert]::ToInt32($bgHex.Substring(2, 2), 16) -shl 8) -bor
                ([Convert]::ToInt32($bgHex.Substring(4, 2), 16) -shl 16)
$window.Add_SourceInitialized({
    $hwnd = (New-Object System.Windows.Interop.WindowInteropHelper $window).Handle
    $dark = [int]$isDark
    if ([WingetGui.Native]::SetWindowAttribute($hwnd, 20, $dark) -ne 0) {      # DWMWA_USE_IMMERSIVE_DARK_MODE
        [void][WingetGui.Native]::SetWindowAttribute($hwnd, 19, $dark)          # ältere Windows-10-Builds
    }
    [void][WingetGui.Native]::SetWindowAttribute($hwnd, 35, $captionColor)     # DWMWA_CAPTION_COLOR (nur Windows 11)
})

# ==================================================
# 8) Seiten und Datenmodell
# ==================================================
$script:uiReady     = $false
$script:currentPage = $null
$script:worker      = $null
$script:infoAction  = $null
$sync = [hashtable]::Synchronized(@{
    Queue  = New-Object 'System.Collections.Concurrent.ConcurrentQueue[object]'
    Cancel = $false
})

function New-PackageItem {
    param([string]$Name, [string]$Id, [string]$Version = '', [string]$Available = '', [string]$Action = 'install')
    $item = New-Object WingetGui.PackageItem
    $item.Name = $Name
    $item.Id = $Id
    $item.Version = $Version
    $item.AvailableVersion = $Available
    $item.Action = $Action
    $item.add_PropertyChanged({
        param($s, $e)
        if ($e.PropertyName -eq 'IsChecked') { Update-Summary }
    })
    $item
}

function New-Page {
    param([string]$Title, [int]$Glyph, [string]$Kind, [string]$Subtitle, [string]$Empty)
    @{
        Title    = $Title
        Glyph    = [string][char]$Glyph
        Kind     = $Kind
        Subtitle = $Subtitle
        Empty    = $Empty
        Items    = New-Object 'System.Collections.ObjectModel.ObservableCollection[WingetGui.PackageItem]'
    }
}

$pages       = [ordered]@{}
$catalogById = @{}

$pages['Suche'] = New-Page -Title 'Suche' -Glyph 0xE721 -Kind 'search' `
    -Subtitle 'Durchsuche den winget-Katalog nach weiteren Programmen.' `
    -Empty 'Gib oben einen Suchbegriff ein und drücke Enter.'

foreach ($cat in $catalog.Keys) {
    $glyph = if ($categoryGlyphs.ContainsKey($cat)) { $categoryGlyphs[$cat] } else { 0xE71D }
    $page = New-Page -Title $cat -Glyph $glyph -Kind 'catalog' `
        -Subtitle "$($catalog[$cat].Count) Programme – anhaken und unten auf „Installieren / Aktualisieren“ klicken." `
        -Empty 'In dieser Kategorie sind keine Programme eingetragen.'
    foreach ($appName in $catalog[$cat].Keys) {
        $id = $catalog[$cat][$appName]
        if (-not $catalogById.ContainsKey($id)) { $catalogById[$id] = New-PackageItem -Name $appName -Id $id }
        $page.Items.Add($catalogById[$id])
    }
    $pages[$cat] = $page
}

$pages['Updates'] = New-Page -Title 'Updates' -Glyph 0xE895 -Kind 'updates' `
    -Subtitle 'Installierte Programme, für die eine neuere Version verfügbar ist.' `
    -Empty 'Klicke auf „Nach Updates suchen“, um installierte Programme zu prüfen.'

# ==================================================
# 9) UI-Funktionen
# ==================================================
function Add-LogLine([string]$Text) {
    $ui.LogBox.AppendText(('[{0:HH:mm:ss}] {1}' -f (Get-Date), $Text) + "`r`n")
    $ui.LogBox.ScrollToEnd()
}

function Write-LogFile([string]$Line) {
    try { Add-Content -LiteralPath $logFile -Value $Line -Encoding UTF8 -ErrorAction Stop }
    catch { Add-LogLine "Protokolldatei konnte nicht geschrieben werden: $($_.Exception.Message)" }
}

function Open-LogFile {
    if (Test-Path -LiteralPath $logFile) { Start-Process notepad.exe -ArgumentList "`"$logFile`"" }
    else { Show-InfoBar -Kind info -Title 'Protokoll' -Text "Es gibt noch keine Protokolldatei ($logFile)." }
}

function Show-InfoBar {
    param(
        [ValidateSet('success', 'warning', 'error', 'info')][string]$Kind,
        [string]$Title, [string]$Text, [string]$ActionText, [scriptblock]$Action
    )
    $map = @{
        success = @('SuccessBg',  'SuccessFg',  0xE930)
        warning = @('CautionBg',  'CautionFg',  0xE7BA)
        error   = @('CriticalBg', 'CriticalFg', 0xEA39)
        info    = @('InfoBg',     'Accent',     0xE946)
    }
    $bg, $fg, $glyph = $map[$Kind]
    $ui.InfoBar.Background  = $window.FindResource($bg)
    $ui.InfoIcon.Foreground = $window.FindResource($fg)
    $ui.InfoIcon.Text  = [string][char]$glyph
    $ui.InfoTitle.Text = $Title
    $ui.InfoText.Text  = $Text
    $script:infoAction = $Action
    if ($ActionText -and $Action) {
        $ui.InfoAction.Content = $ActionText
        $ui.InfoAction.Visibility = 'Visible'
    } else {
        $ui.InfoAction.Visibility = 'Collapsed'
    }
    $ui.InfoBar.Visibility = 'Visible'
}

function Hide-InfoBar { $ui.InfoBar.Visibility = 'Collapsed' }

function Find-Items([string]$Id) {
    foreach ($p in $pages.Values) {
        foreach ($it in $p.Items) { if ($it.Id -eq $Id) { $it } }
    }
}

function Set-ItemStatus([string]$Id, [string]$Kind, [string]$Text) {
    foreach ($it in @(Find-Items $Id)) {
        $it.StatusKind = $Kind
        $it.Status = $Text
    }
}

# Ausgewählte Pakete aus allen Seiten, doppelte IDs nur einmal (Update hat Vorrang)
function Get-SelectedPackages {
    $byId = [ordered]@{}
    foreach ($p in $pages.Values) {
        foreach ($it in $p.Items) {
            if (-not $it.IsChecked) { continue }
            if (-not $byId.Contains($it.Id) -or $it.Action -eq 'upgrade') { $byId[$it.Id] = $it }
        }
    }
    @($byId.Values)
}

function Update-PageState {
    if (-not $script:currentPage) { return }
    $p = $pages[$script:currentPage]
    $n = $p.Items.Count
    $ui.EmptyState.Visibility = if ($n -eq 0) { 'Visible' } else { 'Collapsed' }
    $ui.EmptyGlyph.Text = $p.Glyph
    $ui.EmptyText.Text  = $p.Empty
    $ui.CountText.Text  = switch ($n) { 0 { '' } 1 { '1 Paket' } default { "$n Pakete" } }
    $ui.ChkAll.Visibility = if ($n -gt 0) { 'Visible' } else { 'Collapsed' }
    $checked = @($p.Items | Where-Object { $_.IsChecked }).Count
    if ($n -gt 0 -and $checked -eq $n) { $ui.ChkAll.IsChecked = $true }
    elseif ($checked -eq 0)             { $ui.ChkAll.IsChecked = $false }
    else                                { $ui.ChkAll.IsChecked = $null }
}

function Update-Summary {
    if (-not $script:uiReady) { return }
    $sel = @(Get-SelectedPackages)
    $upd = @($sel | Where-Object { $_.Action -eq 'upgrade' }).Count
    $text = switch ($sel.Count) { 0 { 'Keine Pakete ausgewählt' } 1 { '1 Paket ausgewählt' } default { "$_ Pakete ausgewählt" } }
    if ($upd -eq 1) { $text += ' · davon 1 Update' } elseif ($upd -gt 1) { $text += " · davon $upd Updates" }
    $ui.SelectionText.Text = $text
    $ui.BtnInstall.IsEnabled = ($sel.Count -gt 0) -and -not $script:worker
    Update-PageState
}

function Update-UpdateBadge {
    $n = @($pages['Updates'].Items | Where-Object { $_.StatusKind -ne 'ok' }).Count
    $ui.UpdBadgeText.Text = [string]$n
    $ui.UpdBadge.Visibility = if ($n -gt 0) { 'Visible' } else { 'Collapsed' }
}

function Show-Page([string]$Key) {
    $script:currentPage = $Key
    $p = $pages[$Key]
    $ui.PageTitle.Text    = $p.Title
    $ui.PageSubtitle.Text = $p.Subtitle
    $ui.PackageList.ItemsSource = $p.Items
    $ui.SearchBar.Visibility = if ($p.Kind -eq 'search')  { 'Visible' } else { 'Collapsed' }
    $ui.UpdateBar.Visibility = if ($p.Kind -eq 'updates') { 'Visible' } else { 'Collapsed' }
    $ui.ListScroll.ScrollToTop()
    Update-Summary
    if ($p.Kind -eq 'search') { [void]$ui.SearchBox.Focus() }
}

function Set-Busy([bool]$On, [string]$Text, [bool]$Cancelable = $false) {
    $ui.Ring.Visibility = if ($On) { 'Visible' } else { 'Collapsed' }
    $ui.StatusText.Text = $Text
    $ui.BtnSearch.IsEnabled = -not $On
    $ui.BtnCheckUpdates.IsEnabled = -not $On
    $ui.BtnCancel.Visibility = if ($On -and $Cancelable) { 'Visible' } else { 'Collapsed' }
    $ui.BtnCancel.IsEnabled = $true
    Update-Summary
}

function Start-UiWorker {
    param([string]$Kind, [string]$Code, $Argument, [string]$BusyText)
    if ($script:worker) { return }
    $sync.Cancel = $false
    $rs = [runspacefactory]::CreateRunspace()
    $rs.Open()
    $rs.SessionStateProxy.SetVariable('sync', $sync)
    $rs.SessionStateProxy.SetVariable('arg', $Argument)
    $rs.SessionStateProxy.SetVariable('WingetPath', $wingetPath)
    $rs.SessionStateProxy.SetVariable('UseModule', $useModule)
    $ps = [powershell]::Create()
    $ps.Runspace = $rs
    [void]$ps.AddScript($WorkerLib + "`r`n" + $WorkerWrapper.Replace('#CODE#', $Code))
    $script:worker = @{ Kind = $Kind; PS = $ps; RS = $rs; Handle = $ps.BeginInvoke() }
    Set-Busy -On $true -Text $BusyText -Cancelable ($Kind -eq 'install')
}

function Complete-UiWorker {
    $w = $script:worker
    try { [void]$w.PS.EndInvoke($w.Handle) }
    catch { Add-LogLine "Interner Fehler: $($_.Exception.Message)" }
    Receive-UiMessages
    $w.PS.Dispose()
    $w.RS.Dispose()
    $script:worker = $null
    Set-Busy -On $false -Text 'Bereit'
}

function Set-SearchResults($Data, [string]$Term) {
    $col = $pages['Suche'].Items
    $keep = @($col | Where-Object { $_.IsChecked })   # bereits angehakte Treffer behalten
    $col.Clear()
    $seen = @{}
    foreach ($it in $keep) { $col.Add($it); $seen[$it.Id] = $true }
    foreach ($d in @($Data)) {
        if (-not $d.Id -or $seen.ContainsKey($d.Id)) { continue }
        $seen[$d.Id] = $true
        if ($catalogById.ContainsKey($d.Id)) {
            $item = $catalogById[$d.Id]
            if (-not $item.Version) { $item.Version = $d.Version }
        } else {
            $item = New-PackageItem -Name $d.Name -Id $d.Id -Version $d.Version
        }
        $col.Add($item)
    }
    $hits = @($Data).Count
    Add-LogLine "Suche „$Term“: $hits Treffer"
    $pages['Suche'].Empty = "Keine Treffer für „$Term“. Versuche einen anderen Begriff."
    Update-Summary
}

function Set-UpdateResults($Data) {
    $col = $pages['Updates'].Items
    $col.Clear()
    foreach ($d in @($Data)) {
        $col.Add((New-PackageItem -Name $d.Name -Id $d.Id -Version $d.Version -Available $d.Available -Action 'upgrade'))
    }
    $n = $col.Count
    Add-LogLine "Update-Prüfung: $n Update(s) verfügbar"
    if ($n -eq 0) {
        $pages['Updates'].Empty = 'Alle Programme sind auf dem neuesten Stand.'
        Show-InfoBar -Kind success -Title 'Alles aktuell' -Text 'Für alle über winget verwalteten Programme ist die neueste Version installiert.'
    }
    Update-UpdateBadge
    Update-Summary
}

function Invoke-UiMessage($m) {
    switch ($m.Type) {
        'log'      { Add-LogLine $m.Text }
        'busyText' { $ui.StatusText.Text = $m.Text }
        'progress' { $ui.Progress.Value = $m.Value }
        'status'   { Set-ItemStatus -Id $m.Id -Kind $m.Kind -Text $m.Text }
        'search'   { Set-SearchResults -Data $m.Data -Term $m.Term }
        'updates'  { Set-UpdateResults -Data $m.Data }
        'result' {
            $prefix = if ($m.Ok) { '[OK]    ' } else { '[FEHLER]' }
            Add-LogLine "$prefix $($m.Name): $($m.Text)"
            Write-LogFile ('{0:yyyy-MM-dd HH:mm:ss}  {1} {2} ({3}) – {4}' -f (Get-Date), $prefix, $m.Name, $m.Id, $m.Text)
            if ($m.Ok) { foreach ($it in @(Find-Items $m.Id)) { $it.IsChecked = $false } }
        }
        'installDone' {
            $ui.Progress.Visibility = 'Collapsed'
            Update-UpdateBadge
            $text = "$($m.Ok) erfolgreich, $($m.Fail) fehlgeschlagen"
            if ($m.Skipped -gt 0) { $text += ", $($m.Skipped) abgebrochen" }
            Add-LogLine "Fertig: $text"
            if ($m.Fail -gt 0)        { $kind = 'warning'; $title = 'Abgeschlossen mit Fehlern' }
            elseif ($m.Skipped -gt 0) { $kind = 'info';    $title = 'Abgebrochen' }
            else                      { $kind = 'success'; $title = 'Fertig' }
            Show-InfoBar -Kind $kind -Title $title -Text $text -ActionText 'Protokoll öffnen' -Action { Open-LogFile }
        }
        'error' {
            Add-LogLine "Fehler: $($m.Text)"
            Show-InfoBar -Kind error -Title 'Fehler' -Text $m.Text
        }
    }
}

function Receive-UiMessages {
    $msg = $null
    while ($sync.Queue.TryDequeue([ref]$msg)) {
        try { Invoke-UiMessage $msg }
        catch { Add-LogLine "Interner Fehler: $($_.Exception.Message)" }
    }
}

function Invoke-Search {
    if ($script:worker) { return }
    $term = $ui.SearchBox.Text.Trim()
    if (-not $term) {
        Show-InfoBar -Kind info -Title 'Suche' -Text 'Bitte einen Suchbegriff eingeben.'
        [void]$ui.SearchBox.Focus()
        return
    }
    Hide-InfoBar
    Start-UiWorker -Kind 'search' -Code $SearchJob -Argument $term -BusyText "Suche nach „$term“ …"
}

function Invoke-UpdateCheck {
    if ($script:worker) { return }
    Hide-InfoBar
    Start-UiWorker -Kind 'updates' -Code $UpdatesJob -Argument $null -BusyText 'Suche nach Updates …'
}

function Invoke-Install {
    if ($script:worker) { return }
    $sel = @(Get-SelectedPackages)
    if ($sel.Count -eq 0) { return }
    foreach ($it in $sel) { Set-ItemStatus -Id $it.Id -Kind 'wait' -Text 'Wartet' }
    $payload = @($sel | ForEach-Object { @{ Id = $_.Id; Name = $_.Name; Action = $_.Action } })

    Hide-InfoBar
    $ui.Progress.Value = 0
    $ui.Progress.Visibility = 'Visible'
    $ui.LogPanel.Visibility = 'Visible'
    Add-LogLine "Starte Vorgang für $($sel.Count) Paket(e) …"
    Write-LogFile ''
    Write-LogFile ('===== {0:yyyy-MM-dd HH:mm:ss} – {1} Paket(e) =====' -f (Get-Date), $sel.Count)
    Start-UiWorker -Kind 'install' -Code $InstallJob -Argument $payload -BusyText 'Vorgang wird vorbereitet …'
}

# ==================================================
# 10) Navigation aufbauen
# ==================================================
$navButtons = @{}
foreach ($key in $pages.Keys) {
    $p = $pages[$key]

    if ($p.Kind -eq 'updates') {
        $sep = New-Object System.Windows.Controls.Border
        $sep.Height = 1
        $sep.Margin = [System.Windows.Thickness]::new(12, 8, 12, 8)
        $sep.Background = $window.FindResource('Divider')
        [void]$ui.NavList.Children.Add($sep)
    }

    $content = New-Object System.Windows.Controls.DockPanel
    if ($p.Kind -eq 'updates') {
        $badge = New-Object System.Windows.Controls.Border
        $badge.CornerRadius = [System.Windows.CornerRadius]::new(8)
        $badge.Background = $window.FindResource('Accent')
        $badge.Padding = [System.Windows.Thickness]::new(6, 0, 6, 1)
        $badge.MinWidth = 18
        $badge.VerticalAlignment = 'Center'
        $badge.Visibility = 'Collapsed'
        $badgeText = New-Object System.Windows.Controls.TextBlock
        $badgeText.FontSize = 11
        $badgeText.Foreground = $window.FindResource('OnAccent')
        $badgeText.HorizontalAlignment = 'Center'
        $badge.Child = $badgeText
        [System.Windows.Controls.DockPanel]::SetDock($badge, [System.Windows.Controls.Dock]::Right)
        [void]$content.Children.Add($badge)
        $ui.UpdBadge = $badge
        $ui.UpdBadgeText = $badgeText
    }
    $label = New-Object System.Windows.Controls.TextBlock
    $label.Text = $p.Title
    $label.VerticalAlignment = 'Center'
    [void]$content.Children.Add($label)

    $rb = New-Object System.Windows.Controls.RadioButton
    $rb.Style = $window.FindResource('NavItem')
    $rb.GroupName = 'nav'
    $rb.Tag = $p.Glyph
    $rb.Uid = $key
    $rb.Content = $content
    $rb.Add_Checked({ param($s, $e) Show-Page $s.Uid })
    [void]$ui.NavList.Children.Add($rb)
    $navButtons[$key] = $rb
}

# ==================================================
# 11) Ereignisse
# ==================================================
$ui.BtnSearch.Add_Click({ Invoke-Search })
$ui.SearchBox.Add_KeyDown({
    param($s, $e)
    if ($e.Key -eq [System.Windows.Input.Key]::Return) { Invoke-Search; $e.Handled = $true }
})
$ui.BtnCheckUpdates.Add_Click({ Invoke-UpdateCheck })
$ui.BtnInstall.Add_Click({ Invoke-Install })
$ui.BtnCancel.Add_Click({
    $sync.Cancel = $true
    $ui.BtnCancel.IsEnabled = $false
    $ui.StatusText.Text = 'Wird nach dem aktuellen Paket abgebrochen …'
})
$ui.BtnLog.Add_Click({
    $ui.LogPanel.Visibility = if ($ui.LogPanel.Visibility -eq 'Visible') { 'Collapsed' } else { 'Visible' }
})
$ui.BtnOpenLog.Add_Click({ Open-LogFile })
$ui.BtnClearLog.Add_Click({ $ui.LogBox.Clear() })
$ui.InfoClose.Add_Click({ Hide-InfoBar })
$ui.InfoAction.Add_Click({ if ($script:infoAction) { & $script:infoAction } })

# "Alle auswählen" – Click statt Checked, damit das programmgesteuerte
# Nachführen des Zustands keine Endlosschleife auslöst
$ui.ChkAll.Add_Click({
    $want = $ui.ChkAll.IsChecked -eq $true
    foreach ($it in $pages[$script:currentPage].Items) { $it.IsChecked = $want }
    Update-Summary
})

# Strg+F springt zur Suche
$window.Add_PreviewKeyDown({
    param($s, $e)
    if ($e.Key -eq [System.Windows.Input.Key]::F -and
        [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control) {
        $navButtons['Suche'].IsChecked = $true
        [void]$ui.SearchBox.Focus()
        $e.Handled = $true
    }
})

$window.Add_Closing({
    param($s, $e)
    if ($script:worker -and $script:worker.Kind -eq 'install') {
        $answer = [System.Windows.MessageBox]::Show($window,
            "Es läuft noch eine Installation. Trotzdem beenden?`n`nDas aktuelle Paket wird von winget noch zu Ende installiert, alle weiteren werden übersprungen.",
            'Winget Installer', [System.Windows.MessageBoxButton]::YesNo, [System.Windows.MessageBoxImage]::Warning)
        if ($answer -ne [System.Windows.MessageBoxResult]::Yes) { $e.Cancel = $true; return }
        $sync.Cancel = $true
    }
})

# Nachrichten aus dem Hintergrund regelmäßig abholen
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(120)
$timer.Add_Tick({
    Receive-UiMessages
    if ($script:worker -and $script:worker.Handle.IsCompleted) { Complete-UiWorker }
})

# ==================================================
# 12) Start
# ==================================================
$envParts = @()
if ($wingetVersion) { $envParts += "winget $wingetVersion" }
$envParts += if ($isAdmin) { 'Administrator' } else { 'ohne Adminrechte' }
if ($useModule) { $envParts += 'WinGet-Modul' }
$ui.EnvText.Text = $envParts -join ' · '
$ui.StatusText.Text = 'Bereit'

$script:uiReady = $true
$firstCategory = @($catalog.Keys)[0]
if ($firstCategory) { $navButtons[$firstCategory].IsChecked = $true } else { $navButtons['Suche'].IsChecked = $true }
Update-UpdateBadge

$timer.Start()
[void]$window.ShowDialog()
$timer.Stop()

if ($script:worker) {
    # Laufenden Hintergrundjob nicht abwarten
    try { [void]$script:worker.PS.BeginStop($null, $null) } catch { }
    [Environment]::Exit(0)
}
exit 0

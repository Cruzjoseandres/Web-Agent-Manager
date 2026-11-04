#Requires -Version 5.1
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing, System.Windows.Forms

try {
    # Determinar rutas dinámicas basadas en la ubicación del script o en %LOCALAPPDATA%
    $currentDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    if (Test-Path "$currentDir\..\versions") {
        $appRoot = (Get-Item "$currentDir\..").FullName
    } elseif (Test-Path "$env:LOCALAPPDATA\Programs\WebAgentManager\versions") {
        $appRoot = "$env:LOCALAPPDATA\Programs\WebAgentManager"
    } else {
        $appRoot = $currentDir
    }

    $versionsDir = Join-Path $appRoot "versions"
    $configFile = Join-Path $appRoot "config\clientes.json"
    $regPath = "HKCU:\Software\Classes\web-agent\shell\open\command"
    $7z = "C:\ProgramData\chocolatey\tools\7z.exe"

    function Get-ActiveVersionName {
        $cmd = (Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue).'(default)'
        if ($cmd -match "([^\\]+)\\web-agent\.exe") {
            return $matches[1]
        }
        return "Ninguna (no configurada)"
    }

    function Get-RunningAgents {
        $procs = Get-CimInstance Win32_Process -Filter "Name = 'web-agent.exe'" -ErrorAction SilentlyContinue
        if (-not $procs) { return @() }
        $conns = Get-NetTCPConnection -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -in $procs.ProcessId }
        $result = @()
        foreach ($p in $procs) {
            $exePath = $p.ExecutablePath
            $ver = "?"
            if ($exePath) {
                $pkgPath = Join-Path (Split-Path $exePath -Parent) "package.json"
                if (Test-Path $pkgPath) {
                    try { $ver = (Get-Content $pkgPath -Raw | ConvertFrom-Json).version } catch {}
                }
            }
            $pConns = $conns | Where-Object { $_.OwningProcess -eq $p.ProcessId }
            $listenPort = ($pConns | Where-Object { $_.State -eq 'Listen' } | Select-Object -First 1).LocalPort
            $isEstab = [bool]($pConns | Where-Object { $_.State -eq 'Established' })
            $result += [PSCustomObject]@{
                PID         = $p.ProcessId
                Version     = $ver
                Port        = $listenPort
                Established = $isEstab
            }
        }
        return $result
    }

    function Switch-WebAgent {
        param([string]$TargetFolder, [switch]$KillAll)

        if ($KillAll) {
            Get-Process "web-agent" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            return @{ Success = $true; Message = "Todos los procesos de WebAgent fueron cerrados." }
        }

        $targetExe = Join-Path (Join-Path $versionsDir $TargetFolder) "web-agent.exe"
        if (-not (Test-Path $targetExe)) {
            $fallback = Join-Path "$env:LOCALAPPDATA\Programs\$TargetFolder" "web-agent.exe"
            if (Test-Path $fallback) {
                $targetExe = $fallback
            } else {
                return @{ Success = $false; Message = "No se encontro el ejecutable en: $targetExe" }
            }
        }

        $agents = Get-RunningAgents
        $killed = 0
        $kept = 0
        foreach ($a in $agents) {
            if (-not $a.Established) {
                Stop-Process -Id $a.PID -Force -ErrorAction SilentlyContinue
                $killed++
            } else {
                $kept++
            }
        }

        if (-not (Test-Path $regPath)) {
            New-Item -Path $regPath -Force | Out-Null
        }
        Set-ItemProperty -Path $regPath -Name "(default)" -Value "$targetExe %1"

        return @{
            Success = $true
            Killed  = $killed
            Kept    = $kept
            Message = "WebAgent activado: $TargetFolder"
        }
    }

    function Import-FromDownloads {
        $downDir = "$env:USERPROFILE\Downloads"
        $files = Get-ChildItem "$downDir\web-agent*.exe" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
        $imported = 0

        if (-not (Test-Path $7z)) {
            return @{ Success = $false; Message = "Herramienta de descompresion no disponible en el sistema." }
        }

        foreach ($f in $files) {
            if ($f.Name -match "web-agent-([0-9.]+)-windows-(x64|x86)") {
                $ver = $matches[1]
                $arch = $matches[2]
                $folderName = "web-agent-$ver-$arch"
                $targetDir = Join-Path $versionsDir $folderName
                $targetExe = Join-Path $targetDir "web-agent.exe"

                if (-not (Test-Path $targetExe)) {
                    $innerName = if ($arch -eq "x86") { "app-32.7z" } else { "app-64.7z" }
                    $tempDir = Join-Path $env:TEMP "wa_gui_imp_$ver`_$arch"
                    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
                    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

                    & $7z e $f.FullName "`$PLUGINSDIR\$innerName" "-o$tempDir" -y | Out-Null
                    $innerArchive = Join-Path $tempDir $innerName
                    if (Test-Path $innerArchive) {
                        & $7z x $innerArchive "-o$targetDir" -y | Out-Null
                        $hash = (Get-FileHash $f.FullName -Algorithm MD5).Hash.ToLower()
                        $iniContent = "[AGENT]`r`nCMDLINE=$($f.FullName)`r`nINSTDIR=$targetDir`r`nINSTHASH=$hash`r`n[UPDATE]`r`n"
                        Set-Content -Path (Join-Path $targetDir "web-agent.ini") -Value $iniContent -Encoding ASCII
                        $imported++
                    }
                    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
                }
            }
        }
        return @{ Success = $true; Count = $imported }
    }

    # ----------------- XAML / INTERFAZ GRAFICA WPF -----------------
    [xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="TOTVS WebAgent Manager" Height="680" Width="820"
        WindowStartupLocation="CenterScreen" Background="#F3F3F3"
        FontFamily="Segoe UI" ResizeMode="CanResizeWithGrip">
    <Window.Resources>
        <Style TargetType="Button" x:Key="SecondaryButton">
            <Setter Property="Background" Value="#FFFFFF"/>
            <Setter Property="Foreground" Value="#323130"/>
            <Setter Property="FontWeight" Value="Normal"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="BorderBrush" Value="#D2D0CE"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#F3F2F1"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- ENCABEZADO -->
        <Border Grid.Row="0" Background="#FFFFFF" CornerRadius="10" Padding="18" Margin="0,0,0,14" BorderBrush="#E1DFDD" BorderThickness="1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0">
                    <TextBlock Text="TOTVS Protheus - Gestor de WebAgent" FontSize="20" FontWeight="Bold" Foreground="#201F1E"/>
                    <TextBlock Text="Cambia de cliente con 1 clic sin reinstalar nada. Compatible con multiples pestañas." FontSize="13" Foreground="#605E5C" Margin="0,3,0,0"/>
                </StackPanel>
                <Button Grid.Column="1" Name="BtnRefresh" Style="{StaticResource SecondaryButton}" Content="Actualizar estado" VerticalAlignment="Center"/>
            </Grid>
        </Border>

        <!-- ESTADO ACTUAL -->
        <Border Grid.Row="1" Background="#FFFFFF" CornerRadius="10" Padding="16" Margin="0,0,0,14" BorderBrush="#E1DFDD" BorderThickness="1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                
                <Border Grid.Column="0" Name="StatusIconBox" Background="#DFF6DD" CornerRadius="20" Width="40" Height="40" Margin="0,0,14,0">
                    <TextBlock Name="StatusIcon" Text="OK" FontSize="14" FontWeight="Bold" Foreground="#107C41" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                
                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="Version activa en Windows: " FontSize="13" Foreground="#605E5C"/>
                        <TextBlock Name="TxtActiveVersion" Text="Cargando..." FontSize="14" FontWeight="Bold" Foreground="#107C41"/>
                    </StackPanel>
                    <TextBlock Name="TxtActiveDetails" Text="Monitoreando puertos y sesiones activas..." FontSize="12" Foreground="#8A8886" Margin="0,2,0,0"/>
                </StackPanel>

                <Button Grid.Column="2" Name="BtnKillIdle" Style="{StaticResource SecondaryButton}" Content="Limpiar puertos libres" ToolTip="Cierra procesos que quedaron en espera sin desconectar tus pestañas activas." VerticalAlignment="Center"/>
            </Grid>
        </Border>

        <!-- LISTA DE CLIENTES -->
        <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" Margin="0,0,0,14">
            <StackPanel Name="CardsPanel" Orientation="Vertical"/>
        </ScrollViewer>

        <!-- BARRA INFERIOR DE ACCIONES -->
        <Border Grid.Row="3" Background="#FFFFFF" CornerRadius="10" Padding="14,10" BorderBrush="#E1DFDD" BorderThickness="1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Name="TxtToast" Grid.Column="0" Text="Listo para operar." FontSize="12" Foreground="#605E5C" VerticalAlignment="Center"/>
                <Button Grid.Column="1" Name="BtnImport" Style="{StaticResource SecondaryButton}" Content="Importar nuevo desde Descargas" Margin="0,0,10,0"/>
                <Button Grid.Column="2" Name="BtnKillAll" Style="{StaticResource SecondaryButton}" Content="Cerrar TODO" ToolTip="Fuerza el cierre de todas las instancias de WebAgent en memoria." Foreground="#A80000"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

    $reader = [System.Xml.XmlNodeReader]::new($xaml)
    $window = [System.Windows.Markup.XamlReader]::Load($reader)

    $txtActiveVersion = $window.FindName("TxtActiveVersion")
    $txtActiveDetails = $window.FindName("TxtActiveDetails")
    $cardsPanel = $window.FindName("CardsPanel")
    $txtToast = $window.FindName("TxtToast")
    $btnRefresh = $window.FindName("BtnRefresh")
    $btnKillIdle = $window.FindName("BtnKillIdle")
    $btnKillAll = $window.FindName("BtnKillAll")
    $btnImport = $window.FindName("BtnImport")

    function Update-UI {
        $activeFolder = Get-ActiveVersionName
        $txtActiveVersion.Text = $activeFolder

        $running = Get-RunningAgents
        $activeConns = ($running | Where-Object { $_.Established }).Count
        $idleConns = ($running | Where-Object { -not $_.Established }).Count
        
        if ($activeConns -gt 0) {
            $ports = ($running | Where-Object { $_.Established } | ForEach-Object { "$($_.Port) (PID $($_.PID))" }) -join ", "
            $txtActiveDetails.Text = "Sesiones en uso: $activeConns (puertos: $ports) | Puertos en espera: $idleConns"
        } elseif ($idleConns -gt 0) {
            $txtActiveDetails.Text = "No hay sesiones activas de Protheus en uso. ($idleConns proceso(s) en espera)."
        } else {
            $txtActiveDetails.Text = "No hay procesos de WebAgent corriendo en segundo plano."
        }

        $cardsPanel.Children.Clear()

        if (Test-Path $configFile) {
            $clients = Get-Content $configFile -Raw -Encoding UTF8 | ConvertFrom-Json
        } else {
            $clients = @()
        }

        $knownVersions = $clients | ForEach-Object { $_.version }
        if (Test-Path $versionsDir) {
            $extraDirs = Get-ChildItem -Path $versionsDir -Directory | Where-Object { $_.Name -notin $knownVersions -and (Test-Path (Join-Path $_.FullName "web-agent.exe")) }
            foreach ($d in $extraDirs) {
                $clients += [PSCustomObject]@{
                    id          = $d.Name
                    nombre      = "Version $($d.Name)"
                    descripcion = "Version disponible en carpeta versions"
                    version     = $d.Name
                    badge       = "Detectada"
                    color       = "#5C2D91"
                }
            }
        }

        foreach ($cli in $clients) {
            $isCurrent = ($cli.version -eq $activeFolder)
            
            $card = New-Object System.Windows.Controls.Border
            $card.Background = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#FFFFFF")
            $card.CornerRadius = [System.Windows.CornerRadius]::new(8)
            $card.Padding = [System.Windows.Thickness]::new(14)
            $card.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
            
            $bThick = if ($isCurrent) { 2 } else { 1 }
            $card.BorderThickness = [System.Windows.Thickness]::new($bThick)
            
            $bColor = if ($isCurrent) { "#107C41" } else { "#E1DFDD" }
            $card.BorderBrush = [System.Windows.Media.BrushConverter]::new().ConvertFromString($bColor)

            $grid = New-Object System.Windows.Controls.Grid
            $c0 = New-Object System.Windows.Controls.ColumnDefinition
            $c0.Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)
            $c1 = New-Object System.Windows.Controls.ColumnDefinition
            $c1.Width = [System.Windows.GridLength]::Auto
            [void]$grid.ColumnDefinitions.Add($c0)
            [void]$grid.ColumnDefinitions.Add($c1)

            $infoStack = New-Object System.Windows.Controls.StackPanel
            
            $topRow = New-Object System.Windows.Controls.StackPanel
            $topRow.Orientation = [System.Windows.Controls.Orientation]::Horizontal
            
            $badge = New-Object System.Windows.Controls.Border
            $badgeColor = if ($cli.color) { $cli.color } else { "#0078D4" }
            $badge.Background = [System.Windows.Media.BrushConverter]::new().ConvertFromString($badgeColor)
            $badge.CornerRadius = [System.Windows.CornerRadius]::new(4)
            $badge.Padding = [System.Windows.Thickness]::new(8, 2, 8, 2)
            $badge.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
            $badgeText = New-Object System.Windows.Controls.TextBlock
            $badgeText.Text = if ($cli.badge) { $cli.badge } else { "Cliente" }
            $badgeText.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#FFFFFF")
            $badgeText.FontSize = 11
            $badgeText.FontWeight = [System.Windows.FontWeights]::SemiBold
            $badge.Child = $badgeText
            [void]$topRow.Children.Add($badge)

            $title = New-Object System.Windows.Controls.TextBlock
            $title.Text = $cli.nombre
            $title.FontSize = 15
            $title.FontWeight = [System.Windows.FontWeights]::Bold
            $title.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#201F1E")
            [void]$topRow.Children.Add($title)
            
            [void]$infoStack.Children.Add($topRow)

            $desc = New-Object System.Windows.Controls.TextBlock
            $desc.Text = $cli.descripcion
            $desc.FontSize = 12
            $desc.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#605E5C")
            $desc.Margin = [System.Windows.Thickness]::new(0, 4, 0, 0)
            [void]$infoStack.Children.Add($desc)

            [System.Windows.Controls.Grid]::SetColumn($infoStack, 0)
            [void]$grid.Children.Add($infoStack)

            $btn = New-Object System.Windows.Controls.Button
            $btn.Tag = $cli.version
            $btn.Cursor = [System.Windows.Input.Cursors]::Hand
            $btn.Padding = [System.Windows.Thickness]::new(16, 8, 16, 8)
            $btn.FontSize = 13
            $btn.FontWeight = [System.Windows.FontWeights]::SemiBold

            if ($isCurrent) {
                $btn.Content = "ACTIVO AHORA"
                $btn.Background = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#DFF6DD")
                $btn.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#107C41")
                $btn.BorderBrush = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#107C41")
                $btn.BorderThickness = [System.Windows.Thickness]::new(1)
            } else {
                $btn.Content = "Activar este cliente"
                $btn.Background = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#0078D4")
                $btn.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#FFFFFF")
                $btn.BorderThickness = [System.Windows.Thickness]::new(0)
                
                $btn.Add_Click({
                    param($s, $e)
                    $verToActivate = $s.Tag
                    $res = Switch-WebAgent -TargetFolder $verToActivate
                    if ($res.Success) {
                        $txtToast.Text = "Listo: WebAgent cambiado a $verToActivate."
                        $txtToast.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#107C41")
                    } else {
                        $txtToast.Text = $res.Message
                        $txtToast.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#A80000")
                    }
                    Update-UI
                })
            }

            [System.Windows.Controls.Grid]::SetColumn($btn, 1)
            [void]$grid.Children.Add($btn)

            $card.Child = $grid
            [void]$cardsPanel.Children.Add($card)
        }
    }

    $btnRefresh.Add_Click({
        Update-UI
        $txtToast.Text = "Estado actualizado."
    })

    $btnKillIdle.Add_Click({
        $agents = Get-RunningAgents
        $killed = 0
        foreach ($a in $agents) {
            if (-not $a.Established) {
                Stop-Process -Id $a.PID -Force -ErrorAction SilentlyContinue
                $killed++
            }
        }
        $txtToast.Text = "Se cerraron $killed proceso(s) inactivos."
        Update-UI
    })

    $btnKillAll.Add_Click({
        $confirm = [System.Windows.MessageBox]::Show(
            "Deseas cerrar todas las instancias de WebAgent? Si tienes alguna pestaña de Protheus abierta, se desconectara.",
            "Confirmar cierre total",
            [System.Windows.MessageBoxButton]::YesNo,
            [System.Windows.MessageBoxIcon]::Warning
        )
        if ($confirm -eq [System.Windows.MessageBoxResult]::Yes) {
            Switch-WebAgent -KillAll
            $txtToast.Text = "Todos los procesos fueron finalizados."
            Update-UI
        }
    })

    $btnImport.Add_Click({
        $res = Import-FromDownloads
        if ($res.Success) {
            if ($res.Count -gt 0) {
                [System.Windows.MessageBox]::Show(
                    "Se importaron $($res.Count) version(es) nueva(s) desde Descargas.",
                    "Importacion Exitosa",
                    [System.Windows.MessageBoxButton]::OK,
                    [System.Windows.MessageBoxIcon]::Information
                )
            } else {
                [System.Windows.MessageBox]::Show(
                    "No hay instaladores nuevos en Descargas.",
                    "Sin novedades",
                    [System.Windows.MessageBoxButton]::OK,
                    [System.Windows.MessageBoxIcon]::Information
                )
            }
        } else {
            [System.Windows.MessageBox]::Show($res.Message, "Error al importar", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxIcon]::Error)
        }
        Update-UI
    })

    Update-UI
    $window.ShowDialog() | Out-Null

} catch {
    [System.Windows.Forms.MessageBox]::Show($_.Exception.ToString(), "Error en WebAgent Manager", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
}

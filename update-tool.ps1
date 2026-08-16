<#
.SYNOPSIS
    Actualiza la versión de FeatureCli, compila, empaqueta el NuGet y actualiza la herramienta global con Spectre.Console.
.DESCRIPTION
    Script interactivo que muestra la versión actual, solicita la siguiente versión con validación SemVer,
    actualiza FeatureCli.csproj, ejecuta pruebas, empaqueta el proyecto y actualiza la herramienta global.
.PARAMETER Version
    Versión explícita a asignar (opcional). Si no se indica, se solicita interactivamente.
.PARAMETER SkipTests
    Omite la ejecución de pruebas antes de empaquetar.
.PARAMETER Yes
    Omite la confirmación interactiva y procede directamente.
.EXAMPLE
    .\update-tool.ps1
.EXAMPLE
    .\update-tool.ps1 -Version 0.2.1
.EXAMPLE
    .\update-tool.ps1 -Version 0.2.1 -Yes
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromPipeline = $true)]
    [string]$Version,

    [switch]$SkipTests,

    [Alias("y", "Force")]
    [switch]$Yes
)

$ErrorActionPreference = "Stop"

# 1. Localizar archivo csproj
$repoRoot = $PSScriptRoot
$csprojPath = Join-Path $repoRoot "src/FeatureCli/FeatureCli.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Error "No se encontró el archivo $csprojPath"
    exit 1
}

# 2. Cargar Spectre.Console.dll
$spectreDll = (Get-ChildItem -Path (Join-Path $repoRoot "src/FeatureCli/bin") -Filter "Spectre.Console.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1)?.FullName

if (-not $spectreDll -or -not (Test-Path $spectreDll)) {
    Write-Host "Compilando dependencias iniciales para cargar Spectre..." -ForegroundColor Cyan
    dotnet build (Join-Path $repoRoot "src/FeatureCli/FeatureCli.csproj") -c Release -v q | Out-Null
    $spectreDll = (Get-ChildItem -Path (Join-Path $repoRoot "src/FeatureCli/bin") -Filter "Spectre.Console.dll" -Recurse | Select-Object -First 1)?.FullName
}

if ($spectreDll -and (Test-Path $spectreDll)) {
    Add-Type -Path $spectreDll -ErrorAction SilentlyContinue
}

# Funciones auxiliares con Spectre
function Write-SpectreHeader {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        $rule = [Spectre.Console.Rule]::new("[bold cyan]FeatureCli[/] [dim]•[/] [bold yellow]Release & Tool Updater[/]")
        $rule.Style = [Spectre.Console.Style]::Parse("cyan")
        [Spectre.Console.AnsiConsole]::WriteLine()
        [Spectre.Console.AnsiConsole]::Write($rule)
        [Spectre.Console.AnsiConsole]::WriteLine()
    } else {
        Write-Host "`n=== FeatureCli • Release & Tool Updater ===`n" -ForegroundColor Cyan
    }
}

function Write-SpectreInfo([string]$currentVer, [string]$suggestedVer) {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        $table = [Spectre.Console.Table]::new()
        $table.Border = [Spectre.Console.TableBorder]::Rounded
        $null = $table.AddColumn("[bold]Propiedad[/]")
        $null = $table.AddColumn("[bold]Valor[/]")

        $null = [Spectre.Console.TableExtensions]::AddRow($table, [string[]]@("[dim]Proyecto[/]", "[white]FeatureCli[/]"))
        $null = [Spectre.Console.TableExtensions]::AddRow($table, [string[]]@("[dim]Archivo[/]", "[dim]src/FeatureCli/FeatureCli.csproj[/]"))
        $null = [Spectre.Console.TableExtensions]::AddRow($table, [string[]]@("[bold yellow]Versión Actual[/]", "[bold yellow]$currentVer[/]"))
        $null = [Spectre.Console.TableExtensions]::AddRow($table, [string[]]@("[bold green]Siguiente Sugerida (patch)[/]", "[bold green]$suggestedVer[/]"))

        [Spectre.Console.AnsiConsole]::Write($table)
        [Spectre.Console.AnsiConsole]::WriteLine()
    } else {
        Write-Host "Versión actual: $currentVer" -ForegroundColor Yellow
        Write-Host "Siguiente sugerida: $suggestedVer" -ForegroundColor Green
    }
}

function Prompt-NewVersion([string]$suggestedVer) {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        try {
            $prompt = [Spectre.Console.TextPrompt[string]]::new("👉 [bold white]Ingresa la nueva versión[/]:")
            $prompt.DefaultValue($suggestedVer)
            return [Spectre.Console.AnsiConsole]::Prompt($prompt)
        } catch {
            # Fallback si no hay terminal interactiva adjunta
            $inputVal = Read-Host "Ingresa la nueva versión [$suggestedVer]"
            if ([string]::IsNullOrWhiteSpace($inputVal)) { return $suggestedVer }
            return $inputVal
        }
    } else {
        $inputVal = Read-Host "Ingresa la nueva versión [$suggestedVer]"
        if ([string]::IsNullOrWhiteSpace($inputVal)) { return $suggestedVer }
        return $inputVal
    }
}

function Confirm-Action([string]$message) {
    if ($Yes) { return $true }

    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        try {
            $confirm = [Spectre.Console.ConfirmationPrompt]::new($message)
            $confirm.DefaultValue = $true
            return [Spectre.Console.AnsiConsole]::Prompt($confirm)
        } catch {
            $r = Read-Host "$message (S/n)"
            return ($r -eq '' -or $r -match '^[sSyY]')
        }
    } else {
        $r = Read-Host "$message (S/n)"
        return ($r -eq '' -or $r -match '^[sSyY]')
    }
}

# 3. Leer versión actual del XML
[xml]$csprojXml = Get-Content $csprojPath
$currentVersion = ($csprojXml.Project.PropertyGroup.Version | Where-Object { $_ }) -join ''

if ([string]::IsNullOrWhiteSpace($currentVersion)) {
    $currentVersion = "0.1.0"
}

# Calcular siguiente versión patch sugerida
$suggestedVersion = $currentVersion
if ($currentVersion -match '^(\d+)\.(\d+)\.(\d+)$') {
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3] + 1
    $suggestedVersion = "$major.$minor.$patch"
}

# 4. Mostrar encabezado e información
Write-SpectreHeader
Write-SpectreInfo -currentVer $currentVersion -suggestedVer $suggestedVersion

# 5. Obtener nueva versión
$targetVersion = $Version
if ([string]::IsNullOrWhiteSpace($targetVersion)) {
    $targetVersion = Prompt-NewVersion -suggestedVer $suggestedVersion
}

# Validar formato
if ($targetVersion -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        [Spectre.Console.AnsiConsole]::MarkupLine("[red bold]Error:[/] La versión '$targetVersion' no es un formato SemVer válido.")
    } else {
        Write-Error "La versión '$targetVersion' no es válida."
    }
    exit 1
}

# Confirmar
if (-not (Confirm-Action "¿Deseas compilar, empaquetar e instalar la versión [bold green]$targetVersion[/]?")) {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        [Spectre.Console.AnsiConsole]::MarkupLine("[yellow]Operación cancelada por el usuario.[/]")
    } else {
        Write-Host "Operación cancelada." -ForegroundColor Yellow
    }
    exit 0
}

# 6. Actualizar versión en el archivo .csproj
if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
    [Spectre.Console.AnsiConsole]::WriteLine()
    [Spectre.Console.AnsiConsole]::MarkupLine("[cyan]1/4[/] Actualizando versión en [dim]FeatureCli.csproj[/] a [bold green]$targetVersion[/]...")
} else {
    Write-Host "`n1/4 Actualizando versión a $targetVersion..." -ForegroundColor Cyan
}

$setversionCmd = Get-Command "setversion" -ErrorAction SilentlyContinue
if ($setversionCmd) {
    & setversion $targetVersion $csprojPath | Out-Null
} else {
    # Actualización directa en XML
    $content = Get-Content $csprojPath -Raw
    $newContent = $content -replace '<Version>.*?</Version>', "<Version>$targetVersion</Version>"
    Set-Content -Path $csprojPath -Value $newContent -NoNewline
}

# 7. Ejecutar pruebas (si no se omitieron)
if (-not $SkipTests) {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        [Spectre.Console.AnsiConsole]::MarkupLine("[cyan]2/4[/] Ejecutando pruebas unitarias ([dim]dotnet test[/])...")
    } else {
        Write-Host "2/4 Ejecutando pruebas..." -ForegroundColor Cyan
    }

    dotnet test (Join-Path $repoRoot "tests/FeatureCli.Tests/FeatureCli.Tests.csproj") -v q --nologo
    if ($LASTEXITCODE -ne 0) {
        if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
            [Spectre.Console.AnsiConsole]::MarkupLine("[red bold]Error:[/] Las pruebas unitarias fallaron. Despliegue abortado.")
        }
        exit 1
    }
} else {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        [Spectre.Console.AnsiConsole]::MarkupLine("[dim]2/4 Pruebas omitidas (-SkipTests)[/]")
    }
}

# 8. Empaquetar NuGet
if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
    [Spectre.Console.AnsiConsole]::MarkupLine("[cyan]3/4[/] Generando paquete NuGet ([dim]dotnet pack -c Release[/])...")
} else {
    Write-Host "3/4 Generando paquete NuGet..." -ForegroundColor Cyan
}

dotnet pack (Join-Path $repoRoot "src/FeatureCli/FeatureCli.csproj") -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        [Spectre.Console.AnsiConsole]::MarkupLine("[red bold]Error:[/] Falló la creación del paquete NuGet.")
    }
    exit 1
}

# 9. Actualizar herramienta global
if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
    [Spectre.Console.AnsiConsole]::MarkupLine("[cyan]4/4[/] Actualizando herramienta global [bold green]feature[/] a versión [bold yellow]$targetVersion[/]...")
} else {
    Write-Host "4/4 Actualizando herramienta global a versión $targetVersion..." -ForegroundColor Cyan
}

$releaseDir = Join-Path $repoRoot "src/FeatureCli/bin/Release"

# Desinstalar versión previa (si existe) para evitar conflictos de caché o versiones
& dotnet tool uninstall --global FeatureCli 2>&1 | Out-Null

# Instalar versión exacta recién compilada sin usar caché previa de NuGet
$installOutput = & dotnet tool install --global --add-source $releaseDir FeatureCli --version $targetVersion --no-cache 2>&1

if ($LASTEXITCODE -ne 0) {
    if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
        [Spectre.Console.AnsiConsole]::MarkupLine("[red bold]Error al instalar la herramienta global:[/] $installOutput")
    } else {
        Write-Error "Error al instalar la herramienta global: $installOutput"
    }
    exit 1
}

# 10. Resumen final con Spectre Panel
if ([Type]::GetType("Spectre.Console.AnsiConsole, Spectre.Console")) {
    [Spectre.Console.AnsiConsole]::WriteLine()
    $panelContent = "[bold green]✔ ¡FeatureCli actualizado exitosamente![/]`n`n" +
                    "[white]Versión instalada:[/] [bold yellow]$targetVersion[/]`n" +
                    "[white]Comando global:[/]    [bold cyan]feature --help[/]"

    $panel = [Spectre.Console.Panel]::new($panelContent)
    $panel.Header = [Spectre.Console.PanelHeader]::new("[bold green] Éxito [/]")
    $panel.Border = [Spectre.Console.BoxBorder]::Rounded
    $panel.BorderStyle = [Spectre.Console.Style]::Parse("green")
    $panel.Padding = [Spectre.Console.Padding]::new(2, 1, 2, 1)

    [Spectre.Console.AnsiConsole]::Write($panel)
    [Spectre.Console.AnsiConsole]::WriteLine()
} else {
    Write-Host "`n✔ ¡FeatureCli actualizado exitosamente a la versión $targetVersion!" -ForegroundColor Green
    Write-Host "Ejecuta 'feature --help' para comenzar.`n" -ForegroundColor White
}

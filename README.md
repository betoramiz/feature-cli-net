# FeatureCli - .NET Tool para Vertical Slice Architecture (VSA)

Herramienta de línea de comandos (CLI) desarrollada en .NET para la generación y scaffolding automático de características (*Features*), casos de uso (*Use Cases*) y mapeo de endpoints bajo el patrón de arquitectura **Vertical Slice Architecture (VSA)**.

---

## 📋 Tabla de Contenidos
1. [Requisitos Previos](#requisitos-previos)
2. [Configuración del Proyecto como .NET Tool](#1-configuración-del-proyecto-como-net-tool)
3. [Empaquetado del Proyecto (.nupkg)](#2-empaquetado-del-proyecto-nupkg)
4. [Instalación y Ejecución](#3-instalación-y-ejecución)
   - [Opción A: Instalación Global (Recomendada)](#opción-a-instalación-global)
   - [Opción B: Instalación Local por Proyecto](#opción-b-instalación-local-por-proyecto)
5. [Comandos y Uso](#4-comandos-y-uso)
6. [Actualización y Desinstalación](#5-actualización-y-desinstalación)
7. [Publicación y Distribución](#6-publicación-y-distribución)

---

## Requisitos Previos

- [.NET SDK](https://dotnet.microsoft.com/download) (versión compatible con el TargetFramework del proyecto).
- Terminal de comandos (PowerShell, Bash, Command Prompt).

---

## 1. Configuración del Proyecto como .NET Tool

Para que una aplicación de consola en .NET pueda empaquetarse y distribuirse como una herramienta ejecutable (`dotnet tool`), se agregaron las siguientes propiedades en el archivo de proyecto `FeatureCli.csproj`:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  
  <!-- Configuración para empaquetar como .NET Tool -->
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>feature</ToolCommandName>
  <PackageId>FeatureCli</PackageId>
  <Version>1.0.0</Version>
  <Authors>VSA Tools</Authors>
  <Description>CLI tool to scaffold Vertical Slice Architecture (VSA) features and use cases in .NET</Description>
</PropertyGroup>
```

### Propiedades Clave:
- `<PackAsTool>true</PackAsTool>`: Le indica al compilador que genere un paquete ejecutable como herramienta de .NET.
- `<ToolCommandName>feature</ToolCommandName>`: Define el nombre del comando que se invocará en la terminal (`feature`).
- `<PackageId>FeatureCli</PackageId>`: Identificador del paquete NuGet.

---

## 2. Empaquetado del Proyecto (.nupkg)

Para compilar y empaquetar la herramienta en un archivo `.nupkg` para distribución:

1. Abre la terminal en el directorio del proyecto CLI:
   ```bash
   cd FeatureCli/src/FeatureCli
   ```

2. Ejecuta el comando de empaquetado en configuración `Release`:
   ```bash
   dotnet pack -c Release
   ```

3. El archivo del paquete se generará en la ruta:
   ```text
   bin/Release/FeatureCli.1.0.0.nupkg
   ```

---

## 3. Instalación y Ejecución

### Opción A: Instalación Global

Permite ejecutar el comando `feature` desde cualquier terminal y en cualquier carpeta de tu máquina.

1. **Instalar la herramienta apuntando a la carpeta de salida local:**
   ```powershell
   dotnet tool install --global --add-source ./bin/Release FeatureCli
   ```
   *O usando la ruta absoluta:*
   ```powershell
   dotnet tool install --global --add-source C:\projects\NetCore\tools\feature-console\FeatureCli\src\FeatureCli\bin\Release FeatureCli
   ```

2. **Verificar la instalación:**
   ```powershell
   feature --help
   ```

---

### Opción B: Instalación Local por Proyecto

Permite vincular la herramienta a un repositorio específico mediante un archivo de manifiesto (`dotnet-tools.json`), útil para estandarizar herramientas en equipos de desarrollo.

1. En la carpeta raíz del proyecto .NET donde quieras usar la herramienta:
   ```bash
   dotnet new tool-manifest
   ```

2. Instala la herramienta localmente:
   ```bash
   dotnet tool install --add-source /ruta/hacia/FeatureCli/src/FeatureCli/bin/Release FeatureCli
   ```

3. Ejecútala dentro del proyecto:
   ```bash
   dotnet feature --help
   # o bien:
   dotnet tool run feature --help
   ```

---

## 4. Comandos y Uso

Una vez instalada, puedes usar la herramienta en cualquier proyecto .NET con arquitectura VSA:

### 4.1 Crear un nuevo Feature
Crea una carpeta de feature con su estructura base:
```bash
feature create -n Orders
```

**Opciones:**
- `-n, --name <NAME>`: Nombre del feature (Requerido).
- `-p, --path <PATH>`: Ruta base donde se generará el feature (Opcional, por defecto el directorio actual).

---

### 4.2 Crear un Caso de Uso (Use Case)
Crea un caso de uso dentro de un feature existente y actualiza automáticamente los endpoints asociados:

```bash
# Caso de uso con método GET
feature usecase -n GetOrderById -f Orders -m GET

# Caso de uso con método POST y validación FluentValidation
feature usecase -n CreateOrder -f Orders -m POST --withValidation
```

**Opciones:**
- `-n, --name <NAME>`: Nombre del caso de uso (Requerido).
- `-f, --feature <FEATURE>`: Nombre del feature existente donde se agregará (Requerido).
- `-m, --method <METHOD>`: Método HTTP (`GET`, `POST`, `PUT`, `DELETE`, etc.) (Requerido).
- `-wv, --withValidation`: Genera el validador con FluentValidation (Opcional).
- `-p, --path <PATH>`: Ruta base del proyecto (Opcional).

---

## 5. Actualización y Desinstalación

### Actualizar la herramienta localmente (Recomendado)
Puedes utilizar el script interactivo con interfaz enriquecida (**Spectre.Console**) para versionar, compilar, probar y actualizar la herramienta global en un solo paso:

```powershell
.\update-tool.ps1
```

O indicando la versión directamente:
```powershell
.\update-tool.ps1 -Version 0.2.2
```

### Actualización manual
1. Incrementa la versión en `src/FeatureCli/FeatureCli.csproj` (ej. `<Version>0.2.2</Version>`).
2. Empaqueta el proyecto:
   ```powershell
   dotnet pack -c Release
   ```
3. Actualiza la herramienta instalada:
   ```powershell
   dotnet tool update --global --add-source ./src/FeatureCli/bin/Release FeatureCli
   ```

### Desinstalar la herramienta
```powershell
dotnet tool uninstall --global FeatureCli
```

---

## 6. Publicación y Distribución

### Publicar en NuGet.org (Público)
Para permitir que cualquier usuario instale la herramienta mediante `dotnet tool install --global FeatureCli`:

```bash
dotnet nuget push bin/Release/FeatureCli.1.0.0.nupkg --api-key <TU_API_KEY_NUGET> --source https://api.nuget.org/v3/index.json
```

### Publicar en un Feed Privado (GitHub Packages / Azure DevOps)
```bash
dotnet nuget push bin/Release/FeatureCli.1.0.0.nupkg --api-key <TOKEN> --source https://nuget.pkg.github.com/ORGANIZACION/index.json
```

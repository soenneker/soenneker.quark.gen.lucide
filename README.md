[![](https://img.shields.io/nuget/v/soenneker.quark.gen.lucide.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.quark.gen.lucide/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.quark.gen.lucide/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.quark.gen.lucide/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.quark.gen.lucide.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.quark.gen.lucide/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Quark.Gen.Lucide
### Build-time source generation for Lucide icons in Quark and Razor projects

## Installation

```
dotnet add package Soenneker.Quark.Gen.Lucide
dotnet add package Soenneker.Lucide.Enums.Icons
dotnet add package Soenneker.Lucide.Icons
```

## What it does

1. **BuildTasks** (runs before Build): Scans .cs and .razor files for Lucide icon usage (`Icon="LucideIcon.Arrow"`, `LucideIcon.Check`, etc.)
2. **Reads SVGs** from the Soenneker.Lucide.Icons package (NuGet cache)
3. **Emits** `LucideIconSvgMap.g.cs` with a switch that maps icon names to SVG content

## Usage

Use `LucideIcon` enum values in components, e.g.:

```razor
<SomeComponent Icon="LucideIcon.Arrow" />
<SomeComponent Icon="@LucideIcon.Check" />
```

```csharp
var icon = LucideIcon.Arrow;
```

The generator emits a static helper:

```csharp
var svg = LucideIconSvgMap.GetSvg("Arrow"); // "<svg xmlns=\"...\">...</svg>"
```

## How it works

The package includes MSBuild targets that run `Soenneker.Quark.Gen.Lucide.BuildTasks` before each build. The tool scans your project for Lucide icon references and generates `obj/Generated/LucideIconSvgMap.g.cs`, which is compiled into your assembly. Override the output path with:

```xml
<PropertyGroup>
  <LucideSvgMapOutput>$(IntermediateOutputPath)MyPath\LucideIconSvgMap.g.cs</LucideSvgMapOutput>
</PropertyGroup>
```

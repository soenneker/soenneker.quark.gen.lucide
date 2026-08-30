[![](https://img.shields.io/nuget/v/soenneker.quark.gen.lucide.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.quark.gen.lucide/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.quark.gen.lucide/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.quark.gen.lucide/actions/workflows/publish-package.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.quark.gen.lucide/build-and-test.yml?label=Build&style=for-the-badge)](https://github.com/soenneker/soenneker.quark.gen.lucide/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/nuget/dt/soenneker.quark.gen.lucide.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.quark.gen.lucide/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.quark.gen.lucide/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.quark.gen.lucide/actions/workflows/codeql.yml)

# Soenneker.Quark.Gen.Lucide

Build-time generation of a trimmed Lucide SVG provider for Quark and Razor projects.

## Install

```bash
dotnet add package Soenneker.Quark.Gen.Lucide
dotnet add package Soenneker.Lucide.Enums.Icons
dotnet add package Soenneker.Lucide.Icons
```

The enum package supplies `LucideIcon` values. The icons package supplies the SVG resources used during generation.

## Usage

Reference icons directly in C# or Razor so the build can discover them:

```razor
<Lucide Icon="LucideIcon.Check" />
<Button Icon="LucideIcon.ArrowRight">Continue</Button>
```

Register the generated provider with dependency injection:

```csharp
using Soenneker.Quark.Gen.Lucide.Generated;

services.AddLucideIconsAsScoped();
```

At build time, the package finds `LucideIcon.Name` references in `.cs` and `.razor` files and embeds only those SVGs in the consuming assembly. Generated files are written under the intermediate output directory and compiled automatically.

Icon names created only through reflection, concatenation, configuration, or other dynamic logic cannot be discovered. Add a direct `LucideIcon.Name` reference for every icon that must be included.

## Build options

Disable generation for a project:

```xml
<PropertyGroup>
  <LucideGeneratorBuildEnabled>false</LucideGeneratorBuildEnabled>
</PropertyGroup>
```

Override the generated map path when the default intermediate location is unsuitable:

```xml
<PropertyGroup>
  <LucideSvgMapOutput>$(IntermediateOutputPath)MyPath\LucideIconSvgMap.g.cs</LucideSvgMapOutput>
</PropertyGroup>
```

The generated SVG map and provider are implementation details. Consume them through `ILucideIconSvgProvider` or Quark’s Lucide components.

# ghFolding -- Agent Instructions

## Project

Grasshopper plugin for Rhino 8. Compiles to `.gha` (a renamed .NET assembly
loaded by Grasshopper at runtime). Each public `GH_Component` subclass becomes
a draggable node on the Grasshopper canvas.

## Build & Debug

```
dotnet build                        # outputs bin/Debug/net7.0/ghFolding.gha
```

Launch Rhino 8 via VSCode (F5) -- see `.vscode/launch.json`.
`RHINO_PACKAGE_DIRS` points Grasshopper at `bin/Debug` so the plugin
loads automatically.

Target frameworks: `net48`, `net7.0`, `net7.0-windows`.

## File Layout

```
ghFolding.csproj          Project file (multi-target, Grasshopper 8.x NuGet)
ghFoldingInfo.cs          GH_AssemblyInfo -- plugin metadata
ghFoldingComponent.cs     Archimedean spiral component
SpiralMaker.cs            Fibonacci spiral component
Properties/               Assembly attributes
```

## Architecture Rules

- One `GH_Component` subclass per file. File name must match class name.
- Every component needs a unique, stable `ComponentGuid`. Never change it
  after release.
- Keep `SolveInstance` small. Extract geometry logic into private methods.
- Validate all inputs. Use `AddRuntimeMessage` for errors/warnings.
- Prefer `PolyCurve.Append` over `Curve.JoinCurves` when arc/curve order
  is known -- avoids tolerance failures at large scales.

## Style

Follow the **Google C# Style Guide**
(<https://google.github.io/styleguide/csharp-style.html>).

Key points that apply to this repo:

| Rule | Convention |
|------|-----------|
| Indentation | 2 spaces, no tabs |
| Braces | Opening brace on same line as statement |
| Line length | 100 columns max |
| Naming | `PascalCase` public, `_camelCase` private fields, `camelCase` locals |
| `var` | Use when type is obvious from RHS |
| Properties | Expression-body (`=>`) for single-line read-only |

Run `/style` for the full reference.

## Available Skills

| Command | Purpose |
|---------|---------|
| `/style` | Full Google C# style conventions for this project |
| `/gh-component` | Scaffold and patterns for new Grasshopper components |
| `/review` | Code review checklist |

# paperfold-plugin

[English](README.md) | [한국어](README.ko.md)

A Grasshopper plugin for Rhino 8 that generates spiral geometry.

## Components

### Archimedean Spiral

Builds a spiral from concentric semicircular arcs between an inner and
outer radius.

| Parameter | Type | Description |
|-----------|------|-------------|
| **Plane** (P) | Plane | Base plane (default: World XY) |
| **Inner Radius** (R0) | Number | Starting radius (default: 1.0) |
| **Outer Radius** (R1) | Number | Ending radius (default: 10.0) |
| **Turns** (T) | Integer | Number of half-turns (default: 10) |

**Output:** Spiral (S) -- a single joined `PolyCurve`.

### Fibonacci Spiral

Tiles squares whose side lengths follow the Fibonacci sequence
(1, 1, 2, 3, 5, 8, 13, ...) and inscribes a quarter-circle arc in each
square. The connected arcs approximate the golden spiral.

| Parameter | Type | Description |
|-----------|------|-------------|
| **Steps** (N) | Integer | Number of Fibonacci steps (default: 10, max: 50) |

**Outputs:**

| Output | Description |
|--------|-------------|
| Rectangles (R) | Golden rectangles as closed polylines |
| Arcs (A) | Individual quarter-circle arcs |
| Spiral (S) | All arcs joined into a single curve |

#### Technical notes

- Fibonacci numbers are computed iteratively (not via Binet's formula)
  to maintain exact integer precision at all step counts.
- Rotation axes use an exact vector lookup table instead of trigonometric
  functions, avoiding floating-point drift such as `cos(pi/2) = 6.12e-17`.
- Arcs are joined with `PolyCurve.Append` instead of `Curve.JoinCurves`
  to avoid tolerance-based failures at large geometric scales (F(47) exceeds
  3 billion units).

## Requirements

- Rhino 8
- .NET SDK 7.0+

## Build

```
dotnet build
```

Output: `bin/Debug/net7.0/ghFolding.gha`

Target frameworks: `net48`, `net7.0`, `net7.0-windows`.

## Install

Copy `ghFolding.gha` from the build output to your Grasshopper Libraries
folder, or launch via VSCode (F5) which sets `RHINO_PACKAGE_DIRS`
automatically.

## License

MIT

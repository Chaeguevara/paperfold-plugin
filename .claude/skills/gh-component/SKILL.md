---
name: gh-component
description: Scaffold and patterns for creating new Grasshopper GH_Component subclasses. Use when adding a new component to this plugin or modifying component inputs/outputs.
---

# Grasshopper Component Patterns

Use this reference when creating or modifying `GH_Component` subclasses.
All components target Grasshopper for Rhino 8 (NuGet `Grasshopper 8.x`).

---

## Scaffold

Every component needs these six members:

```csharp
using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace ghFolding {
  public class MyComponent : GH_Component {
    // 1. Constructor -- name, nickname, description, tab, panel
    public MyComponent()
      : base("Display Name", "Short", "Tooltip description",
             "Category", "Subcategory") {
    }

    // 2. Inputs
    protected override void RegisterInputParams(
        GH_Component.GH_InputParamManager pManager) {
      pManager.AddIntegerParameter(
          "Count", "N", "Number of items", GH_ParamAccess.item, 10);
    }

    // 3. Outputs
    protected override void RegisterOutputParams(
        GH_Component.GH_OutputParamManager pManager) {
      pManager.AddCurveParameter(
          "Result", "R", "Output curves", GH_ParamAccess.list);
    }

    // 4. Solver -- keep small, delegate to private methods
    protected override void SolveInstance(IGH_DataAccess DA) {
      int count = 0;
      if (!DA.GetData(0, ref count)) return;

      if (count < 1) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Count >= 1");
        return;
      }

      var results = BuildGeometry(count);
      DA.SetDataList(0, results);
    }

    // 5. Icon (null until you add a 24x24 bitmap resource)
    protected override System.Drawing.Bitmap Icon => null;

    // 6. Stable GUID -- generate once, never change
    public override Guid ComponentGuid =>
        new Guid("XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX");

    List<Curve> BuildGeometry(int count) {
      // ...
    }
  }
}
```

## Checklist for a New Component

1. **File**: one component per `.cs` file. File name = class name.
2. **GUID**: generate a fresh GUID (`System.Guid.NewGuid()`). Never reuse.
3. **Category / Subcategory**: match existing components so they appear
   in the same Grasshopper tab and panel.
4. **Input defaults**: always supply a default value for optional params.
5. **Validation**: check every input. Use `AddRuntimeMessage` with
   `Error` (blocks output) or `Warning` (output still produced).
6. **Output access**: `GH_ParamAccess.item` for singles, `.list` for collections.
7. **Build**: `dotnet build` must pass with 0 errors, 0 warnings.

## Common Parameter Types

| Method | Rhino Type |
|--------|-----------|
| `AddNumberParameter` | `double` |
| `AddIntegerParameter` | `int` |
| `AddPointParameter` | `Point3d` |
| `AddPlaneParameter` | `Plane` |
| `AddCurveParameter` | `Curve` |
| `AddSurfaceParameter` | `Surface` / `Brep` |
| `AddBooleanParameter` | `bool` |
| `AddTextParameter` | `string` |

## Joining Curves at Scale

Prefer `PolyCurve.Append` over `Curve.JoinCurves` when order is known:

```csharp
var spiral = new PolyCurve();
foreach (var arc in arcs) {
  spiral.Append(arc);
}
```

`Curve.JoinCurves` uses document tolerance (default 0.001) to match
endpoints. At large geometric scales, floating-point drift in `Arc` /
`NurbsCurve` can exceed this tolerance.

## Numerical Precision

- Compute integer sequences iteratively (not via closed-form formulas).
- Use exact axis vectors (`Vector3d.XAxis`, etc.) for cardinal directions.
- Coordinates from integer sequences + cardinal axes remain exact doubles.

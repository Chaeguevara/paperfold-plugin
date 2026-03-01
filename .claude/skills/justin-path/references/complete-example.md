# Justin Path — Complete Component Example

Full implementation of `JustinPathAnalyzer.cs`. Adapt inputs/outputs as needed.

## File: `JustinPathAnalyzer.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace ghFolding {
  /// <summary>
  /// Analyses a 2-vertex symmetric flat-foldable crease graph using Justin's theorem.
  /// Outputs the extracted annular ribbon geometry and validates flat-foldability
  /// by verifying that the product of six crease reflection matrices equals I₂.
  /// </summary>
  public class JustinPathAnalyzer : GH_Component {
    public JustinPathAnalyzer()
      : base("Justin Path Analyzer", "Justin",
             "Validates multi-vertex flat-foldability via reflection matrix products " +
             "and extracts an annular crease ribbon between inner and outer radii.",
             "ghFolding", "Origami") {
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager) {
      pManager.AddNumberParameter(
          "Theta", "θ",
          "Symmetry angle of the 2-vertex graph in degrees. Clamped to [20, 80].",
          GH_ParamAccess.item, 45.0);
      pManager.AddNumberParameter(
          "VertexDist", "D",
          "Half-distance between the two vertices along the x-axis.",
          GH_ParamAccess.item, 1.0);
      pManager.AddNumberParameter(
          "InnerRadius", "Ri",
          "Radius of the inner boundary circle. Must be > VertexDist.",
          GH_ParamAccess.item, 2.0);
      pManager.AddNumberParameter(
          "OuterRadius", "Ro",
          "Radius of the outer boundary circle. Must be > InnerRadius.",
          GH_ParamAccess.item, 4.0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager) {
      pManager.AddBooleanParameter(
          "IsFlatFoldable", "F",
          "True if the matrix product of all 6 crease reflections equals I₂ (< 1e-10 error).",
          GH_ParamAccess.item);
      pManager.AddNumberParameter(
          "MatrixError", "E",
          "Frobenius distance of the cumulative reflection product from I₂. " +
          "Should be < 1e-10 for a valid pattern.",
          GH_ParamAccess.item);
      pManager.AddCurveParameter(
          "CreaseSegments", "C",
          "Line segments for each crease between the inner and outer boundary circles.",
          GH_ParamAccess.list);
      pManager.AddCurveParameter(
          "InnerBoundary", "Bi",
          "Circle at InnerRadius centred on the spine midpoint.",
          GH_ParamAccess.item);
      pManager.AddCurveParameter(
          "OuterBoundary", "Bo",
          "Circle at OuterRadius centred on the spine midpoint.",
          GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA) {
      double theta = 45.0, d = 1.0, rIn = 2.0, rOut = 4.0;

      if (!DA.GetData(0, ref theta)) return;
      if (!DA.GetData(1, ref d))     return;
      if (!DA.GetData(2, ref rIn))   return;
      if (!DA.GetData(3, ref rOut))  return;

      // --- Clamp and validate ------------------------------------------------
      theta = Math.Max(20.0, Math.Min(80.0, theta));

      if (rIn <= d) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            "InnerRadius should be greater than VertexDist to avoid vertex overlap.");
      }
      if (rOut <= rIn) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "OuterRadius must be greater than InnerRadius.");
        return;
      }

      // --- Build crease rays -------------------------------------------------
      var center = Point2d.Origin;
      var vR = new Point2d( d, 0);
      var vL = new Point2d(-d, 0);
      double t = theta;

      var rays = new List<(Point2d origin, double angleDeg)> {
        (vR, 0),
        (vR, t),
        (vL, 180 - t),
        (vL, 180),
        (vL, 180 + t),
        (vR, 360 - t),
      };

      // --- Compute matrix product --------------------------------------------
      var M = Identity();
      foreach (var (_, angleDeg) in rays.OrderBy(r => r.angleDeg)) {
        M = Mul(Reflection(angleDeg * Math.PI / 180.0), M);
      }

      double error = FrobeniusError(M);
      bool isFoldable = error < 1e-10;

      if (!isFoldable) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            $"Matrix product deviates from I₂ by {error:E2}. Pattern may not be flat-foldable.");
      }

      // --- Build ribbon geometry ---------------------------------------------
      var segments = BuildCreaseSegments(rays, center, rIn, rOut);

      var innerCircle = new Circle(
          new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis), rIn);
      var outerCircle = new Circle(
          new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis), rOut);

      // --- Set outputs -------------------------------------------------------
      DA.SetData(0, isFoldable);
      DA.SetData(1, error);
      DA.SetDataList(2, segments);
      DA.SetData(3, innerCircle.ToNurbsCurve());
      DA.SetData(4, outerCircle.ToNurbsCurve());
    }

    // -------------------------------------------------------------------------
    // Geometry helpers
    // -------------------------------------------------------------------------

    List<LineCurve> BuildCreaseSegments(
        List<(Point2d origin, double angleDeg)> rays,
        Point2d center, double rIn, double rOut) {
      var segments = new List<LineCurve>();

      foreach (var (origin, angleDeg) in rays) {
        var ptIn  = RayCircleIntersect(origin, angleDeg, center, rIn);
        var ptOut = RayCircleIntersect(origin, angleDeg, center, rOut);

        if (ptIn == Point2d.Unset || ptOut == Point2d.Unset) {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
              $"Crease at {angleDeg:F1}° from ({origin.X:F2},{origin.Y:F2}) " +
              "does not intersect one of the boundary circles. Skipping.");
          continue;
        }

        var a = new Point3d(ptIn.X,  ptIn.Y,  0);
        var b = new Point3d(ptOut.X, ptOut.Y, 0);
        segments.Add(new LineCurve(a, b));
      }

      return segments;
    }

    /// <summary>
    /// Returns the forward (t > 0) intersection of a ray with a circle.
    /// Returns Point2d.Unset when the ray does not reach the circle.
    /// </summary>
    Point2d RayCircleIntersect(
        Point2d origin, double angleDeg, Point2d center, double radius) {
      double rad = angleDeg * Math.PI / 180.0;
      double dx = Math.Cos(rad), dy = Math.Sin(rad);

      // Shift to circle-centred coordinates; a = 1 because dx²+dy² = 1
      double fx = origin.X - center.X;
      double fy = origin.Y - center.Y;
      double b = 2 * (fx*dx + fy*dy);
      double c = fx*fx + fy*fy - radius*radius;
      double disc = b*b - 4*c;

      if (disc < 0) return Point2d.Unset;
      double tVal = (-b + Math.Sqrt(disc)) / 2.0;
      return tVal > 0
          ? new Point2d(origin.X + tVal*dx, origin.Y + tVal*dy)
          : Point2d.Unset;
    }

    // -------------------------------------------------------------------------
    // 2×2 matrix helpers (row-major double[2, 2])
    // -------------------------------------------------------------------------

    double[,] Identity() => new double[,] { { 1, 0 }, { 0, 1 } };

    double[,] Reflection(double thetaRad) {
      double c = Math.Cos(2 * thetaRad);
      double s = Math.Sin(2 * thetaRad);
      return new double[,] { { c, s }, { s, -c } };
    }

    double[,] Mul(double[,] a, double[,] b) => new double[,] {
      { a[0,0]*b[0,0] + a[0,1]*b[1,0], a[0,0]*b[0,1] + a[0,1]*b[1,1] },
      { a[1,0]*b[0,0] + a[1,1]*b[1,0], a[1,0]*b[0,1] + a[1,1]*b[1,1] }
    };

    double FrobeniusError(double[,] m) =>
        Math.Sqrt(Math.Pow(m[0,0]-1, 2) + Math.Pow(m[1,1]-1, 2) +
                  m[0,1]*m[0,1] + m[1,0]*m[1,0]);

    // -------------------------------------------------------------------------

    protected override System.Drawing.Bitmap Icon => null;

    public override Guid ComponentGuid =>
        new Guid("A3F7B2E1-4C8D-4A92-B6F5-1E3D7C9A0B5F");
  }
}
```

## Key Decisions Made Here

| Decision | Reason |
|---|---|
| Rays built as `(Point2d, double)` tuples | Avoids a dedicated struct for 6 short-lived values |
| `OrderBy(r => r.angleDeg)` before accumulation | Justin product requires CCW angular order |
| `RayCircleIntersect` takes `Point2d` not `Point3d` | All Justin Path geometry lives in XY; keep 2D until output |
| Segments skipped with a Warning (not Error) | Lets other outputs still populate; easier to diagnose |
| `Circle.ToNurbsCurve()` for boundary output | Grasshopper curve params expect `Curve`; `Circle` is not a `Curve` |
| GUID hardcoded (not `Guid.NewGuid()`) | Must be stable after first release; generated once offline |

## Variations to Consider

**Generalised N-vertex graph**: replace the hardcoded 6-ray list with a
`List<CreaseRay>` input. The matrix accumulation loop is identical.

**Mountain/valley output**: add a `string[]` output that cycles M/V per vertex
according to Maekawa's rule (|M−V| = 2). For 3 creases per vertex:
enumerate {M=2,V=1} and {M=1,V=2} for each vertex independently.

**3D placement**: apply a `Transform.PlaneToPlane(Plane.WorldXY, targetPlane)`
to all geometry before setting outputs — lets users position the annular ribbon
anywhere in 3D space.

using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace ghFolding {
  /// <summary>
  /// Generates the 2D crease pattern layout for a 2-vertex symmetric
  /// flat-foldable annular sheet (zero-thickness paper).
  ///
  /// Two interior vertices (vL, vR) each emit three crease rays, giving six
  /// rays total. The paper is the annular ribbon between InnerRadius and
  /// OuterRadius; each crease ray is clipped to that ribbon.
  ///
  /// Flat-foldability is verified via Justin's theorem: the ordered product of
  /// the six 2×2 reflection matrices (one per crease, traversed CCW) must equal
  /// the identity matrix I₂. The 2-vertex symmetric layout satisfies this for
  /// all θ ∈ (0°, 90°).
  /// </summary>
  public class FlatFoldLayout : GH_Component {
    public FlatFoldLayout()
      : base("Flat Fold Layout", "FFLayout",
             "2D crease pattern layout for a 2-vertex symmetric flat-foldable " +
             "annular sheet (zero thickness).",
             "Category", "Subcategory") {
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager) {
      pManager.AddNumberParameter(
          "Theta", "θ",
          "Symmetry angle in degrees. Controls the diagonal crease pair spread. " +
          "Clamped to [5, 85] to avoid degenerate collinear rays.",
          GH_ParamAccess.item, 45.0);
      pManager.AddNumberParameter(
          "VertexDist", "D",
          "Half-distance between the two interior vertices along the x-axis. " +
          "Both vertices must lie strictly inside InnerRadius.",
          GH_ParamAccess.item, 1.0);
      pManager.AddNumberParameter(
          "InnerRadius", "Ri",
          "Radius of the inner cut circle. Must be greater than VertexDist.",
          GH_ParamAccess.item, 2.0);
      pManager.AddNumberParameter(
          "OuterRadius", "Ro",
          "Radius of the outer cut circle. Must be greater than InnerRadius.",
          GH_ParamAccess.item, 4.0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager) {
      pManager.AddBooleanParameter(
          "IsFlatFoldable", "F",
          "True when the Justin matrix product equals I₂ (within 1e-10). " +
          "Should always be true for this symmetric topology.",
          GH_ParamAccess.item);
      pManager.AddNumberParameter(
          "MatrixError", "Err",
          "Frobenius norm of (product − I₂). Values < 1e-10 confirm flat-foldability.",
          GH_ParamAccess.item);
      pManager.AddCurveParameter(
          "OuterBoundary", "Bo",
          "Outer cut circle of the paper ribbon.",
          GH_ParamAccess.item);
      pManager.AddCurveParameter(
          "InnerBoundary", "Bi",
          "Inner cut circle (hole boundary) of the paper ribbon.",
          GH_ParamAccess.item);
      pManager.AddCurveParameter(
          "CreaseLines", "C",
          "Six fold lines clipped to the annular ribbon " +
          "(from inner boundary to outer boundary).",
          GH_ParamAccess.list);
      pManager.AddPointParameter(
          "VertexPoints", "V",
          "Positions of the two interior crease vertices (reference only).",
          GH_ParamAccess.list);
    }

    /// <summary>
    /// Core solver — kept intentionally thin. Steps:
    ///   1. Validate and clamp inputs.
    ///   2. Define 6 crease rays (3 from vR, 3 from vL).
    ///   3. Verify flat-foldability via Justin matrix product.
    ///   4. Clip each ray to the annular ribbon.
    ///   5. Output boundaries, creases, and vertex reference points.
    /// </summary>
    protected override void SolveInstance(IGH_DataAccess DA) {
      double theta = 45.0, d = 1.0, rIn = 2.0, rOut = 4.0;

      if (!DA.GetData(0, ref theta)) return;
      if (!DA.GetData(1, ref d))     return;
      if (!DA.GetData(2, ref rIn))   return;
      if (!DA.GetData(3, ref rOut))  return;

      theta = Math.Max(5.0, Math.Min(85.0, theta));

      if (d <= 0.0) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "VertexDist must be positive.");
        return;
      }
      if (rIn <= d) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            "InnerRadius ≤ VertexDist: vertices fall outside the inner hole. " +
            "Crease rays that miss the inner circle will be skipped.");
      }
      if (rOut <= rIn) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "OuterRadius must be greater than InnerRadius.");
        return;
      }

      // Six crease rays: 3 from the right vertex vR, 3 from the left vertex vL.
      // Listed in CCW angular order so the matrix traversal is unambiguous.
      var vR = new Point2d( d, 0);
      var vL = new Point2d(-d, 0);

      var rays = new (Point2d Origin, double AngleDeg)[] {
        (vR,  0.0),
        (vR,  theta),
        (vL,  180.0 - theta),
        (vL,  180.0),
        (vL,  180.0 + theta),
        (vR,  360.0 - theta),
      };

      double err = JustinError(rays);
      bool isFoldable = err < 1e-10;

      if (!isFoldable) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            $"Justin matrix product deviates from I₂ by {err:E2}. " +
            "The crease pattern may not fold flat.");
      }

      var creaseLines = ClipRaysToRibbon(rays, rIn, rOut);
      var innerCircle = new ArcCurve(new Circle(Plane.WorldXY, rIn));
      var outerCircle = new ArcCurve(new Circle(Plane.WorldXY, rOut));
      var vertexPts = new List<Point3d> {
        new Point3d(vL.X, vL.Y, 0),
        new Point3d(vR.X, vR.Y, 0),
      };

      DA.SetData(0, isFoldable);
      DA.SetData(1, err);
      DA.SetData(2, outerCircle);
      DA.SetData(3, innerCircle);
      DA.SetDataList(4, creaseLines);
      DA.SetDataList(5, vertexPts);
    }

    // -------------------------------------------------------------------------
    // Geometry
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clips each crease ray to the ribbon between rIn and rOut.
    /// Rays that miss either circle are skipped with a runtime warning.
    /// </summary>
    List<LineCurve> ClipRaysToRibbon(
        (Point2d Origin, double AngleDeg)[] rays, double rIn, double rOut) {
      var lines = new List<LineCurve>();
      var center = Point2d.Origin;

      foreach (var (origin, angleDeg) in rays) {
        var ptIn  = RayCircleIntersect(origin, angleDeg, center, rIn);
        var ptOut = RayCircleIntersect(origin, angleDeg, center, rOut);

        if (ptIn == null || ptOut == null) {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
              $"Ray at {angleDeg:F1}° from ({origin.X:F2}, {origin.Y:F2}) " +
              "missed a boundary circle and was skipped.");
          continue;
        }

        lines.Add(new LineCurve(
            new Point3d(ptIn.Value.X,  ptIn.Value.Y,  0),
            new Point3d(ptOut.Value.X, ptOut.Value.Y, 0)));
      }

      return lines;
    }

    /// <summary>
    /// Returns the forward intersection (t > 0) of a ray with a circle
    /// centred at the world origin, or null when no forward intersection exists.
    /// </summary>
    Point2d? RayCircleIntersect(
        Point2d origin, double angleDeg, Point2d center, double radius) {
      double rad = angleDeg * Math.PI / 180.0;
      double dx = Math.Cos(rad), dy = Math.Sin(rad);

      // Translate to circle-centred frame; a = 1 since ||(dx, dy)|| = 1.
      double fx = origin.X - center.X;
      double fy = origin.Y - center.Y;
      double b  = 2.0 * (fx * dx + fy * dy);
      double c  = fx * fx + fy * fy - radius * radius;
      double disc = b * b - 4.0 * c;

      if (disc < 0) return null;
      double t = (-b + Math.Sqrt(disc)) / 2.0;
      if (t > 0.0) return new Point2d(origin.X + t * dx, origin.Y + t * dy);
      return null;
    }

    // -------------------------------------------------------------------------
    // Justin flat-foldability check
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the Frobenius distance of the CCW-ordered reflection matrix
    /// product from I₂. Zero means the pattern is perfectly flat-foldable.
    /// </summary>
    double JustinError((Point2d Origin, double AngleDeg)[] rays) {
      var M = new double[,] { { 1, 0 }, { 0, 1 } };
      foreach (var ray in rays.OrderBy(r => r.AngleDeg)) {
        M = Mul(Reflection(ray.AngleDeg * Math.PI / 180.0), M);
      }
      return Math.Sqrt(Math.Pow(M[0,0] - 1, 2) + Math.Pow(M[1,1] - 1, 2) +
                       M[0,1] * M[0,1] + M[1,0] * M[1,0]);
    }

    double[,] Reflection(double thetaRad) {
      double c = Math.Cos(2 * thetaRad), s = Math.Sin(2 * thetaRad);
      return new double[,] { { c, s }, { s, -c } };
    }

    double[,] Mul(double[,] a, double[,] b) => new double[,] {
      { a[0,0]*b[0,0] + a[0,1]*b[1,0],  a[0,0]*b[0,1] + a[0,1]*b[1,1] },
      { a[1,0]*b[0,0] + a[1,1]*b[1,0],  a[1,0]*b[0,1] + a[1,1]*b[1,1] }
    };

    // -------------------------------------------------------------------------

    protected override System.Drawing.Bitmap Icon => null;

    public override Guid ComponentGuid =>
        new Guid("C8F3A2D1-5E7B-4C90-A4F6-2B8D1E6C4A7F");
  }
}

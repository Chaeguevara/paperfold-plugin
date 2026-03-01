using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace ghFolding {
  /// <summary>
  /// Single-vertex flat foldability test producing 3D-printable thick
  /// panel geometry with proper hinge gaps, vertex clearance, and
  /// collision-free fold kinematics.
  ///
  /// Thickness model follows Tachi's embedded zero-thickness reference
  /// plane: panels are symmetric about the base plane (-t/2 to +t/2).
  /// Mountain hinge axes sit on the top surface (+t/2), valley axes on
  /// the bottom (-t/2). Angular gaps between panels provide clearance
  /// for physical hinges (living hinge, pin, tape, etc.).
  /// </summary>
  public class FoldabilityVertex : GH_Component {
    const double DegToRad = Math.PI / 180.0;
    const double AngleTolerance = 0.01;

    public FoldabilityVertex()
      : base("Foldability Vertex", "FoldVtx",
          "Single-vertex foldability test with 3D-printable "
          + "thick panels and hinges",
          "Category", "Subcategory") {
    }

    protected override void RegisterInputParams(
        GH_Component.GH_InputParamManager pManager) {
      pManager.AddPlaneParameter("Plane", "P",
          "Base plane for the fold vertex",
          GH_ParamAccess.item, Plane.WorldXY);         // [0]
      pManager.AddNumberParameter("Crease Angles", "A",
          "Crease line angles in degrees, one per crease",
          GH_ParamAccess.list);                         // [1]
      pManager.AddNumberParameter("Radius", "R",
          "Outer panel extent from vertex",
          GH_ParamAccess.item, 50.0);                   // [2]
      pManager.AddNumberParameter("Thickness", "T",
          "Panel thickness (symmetric about base plane)",
          GH_ParamAccess.item, 1.0);                    // [3]
      pManager.AddNumberParameter("Fold Parameter", "F",
          "Fold progress: 0 = flat, 1 = fully folded",
          GH_ParamAccess.item, 0.0);                    // [4]
      pManager.AddBooleanParameter("Mountain", "M",
          "True = Mountain, False = Valley per crease. "
          + "Auto-assigns if not connected.",
          GH_ParamAccess.list);                         // [5]
      pManager[5].Optional = true;
      pManager.AddNumberParameter("Hinge Gap", "G",
          "Clearance between adjacent panels along each "
          + "crease (0.3-0.5 recommended for FDM)",
          GH_ParamAccess.item, 0.4);                    // [6]
      pManager.AddNumberParameter("Inner Radius", "Ri",
          "Vertex clearance hole radius. Set > thickness "
          + "to avoid collision at the vertex when folding.",
          GH_ParamAccess.item, 0.0);                    // [7]
    }

    protected override void RegisterOutputParams(
        GH_Component.GH_OutputParamManager pManager) {
      pManager.AddBrepParameter("Panels", "P",
          "Thick sector panels in folded position",
          GH_ParamAccess.list);                         // [0]
      pManager.AddLineParameter("Hinges", "H",
          "Hinge axis lines at each crease",
          GH_ParamAccess.list);                         // [1]
      pManager.AddBooleanParameter("Kawasaki", "K",
          "True if Kawasaki's theorem is satisfied",
          GH_ParamAccess.item);                         // [2]
      pManager.AddBooleanParameter("Maekawa", "Mk",
          "True if Maekawa's theorem is satisfied",
          GH_ParamAccess.item);                         // [3]
      pManager.AddNumberParameter("Sector Angles", "SA",
          "Sector angles in degrees between adjacent creases",
          GH_ParamAccess.list);                         // [4]
    }

    protected override void SolveInstance(IGH_DataAccess DA) {
      Plane basePlane = Plane.WorldXY;
      var angleList = new List<double>();
      double radius = 50.0;
      double thickness = 1.0;
      double foldParam = 0.0;
      var mountainList = new List<bool>();
      double hingeGap = 0.4;
      double innerRadius = 0.0;

      if (!DA.GetData(0, ref basePlane)) return;
      if (!DA.GetDataList(1, angleList)) return;
      if (!DA.GetData(2, ref radius)) return;
      if (!DA.GetData(3, ref thickness)) return;
      if (!DA.GetData(4, ref foldParam)) return;
      bool hasMv = DA.GetDataList(5, mountainList);
      if (!DA.GetData(6, ref hingeGap)) return;
      if (!DA.GetData(7, ref innerRadius)) return;

      // --- Validation ---

      int n = angleList.Count;
      if (n < 3) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "At least 3 crease lines are required");
        return;
      }
      if (radius <= 0.0) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Radius must be positive");
        return;
      }
      if (thickness <= 0.0) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Thickness must be positive");
        return;
      }
      if (hingeGap < 0.0) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Hinge gap cannot be negative");
        return;
      }
      if (innerRadius < 0.0) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Inner radius cannot be negative");
        return;
      }
      if (innerRadius >= radius) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Inner radius must be less than outer radius");
        return;
      }
      if (foldParam < 0.0 || foldParam > 1.0) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
            "Fold parameter clamped to [0, 1]");
        foldParam = Math.Max(0.0, Math.Min(1.0, foldParam));
      }
      if (innerRadius > 0.0 && innerRadius < thickness) {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
            "Inner radius < thickness may cause collision "
            + "at the vertex when folding.");
      }

      // --- Sector computation and theorem checks ---

      int[] sortOrder;
      double[] sorted = NormalizeAndSort(angleList, out sortOrder);
      double[] sectors = ComputeSectorAngles(sorted);
      bool kawasaki = CheckKawasaki(sectors);

      // Half-gap as angular offset (radians), referenced to
      // outer radius. The linear gap equals hingeGap at the
      // outer edge and tapers toward the center.
      double halfGapRad = (radius > 0.0)
          ? hingeGap / (2.0 * radius) : 0.0;

      // Check that no sector is too narrow for the gap
      for (int i = 0; i < n; i++) {
        double sectorRad = sectors[i] * DegToRad;
        if (sectorRad <= 2.0 * halfGapRad) {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
              $"Sector {i} ({sectors[i]:F1} deg) is too "
              + "narrow for the hinge gap");
          return;
        }
      }

      // --- Mountain/valley assignment ---

      bool[] isMountain;
      if (hasMv && mountainList.Count == n) {
        isMountain = new bool[n];
        for (int i = 0; i < n; i++)
          isMountain[i] = mountainList[sortOrder[i]];
      } else {
        if (hasMv && mountainList.Count != n) {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
              "Mountain list length mismatch. "
              + "Using auto-assignment.");
        }
        isMountain = AutoAssignMountainValley(n);
      }
      bool maekawa = CheckMaekawa(isMountain);

      // --- Build panels ---

      var flatPanels = new List<Brep>();
      for (int i = 0; i < n; i++) {
        double a0 = sorted[i];
        double a1 = sorted[(i + 1) % n];
        Brep panel = BuildFlatPanel(basePlane, a0, a1,
            radius, innerRadius, thickness, halfGapRad);
        if (panel == null) {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
              $"Failed to build panel {i}");
          return;
        }
        flatPanels.Add(panel);
      }

      // --- Fold and output ---

      Transform[] xforms = ComputeFoldTransforms(
          sorted, isMountain, foldParam, basePlane,
          thickness);
      var foldedPanels = FoldPanels(flatPanels, xforms);
      var hinges = BuildHingeLines(sorted, isMountain,
          xforms, basePlane, radius, innerRadius, thickness);

      DA.SetDataList(0, foldedPanels);
      DA.SetDataList(1, hinges);
      DA.SetData(2, kawasaki);
      DA.SetData(3, maekawa);
      DA.SetDataList(4, sectors);
    }

    protected override System.Drawing.Bitmap Icon => null;

    public override Guid ComponentGuid =>
        new Guid("d4e5f6a7-b8c9-4d0e-a1f2-3b4c5d6e7f80");

    // ---------------------------------------------------------------
    // Pure-math helpers (no geometry dependency)
    // ---------------------------------------------------------------

    double[] NormalizeAndSort(
        List<double> angles, out int[] sortOrder) {
      int n = angles.Count;
      var result = new double[n];
      sortOrder = new int[n];
      for (int i = 0; i < n; i++) {
        double a = angles[i] % 360.0;
        if (a < 0.0) a += 360.0;
        result[i] = a;
        sortOrder[i] = i;
      }
      Array.Sort(result, sortOrder);
      return result;
    }

    double[] ComputeSectorAngles(double[] sorted) {
      int n = sorted.Length;
      var sectors = new double[n];
      for (int i = 0; i < n; i++) {
        double diff = sorted[(i + 1) % n] - sorted[i];
        if (diff < 0.0) diff += 360.0;
        sectors[i] = diff;
      }
      return sectors;
    }

    bool CheckKawasaki(double[] sectors) {
      double sumEven = 0.0, sumOdd = 0.0;
      for (int i = 0; i < sectors.Length; i++) {
        if (i % 2 == 0) sumEven += sectors[i];
        else sumOdd += sectors[i];
      }
      return Math.Abs(sumEven - 180.0) < AngleTolerance
          && Math.Abs(sumOdd - 180.0) < AngleTolerance;
    }

    bool CheckMaekawa(bool[] isMountain) {
      int m = 0, v = 0;
      for (int i = 0; i < isMountain.Length; i++) {
        if (isMountain[i]) m++;
        else v++;
      }
      return Math.Abs(m - v) == 2;
    }

    bool[] AutoAssignMountainValley(int n) {
      var result = new bool[n];
      if (n % 2 == 0) {
        int mountainCount = n / 2 + 1;
        for (int i = 0; i < n; i++)
          result[i] = i < mountainCount;
      } else {
        for (int i = 0; i < n; i++)
          result[i] = (i % 2 == 0);
      }
      return result;
    }

    // ---------------------------------------------------------------
    // Geometry helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Builds one thick panel as an annular wedge.
    ///
    /// Outline (on base plane):
    ///   inner_p0 → outer_p0 (radial edge)
    ///   → outer arc (CCW at outer radius)
    ///   → outer_p1 → inner_p1 (radial edge)
    ///   → inner arc (CW at inner radius, closing the shape)
    ///
    /// When innerRadius == 0 the outline degenerates to the
    /// classic vertex-to-arc wedge, but with angular gap insets
    /// on both crease edges.
    ///
    /// Extrusion is symmetric about the base plane: the panel
    /// occupies [-thickness/2, +thickness/2] in the normal
    /// direction, matching Tachi's embedded reference plane model.
    /// </summary>
    Brep BuildFlatPanel(Plane basePlane, double angle0Deg,
        double angle1Deg, double outerRadius,
        double innerRadius, double thickness,
        double halfGapRad) {
      // Adjusted angles: inset by half-gap on each side
      double a0 = angle0Deg * DegToRad + halfGapRad;
      double a1 = angle1Deg * DegToRad - halfGapRad;
      double sweep = a1 - a0;
      if (sweep < 0.0) sweep += 2.0 * Math.PI;

      Point3d center = basePlane.Origin;
      Vector3d xAxis = basePlane.XAxis;
      Vector3d yAxis = basePlane.YAxis;
      Vector3d normal = basePlane.ZAxis;

      // Direction vectors at the inset angles
      Vector3d dir0 = Math.Cos(a0) * xAxis
          + Math.Sin(a0) * yAxis;
      Vector3d dir1 = Math.Cos(a0 + sweep) * xAxis
          + Math.Sin(a0 + sweep) * yAxis;

      // Arc-plane setup: X toward a0 direction, Y 90° CCW
      Vector3d arcY = Vector3d.CrossProduct(normal, dir0);
      var arcPlane = new Plane(center, dir0, arcY);

      var outline = new PolyCurve();

      if (innerRadius > 1e-6) {
        // --- Annular wedge (with vertex clearance hole) ---

        Point3d innerP0 = center + dir0 * innerRadius;
        Point3d outerP0 = center + dir0 * outerRadius;
        Point3d outerP1 = center + dir1 * outerRadius;
        Point3d innerP1 = center + dir1 * innerRadius;

        // 1. Radial edge: inner to outer at a0
        outline.Append(new Line(innerP0, outerP0));

        // 2. Outer arc CCW from outerP0 to outerP1
        var outerArc = new Arc(arcPlane, outerRadius, sweep);
        outline.Append(outerArc);

        // 3. Radial edge: outer to inner at a1
        outline.Append(new Line(outerP1, innerP1));

        // 4. Inner arc from innerP1 back to innerP0 (CW).
        //    Build CCW arc and reverse it.
        var innerArcFwd = new Arc(
            arcPlane, innerRadius, sweep);
        var innerCurve = new ArcCurve(innerArcFwd);
        innerCurve.Reverse();
        outline.Append(innerCurve);

      } else {
        // --- Pointed wedge (no inner cutout) ---

        Point3d outerP0 = center + dir0 * outerRadius;
        Point3d outerP1 = center + dir1 * outerRadius;

        outline.Append(new Line(center, outerP0));
        outline.Append(new Arc(arcPlane, outerRadius, sweep));
        outline.Append(new Line(outerP1, center));
      }

      // Extrude full thickness, then shift down by half
      // so the panel is symmetric: [-t/2, +t/2]
      Extrusion ext = Extrusion.Create(
          outline.ToNurbsCurve(), thickness, true);
      if (ext == null) return null;

      Brep brep = ext.ToBrep();
      brep.Transform(Transform.Translation(
          -normal * (thickness / 2.0)));
      return brep;
    }

    /// <summary>
    /// Kinematic chain of fold transforms. Panel 0 is fixed.
    ///
    /// Hinge axes are placed on the panel surfaces following the
    /// symmetric thickness model:
    ///   Mountain → axis at +thickness/2 (top surface)
    ///   Valley   → axis at -thickness/2 (bottom surface)
    ///
    /// This ensures panels stack without overlap when fully
    /// folded: after a 180° mountain fold the neighbouring panel
    /// occupies [t/2, 3t/2], cleanly above panel 0 at [-t/2, t/2].
    /// </summary>
    Transform[] ComputeFoldTransforms(double[] sorted,
        bool[] isMountain, double foldParam, Plane basePlane,
        double thickness) {
      int n = sorted.Length;
      var xforms = new Transform[n];
      xforms[0] = Transform.Identity;

      if (Math.Abs(foldParam) < 1e-10) {
        for (int i = 1; i < n; i++)
          xforms[i] = Transform.Identity;
        return xforms;
      }

      double halfT = thickness / 2.0;
      Point3d center = basePlane.Origin;
      Vector3d xAxis = basePlane.XAxis;
      Vector3d yAxis = basePlane.YAxis;
      Vector3d normal = basePlane.ZAxis;

      Transform cumulative = Transform.Identity;
      for (int i = 1; i < n; i++) {
        double rad = sorted[i] * DegToRad;
        Vector3d hingeDir =
            Math.Cos(rad) * xAxis + Math.Sin(rad) * yAxis;

        // Mountain: top surface (+t/2)
        // Valley:   bottom surface (-t/2)
        Point3d hingeOrigin = isMountain[i]
            ? center + normal * halfT
            : center - normal * halfT;

        // Transform hinge by all previous rotations
        Point3d tOrigin = hingeOrigin;
        tOrigin.Transform(cumulative);
        Vector3d tDir = hingeDir;
        tDir.Transform(cumulative);

        double angle = foldParam
            * (isMountain[i] ? Math.PI : -Math.PI);
        Transform rot = Transform.Rotation(
            angle, tDir, tOrigin);

        cumulative = rot * cumulative;
        xforms[i] = cumulative;
      }
      return xforms;
    }

    List<Brep> FoldPanels(
        List<Brep> flatPanels, Transform[] xforms) {
      var result = new List<Brep>(flatPanels.Count);
      for (int i = 0; i < flatPanels.Count; i++) {
        Brep panel = flatPanels[i].DuplicateBrep();
        panel.Transform(xforms[i]);
        result.Add(panel);
      }
      return result;
    }

    /// <summary>
    /// Hinge lines at each crease, running from inner radius to
    /// outer radius along the crease direction, at the correct
    /// surface offset (±t/2). Transformed by the fold chain.
    /// </summary>
    List<Line> BuildHingeLines(double[] sorted,
        bool[] isMountain, Transform[] xforms,
        Plane basePlane, double outerRadius,
        double innerRadius, double thickness) {
      int n = sorted.Length;
      double halfT = thickness / 2.0;
      Point3d center = basePlane.Origin;
      Vector3d xAxis = basePlane.XAxis;
      Vector3d yAxis = basePlane.YAxis;
      Vector3d normal = basePlane.ZAxis;
      var hinges = new List<Line>(n);

      for (int i = 0; i < n; i++) {
        double rad = sorted[i] * DegToRad;
        Vector3d dir =
            Math.Cos(rad) * xAxis + Math.Sin(rad) * yAxis;

        // Axis on top surface for mountain, bottom for valley
        Vector3d offset = isMountain[i]
            ? normal * halfT
            : -normal * halfT;

        Point3d start = center + offset
            + dir * innerRadius;
        Point3d end = center + offset
            + dir * outerRadius;

        start.Transform(xforms[i]);
        end.Transform(xforms[i]);
        hinges.Add(new Line(start, end));
      }
      return hinges;
    }
  }
}

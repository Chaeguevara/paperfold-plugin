using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace ghFolding
{
    /// <summary>
    /// Grasshopper component that generates a Fibonacci spiral composed of
    /// quarter-circle arcs inscribed in golden rectangles.
    ///
    /// The classic Fibonacci spiral is constructed by tiling squares whose side
    /// lengths follow the Fibonacci sequence (1, 1, 2, 3, 5, 8, 13, ...).
    /// Each square is placed adjacent to the previous combined rectangle,
    /// rotating 90 degrees counter-clockwise at every step. A quarter-circle
    /// arc is drawn inside each square, and the connected arcs approximate
    /// the golden spiral.
    ///
    /// Reference: Dale Fugier, "FibonacciSpiral.rvb", June 2009 (RhinoScript).
    /// </summary>
    public class SpiralMaker : GH_Component
    {
        public SpiralMaker()
          : base("Fibonacci Spiral", "FibSpiral",
              "Generates a Fibonacci spiral with golden rectangles",
              "Category", "Subcategory")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Steps", "N",
                "Number of Fibonacci steps. Each step adds one square and one " +
                "quarter-circle arc. Higher values produce a longer spiral but " +
                "increase the geometric scale exponentially.",
                GH_ParamAccess.item, 10);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Rectangles", "R",
                "Closed polylines representing the golden rectangles (squares) " +
                "at each Fibonacci step.",
                GH_ParamAccess.list);
            pManager.AddCurveParameter("Arcs", "A",
                "Individual quarter-circle arcs at each step. Each arc has a " +
                "radius equal to the Fibonacci number for that step.",
                GH_ParamAccess.list);
            pManager.AddCurveParameter("Spiral", "S",
                "All arcs joined into a single continuous spiral curve.",
                GH_ParamAccess.item);
        }

        /// <summary>
        /// Core solver. For each step n from 1 to N:
        ///   1. Look up F(n) from a pre-computed Fibonacci table.
        ///   2. Pick the axis pair for the current quadrant (n mod 4).
        ///   3. Build a square of side F(n) at the current origin.
        ///   4. Inscribe a quarter-circle arc inside that square.
        ///   5. Advance the origin to the opposite corner.
        /// Finally, the arcs are appended into a single PolyCurve.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int steps = 10;
            if (!DA.GetData(0, ref steps)) return;

            if (steps < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Steps must be at least 1");
                return;
            }
            if (steps > 50)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Steps clamped to 50");
                steps = 50;
            }

            // --- Fibonacci table (iterative) ---
            // We avoid Binet's closed-form (phi^n / sqrt(5)) because at large n
            // the floating-point error in Math.Pow and Math.Round can exceed 0.5,
            // producing incorrect integers. The iterative approach is O(n) and
            // yields exact values up to F(92) — the limit of a 64-bit long.
            var fib = new long[steps + 1];
            long a = 0, b = 1;
            for (int i = 1; i <= steps; i++)
            {
                fib[i] = b;
                long next = a + b;
                a = b;
                b = next;
            }

            // --- Exact axis vectors for each quadrant ---
            // The spiral rotates 90 degrees at every step. Instead of computing
            // axes with Math.Cos / Math.Sin (which introduce residuals like
            // cos(pi/2) ≈ 6.12e-17 instead of exact 0), we use a lookup table
            // of the four cardinal directions. This keeps all vertex coordinates
            // as exact integers, preventing accumulated drift in the origin.
            //
            //   n%4 == 0  →  0°    xAxis = +X,  yAxis = +Y
            //   n%4 == 1  →  90°   xAxis = +Y,  yAxis = -X
            //   n%4 == 2  →  180°  xAxis = -X,  yAxis = -Y
            //   n%4 == 3  →  270°  xAxis = -Y,  yAxis = +X
            Vector3d[] xAxes = {
                 Vector3d.XAxis,
                 Vector3d.YAxis,
                -Vector3d.XAxis,
                -Vector3d.YAxis
            };
            Vector3d[] yAxes = {
                 Vector3d.YAxis,
                -Vector3d.XAxis,
                -Vector3d.YAxis,
                 Vector3d.XAxis
            };

            Point3d origin = Point3d.Origin;

            var rectCurves = new List<Curve>();
            var arcCurves = new List<ArcCurve>();

            for (int n = 1; n <= steps; n++)
            {
                double scale = fib[n];
                Vector3d xAxis = xAxes[n % 4];
                Vector3d yAxis = yAxes[n % 4];

                // --- Golden rectangle corners ---
                //
                //  pt3 -------- pt2      Each square has side length F(n).
                //   |            |       pt0 is the current origin; the other
                //   |            |       three corners are offset along the
                //  pt0 -------- pt1      rotated x and y axes.
                //
                Point3d pt0 = origin;
                Point3d pt1 = pt0 + xAxis * scale;
                Point3d pt2 = pt1 + yAxis * scale;
                Point3d pt3 = pt0 + yAxis * scale;

                rectCurves.Add(
                    new Polyline(new[] { pt0, pt1, pt2, pt3, pt0 })
                        .ToPolylineCurve());

                // --- Quarter-circle arc ---
                // Arc(startPoint, tangentAtStart, endPoint) creates a circular
                // arc from pt0 to pt2 whose tangent at pt0 points along xAxis.
                // This produces a 90-degree arc with radius = F(n), inscribed
                // in the square's diagonal from pt0 to pt2.
                arcCurves.Add(new ArcCurve(new Arc(pt0, xAxis, pt2)));

                // Advance origin to pt2 so the next square attaches here
                origin = pt2;
            }

            DA.SetDataList(0, rectCurves);
            DA.SetDataList(1, arcCurves);

            // --- Join arcs into a single spiral ---
            // We build a PolyCurve by appending arcs sequentially instead of
            // using Curve.JoinCurves. JoinCurves relies on a distance tolerance
            // (typically 0.001) to decide whether endpoints match. At high step
            // counts the arc radii reach billions of units, and tiny internal
            // floating-point differences in the Arc representation can exceed
            // that tolerance, causing the join to break at around step 47.
            // Sequential appending avoids this because it does not perform a
            // tolerance check — it trusts the caller-supplied order.
            var spiral = new PolyCurve();
            foreach (var arc in arcCurves)
                spiral.Append(arc);

            DA.SetData(2, spiral);
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("5181B1F5-E8F5-446B-9D8F-F5543FE772D7");
    }
}

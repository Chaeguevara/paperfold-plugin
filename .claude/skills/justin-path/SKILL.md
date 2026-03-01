---
name: justin-path
description: Reference for implementing Justin Path flat-foldability analysis in Grasshopper components. Use when adding components that compute crease pattern validity (flat-foldability), reflection matrix products, multi-vertex graph traversal, or annular surface extraction (inner/outer boundary ribbons). Covers the Justin theorem, 2×2 reflection matrix math, C# implementation patterns, and GH_Component input/output conventions for origami crease analysis.
---

# Justin Path — Flat-Foldability Reference

**Justin's theorem**: A crease pattern is globally flat-foldable iff the ordered
product of 2D reflection matrices along any closed boundary path equals I₂.

---

## Mathematics

### Reflection Matrix

For a crease at angle θ (radians from +x axis):

```
R(θ) = ┌  cos(2θ)   sin(2θ) ┐
        └  sin(2θ)  -cos(2θ) ┘
```

### Flat-Foldability Condition

Traverse all N creases in angular order (CCW) around a vertex or closed path.
The pattern is flat-foldable iff:

```
R(θ_N) · R(θ_{N-1}) · ... · R(θ_1) = I₂
```

This generalises Kawasaki's theorem (alternating angle sums = 180°) to multi-vertex graphs.

### Multi-Vertex Symmetric Graph (2-vertex canonical form)

Used by this plugin. Place vertices vL and vR at (cx − d, cy) and (cx + d, cy):

```
vR rays: 0°,     +θ,    360°−θ
vL rays: 180°−θ, 180°,  180°+θ
```

For all θ ∈ (0°, 90°) the 6-crease product = I₂ (globally flat-foldable).

### Annular Surface Extraction

Cut an annular ribbon (inner radius r_in, outer radius r_out, both centred at
midpoint of the spine). Both the inner boundary path and the outer boundary path
independently satisfy the Justin condition — the ribbon folds flat as a coherent
kinematic ring.

---

## C# Patterns

### 2×2 Matrix Helpers

```csharp
// Row-major [row, col]
double[,] Identity2x2() => new double[,] { { 1, 0 }, { 0, 1 } };

double[,] ReflectionMatrix(double thetaRad) {
  double c = Math.Cos(2 * thetaRad);
  double s = Math.Sin(2 * thetaRad);
  return new double[,] { { c, s }, { s, -c } };
}

double[,] Multiply2x2(double[,] a, double[,] b) => new double[,] {
  { a[0,0]*b[0,0] + a[0,1]*b[1,0], a[0,0]*b[0,1] + a[0,1]*b[1,1] },
  { a[1,0]*b[0,0] + a[1,1]*b[1,0], a[1,0]*b[0,1] + a[1,1]*b[1,1] }
};

bool IsIdentity(double[,] m, double tol = 1e-10) =>
    Math.Abs(m[0,0] - 1) < tol && Math.Abs(m[1,1] - 1) < tol &&
    Math.Abs(m[0,1])     < tol && Math.Abs(m[1,0])     < tol;

// Frobenius distance from I₂ — useful as a continuous "foldability error" output
double FrobeniusError(double[,] m) =>
    Math.Sqrt(Math.Pow(m[0,0]-1,2) + Math.Pow(m[1,1]-1,2) +
              m[0,1]*m[0,1] + m[1,0]*m[1,0]);
```

### Crease Ray — Canonical Struct

```csharp
struct CreaseRay {
  public Point2d Origin;  // vertex position in the XY plane
  public double AngleDeg; // angle from +x, degrees [0, 360)
}
```

### Traversal and Validation

```csharp
// Sort rays by angle then accumulate product (left-to-right = CCW)
var M = Identity2x2();
foreach (var ray in rays.OrderBy(r => r.AngleDeg)) {
  var R = ReflectionMatrix(ray.AngleDeg * Math.PI / 180.0);
  M = Multiply2x2(R, M);
}

if (!IsIdentity(M)) {
  AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
      "Crease pattern is not flat-foldable (product ≠ I₂)");
}
```

### Ray–Circle Intersection (for annular boundary points)

```csharp
// Returns the forward intersection of a ray with a circle centred at (cx, cy).
// Returns Point2d.Unset if the ray does not reach the circle.
Point2d RayCircleIntersect(
    double ox, double oy, double angleDeg,
    double cx, double cy, double radius) {
  double rad = angleDeg * Math.PI / 180.0;
  double dx = Math.Cos(rad), dy = Math.Sin(rad);
  double fx = ox - cx, fy = oy - cy;
  // a = 1 because dx²+dy² = 1
  double b = 2 * (fx*dx + fy*dy);
  double c = fx*fx + fy*fy - radius*radius;
  double disc = b*b - 4*c;
  if (disc < 0) return Point2d.Unset;
  double t = (-b + Math.Sqrt(disc)) / 2.0;
  return t > 0 ? new Point2d(ox + t*dx, oy + t*dy) : Point2d.Unset;
}
```

### Annular Crease Segment (line from inner to outer boundary)

```csharp
// Lift 2D intersection points to 3D world XY plane, return as LineCurve.
LineCurve CreaseSegment(CreaseRay ray, Point2d inner, Point2d outer) {
  var a = new Point3d(inner.X, inner.Y, 0);
  var b = new Point3d(outer.X, outer.Y, 0);
  return new LineCurve(a, b);
}
```

---

## Bundled References

Load these files when you need deeper context:

- **[references/math.md](references/math.md)** — Read when you need the formal theorem
  statements (Kawasaki, Justin, Maekawa), a proof sketch for why the 2-vertex symmetric
  graph is always foldable, or the argument for why both annular boundaries satisfy Justin.

- **[references/complete-example.md](references/complete-example.md)** — Read when
  implementing a new Justin Path component from scratch. Contains a full, compilable
  `JustinPathAnalyzer.cs` with all helpers, plus a table of key design decisions and
  common variations (N-vertex generalisation, mountain/valley output, 3D placement).

---

## Component Design

### Typical Inputs

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `Theta` | `double` | 45 | Symmetry angle (degrees). Clamped [20, 80]. |
| `VertexDist` | `double` | 1.0 | Half-distance between the two vertices. |
| `InnerRadius` | `double` | 1.5 | Must be > VertexDist. |
| `OuterRadius` | `double` | 3.0 | Must be > InnerRadius. |

### Typical Outputs

| Name | Type | Notes |
|------|------|-------|
| `IsFoldable` | `bool` | True iff product = I₂ within 1e-10. |
| `MatrixError` | `double` | Frobenius distance from I₂. |
| `CreaseSegments` | `Curve[]` | Lines between inner and outer circles. |
| `InnerBoundary` | `Curve` | Circle at InnerRadius. |
| `OuterBoundary` | `Curve` | Circle at OuterRadius. |

### Validation Guards

```csharp
if (theta < 0 || theta > 90) {
  AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Theta must be in [0, 90]°");
  return;
}
if (innerRadius <= vertexDist) {
  AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
      "Inner radius should exceed vertex separation to avoid overlap");
}
if (outerRadius <= innerRadius) {
  AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
      "OuterRadius must be greater than InnerRadius");
  return;
}
```

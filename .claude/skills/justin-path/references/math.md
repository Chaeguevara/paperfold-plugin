# Justin Path — Mathematical Background

## Table of Contents
1. Kawasaki's Theorem (single-vertex form)
2. Justin's Theorem (matrix / closed-path form)
3. Why the 2-vertex symmetric graph is always flat-foldable
4. Maekawa's Theorem — mountain/valley parity
5. Annular surface: why both boundaries independently satisfy Justin

---

## 1. Kawasaki's Theorem (single-vertex form)

For a single interior vertex with 2N creases, the crease pattern is flat-foldable iff
alternating sector angles sum to π:

```
α₁ + α₃ + α₅ + ... + α_{2N-1} = π
α₂ + α₄ + α₆ + ... + α_{2N}   = π
```

Where αᵢ is the sector angle between crease i and crease i+1 (measured CCW).
This is a necessary and sufficient condition for a single interior vertex with
an even number of creases.

---

## 2. Justin's Theorem (matrix / closed-path form)

Justin generalises Kawasaki to arbitrary multi-vertex crease graphs.

**Statement**: A planar crease graph is flat-foldable iff for every closed path γ
through the graph, the ordered product of 2D reflection matrices across each
crease intersection along γ equals the 2×2 identity:

```
∏ R(θᵢ) = I₂
```

Where R(θ) for a crease line at angle θ from +x is:

```
R(θ) = ┌  cos(2θ)   sin(2θ) ┐
        └  sin(2θ)  -cos(2θ) ┘
```

Properties of R(θ):
- det(R) = −1   (it is an improper rotation — a reflection)
- R(θ)² = I₂    (applying the same reflection twice returns to identity)
- R(θ)ᵀ = R(θ)  (it is its own inverse)

The product of an even number of distinct reflections can equal I₂; for
the product of an odd number, det = −1 ≠ 1, so odd crease counts at a
vertex are impossible for flat-foldability.

**Relation to Kawasaki**: For a single vertex, the Justin matrix condition
is exactly equivalent to Kawasaki's angle-sum rule. The matrix formulation
is preferred for multi-vertex graphs because it composes naturally.

---

## 3. Why the 2-vertex symmetric graph is always flat-foldable

The canonical 2-vertex graph used in this plugin has rays:

```
vR (right vertex at +d): angles  0°,        +θ,     360°−θ
vL (left vertex at −d):  angles  180°−θ,    180°,   180°+θ
```

Traversing the six creases in CCW angular order:

```
R(0) · R(θ) · R(180°−θ) · R(180°) · R(180°+θ) · R(360°−θ)
```

Proof sketch:
- R(0) and R(180°) are reflections across x-axis and y-axis respectively;
  their product is a 180° rotation.
- The pairs (θ, 180°−θ) and (180°+θ, 360°−θ) are symmetric about 90° and 270°;
  each pair contributes R(θ)·R(180°−θ) = rotation by 2(180°−2θ).
- All rotational contributions cancel, yielding I₂ for all θ ∈ (0°, 90°).

**Consequence**: the symmetry angle θ is a free parameter. Any value in (0°, 90°)
produces a valid flat-foldable graph — no constraint needs to be enforced beyond
clamping θ away from the degenerate endpoints 0° and 90°.

---

## 4. Maekawa's Theorem — Mountain/Valley Parity

At every interior vertex, the number of mountain folds M and valley folds V must
satisfy:

```
|M − V| = 2
```

In the 2-vertex graph (3 creases per vertex):
- Valid assignments: (M=2, V=1) or (M=1, V=2) per vertex.
- The two vertices may have independent assignments.
- Global consistency: shared edges (the spine) must be assigned the same
  mountain/valley value from both vertices' perspectives.

When implementing a component that outputs fold assignments, enumerate the
2×2 = 4 combinations and filter by spine consistency.

---

## 5. Annular Surface: Why Both Boundaries Satisfy Justin

Given a ribbon extracted at inner radius r_in and outer radius r_out (both
centred at the spine midpoint):

**Inner boundary γ_in**: a closed loop that passes through the 6 crease lines
at their intersections with the inner circle. The traversal order is the same
CCW angular sequence as the full graph. Therefore the product equals I₂ by
the same argument as §3.

**Outer boundary γ_out**: similarly passes through all 6 creases at their outer
circle intersections. The angular order and vertex origins are identical; only
the radial distances change. Because reflection matrices depend only on crease
angle (not position along the crease), the product is again I₂.

**Physical implication**: the annular ribbon is a kinematically independent
folding unit. When cut free from the parent sheet, it retains full flat-foldability.
The inner and outer circular cuts are "Justin paths" in the sense that traversing
either one encounters an identity product — no net rotation or shear is introduced
by folding.

**Numerical note**: in the discrete simulation the ribbon folds as a rigid ring.
All 6 crease segments (between r_in and r_out) fold simultaneously; no segment
is redundant. This is why all 6 must intersect both circles — segments that miss
the inner circle (because the vertex is inside r_in) must be handled separately
as "spine-straddling" creases.

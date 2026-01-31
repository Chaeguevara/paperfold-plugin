---
name: review
description: Code review checklist for ghFolding Grasshopper components.
disable-model-invocation: true
---

# Code Review Checklist

Run through this checklist before committing changes to the ghFolding plugin.

---

## Build

- [ ] `dotnet build` passes with **0 errors, 0 warnings** on all target
      frameworks (net48, net7.0, net7.0-windows).

## Component Structure

- [ ] One `GH_Component` per file. File name matches class name.
- [ ] `ComponentGuid` is unique and has not been changed from a previous release.
- [ ] Constructor category/subcategory matches sibling components.
- [ ] All inputs have descriptions and sensible defaults.
- [ ] All outputs have descriptions and correct `GH_ParamAccess`.

## Input Validation

- [ ] Every `DA.GetData` / `DA.GetDataList` failure causes an early `return`.
- [ ] Invalid ranges produce `AddRuntimeMessage(Error, ...)` and `return`.
- [ ] Edge cases handled: zero, negative, very large values.

## Numerical Precision

- [ ] Integer sequences computed iteratively (not via floating-point formulas).
- [ ] Cardinal directions use exact vectors, not `Math.Cos`/`Math.Sin`.
- [ ] Curves joined with `PolyCurve.Append` when order is known.

## Style (run `/style` for full reference)

- [ ] 2-space indentation, no tabs.
- [ ] Opening brace on same line as statement.
- [ ] Max 100 columns per line.
- [ ] `PascalCase` public, `_camelCase` private fields, `camelCase` locals.
- [ ] `var` only when type is obvious from RHS.
- [ ] No unused `using` directives.

## Documentation

- [ ] Class-level `<summary>` explains what the component does.
- [ ] `SolveInstance` has a `<summary>` describing the algorithm steps.
- [ ] Non-obvious choices have inline comments explaining *why*.
- [ ] Parameter descriptions are meaningful.

## General

- [ ] No secrets, credentials, or absolute paths in committed code.
- [ ] No leftover `Console.WriteLine` or debug output.
- [ ] Changes are minimal -- no unrelated reformatting or refactoring.

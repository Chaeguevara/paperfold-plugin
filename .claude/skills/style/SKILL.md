---
name: style
description: Google C# style conventions for this project. Use when writing or reviewing C# code to ensure naming, formatting, and organization follow the project standard.
---

# Google C# Style Guide -- Project Reference

Source: https://google.github.io/styleguide/csharp-style.html

Apply these rules to all C# files in this repository.

---

## Naming

| Symbol | Convention | Example |
|--------|-----------|---------|
| Class, struct, enum, method, property, event, namespace | `PascalCase` | `SpiralMaker`, `SolveInstance` |
| Interface | `I` + `PascalCase` | `ISerializable` |
| Local variable, parameter | `camelCase` | `arcCurves`, `steps` |
| Private / protected / internal field | `_camelCase` | `_origin` |
| Constant, static readonly | `PascalCase` | `MaxSteps` |
| Type parameter | `T` + `PascalCase` | `TResult` |
| Enum member | `PascalCase` | `GH_ParamAccess.item` |

- Acronyms are words: `GetHttpResponse`, not `GetHTTPResponse`.
- `const`, `static`, `readonly` modifiers do not change the convention.

## Formatting

### Indentation & Spacing
- **2 spaces** per indent level. No tabs.
- Max **100 columns** per line.
- One statement per line. One assignment per statement.
- Space after `if`/`for`/`while`/`switch` and after commas.
- No space after `(` or before `)`.
- One space around binary operators: `a + b`, `x == y`.
- No space between unary operator and operand: `!flag`, `-value`.

### Braces
- Opening brace on **same line** as the statement:
  ```csharp
  if (steps < 1) {
    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Steps must be at least 1");
    return;
  }
  ```
- Closing brace on its own line.
- `} else {` -- no line break between closing brace and `else`.
- Always use braces, even for single-line bodies.

### Line Wrapping
- Continuation lines indented **4 spaces** from the original.
- If wrapping method arguments, align with first argument or use 4-space indent.
- Brace-enclosed blocks (initializers, lambdas) do not count as continuations.

## File Organization

### Usings
- `using` directives go at the top, **before** the namespace.
- `System` imports first, then alphabetical.

### Member Order (within a class)
1. Nested types (classes, enums, delegates)
2. `static`, `const`, `readonly` fields
3. Fields and properties
4. Constructors and finalizers
5. Methods

### Access Level Order (within each group)
`public` > `internal` > `protected internal` > `protected` > `private`

### Modifier Order
```
public protected internal private new abstract virtual override
sealed static readonly extern unsafe volatile async
```

## Language Features

### `var`
- Use when the type is obvious: `var list = new List<Curve>();`
- Avoid with primitives or when the type is not clear from RHS.

### Properties
- Single-line read-only: expression body `=>`.
  ```csharp
  public override Guid ComponentGuid => new Guid("...");
  ```
- Multi-line or read-write: `{ get; set; }` block.

### Expression-Bodied Members
- Use for properties and simple lambdas.
- Do **not** use for method definitions.

### Constants
- Prefer `const` when possible; fall back to `static readonly`.
- Name magic numbers: `const int MaxSteps = 50;`

### Collections
- Prefer `List<T>` for public APIs and variable-length data.
- Use arrays for fixed-size or multidimensional data.

### Strings
- Use interpolation for readability: `$"Step {n}"`.
- Use `StringBuilder` for loops with many concatenations.

### LINQ
- Prefer single-call chains over long pipelines.
- Use extension-method syntax (`Where`, `Select`), not SQL keywords.

### Null Handling
- Use `?.Invoke()` for delegate calls.
- Prefer `??` over explicit null checks where appropriate.

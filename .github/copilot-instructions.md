# Copilot Instructions 

## General

- Always ask clarifying questions
- Use established best practases for public NuGet packages
- Always review documentation for inconsitencies or errors

---

##  Coding Style

- Always follow the .editorconfig unless expressly told not to
- Use explicit types instead of `var` unless the type is obvious from the right-hand side.
- Retain trailing commas in multi-line collections and object initializers.
- Order methods, properties, and variables **alphabetically** within their scope.
- Do not prefix local variables with underscores.
- Do not use `#region` directives or use other regional segmentation schemes. 
- Do not use banner comment, comment dividers, section dividers, or box comment headers. 
- All classes, methods, enums, members, and properties should have `<summary>` documentation
-  `<summary>` and `<summary />` tags must always be placed on their own lines. 

---

## Unit Testing

- Framework: **MSTest SDK** (`[TestClass]`, `[TestMethod]`).
- Assertions: **AwesomeAssertions** — use fluent `.Should()` syntax.
  - `Should()`, `NotBeNull()`, `Be()`, etc. are separate chained calls.
  - For date/time assertions use `BeCloseTo()` with an appropriate precision.
- Mocking: **NSubstitute** — only mock interfaces and abstract classes.
- Test method naming: `MethodName_Scenario_ExpectedBehavior`.
- Aim for 100% coverage: happy paths, edge cases, error paths, boundary conditions, and all meaningful branches.
- Shared test configuration goes in `MSTestSettings.cs`.
- When a test fails, carefully evaluate whether the failure indicates a bug in the production code or an error in the test itself. Default to assuming the production code contains a bug and alert the developer — only conclude the test is wrong if there is clear evidence (e.g., incorrect assertion value, wrong mock setup, test targeting the wrong method).

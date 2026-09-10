# Cntryl.Conventions

Roslyn analyzers and code fixes that enforce the team's test conventions.

| Rule | Enforces |
|---|---|
| `CNTRYL0001` | Test names read as a specification: `Should<Outcome>Given<State>When<Action>` |
| `CNTRYL0002` | Test bodies carry ordered `// Arrange`, `// Act`, `// Assert` comments |

Both rules detect xUnit, NUnit and MSTest, and stay inert in projects that reference none of them.

## Install

Both packages are published to GitHub Packages, not nuget.org. Add the `cntryl` source to
your `NuGet.Config`:

```xml
<packageSources>
  <add key="cntryl" value="https://nuget.pkg.github.com/cntryl/index.json" />
</packageSources>
<packageSourceMapping>
  <packageSource key="cntryl">
    <package pattern="Cntryl.*" />
  </packageSource>
</packageSourceMapping>
```

Then reference them from the test project only:

```
dotnet add package Cntryl.Conventions.Analyzers
dotnet add package Cntryl.Conventions.CodeFixes
```

Both are development dependencies; neither adds a runtime reference.

## Configure

`CNTRYL0001` requires a `Given` clause and treats `When` as optional. Both are configurable
per directory in `.editorconfig`:

```ini
[test/**.cs]
conventions_test_naming_require_given = true
conventions_test_naming_require_when  = false

dotnet_diagnostic.CNTRYL0001.severity = warning
dotnet_diagnostic.CNTRYL0002.severity = warning
```

Adopting on an existing suite: start both rules at `suggestion`, then raise them to
`warning` (or `error`) per directory or per file as each is migrated.

## Code fixes

`CNTRYL0002` inserts the three phase comments, placing `// Assert` at the first asserting
statement and `// Act` at the statement before it. When those cannot be told apart — no
assertion found, or fewer than three statements — no fix is offered rather than guessing.

`CNTRYL0001` can only rename a third-person name to its `Should` form
(`AcceptsValidRoute` → `ShouldAcceptValidRoute`), via `Renamer` so references update. A
missing `Given` or `When` clause states intent that is absent from the source, so no fix is
offered for it.

# Cntryl Conventions .NET

Roslyn analyzers and code fixes for consistent C# formatting and test structure.
The packages run in the compiler and IDE and add no application runtime dependency.
Build this repository with the .NET 11 SDK or later. The compiler and IDE must
host Roslyn 5.12 or later.

## Install

Packages are available from the Cntryl GitHub Packages feed. Configure
[GitHub Packages authentication](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
with a classic personal access token that has `read:packages`.

```sh
dotnet nuget add source https://nuget.pkg.github.com/cntryl/index.json --name cntryl
```

For general C# formatting rules:

```sh
dotnet add package Cntryl.Conventions.Analyzers
dotnet add package Cntryl.Conventions.CodeFixes
```

For test naming and structure, add these packages to test projects:

```sh
dotnet add package Cntryl.Conventions.Testing.Analyzers
dotnet add package Cntryl.Conventions.Testing.CodeFixes
```

The testing rules detect xUnit, NUnit, and MSTest.

## Rules

| Rule | Enforces |
| --- | --- |
| `CNTRYL0001` | Test names in the form `Should<Outcome>Given<State>When<Action>`, such as `ShouldRejectOrderGivenInsufficientStock`. |
| `CNTRYL0002` | Ordered `// Arrange`, `// Act`, and `// Assert` comments. |
| `CNTRYL1001` | Consistent formatting across a wrapped declaration parameter list. |
| `CNTRYL1002` | Blank lines between control-flow blocks and surrounding statements. |

The general fixers support Fix All. The testing fixers update references when
renaming a test and preserve indentation and line endings when inserting phase
comments. They offer a fix where they can identify the change from the source.

## Configure

`CNTRYL0001` requires `Given` by default and treats `When` as optional. Set
naming requirements and rule severity per directory in `.editorconfig`.

```ini
[test/**.cs]
conventions_test_naming_require_given = true
conventions_test_naming_require_when = false
dotnet_diagnostic.CNTRYL0001.severity = warning
dotnet_diagnostic.CNTRYL0002.severity = warning
```

Use `suggestion` for gradual adoption in an existing suite.

## Development

```sh
dotnet test --solution Conventions.slnx -c Release
```

See [Contributing](CONTRIBUTING.md) for repository conventions and the
[changelog](CHANGELOG.md) for rule changes. The
[benchmark project](benchmark/Conventions.Analyzers.Benchmarks) measures time and
allocations on representative test methods.

Cntryl Conventions is licensed under [Apache 2.0](LICENSE).

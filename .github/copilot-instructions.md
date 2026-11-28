# Copilot instructions

## Build, test, and lint

- Use the .NET 11 SDK or later. Dependencies are centrally versioned in `Directory.Packages.props`, and `global.json` selects Microsoft.Testing.Platform for test commands. The analyzer packages require a compiler/IDE with Roslyn 5.12 or later.
- Restore and validate from the repository root:

  ```shell
  dotnet restore Conventions.slnx
  dotnet format Conventions.slnx --verify-no-changes --no-restore --severity warn
  dotnet build Conventions.slnx -c Release --no-restore
  dotnet test --solution Conventions.slnx -c Release --no-build --no-restore
  ```

- Run one test project with `dotnet test --project <path>`, not the VSTest `--filter` form. `global.json` opts into Microsoft.Testing.Platform and the tests use xUnit v3; pass xUnit arguments after `--`. For example:

  ```shell
  dotnet test --project test/Conventions.Analyzers.Tests/Conventions.Analyzers.Tests.csproj \
    -c Release -- -method "Cntryl.Conventions.Analyzers.Tests.ClassName.MethodName"
  ```

  Replace `-method` with `-class`, `-namespace`, or `-filter` as needed. Build first when source has changed (omit `--no-build`).
- Run analyzer performance checks with:

  ```shell
  dotnet run --project benchmark/Conventions.Analyzers.Benchmarks/Conventions.Analyzers.Benchmarks.csproj -c Release -- --filter '*'
  ```

## Architecture

- `Conventions.slnx` separates production analyzers, tests, and benchmarks. `Conventions.Analyzers` owns general C# rules (`CNTRYL1001` and `CNTRYL1002`); `Conventions.Testing.Analyzers` owns test-framework rules (`CNTRYL0001` and `CNTRYL0002`).
- Testing analyzers recognize xUnit, NUnit, and MSTest through compilation references and remain inert when the inspected project has none of those frameworks. Code fixes live in the separate `Conventions.Testing.CodeFixes` package/project because they require Roslyn Workspaces; keep analyzer-only packages free of Workspaces dependencies.
- The two test projects reference analyzer projects with `OutputItemType="Analyzer"` so the repository exercises its rules while testing their behavior. The testing test project additionally references the code-fix assembly.
- `src/Shared/TestVerbInflection.cs` is deliberately compiled into both the testing analyzer and testing code-fix assemblies. Preserve that linkage when changing the inflection behavior; the assemblies are distributed separately.
- The benchmark project references both analyzer assemblies and measures the general, testing, and combined analyzer paths.
- Shared compiler settings are inherited from `Directory.Build.props`; package versions are centralized in `Directory.Packages.props`. `Directory.Build.targets` builds analyzer project references during design-time builds and validates package metadata during packing. Test/package/build behavior in CI is defined by `.github/workflows/ci.yml` and `.github/workflows/packages.yml`.

## Analyzer and code-fix conventions

- Keep diagnostic definitions in each analyzer project's `ConventionDiagnostics` class and consume those descriptors from analyzer classes rather than constructing duplicate metadata. Diagnostic IDs, titles, categories, and defaults are part of the public behavior.
- When adding or changing a diagnostic, update the corresponding `AnalyzerReleases.Unshipped.md` table in the analyzer package. Package releases are tracked separately; testing analyzer and testing code-fix release files are distinct.
- Register the narrowest Roslyn action required. General formatting analysis belongs in `Conventions.Analyzers`; rules that depend on a test framework belong in `Conventions.Testing.Analyzers`.
- Keep code fixes conservative: offer a fix only when the source supplies enough intent to make a deterministic edit. Use `Renamer` for test renames so references are updated, preserve source trivia/indentation/line endings, account for member-name conflicts in Fix All, and avoid guessing Arrange/Act/Assert boundaries.
- Test naming is configurable with `conventions_test_naming_require_given` and `conventions_test_naming_require_when`. Keep option reads and defaults consistent with the behavior documented in `README.md`.
- Wrapped declaration parameter lists and their continuation indentation are covered by `CNTRYL1001`: wrap the complete signature or constructor parameter list atomically rather than partially wrapping it.
- The repository enables nullable annotations, implicit usings, latest .NET analyzer checks, code-style enforcement, and warnings-as-errors through shared build properties. Formatting or analyzer warnings must be resolved rather than suppressed broadly.

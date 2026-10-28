# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1]

### Changed

- `CNTRYL0002` now runs as a symbol action and reads the method body through its declaring
  syntax, so it no longer forces a semantic model for every method declaration. Measured on a
  20k-method project, its compile-time cost fell from 0.31 s to 0.04 s. Behaviour is unchanged.
- Phase-marker scanning stops once `// Arrange`, `// Act` and `// Assert` have all been found.

## [1.0.0]

### Added

- `CNTRYL0001` reports test methods that do not read as `Should<Outcome>Given<State>When<Action>`,
  with `Given` required and `When` optional by default. Both clauses are configurable per
  directory through `conventions_test_naming_require_given` and `conventions_test_naming_require_when`.
- `CNTRYL0002` reports test method bodies that do not carry ordered `// Arrange`, `// Act` and
  `// Assert` comments. Markers may carry trailing prose; expression-bodied tests are exempt.
- A code fix for `CNTRYL0002` that inserts the three phase comments, placing `// Assert` at the
  first asserting statement and `// Act` at the statement before it.
- A code fix for `CNTRYL0001` that renames a third-person test name to its `Should` form.
- xUnit, NUnit and MSTest attribute detection. Both analyzers stay inert in projects that
  reference no supported test framework.

[Unreleased]: https://github.com/cntryl/conventions-dotnet/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/cntryl/conventions-dotnet/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/cntryl/conventions-dotnet/releases/tag/v1.0.0

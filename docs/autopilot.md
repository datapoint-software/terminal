# Autopilot notes

Decisions taken while the product owner was away, for review. Each one either follows the documented specification, or works around something the specification didn't foresee; the second kind is marked **Deviation**. Settled points are also recorded where they belong (product.md, engineering.md); this file is the list to review, newest last.

| # | Date | Decision | Why | Kind |
| --- | --- | --- | --- | --- |
| 1 | 2026-11-06 | Tests reference `xunit.v3.mtp-v2` 3.2.2 rather than `xunit.v3` 3.2.2. | `xunit.v3` 3.2.x brings Microsoft.Testing.Platform 1, which crashes with the current code coverage extension (built for version 2); the `mtp-v2` package is the same xUnit on version 2. Recorded in engineering.md. | Deviation |
| 2 | 2026-11-06 | Test projects are created with their first tests, not all up front. | Microsoft.Testing.Platform fails a run when a test project has no tests (exit code 8). Recorded in engineering.md. | Clarification |
| 3 | 2026-11-06 | `.editorconfig` asks for UTF-8 without a byte order mark, overriding the dotnet/docs base. | No tool we use needs one, and they show up as noise in diffs. Marked with a comment in the file. | Clarification |
| 4 | 2026-11-06 | The third-party notices generator (`tools/`) is postponed until the first release build; until then each package's license is checked by hand against the allowlist when it's added. | Building it now would delay the core work, and nothing is distributed yet. Packages so far: Avalonia, CommunityToolkit.Mvvm, Microsoft.Extensions.* (MIT), BouncyCastle (MIT), Porta.Pty (MIT), xUnit (Apache-2.0), analyzers (MIT/Apache-2.0, not distributed). | Deviation |

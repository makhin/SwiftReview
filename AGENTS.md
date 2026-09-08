# Repository Agent Instructions

Before writing, reviewing, or refactoring code, read and follow the repository-local
`karpathy-guidelines` skill at `skills/karpathy-guidelines/SKILL.md`.

For server-backed DevExtreme grids, keep data loading in `CustomStore` backed by
`DevExtreme.AspNet.Data`; do not use TanStack Query for grid load operations.

After modifying code, run the relevant automated tests before finishing. Changes
spanning backend and frontend require both test suites. Explicitly report failed
or skipped tests; do not claim completion while relevant tests are failing.

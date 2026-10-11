# Contributing to HIDGuard

Thank you for contributing to HIDGuard. This document outlines the engineering guidelines, branching strategy, commit conventions, and pull request standards required for all contributions.

## Branching Strategy

All work must originate from an up-to-date `main` branch. Direct commits to `main` are strictly forbidden.

Branch names must follow the format `<type>/<scope>`:

- `feat/<scope>`: New features or capabilities
- `fix/<scope>`: Bug fixes
- `chore/<scope>`: Maintenance, build, or configuration tasks
- `test/<scope>`: Adding or updating test suites
- `docs/<scope>`: Documentation updates
- `ci/<scope>`: Continuous integration workflow adjustments

Examples:
- `feat/timing-engine`
- `fix/device-listener-handle`
- `chore/repo-foundation`

## Commit Message Guidelines

All commit messages within a branch must follow the Conventional Commits specification:

Format: `type(scope): imperative summary`

Rules:
- The summary must start in lowercase immediately following the colon and space.
- The summary must be written in the imperative mood (e.g., `add`, `fix`, `update`, not `added`, `fixes`, `updating`).
- The entire header line must stay under 72 characters.
- Do not append a trailing period.
- Do not include emojis or decorative unicode.

Examples:
- `feat(core): add inter-key timing analysis engine`
- `fix(platform): release device handle on unplug notification`
- `test(core): add edge case tests for interval calculation`

## Pull Request Workflow

1. One stage or cohesive unit of work per pull request. Keep changes focused and reviewable.
2. The pull request title must follow the exact same Conventional Commits format as commit headers, because squash-and-merge is enforced.
3. Every pull request description must strictly follow the standard structure:
   - Summary: Two to four concise sentences stating what was changed and why.
   - Changes: Bulleted list of specific additions and modifications.
   - Testing: Exact commands and scenarios run, along with outcomes.
   - Notes: Assumptions made, trade-offs, constraints, or next steps.
4. Pull requests will only be merged when:
   - Solution builds with zero warnings (`TreatWarningsAsErrors` enabled).
   - All xUnit test suites pass.
   - GitHub Actions CI workflow succeeds.
   - The diff contains zero comments and zero emojis.
   - No unrelated files or unintended modifications exist.

## Code and Engineering Standards

- Language and Framework: C# 12 on .NET 8.
- Nullable Reference Types: Enabled throughout the entire solution.
- Warnings: `TreatWarningsAsErrors` is set to `true`. Roslyn analyzers are enforced at `latest-recommended`.
- Zero Comments Rule: Code must not contain comments of any kind (`//`, `/* */`, `///` XML doc comments, `#region`, `#endregion`, `TODO`, `FIXME`). Code intent must be expressed solely through self-documenting naming, strong types, and small focused methods.
- No Emojis: Emojis and decorative unicode characters are strictly forbidden in code, commit messages, PRs, logs, user interfaces, and documentation.
- Architecture: Maintain strict separation between `HidGuard.Core` and platform-specific implementations. The core domain library must never reference Win32 APIs or platform assemblies.
- Resource Safety: Wrap native handles using `SafeHandle` derivatives. Check all Win32 return codes and surface errors as typed exceptions.
- Privacy and Security: Keystroke content is never logged during normal operations. Only timing and device metadata are processed. Network communication and telemetry are strictly forbidden.
- Testing: Write deterministic unit tests using xUnit and Shouldly. Avoid static mutable state.

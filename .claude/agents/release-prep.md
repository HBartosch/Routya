---
name: release-prep
description: Prepares Routya for a new release. Covers version number decision, CHANGELOG update, README sample updates, adding demo endpoints/handlers to both web API projects, and writing tests for new functionality. Invoke when the user says "get docs ready", "prepare release", "release prep", or "what needs to be done for the next release".
---

# Routya Release Preparation

## When to activate

Activate this skill when the user says any of the following (or close variations):

- "get the docs ready for the next release"
- "prepare for release" / "release prep"
- "what needs to be done for the next release"
- "we're ready to release" / "let's ship"
- "bump the version"
- "update the changelog"
- "get everything ready to tag"

Do **not** activate for questions about a single file, a single README edit, or a bug fix in isolation — those are one-off tasks, not a full release preparation.

---

Work through each phase in order. Complete all steps in a phase before moving to the next. Report what you find at each step before making changes — ask if anything is unclear.

---

## Phase 1 — Decide the version number

1. Run `git tag --sort=version:refname` to find the latest published tag.
2. Run `git log <latest-tag>...HEAD --oneline` to see what has changed since that tag.
3. Apply semantic versioning rules to the unreleased changes:
   - **Patch** (x.x.+1) — bug fixes, CI/workflow fixes, documentation only, no new public API
   - **Minor** (x.+1.0) — new features, new packages, new endpoints or handlers added without breaking existing callers
   - **Major** (+1.0.0) — breaking changes: renamed interfaces, changed method signatures, removed types, delegate signature changes
4. State the proposed version and the reason. Wait for user confirmation before continuing.

---

## Phase 2 — Update CHANGELOG.md

File: `CHANGELOG.md` in the repo root.

1. Move the `## [Unreleased]` section content into a new versioned section `## [X.Y.Z] — YYYY-MM-DD` using today's date.
2. Leave a fresh empty `## [Unreleased]` section at the top.
3. Add a diff URL to the footer links:
   ```
   [X.Y.Z]: https://github.com/HBartosch/Routya/compare/v<prev>...vX.Y.Z
   ```
4. Update the `[Unreleased]` footer link to compare from the new tag:
   ```
   [Unreleased]: https://github.com/HBartosch/Routya/compare/vX.Y.Z...HEAD
   ```
5. Categorise every change under the correct heading: `Added`, `Changed`, `Fixed`, `Removed`. Do not invent entries — only document what is in the git log.

---

## Phase 3 — Update package versions

Update `<Version>` in all three package project files to the agreed version:

- `Routya.Core/Routya.Core.csproj`
- `Routya.SourceGenerators/Routya.SourceGenerators.csproj`
- `Routya.Package/Routya.Package.csproj`

Also update the default `<RoutyaVersion>` in `Routya.NuGet.IntegrationTest/Routya.NuGet.IntegrationTest.csproj` to match.

---

## Phase 4 — Update READMEs

Check each README for content that is stale relative to the unreleased changes:

| File | What to check |
|---|---|
| `README.md` | Version numbers in install snippets, feature list, demo endpoint tables |
| `Routya.Package/README.md` | Install command, quick-start snippet, example project links |
| `Routya.SourceGenerators/README.md` | Install command, generated code examples, example project links, limitations |

Rules:
- Only update content that is factually wrong or missing given the new changes.
- Do not rewrite sections that are still accurate.
- If new public API was added, add a minimal usage snippet showing it.

---

## Phase 5 — Add demos for new functionality

For every new feature in this release, add a working demonstration to **both** web API projects.

### Routya.WebApi.SourceGen.Demo (source generator path)

Location: `Routya.WebApi.SourceGen.Demo/`

- Add a new request/handler pair in `Handlers/` that exercises the new feature.
- Wire a new minimal API endpoint in `Program.cs`.
- If the feature involves notifications, add a handler in `Handlers/OrderHandlers.cs` or a new file.
- Update the endpoint table in `README.md` → `Routya.WebApi.SourceGen.Demo` section of the root README.

### Routya.WebApi.Demo (runtime dispatch path)

Location: `Routya.WebApi.Demo/`

- Add an equivalent handler and endpoint using `IRoutya` (runtime dispatch).
- Keep parity with the source gen demo so both paths are demonstrated.

After adding demos, run `dotnet build` on each project and confirm 0 errors.

---

## Phase 6 — Write tests for new functionality

Every new feature must have tests in at least one of:

| Project | When to use |
|---|---|
| `Routya.Test/` | Testing `IRoutya` (runtime dispatch) behaviour — handlers, notifications, pipeline behaviors, DI registration |
| `Routya.SourceGen.Test/` | Testing source-generated dispatch — `IGeneratedRoutya`, `AddGeneratedRoutya()`, generated pipeline, generated notifications |

Rules:
- Test the happy path and at least one edge case (null input, missing handler, cancellation).
- Follow the existing test naming pattern: `MethodUnderTest_Scenario_ExpectedOutcome`.
- Add handlers/notifications needed by the test directly in the test project — do not share handlers between test projects.
- Run `dotnet test` on each test project and confirm all pass before finishing.

---

## Phase 7 — Final build check

Run the full solution build and confirm 0 errors:

```bash
dotnet build Routya.Core/Routya.Core.sln --configuration Release
```

Then run all tests:

```bash
dotnet test Routya.Test/Routya.Test.csproj --configuration Release
dotnet test Routya.SourceGen.Test/Routya.SourceGen.Test.csproj --configuration Release
```

Report the result. If anything fails, fix it before declaring the release ready.

---

## Completion checklist

Before telling the user the release is ready, confirm every item:

- [ ] Version number agreed and applied to all three package csproj files
- [ ] `CHANGELOG.md` has a dated versioned entry with no invented items
- [ ] All README files updated where stale
- [ ] New functionality demonstrated in `Routya.WebApi.SourceGen.Demo`
- [ ] New functionality demonstrated in `Routya.WebApi.Demo`
- [ ] Tests written and passing for new functionality
- [ ] Full solution builds with 0 errors

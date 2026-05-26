# Changelog

All notable changes to Routya are documented here.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).  
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

---

## [3.1.0] — 2026-05-26

### Added
- `Routya` meta-package — single `dotnet add package Routya` install that pulls in both `Routya.Core` and `Routya.SourceGenerators`
- `Routya.SourceGen.Test` — test project proving the source generator discovers handlers and wires DI correctly at compile time
- `Routya.NuGet.IntegrationTest` — end-to-end test against the packed NuGet packages (not project references) to verify source gen runs through the real install path
- `Routya.WebApi.SourceGen.Demo` — ASP.NET Core minimal API demo showing source-generated dispatch, open-generic pipeline behaviors, notification fan-out, and `IAsyncEnumerable<T>` streaming

### Changed
- All packages aligned to a single shared version number (`3.1.0`). Previously `Routya.Core` published as `2.0.0` and `Routya.SourceGenerators` published as `3.0.x`
- `Routya.SourceGenerators` README rewritten — removed duplicated Core content, added project file configuration guide for inspecting generated files, added handler discovery rules and limitations
- `PackageReleaseNotes` on all packages now links to this changelog

---

## [3.0.1] — 2026-04-07

### Fixed
- GitHub Actions workflow: added `Routya.SourceGenerators` NuGet publish step that was missing from the CI pipeline

---

## [3.0.0] — 2026-01-29

> First public release of `Routya.SourceGenerators`.

### Added
- `Routya.SourceGenerators` package — Roslyn incremental source generator that discovers handlers at compile time
  - `IGeneratedRoutya` interface with a typed `SendAsync` overload per request handler (no generic type arguments at call sites)
  - `AddGeneratedRoutya()` DI extension — zero assembly scanning, zero reflection
  - `PublishAsync` fan-out executes all notification handlers concurrently in generated code
  - `IPipelineBehavior<,>` pipeline fully supported in generated dispatch paths
  - Generated files written to `obj/Generated/` when `EmitCompilerGeneratedFiles=true` is set

---

## [2.0.0] — 2025-11-20

> **Breaking change** — see migration notes below.

### Added
- .NET 8, 9, and 10 multi-targeting
- XML documentation on all public APIs for IntelliSense support

### Changed
- **Breaking:** `RequestHandlerDelegate<TResponse>` now takes a `CancellationToken` parameter

  ```csharp
  // 1.x
  RequestHandlerDelegate<TResponse> next = () => handler.HandleAsync(request);

  // 2.0
  RequestHandlerDelegate<TResponse> next = ct => handler.HandleAsync(request, ct);
  ```

  Update every `IPipelineBehavior<,>` implementation that constructs or invokes a delegate.

### Fixed
- Nullable reference warnings across all public APIs

---

## [1.0.5] — 2025-11-12

### Added
- Registry-based handler cache — handler lookup cost amortized after first call per request type, with smart fallback to DI resolution

---

## [1.0.3] — 2025-04-25

### Added
- NuGet package icon

---

## [1.0.2] — 2025-04-24

### Added
- `netstandard2.0` target — enables use in .NET Framework 4.6.1+ projects

---

## [1.0.1] — 2025-04-23

### Added
- NuGet package metadata: project URL, repository link

---

## [1.0.0] — 2025-04-22

### Added
- `IRoutya` dispatcher interface — runtime request/response dispatch and notification fan-out
- `IRequest<TResponse>` and `INotification` marker interfaces
- `IRequestHandler<,>` and `IAsyncRequestHandler<,>` handler contracts
- `INotificationHandler<TNotification>` for fan-out notifications
- `IPipelineBehavior<TRequest, TResponse>` middleware pipeline
- `AddRoutya()` DI extension with assembly scanning
- `RoutyaDispatchScope` — choose `Scoped` or `Singleton` handler lifetime
- `INotificationStrategy` — pluggable fan-out strategies (parallel, sequential, fire-and-forget)

---

[Unreleased]: https://github.com/HBartosch/Routya/compare/v3.1.0...HEAD
[3.1.0]: https://github.com/HBartosch/Routya/compare/v3.0.1...v3.1.0
[3.0.1]: https://github.com/HBartosch/Routya/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/HBartosch/Routya/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/HBartosch/Routya/compare/v1.0.5...v2.0.0
[1.0.5]: https://github.com/HBartosch/Routya/compare/v1.0.3...v1.0.5
[1.0.3]: https://github.com/HBartosch/Routya/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/HBartosch/Routya/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/HBartosch/Routya/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/HBartosch/Routya/releases/tag/v1.0.0

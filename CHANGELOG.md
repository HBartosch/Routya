# Changelog

All notable changes to Routya are documented here.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).  
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

> A correctness release. No public API changed, and no source change is required to upgrade.
> Read **Upgrade notes** below before upgrading if you use the source generator.

### Fixed

#### `Routya.Core` — runtime dispatch

- **Pipeline behaviors were resolved once and reused for the lifetime of the process.**
  `Scoped` behaviors were built from the first dispatch scope and then reused after that scope had
  been disposed, so a behavior holding a `DbContext`, unit of work or any other scoped dependency
  operated on a disposed scope from the second dispatch onwards. Behaviors are now resolved from
  the current dispatch scope every time. The cache was also `static`, which leaked behavior
  instances between DI containers in the same process and broke tests that build more than one host
- **Notification handlers registered directly against `IServiceCollection` were resolved from the
  root provider**, before the dispatch scope existed. Under scope validation, which is the
  ASP.NET Core Development default, the first publish threw
  `Cannot resolve scoped service ... from root provider` and the notification never reached its
  handlers. With validation off, the handler was captured as a root singleton and its scoped
  dependencies reused after disposal. Handlers are now resolved from the dispatch scope on every
  publish
- **Assembly scanning failed the whole application when any single type could not be loaded.**
  `Assembly.GetTypes()` throws `ReflectionTypeLoadException` if one type in the assembly fails to
  load, which happens whenever a scanned assembly references something that is not deployed.
  Scanning now continues with the types that did load

#### `Routya.SourceGenerators`

- **The generator crashed on array response types.** `IRequest<string[]>` produced an
  `InvalidCastException`, reported as `CS8785`, after which the generator contributed nothing at
  all and the build failed with unrelated looking `CS0234` errors
- **Nested request types generated code that did not compile.** `Features.CreateOrder.Command` was
  emitted as `Features.Command`, dropping the containing type. This affects the feature folder
  convention, where the request and its handler are nested in one type per feature
- **Synchronous handlers generated code that did not compile.** The generated interface declared
  `TResponse Send(TRequest)` for `IRequestHandler<,>`, but the dispatcher emitted `SendAsync`
  calling `HandleAsync`, which a synchronous handler does not have
- **`internal` and `record` handlers were silently skipped**, so they were never registered and got
  no typed member. `internal sealed class Handler` is a common convention
- **Generated type names had no `global::` prefix**, so a user type shadowing a framework type broke
  the generated code
- **A faulting notification handler could hide another handler's exception.** With exactly two
  handlers, both tasks were started and then awaited one after another, so if the first faulted the
  second was never awaited and its exception went unobserved. All fan outs above one handler now go
  through `Task.WhenAll`

### Changed

- Generated type names are now fully qualified with `global::` and carry nullable reference
  annotations. This changes generated output but not behaviour
- Removed the registry population that ran when a handler was not found in the registry. It never
  took effect, because the registry is read once when the dispatch delegate is built and that
  delegate is cached per request type. Making it effective would have broken dispatch for handlers
  registered by interface only, whose implementation type is not registered
- Removed five unreferenced internal types that built and compiled expression trees. `Routya.Core`
  now has no expression tree construction or compilation on any path, which removes the usual
  obstacle to trimming and Native AOT

### Added

- `ROUTYA005` warning when a handler is registered but gets no typed member on `IGeneratedRoutya`,
  because its request or notification type is not publicly accessible. Previously this was silent
- `Routya.SourceGenerators.Test`, a Roslyn harness that drives the generator over source text in
  memory, so generator defects that break the consuming build can be expressed as failing tests

### Documentation

- Corrected the benchmarked MediatR version in the README. Both tables stated 13.1.0; the benchmark
  projects reference 12.5.0. The distinction matters, since 12.5.0 is the Apache 2.0 release
- Corrected every Routya allocation figure in both benchmark tables. All were overstated, by 200 B
  on `Send` rows, 240 B on `SendAsync` rows and 32 B on every notification row. Several derived
  memory percentages were understating Routya, for example Singleton sequential notifications use
  64% less memory than MediatR rather than the 56% published
- Removed the claim that handlers discovered outside the registry are added to it and that later
  calls become registry optimised. That never happened
- Corrected the XML documentation on both dispatchers, which described them as using compiled
  expression trees. Neither does

### Upgrade notes

- **If you use the source generator and have `internal` handlers**, they were previously skipped in
  silence and are now discovered and registered. If you worked around the omission by registering
  one manually, you will now have a duplicate registration. Check for manual registrations of
  `internal` handlers before upgrading
- **If you build with `TreatWarningsAsErrors`**, the new `ROUTYA005` warning can fail your build
  where a request or notification type is not publicly accessible. Make the type public, or suppress
  `ROUTYA005`
- **If you register pipeline behaviors as `Scoped`**, they are now correctly constructed once per
  dispatch scope rather than once per process. This costs roughly 272 B per dispatch for two
  behaviors. Register behaviors as `Singleton` where they are stateless

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

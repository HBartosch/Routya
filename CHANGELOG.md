# Changelog

All notable changes to Routya are documented here.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).  
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added

- **The source generator now finds handlers in referenced assemblies.** It previously only saw
  handlers declared in the compilation it was generating for, because the syntax provider can only
  see syntax trees in that compilation. In the usual Clean Architecture layout, where handlers live
  in an Application project and the generator runs in the Web or API project, that meant every
  handler was invisible and `AddGeneratedRoutya` registered nothing. Handlers are now also read from
  the metadata of referenced assemblies.

  Only assemblies that themselves reference `Routya.Core` are walked, checked on assembly identity
  before any type is enumerated, so the cost is proportional to the number of projects actually
  using Routya rather than to the size of the reference closure.
- **The generated handler lifetime is now configurable.** `AddGeneratedRoutya` takes an optional
  `ServiceLifetime`, and a new `[RoutyaHandler(ServiceLifetime)]` attribute overrides it for an
  individual handler. The generator previously hardcoded `Transient` with no way to change it, which
  a project migrating from runtime dispatch had to simply accept.

  The default stays `Transient`. That is not merely for compatibility: the runtime dispatcher creates
  a DI scope per dispatch while the generated one does not, so `IRoutya` can resolve a `Scoped`
  handler wherever it was itself resolved from, and `IGeneratedRoutya` cannot. `Transient` is the
  only lifetime that works regardless of where the consumer resolves from, which is why the two
  paths default differently. Previously undocumented; now explained in the README, on the attribute,
  and in the generated method's own documentation comment, with guidance on when
  `AddGeneratedRoutya(ServiceLifetime.Scoped)` is safe and when it is not.
- `ROUTYA006` warning when a handler in a referenced assembly is not public. Generated registration
  code lives in the consuming assembly, so such a handler is genuinely unreachable from it. It is
  skipped with a diagnostic naming the handler and the assembly, rather than going silently missing
  at runtime.

---

## [4.0.0] — 2026-09-28

> A correctness release, plus first class streaming and verified Native AOT support.
>
> The major version reflects two breaking changes to the public surface, both narrow: `IRoutya`
> gained a member, and `DefaultRoutya`'s public constructor gained a parameter. Most consumers
> resolve `IRoutya` from the container and need no source change. Read **Upgrade notes** before
> upgrading.

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
- **`PublishParallelAsync` handed every handler the same scoped dependency.** It created one
  dispatch scope and then started all handlers against it concurrently, so two handlers resolving
  the same `DbContext` produced `A second operation was started on this context before a previous
  operation completed`. That appears under load rather than in tests, in exactly the fan out
  scenario parallel publishing is chosen for. Each handler now gets its own scope. Sequential
  publishing deliberately still shares one, since handlers run one after another

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
- `AddRoutya` is split into two overloads. `AddRoutya(services, configure)` is free of reflection
  and usable under trimming; the overload taking assemblies carries the scanning annotation.
  Existing source and existing compiled callers are both unaffected, because overload resolution
  prefers the first in normal form
- Benchmark projects now take the MediatR version from a single `MediatRVersion` property in
  `Directory.Build.props`. They previously pinned 12.4.1 in one project and 12.5.0 in two others,
  which made their results incomparable with each other. Held at 12.5.0, the last Apache 2.0
  release, since moving to 13 or later is a licensing decision rather than a maintenance one
- Split the source generator benchmark into `RequestBenchmarks` and `NotificationBenchmarks`, each
  with its own baseline. Both previously lived in one class with a single baseline on the request
  benchmark, so every notification row was divided by a request timing. A notification ratio of
  0.56 meant "a notification takes 56% of the time a request does", not "56% of MediatR". Measured
  correctly, source generated notifications sit at ratio 0.65 against MediatR
- Moved twelve internal planning and analysis documents from the repository root into `docs/`,
  leaving only README, CHANGELOG, CONTRIBUTING and SECURITY at the top level. Added `docs/README.md`
  as an index that separates current guidance from historical notes, and marked
  `RELEASE_NOTES_V3.md` as superseded, since it still carries the "46% faster" claim measured on
  hardware no longer in use. Fixed a link in the getting started guide that pointed outside the
  repository, and updated its install instructions from 3.0.0 to 4.0.0
- `Routya.Events` no longer produces a package. It is upcoming work with no implementation, but
  `GeneratePackageOnBuild` was writing a `.nupkg` carrying the real `Routya.Events` package id on
  every build. Nothing in CI pushed it, but a published NuGet version can only be delisted, never
  withdrawn, so an accidental push would have squatted the id with an empty assembly. It is marked
  `IsPackable=false` until there is something to ship
- The build workflow no longer runs BenchmarkDotNet. Timings are meaningless on a shared runner:
  the MediatR baseline was observed drifting by a third between runs on identical code. The project
  is still built so it cannot rot, and performance is guarded by the allocation budget tests
  instead. The same workflow now runs all three test suites, rather than only one behind a
  `Test-Path` guard that would have silently skipped it

### Added

- `ROUTYA005` warning when a handler is registered but gets no typed member on `IGeneratedRoutya`,
  because its request or notification type is not publicly accessible. Previously this was silent
- `Routya.SourceGenerators.Test`, a Roslyn harness that drives the generator over source text in
  memory, so generator defects that break the consuming build can be expressed as failing tests
- **Trimming and Native AOT support.** `Routya.Core` now builds with the trimming and AOT analyzers
  enabled, through `IsAotCompatible`, and produces no IL warnings. A Native AOT console application
  using the source generator was verified to compile and run, covering async and synchronous
  handlers, pipeline behaviours and notification fan out. Handler type parameters carry
  `DynamicallyAccessedMembers` so the trimmer keeps their constructors, and the assembly scanning
  overload is marked `RequiresUnreferencedCode` so callers are warned at build time rather than
  losing a handler silently at runtime
- **First class streaming.** `IStreamRequest<TResponse>`, `IStreamRequestHandler<TRequest, TResponse>`,
  `IStreamPipelineBehavior<TRequest, TResponse>` and `IRoutya.CreateStream<TRequest, TResponse>`,
  with a matching `CreateStream` member generated by `Routya.SourceGenerators`.

  This replaces the previous `IRequest<IAsyncEnumerable<T>>` shape, which was a workaround rather
  than a feature. That shape returns a task that hands back a sequence, so the pipeline completed
  the moment the sequence was handed over, before a single item had been produced. A behavior
  wrapped around it could not observe items, could not catch an exception thrown part way through,
  and timed only the handover. A stream behavior wraps the enumeration itself, so it sees every
  item and any failure.

  Items are produced lazily and nothing is buffered, so a consumer can begin processing before the
  handler has finished. Under `RoutyaDispatchScope.Scoped` the dispatch scope lives for the whole
  enumeration and is disposed when it completes or is abandoned, so a handler may hold a
  `DbContext` across the stream.

  `Routya.Core` now references `Microsoft.Bcl.AsyncInterfaces` on the `netstandard2.0` target only,
  which has no `IAsyncEnumerable<T>` of its own. Every other target uses the one in the base class
  library, and the API is identical on all five.
- **Allocation budget tests** in `Routya.Test` and `Routya.SourceGen.Test`. Allocated bytes are
  deterministic, unlike timings, so these run reliably on any CI runner and guard the figures the
  README advertises

### Documentation

- Corrected the benchmarked MediatR version in the README. Both tables stated 13.1.0; the benchmark
  projects reference 12.5.0. The distinction matters, since 12.5.0 is the Apache 2.0 release
- Corrected every Routya allocation figure in both benchmark tables. All were overstated, by 200 B
  on `Send` rows, 240 B on `SendAsync` rows and 32 B on every notification row. Several derived
  memory percentages were understating Routya, for example Singleton sequential notifications use
  64% less memory than MediatR rather than the 56% published
- Removed every timing claim from the published documentation and replaced the benchmark tables
  with allocation tables. Allocated bytes are deterministic and asserted on every build, so they
  hold on the reader's hardware; timings are not reproducible, with the MediatR baseline observed
  drifting by a third between runs on identical code. The one remaining reference to speed is the
  Native AOT caveat, which warns that AOT costs throughput rather than claiming an advantage
- Removed the claim that handlers discovered outside the registry are added to it and that later
  calls become registry optimised. That never happened
- Corrected the XML documentation on both dispatchers, which described them as using compiled
  expression trees. Neither does
- Replaced twenty one hard coded nanosecond figures across the XML documentation with measured
  allocation figures. Those numbers ship in the package and drive IntelliSense, and absolute
  timings cannot be stated in documentation read on someone else's hardware
- Added a Trimming and Native AOT section to the README, including two things a user would
  otherwise find the hard way: pipeline behaviours must be registered closed rather than as open
  generics under Native AOT when a response type is a value type, and Native AOT costs roughly
  three to four times the dispatch throughput of the JIT while buying startup time and binary size
- Documented the differing notification scope semantics on `PublishAsync` and
  `PublishParallelAsync`, in both the README and the XML documentation
- Rewrote the README streaming material around the new first class API. It previously listed
  "Streaming support" as a feature while the demo it pointed at described the same thing as a
  workaround

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
- **If your `PublishParallelAsync` handlers relied on sharing a scoped dependency with each other**,
  they no longer can, because each handler now gets its own scope. Relying on that was already
  unsafe, since the handlers run concurrently. Use `PublishAsync` if the handlers genuinely need to
  share scoped state. The extra scopes cost roughly 300 B per additional handler
- **If you implement `IRoutya` yourself**, for example as a test double, it has gained a
  `CreateStream<TRequest, TResponse>` member and your implementation will no longer compile until
  you add it. Consumers of the interface are unaffected. Mocking frameworks that generate
  implementations at runtime need no change
- **If you construct `DefaultRoutya` directly**, its constructor has gained an
  `IRoutyaStreamDispatcher` parameter. Resolving `IRoutya` from the container is unaffected, since
  `AddRoutya` registers the new dispatcher for you. Only code that calls `new DefaultRoutya(...)`
  needs updating
- **If you publish to Native AOT and register pipeline behaviors as open generics**, that throws for
  any request whose response is a value type, such as `int` or `decimal`. This is a limitation of
  `Microsoft.Extensions.DependencyInjection` rather than of Routya. Register those behaviors closed,
  and remove the open generic registration entirely; adding a closed one alongside it does not help

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

[Unreleased]: https://github.com/HBartosch/Routya/compare/v4.0.0...HEAD
[4.0.0]: https://github.com/HBartosch/Routya/compare/v3.1.0...v4.0.0
[3.1.0]: https://github.com/HBartosch/Routya/compare/v3.0.1...v3.1.0
[3.0.1]: https://github.com/HBartosch/Routya/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/HBartosch/Routya/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/HBartosch/Routya/compare/v1.0.5...v2.0.0
[1.0.5]: https://github.com/HBartosch/Routya/compare/v1.0.3...v1.0.5
[1.0.3]: https://github.com/HBartosch/Routya/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/HBartosch/Routya/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/HBartosch/Routya/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/HBartosch/Routya/releases/tag/v1.0.0

# Routya documentation

## Start here

- [Getting Started with the source generator](./GETTING_STARTED_V3.md) — installation through to
  handlers, notifications and pipeline behaviours
- [Changelog](../CHANGELOG.md) — what changed in each release, including upgrade notes
- [Main README](../README.md) — feature overview, streaming, trimming and Native AOT

## Historical notes

Everything below is a record of past work rather than current guidance. These documents were written
during development and are kept for context, not maintained.

> ⚠️ **Performance figures in these documents are superseded.** They were measured on hardware no
> longer in use, and several were later found to be wrong: every Routya allocation figure in the
> published benchmark tables was overstated, by 200 B on `Send` rows, 240 B on `SendAsync` rows and
> 32 B on every notification row. Current allocation figures are asserted on every build by the
> budget tests in `Routya.Test` and `Routya.SourceGen.Test`, and reported to the CI job summary.
>
> Timings are not published anywhere, because they are not reproducible: the MediatR baseline alone
> has been observed drifting by a third between runs on identical code.

| Document | What it is |
|---|---|
| [RELEASE_NOTES_V3.md](./RELEASE_NOTES_V3.md) | Release notes for v3.0 |
| [BENCHMARK_RESULTS.md](./BENCHMARK_RESULTS.md) | Benchmark run from the v3.0 era |
| [PERFORMANCE_ANALYSIS.md](./PERFORMANCE_ANALYSIS.md) | Analysis behind the v3.0 performance work |
| [PERFORMANCE_SUMMARY.md](./PERFORMANCE_SUMMARY.md) | Summary of the same |
| [SOURCEGEN_PERFORMANCE_ANALYSIS.md](./SOURCEGEN_PERFORMANCE_ANALYSIS.md) | Source generator performance analysis |
| [SOURCE_GENERATOR_PLAN.md](./SOURCE_GENERATOR_PLAN.md) | Design plan for the source generator |
| [OPTIMIZATION_PLAN_V31.md](./OPTIMIZATION_PLAN_V31.md) | Optimisation plan for v3.1 |
| [V31_IMPLEMENTATION_COMPLETE.md](./V31_IMPLEMENTATION_COMPLETE.md) | v3.1 implementation write up |
| [V31_FIX_DOUBLE_SCOPING.md](./V31_FIX_DOUBLE_SCOPING.md) | Notes on the v3.1 double scoping fix |
| [PIPELINE_VERIFICATION.md](./PIPELINE_VERIFICATION.md) | Pipeline behaviour verification notes |
| [TESTING_SUMMARY.md](./TESTING_SUMMARY.md) | Testing summary from the v3.0 era |

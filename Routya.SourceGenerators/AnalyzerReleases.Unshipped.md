; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ROUTYA001 | Routya.SourceGenerator | Error | No handler found for request type
ROUTYA002 | Routya.SourceGenerator | Error | Multiple handlers found for request type
ROUTYA003 | Routya.SourceGenerator | Info | Handler discovered
ROUTYA004 | Routya.SourceGenerator | Info | Code generation complete
ROUTYA005 | Routya.SourceGenerator | Warning | No typed dispatch member generated for handler
ROUTYA006 | Routya.SourceGenerator | Warning | Handler in a referenced assembly is not accessible
ROUTYA007 | Routya.SourceGenerator | Info | Generated dispatcher already provided by a referenced assembly
ROUTYA008 | Routya.SourceGenerator | Info | No generated dispatcher was requested
ROUTYA009 | Routya.SourceGenerator | Warning | More than one assembly generates a Routya dispatcher

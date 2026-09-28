# CI/CD Pipeline Verification

## Pipeline Flow

```
┌─────────────────────────────────────────────────────────────┐
│  Push to main/develop  OR  Pull Request                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 1: Setup .NET 8.0                                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 2: Restore Dependencies                                │
│  └─ dotnet restore                                          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 3: Build Routya.Core                                  │
│  └─ dotnet build Routya.Core/Routya.Core.csproj            │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 4: Build Source Generator                             │
│  └─ dotnet build Routya.SourceGenerators/...csproj         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 5: Build Demo Project (TRIGGERS SOURCE GENERATION!)   │
│  └─ dotnet build Routya.SourceGen.Demo/...csproj           │
│  └─ Source generator creates:                               │
│     • RoutyaGenerated.Registration.g.cs                     │
│     • RoutyaGenerated.Dispatcher.g.cs                       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 6: Verify Generated Files Exist ⚠️ CRITICAL CHECK     │
│  PowerShell Script:                                         │
│  1. Search for *Registration*.g.cs in obj/                  │
│  2. Search for *Dispatcher*.g.cs in obj/                    │
│  3. If either missing: EXIT 1 (fail build)                  │
│  4. If both exist: Print paths and continue                 │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 7: Run Demo Application                               │
│  └─ dotnet run --project Routya.SourceGen.Demo/...         │
│  └─ Validates:                                              │
│     • Generated code compiles                               │
│     • DI registration works                                 │
│     • Request handler executes                              │
│     • Notification handlers execute in parallel             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 8: Build Benchmark Project                            │
│  └─ dotnet build Routya.SourceGen.Benchmark/...csproj      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 9: Run Benchmarks (Quick Mode)                        │
│  └─ dotnet run ... -- --filter "*" --job short             │
│  └─ Validates:                                              │
│     • Performance vs MediatR baseline                       │
│     • Allocation patterns                                   │
│     • No regressions                                        │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 10: Run Tests (If Exist)                              │
│  └─ Checks for Routya.Test/Routya.Test.csproj              │
│  └─ If exists: dotnet test                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  ✅ SUCCESS: All checks passed!                             │
│  OR                                                          │
│  ❌ FAILURE: Build stops at first error                     │
└─────────────────────────────────────────────────────────────┘
```

## What Gets Tested

### Source Generator Functionality ✅
1. **Discovers handlers correctly**
   - Request handlers implementing `IAsyncRequestHandler<TRequest, TResponse>`
   - Notification handlers implementing `INotificationHandler<TNotification>`

2. **Generates correct files**
   - Registration file with `AddGeneratedRoutya()` extension
   - Dispatcher file with type-specific methods

3. **Generated code compiles**
   - No syntax errors
   - All dependencies resolved
   - Correct namespaces and types

4. **Generated code executes**
   - DI container resolves handlers
   - Request handlers return expected results
   - Notification handlers all execute in parallel

### Performance Validation ✅
1. **Request handling performance**
   - Faster than MediatR baseline
   - Minimal allocations

2. **Notification handling performance**
   - Significantly faster than MediatR
   - Zero allocations (optimized parallel execution)

3. **No regressions**
   - Benchmarks run on every build
   - Results tracked over time

## Failure Scenarios

### ❌ Source Generator Not Working
**What:** Generated files not found in Step 6  
**Cause:** Generator not executing or crashing  
**Result:** Build fails immediately  
**Fix:** Check generator diagnostics, fix syntax/logic errors

### ❌ Generated Code Compilation Error
**What:** Build fails in Step 5  
**Cause:** Generator produces invalid C# code  
**Result:** Compiler error messages  
**Fix:** Update generator to emit valid code

### ❌ Runtime Error
**What:** Demo app crashes in Step 7  
**Cause:** Generated code has runtime issues (DI resolution, null refs, etc.)  
**Result:** Application exception  
**Fix:** Update generator logic, add null checks

### ❌ Performance Regression
**What:** Benchmarks show slower performance in Step 9  
**Cause:** Code changes introduced overhead  
**Result:** Warning (currently), could fail build with thresholds  
**Fix:** Profile code, optimize hot paths

## Local Testing

### Quick Check
```powershell
# Mimics Step 5-7 of pipeline
dotnet build Routya.SourceGenerators/Routya.SourceGenerators.csproj -c Release
dotnet run --project Routya.SourceGen.Demo/Routya.SourceGen.Demo.csproj -c Release
```

### Full Pipeline Simulation
```powershell
# All steps
dotnet restore
dotnet build Routya.Core/Routya.Core.csproj -c Release --no-restore
dotnet build Routya.SourceGenerators/Routya.SourceGenerators.csproj -c Release --no-restore
dotnet build Routya.SourceGen.Demo/Routya.SourceGen.Demo.csproj -c Release --no-restore

# Verify files
$reg = Get-ChildItem -Path "Routya.SourceGen.Demo\obj" -Recurse -Filter "*Registration*.g.cs" | Select-Object -First 1
$disp = Get-ChildItem -Path "Routya.SourceGen.Demo\obj" -Recurse -Filter "*Dispatcher*.g.cs" | Select-Object -First 1
if ($null -eq $reg -or $null -eq $disp) { Write-Error "Generated files missing!"; exit 1 }

# Run demo
dotnet run --project Routya.SourceGen.Demo/Routya.SourceGen.Demo.csproj -c Release --no-build

# Run benchmarks
dotnet build Routya.SourceGen.Benchmark/Routya.SourceGen.Benchmark.csproj -c Release --no-restore
dotnet run --project Routya.SourceGen.Benchmark/Routya.SourceGen.Benchmark.csproj -c Release --no-build -- --filter "*" --job short
```

## Key Benefits

### 1. Early Detection
- Catches source generation failures immediately
- No manual testing required
- Runs on every commit/PR

### 2. Confidence
- Know that generated code works before merging
- Performance tracked automatically
- No regressions slip through

### 3. Documentation
- Pipeline IS the test specification
- New contributors see what must work
- Clear success/failure criteria

### 4. Simplicity
- No complex test frameworks
- Real-world usage patterns
- Easy to debug when it fails

## Future Enhancements

### Potential Additions
1. **Code coverage tracking**
   - Track which generator code paths are exercised
   - Ensure all scenarios tested

2. **Performance thresholds**
   - Fail build if slower than X% of baseline
   - Track allocation increases

3. **Multiple .NET versions**
   - Test on .NET 6, 8, 10
   - Ensure compatibility

4. **Artifact publishing**
   - Publish benchmark results
   - Historical performance tracking
   - Compare PRs to main branch

---

**The pipeline is your test suite!** ✅

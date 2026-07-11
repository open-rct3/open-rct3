; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
GDK001 | Design | Warning | UnownedReferenceAnalyzer
GDK002 | Design | Warning | DisposableOwnershipAnalyzer, undeclared ownership (disposed field aliases un-annotated parameter)
GDK003 | Design | Warning | DisposableOwnershipAnalyzer, double dispose (argument disposed again after [TakesOwnership] transfer)

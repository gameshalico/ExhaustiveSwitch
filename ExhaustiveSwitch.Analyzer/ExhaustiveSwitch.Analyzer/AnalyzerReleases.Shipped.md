; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EXH0001 | Usage    | Error    | Missing case in switch statement for Exhaustive type
EXH0002 | Usage    | Warning  | Type with Case attribute does not inherit/implement an Exhaustive type

## Release 1.1.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EXH1001 | Usage    | Error    | Missing enum value in switch statement for Exhaustive enum
; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

| Rule ID | Category | Severity | Notes                                    |
|---------|----------|----------|------------------------------------------|
| EXH0001 | Usage    | Error    | Exhaustive 型のケースが switch で処理されていません      |
| EXH0002 | Usage    | Warning  | Case 属性が付与された型が Exhaustive 型を継承/実装していません |

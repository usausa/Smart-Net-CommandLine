# レビュー結果 GPT-5.5

対象: `Smart.CommandLine.slnx`
観点: パフォーマンス向上、バグ対策
実施日: 2026-06-25

## 概要

`Smart.CommandLine.Hosting` は `System.CommandLine` と `Microsoft.Extensions.*` を組み合わせた CLI ホスティングライブラリで、ソースジェネレーターによりリフレクションを回避する設計になっています。全体として構造は小さく、責務分離も明確です。一方で、ソースジェネレーターの文字列生成、メタデータの重複登録、リフレクションフォールバックとの挙動差、フィルターパイプラインの実行時コストに改善余地があります。

## 重要度別レビュー結果

| 重要度 | 分類 | 指摘 |
| --- | --- | --- |
| 高 | バグ対策 | ソースジェネレーターが文字列リテラルをエスケープせずに生成しており、コマンド名・説明・オプション名・alias・completion に `"`、`\\`、改行などが含まれると生成コードが壊れる可能性があります。 |
| 高 | バグ対策 | ソースジェネレーターとリフレクションフォールバックで、対象プロパティの抽出条件が一致していません。静的・非公開・読み取り専用・インデクサー等に属性が付いた場合、生成コード側だけコンパイルエラーや異なる挙動になる可能性があります。 |
| 中 | パフォーマンス | フィルターパイプラインがコマンド実行ごとにフィルター一覧の `List` 作成、`AddRange`、`Sort`、デリゲートチェーン生成を行っています。CLI では通常問題になりにくいものの、同一ホストで多数回実行する用途では無駄な割り当てになります。 |
| 中 | バグ対策 | 同一コマンド型が複数の `AddCommand` / `AddSubCommand` / `UseHandler` 呼び出しで検出された場合、フィルター記述子が重複登録される可能性があります。 |
| 中 | バグ対策 | `CommandMetadataProvider` の静的 `Dictionary` / `List` は排他制御がなく、モジュール初期化やホスト構築が並行した場合に競合する余地があります。 |
| 中 | バグ対策 | `CommandAttribute` がない型を実行時に登録した場合、`ResolveCommandMetadata` が null 許容を無視して `NullReferenceException` になり得ます。ユーザー入力ミスに対する診断が弱いです。 |
| 低 | パフォーマンス | リフレクションフォールバックの `SetOptionValue` がオプションごと・実行ごとに `ParseResult.GetValue<T>` を検索して `MakeGenericMethod` しています。フォールバック経路ではホットスポットになり得ます。 |
| 低 | バグ対策 | キャンセル伝播のテストが薄く、サンプルの `Task.Delay` も `CancellationToken` を渡していません。長時間処理のコマンドではキャンセル応答性が低下します。 |

## 詳細指摘

### 1. 生成コードの文字列エスケープ不足

- 該当箇所: `Smart.CommandLine.Hosting.Generator/CommandGenerator.cs` 466-475, 545-558, 571-574, 612-614
- 内容: `SourceBuilder.Append` に属性値をそのまま渡し、前後に `"` を付けて C# 文字列リテラルを作っています。
- 影響: 例えば `[Command("say", "quote: \"x\"")]`、`[Option("--path\\name")]`、completion に改行を含む値などで、生成された `CommandInitializer.g.cs` がコンパイル不能または意味の違う文字列になります。
- 推奨対応: 文字列を C# リテラルとして出力する共通関数を追加し、`SymbolDisplay.FormatLiteral(value, quote: true)` の利用を検討してください。`CommandInfo.Name`、`Description`、`Option.Name`、`Aliases`、`Completions` はすべて同じ経路でエスケープするのが安全です。
- 推奨テスト: ジェネレーター tests に、引用符、バックスラッシュ、改行、Unicode を含む command/option/alias/completion の生成コードがコンパイルできるケースを追加します。

### 2. ジェネレーターとフォールバックのプロパティ抽出条件の差異

- 該当箇所:
  - Runtime: `Smart.CommandLine.Hosting/CommandMetadataProvider.cs` 166-181
  - Generator: `Smart.CommandLine.Hosting.Generator/CommandGenerator.cs` 237-347
- 内容: Runtime は `BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly` のプロパティだけを対象にします。一方 Generator は `currentType.GetMembers()` の `IPropertySymbol` を対象にしており、静的、非公開、読み取り専用、インデクサーを明示的に除外していません。
- 影響: 属性が付いたプロパティの形によって、ソースジェネレーター有効時だけ `target.Property = ...` が生成されてコンパイルエラーになる、またはフォールバック時とオプション順序・対象がずれる可能性があります。
- 推奨対応: Generator 側でも `DeclaredAccessibility == Accessibility.Public`、`!IsStatic`、`SetMethod` が public であること、`Parameters.Length == 0` を確認し、Runtime と仕様を合わせてください。対象外の場合は可能なら Diagnostic を出すと利用者が修正しやすくなります。
- 推奨テスト: static/private/get-only/indexer に `[Option]` が付いた場合の期待挙動を固定するテストを追加します。

### 3. フィルターパイプラインの実行時割り当て

- 該当箇所: `Smart.CommandLine.Hosting/FilterPipeline.cs` 21-47
- 内容: 実行ごとに `List<FilterDescriptor>` を作成し、属性フィルターを追加し、ソートし、デリゲートチェーンを組み立てています。
- 影響: 一般的な CLI は 1 プロセス 1 実行のため影響は限定的ですが、テスト、REPL 的利用、同一ホストを再利用するケースでは割り当てとソートが累積します。
- 推奨対応: `SetupCommandHandler` 時点でグローバルフィルターとコマンドフィルターを結合・ソートした配列を作り、`FilterPipeline` には事前計算済みの `FilterDescriptor[]` を渡す構成にすると実行時コストを削減できます。さらにフィルターインスタンス解決も Scoped/Transient の意図を維持しつつ、記述子の順序計算だけキャッシュできます。
- 推奨テスト: フィルター順序の既存テストを維持したまま、同一コマンドの複数回実行で順序が変わらないことを確認します。

### 4. フィルター記述子の重複登録

- 該当箇所:
  - `Smart.CommandLine.Hosting.Generator/CommandGenerator.cs` 448-495
  - `Smart.CommandLine.Hosting/CommandMetadataProvider.cs` 40-54
- 内容: Generator は検出した invocation ごとに `AddFilterDescriptor<TTarget,TFilter>` を生成し、Provider は `List<FilterDescriptor>` に追記します。同一型が複数箇所で登録されると、同じフィルターが複数回実行される可能性があります。
- 影響: ログ、例外処理、認可などの横断処理が重複して実行されます。特に `ExceptionHandlingFilter` やトランザクション系フィルターでは副作用が増えるリスクがあります。
- 推奨対応: Generator 側で `InvocationModel.TypeFullName` 単位に重複排除するか、Provider 側で `(targetType, filterType, order)` の重複を抑止してください。`ActionBuilders` と `CommandMetadata` は上書きのため致命的ではありませんが、`FilterDescriptors` は追記なので対策優先度が高いです。
- 推奨テスト: 同じ command 型を 2 回 `AddCommand<T>()` した場合にフィルター登録が 1 回になるテストを追加します。

### 5. 静的メタデータストアのスレッドセーフ性

- 該当箇所: `Smart.CommandLine.Hosting/CommandMetadataProvider.cs` 15, 40, 84
- 内容: 静的な `Dictionary<Type,...>` と `List<FilterDescriptor>` に対してロックなしで読み書きしています。
- 影響: 通常の ModuleInitializer 実行後に単一スレッドで Build するケースでは問題化しにくいですが、テスト並列実行、複数 Assembly の初期化、ホスト構築と登録が重なるケースでは競合する可能性があります。
- 推奨対応: 初期化後は不変にする設計、`ConcurrentDictionary` + immutable collection、または登録 API 内の lock を検討してください。特に `FilterDescriptors[type]` の List 追記は競合に弱いです。

### 6. 属性不足時の診断が弱い

- 該当箇所: `Smart.CommandLine.Hosting/CommandMetadataProvider.cs` 24-33, `Smart.CommandLine.Hosting/CommandHostBuilder.cs` 164-183
- 内容: `ResolveCommandMetadata` は `type.GetCustomAttribute<CommandAttribute>()!` を前提にしており、属性がない場合は `NullReferenceException` になり得ます。
- 影響: ユーザーが `AddCommand<SomeType>()` したが `[Command]` を付け忘れた場合に、原因が分かりにくい例外になります。Generator が有効なら生成対象から外れるだけなので、実行時フォールバックで発見されます。
- 推奨対応: 属性がない場合は `InvalidOperationException` または `ArgumentException` で「型に `[Command]` が必要」という明確なメッセージを返してください。可能なら AddCommand 時点で検証する方法もあります。
- 推奨テスト: `[Command]` のない型を AddCommand した場合の例外型とメッセージを固定します。

### 7. リフレクションフォールバックの実行時コスト

- 該当箇所: `Smart.CommandLine.Hosting/CommandMetadataProvider.cs` 270-284
- 内容: `SetOptionValue` が呼ばれるたびに `ParseResult.GetValue<T>` の MethodInfo を検索し、`MakeGenericMethod` して `Invoke` しています。
- 影響: Source Generator が無効、または AOT/trim でフォールバックに入る構成では、コマンド実行ごとに反射コストが発生します。
- 推奨対応: `CreateReflectionBasedDelegate` の中でプロパティごとに getter delegate を事前構築して `propertyArguments` と一緒に保持するか、`MethodInfo` 検索結果を静的にキャッシュしてください。
- 備考: プロジェクトの設計上は Source Generator が主経路なので、優先度は中〜低です。

### 8. キャンセル伝播の補強

- 該当箇所:
  - `Smart.CommandLine.Hosting/CommandHostImplement.cs` 32-35
  - `Smart.CommandLine.Hosting/CommandHostBuilder.cs` 205-212
  - `Develop/Commands.cs` 179-186
- 内容: System.CommandLine から渡された token は `CommandContext` に保持されていますが、サンプルの `Task.Delay(50)` は token を渡していません。
- 影響: 長時間処理や I/O を含むコマンドで Ctrl+C 等のキャンセル応答性が落ちる可能性があります。
- 推奨対応: サンプル・ドキュメントで `await Task.Delay(..., context.CancellationToken)` の利用を示し、フィルター/コマンドのキャンセル時の期待終了コードをテストすると安全です。

### 9. Dispose の所有権境界

- 該当箇所: `Smart.CommandLine.Hosting/CommandHostImplement.cs` 37-58
- 内容: `serviceProvider`、configuration provider、configuration root、content root file provider を明示的に dispose しており、ファイル監視リーク対策としては良い実装です。一方、ユーザーが外部から渡した `RootCommand` や `IFileProvider` がある場合の所有権ルールは明文化されていません。
- 影響: 外部所有オブジェクトを dispose されると困る利用者が出る可能性があります。
- 推奨対応: README/API コメントに「Builder/Host が生成・保持する provider は Host dispose 時に破棄される」ことを記載し、外部注入された provider の扱いを設計として固定してください。

## テスト観点の改善提案

既存テストは以下をよく確認しています。

- `DisposeAsync` による `ServiceProvider` / JSON file watcher / content root provider の破棄
- 環境名の解決
- フィルター順序、短絡、ExitCode 変更
- Option の name/alias/default/completion/nullable の基本動作
- Generator の基本的な生成結果、sub command、root handler、設定有効/無効

追加を推奨するテストは以下です。

1. 文字列エスケープ: command description、option description、alias、completion に `"`、`\\`、改行を含めた生成コードのコンパイル成功。
2. プロパティ抽出条件: static/private/get-only/indexer に `[Option]` が付いた場合の generator/runtime の一致。
3. 重複登録: 同一 command 型を複数回登録しても filter が重複実行されないこと。
4. 属性不足: `[Command]` なしの型登録時に明確な例外が出ること。
5. キャンセル: `CommandContext.CancellationToken` が handler/filter に渡り、キャンセル時に処理が中断されること。
6. 並行性: 複数ホスト構築やメタデータ登録を並行実行した場合に例外や重複が発生しないこと。

## 優先順位付き対応案

1. Generator の文字列エスケープを修正し、回帰テストを追加する。
2. Generator の option 対象プロパティ条件を Runtime と揃え、不正な属性利用に Diagnostic または明確な例外を出す。
3. `CommandMetadataProvider` の filter descriptor 重複登録を防止する。
4. フィルター記述子の結合・ソートを Build 時に事前計算して、実行時割り当てを減らす。
5. `[Command]` 付け忘れ時の例外メッセージを改善する。
6. リフレクションフォールバックの `MethodInfo` 検索/Invoke コストをキャッシュする。
7. キャンセル伝播のサンプルとテストを補強する。

## 総評

現状の設計は、Source Generator を主経路にして実行時反射を抑える方向性が明確で、CLI ライブラリとして妥当です。最優先で対処すべきは Generator の C# 文字列リテラル生成と、Generator/Runtime の対象プロパティ仕様差です。この 2 点は通常の利用者入力だけでコンパイルエラーや挙動差につながるため、パフォーマンス改善より先に修正する価値があります。その後、フィルターパイプラインの事前計算とメタデータ重複排除を行うと、安定性と実行時効率の両方を改善できます。

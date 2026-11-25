# Antigravity Global Guidelines: Token Optimization, Aider Delegation & Unity Development

Whenever working on Unity / C# projects, follow these global rules strictly:

## 1. クォータ節約・検索制限（最優先）
- **巨大ディレクトリの探索・読み込み厳禁**: 以下のディレクトリはコンテキスト肥大化・トークン浪費の原因になるため、**絶対に検索・読み込みを行わないこと**：
  - `Library/`
  - `Temp/`
  - `Obj/`
  - `Logs/`
  - `UserSettings/`
  - `Build/` / `Builds/`
- **スクリプト探索範囲の限定**: スクリプトを探す際は原則として `Assets/Scripts/` 配下のみを対象にすること。
- **全体スキャン禁止**: 関連ファイルが不明な場合、プロジェクト全体を闇雲にスキャンせず、まずユーザーにどのファイルを読むべきか質問すること。

## 2. Autonomous Aider Delegation (Default Execution Mode)
- **Automatic Delegation**: Even if the user does NOT explicitly say "use Aider" or "Aiderに実装させて", Antigravity MUST ALWAYS use Aider to implement, modify, or create Unity C# code. Do NOT edit C# files directly unless Aider fails even after rotating all OpenRouter keys.
- **OpenRouter Only (NEVER use Gemini API for Aider)**:
  - Aider MUST be run completely independently using **OpenRouter** (`~/.openrouter_keys.json` and `~/.aider.conf.yml`).
  - NEVER pass `--model gemini/...` or use `GEMINI_API_KEY` with Aider.
- **How to Execute Aider**:
  - Always invoke Aider via the project's `run.ps1` (which automatically sets `PYTHONUTF8=1`, loads the active `OPENROUTER_API_KEY` from `~/.openrouter_keys.json`, and attaches `AiderLogs/unity_errors.log`):
    ```powershell
    powershell -NoProfile -ExecutionPolicy Bypass -File ".\run.ps1" --message "<instruction>" --yes <target_files>
    ```
  - If `run.ps1` does not exist in the current directory, load `OPENROUTER_API_KEY` from `C:\Users\G2546\.openrouter_keys.json` and set `$env:PYTHONUTF8 = "1"` before calling `aider --message "<instruction>" --yes <target_files>`.
- **Token Conservation (Do Not Output Full Code)**:
  - Antigravity must NOT dump full C# source code into the chat when delegating to Aider.
  - Formulate atomic, single-feature, surgical prompts for Aider (Target File Path, Exact Edit Location, Exact Types & Signatures, Preservation Clause: `「既存の変数やメソッドは削除・変更せず、指定箇所のみを追加・修正してください。」`).
- **Quota & Token Management (Key Switching)**:
  - If Aider encounters a rate limit, quota exhaustion (429 / Out of credits / Token limit), or fails to respond due to API limits:
    1. Promptly inform the user: `「Aider（OpenRouter）のトークン上限に達しました。キー切り替えバッチを実行します（または switch-key.bat を実行してください）」`
    2. Execute `powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\G2546\switch-key.ps1"` to rotate to the next OpenRouter account, then **retry Aider** with the new key.

## 3. Unity 固有の安全ルール
- **`.meta` ファイルの直接操作厳禁**: GUID の破損を防ぐため、`.meta` ファイルの直接作成・編集・削除は絶対に行わないこと。リネームや移動が必要な場合は手順を提示して Unity エディタ側での操作を促すこと。
- **シーン・プレハブの直接書き換え禁止**: `.unity` や `.prefab` などの YAML ファイルをテキストとして直接書き換えないこと（Unity MCP 経由での操作を除く）。
- **インスペクター参照の保護**: 既存の `public` フィールドや `[SerializeField]` の変数名を変更する際は、インスペクターの参照が外れるため**必ず事前に警告し、ユーザーの許可を得ること**。

## 4. Unity & C# コーディング規約
- **言語**: 応答およびコード内のコメントはすべて日本語で記述すること。説明は簡潔にし、変更点と要点を端的に提示すること。
- **カプセル化**: インスペクター公開変数は `public` ではなく `[SerializeField] private` を使用すること。
- **キャッシュとパフォーマンス**: `Update()` / `FixedUpdate()` 内で `GetComponent`、`GameObject.Find`、`Camera.main`、不要なメモリ確保（GC Alloc / `new List()` / LINQ）を避け、`Awake()` または `Start()` でキャッシュすること。
- **非推奨 API の回避**: 対象 Unity バージョンの仕様に準拠し、非推奨（Obsolete）の API は使用しないこと。

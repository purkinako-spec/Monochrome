# Monochrome 開発 TODO リスト

Monochrome の「会話・エリア移動・ミニゲーム」を有機的に連動させるための設計確定済み TODO リストです。

---

## 🎯 設計確定済み方針
1. **TimeScale ゼロに頼らない停止制御**: 会話中やポーズはコンポーネント単位の入力・ステート制御で待機化。環境アニメは維持。
2. **釣りミニゲーム中の完全休止**: フィールド側の Camera / Audio / オブジェクトを停止し、ミニゲームを Additive ロード。終了時に復帰。
3. **Unity 新 Input System への完全統一**: 旧式のハードコード入力を全廃。
4. **トリプル入力対応 (PC / Gamepad / Touch)**: 入力デバイスの自動判別とプロンプトアイコン動的切替、EventSystem による UI ナビゲーション。
5. **ロード画面と非同期遷移**: 非同期プリロード (`AsyncOperation.allowSceneActivation = false`) によるノーフレームドロップ遷移。
6. **多重入力ロック (ActionBlocker)**: 会話中・移動中・メニュー中などの多重ロックに耐えるビットフラグ方式。
7. **会話コンテナのメモリ管理**: エリア単位での Addressables ロード/アンロード。NPC は ID/アドレスのみを保持。
8. **会話のセーブデータ一元化**: `FlagManager`（トークカウントやイベントフラグ）による会話分岐の永続化。
9. **移動・演出の基盤共通化**: プレイヤーとNPCで共通の移動モーター（`CharacterMotor2D`）とビジュアルコントローラー（`CharacterVisual2D`）を使用し、カットシーン・イベント演出を統一的に制御。

---

## 📋 実装タスク一覧

### Phase 1: 入力基盤 & マルチプラットフォーム対応 (Input System)
- [x] **1-1. 入力デバイス検知マネージャー (`InputDeviceManager.cs`)**
  - [ ] 現在の入力デバイス（Keyboard/Mouse, Gamepad, Touchscreen）をリアルタイム自動判別
  - [ ] デバイス切り替え通知イベント (`OnInputDeviceChanged`)
- [x] **1-2. インタラクト・プロンプト動的解決 (`InputPromptResolver.cs`)**
  - [ ] 入力デバイスに応じた表示切り替え（PC: `[E] 調べる`、Pad: `(A) 調べる`、Touch: タップUI）
- [x] **1-3. UI ナビゲーション制御 (`UINavigationHelper.cs`)**
  - [ ] 会話選択肢やメニュー表示時に最初のボタンを自動選択 (`EventSystem.SetSelectedGameObject`)
  - [ ] ゲームパッド十字キー/スティック、キーボード、マウス、タッチの全操作両立

---

### Phase 2: プレイヤー＆共通キャラクター移動基盤の刷新
- [x] **2-1. 多重操作ロック構造 (`ActionBlocker.cs`)**
  - [ ] ビットフラグ定義: `None`, `Dialogue`, `Transition`, `Menu`, `Minigame`, `Cutscene`
  - [ ] `AddBlocker()`, `RemoveBlocker()`, `CanMove`, `CanInteract` の安全判定
- [x] **2-2. 共通キャラクター移動モーター (`CharacterMotor2D.cs`)**
  - [ ] プレイヤー・NPC共通の物理移動（Rigidbody2D）、速度管理、向き（4方向/8方向）
  - [ ] スポーン地点への座標・向きワープ機能 (`WarpTo(position, facingDirection)`)
- [x] **2-3. プレイヤーコントローラー (`PlayerController2D.cs`)**
  - [ ] 旧 `PlayerMove.cs` を破棄し、Input Action からの入力を `CharacterMotor2D` へ伝達
  - [ ] `ActionBlocker.CanMove` に応じた入力遮断
- [x] **2-4. 既存インタラクト連携の統合 (`PlayerInteractor2D.cs` の接続)**
  - [ ] `ActionBlocker.CanInteract` と連動し、会話中や移動中のインタラクト多重発火を防止

---

### Phase 3: キャラクター自動移動・アニメーション＆エモート演出システム
- [x] **3-1. 自動移動ナビゲーター (`CharacterNavigator2D.cs`)**
  - [ ] 指定座標への自動歩行（非同期待機 / 到着コールバック対応、タイムアウト付きスタック対策）
  - [ ] 移動スタイルの切り替え（Walk / Run / Sneak / 向き固定移動）
  - [ ] パトロール（複数ウェイポイント巡回）＆ ランダム徘徊（指定半径）モード
  - [ ] 話しかけられた時の一時停止 ＆ プレイヤーへの振り向き（会話終了時に巡回復帰）
- [x] **3-2. キャラクタービジュアル＆ジェスチャー制御 (`CharacterVisual2D.cs`)**
  - [ ] 歩行・走行・待機アニメーションのブレンド
  - [ ] ワンショット感情アクション（`Surprise`, `Nod`, `ShakeHead`, `LookAround`, `Sit`, `Collapse` 等）
  - [ ] 視線制御（指定方向を向く / 特定オブジェクトの方を向く `LookAt`）
- [x] **3-3. 感情バルーン演出 (`EmotionBalloonController.cs`)**
  - [ ] 頭上アイコンのポップアップ演出（`!`, `?`, `...`, `★`, `♥` 等のプレハブ・イージング・SE）
- [x] **3-4. 会話システム用演出コマンドハンドラー (`CutsceneCommandHandlers.cs`)**
  - [ ] `[move:target=xxx, to=yyy, style=Walk, wait=true/false]` 移動コマンド
  - [ ] `[lookat:target=xxx, towards=yyy]` 視線/向き変更コマンド
  - [ ] `[anim:target=xxx, trigger=Nod]` アニメーショントリガーコマンド
  - [ ] `[balloon:target=xxx, type=Exclamation, se=notice]` 感情アイコンポップコマンド

---

### Phase 4: エリア移動＆非同期ロード画面基盤
- [x] **4-1. 画面フェード (`ScreenFader.cs`)**
  - [ ] 全画面フェードイン/アウト（黒・白・カスタム色）
  - [ ] 常駐型 Canvas (`DontDestroyOnLoad`)
- [x] **4-2. 非同期ロード画面 (`LoadingScreenUI.cs`)**
  - [ ] 大規模シーン遷移用のプログレスバー＆ローディング演出
  - [ ] `allowSceneActivation = false` による裏読み込み完了後のスムーズな画面切り替え
  - [ ] 暗転中の GC (`Resources.UnloadUnusedAssets()`) によるフレームドロップ防止
- [x] **4-3. 出現地点マーカー (`SpawnPoint.cs`)**
  - [ ] `SpawnID`、出現座標、出現時のプレイヤー向き（FacingDirection）
  - [ ] Scene ビュー用ギズモ（矢印とアイコン）描画
- [x] **4-4. エリア遷移マネージャー (`AreaTransitionManager.cs` - Singleton)**
  - [ ] シーン内ワープ（暗転のみ）
  - [ ] シーン間ロード（ロード画面＋非同期読み込み）
  - [ ] カメラの瞬間追従ワープ (`CinemachineVirtualCamera.OnTargetObjectWarped`)
  - [ ] 移動完了後の会話自動再生 (`resumeDialogueContainer`)
- [x] **4-5. ドア・ワープトリガー (`AreaDoor2D.cs`)**
  - [ ] `IInteractable2D` 実装による `[E]` / `(A)` キー入力移動
  - [ ] `isTrigger` 接触による自動移動（マップ端・階段用）
  - [ ] `FlagManager` 連携（鍵や進行条件の判定、ロック時のメッセージ表示）

---

### Phase 5: ミニゲーム（釣り等）の休止・統合フロー
- [x] **5-1. フィールド休止＆復帰制御 (`FieldPauseController.cs`)**
  - [ ] `Time.timeScale` を変更せず、フィールドカメラ無効化・BGM一時停止・オブジェクト休止を実行
- [x] **5-2. ミニゲーム起動・結果パイプライン (`MinigameConnector.cs`)**
  - [ ] 釣り場インタラクト → フィールド休止 → ミニゲームシーン（Additive）ロード
  - [ ] クリア/失敗/釣果スコアを `FlagManager` に書き込み
  - [ ] ミニゲームシーンをアンロード → フィールド復帰 → 結果会話への自動遷移

---

### Phase 6: 会話システム・メモリ管理・セーブ統合
- [x] **6-1. 会話コンテナのエリア単位ロード (`DialogueContainerManager` 連携)**
  - [ ] NPC にコンテナ直参照を持たせず、`string containerAddress`（ID）で管理
  - [ ] `AreaTransitionManager` のロード画面裏でエリアラベル（`Area_Town` 等）を一括ロード/アンロード
- [x] **6-2. 会話進行度・分岐セレクター (`DialogueSelector.cs`)**
  - [ ] NPC ごとに「優先度付き会話リスト（フラグ条件＋会話コンテナID）」を設定
  - [ ] 初回会話（`talk_count == 0`）、クエスト中、報告後などの自動切り替え
  - [ ] 会話終了時のトークカウント自動インクリメント（`FlagManager` 連携）
- [x] **6-3. セーブ＆ロード動作検証**
  - [ ] プレイヤーの現在シーン・直近 SpawnID・フラグ辞書のセーブ・ロード検証
  - [ ] 「NPC 会話 → 自動移動演出 → ドア移動 → ミニゲーム → 会話変化」の完全ループ疎通テスト

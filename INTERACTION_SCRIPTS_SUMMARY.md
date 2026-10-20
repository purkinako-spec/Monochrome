# インタラクト関係スクリプト一覧

## 概要
このプロジェクトには2つの異なるインタラクションシステムが存在します。

---

## 1. DialogueSystem (Runtime.Dialogue.Interaction)
**場所:** `Assets/DialogueSystem/Scripts/Interaction/`
**アーキテクチャ:** 3層構造 (PlayerInteractor → InteractableReceiver → IInteractableProcessor)

### Script 1: 検出・入力担当
| ファイル | クラス名 | 役割 |
| :--- | :--- | :--- |
| `PlayerInteractor.cs` | `PlayerInteractor` | プレイヤーにアタッチ。範囲検出、入力監視、最適ターゲット選定、Receiverへの通知を行う。 |

### Script 2: 受信・委譲担当
| ファイル | クラス名 | 役割 |
| :--- | :--- | :--- |
| `InteractableReceiver.cs` | `InteractableReceiver` | インタラクト対象オブジェクトにアタッチ。インスペクタで指定されたProcessorに処理を委譲する。 |

### Script 3: 具象処理担当 (Interface & Implementations)
| ファイル | クラス/インターフェース名 | 役割 |
| :--- | :--- | :--- |
| `IInteractableProcessor.cs` | `IInteractableProcessor` | インタラクト処理の共通インターフェース (`CanInteract`, `Interact`, `GetInteractionText`, `Priority`)。 |
| `Processors/DialogueProcessor.cs` | `DialogueProcessor` | 会話開始処理の実装。`DialogueManager` と連携し、View待機ロジックを含む。 |
| `InteractableNPC.cs` | `InteractableNPC` | **旧実装/別パス**。`IInteractable` (Minigame1のもの) を実装し、直接 `DialogueManager` を叩く MonoBehaviour。Receiver/Processor構造を使わない独立した実装。 |

---

## 2. Minigame1 (MiniGame.Fish.Test2 / Global Namespace)
**場所:** `Assets/Minigame1/Scripts/`
**アーキテクチャ:** シンプルなインターフェースベース (`IInteractable`)

### インターフェース
| ファイル | インターフェース名 | 役割 |
| :--- | :--- | :--- |
| `IInteractable.cs` | `IInteractable` | `Interact(GameObject)`, `GetInteractionText()` のみを定義するシンプルなインターフェース。 |

### 実装 (Processor風)
| ファイル | クラス名 | 役割 |
| :--- | :--- | :--- |
| `Interaction/Processors/FishingSpotProcessor.cs` | `FishingSpotProcessor` | `IInteractableProcessor` (DialogueSystem側) を実装。釣りミニゲーム開始・シーン遷移・プレイヤー制御無効化を行う。 |

---

## 相違点・注意点

### インターフェースの不一致
- **DialogueSystem** は `Runtime.Dialogue.Interaction.IInteractableProcessor` を使用 (Priorityプロパティあり、CanInteractあり)。
- **Minigame1** は独自の `IInteractable` を使用 (Priorityなし、CanInteractなし)。
- **FishingSpotProcessor** は Minigame1フォルダにあるが、**DialogueSystem側の `IInteractableProcessor` を実装している** (Namespace: `MiniGame.Fish.Test2`, Implements: `Runtime.Dialogue.Interaction.IInteractableProcessor`)。
- **InteractableNPC** は DialogueSystemフォルダにあるが、**Minigame1側の `IInteractable` を実装している** (Namespace: `Runtime.Dialogue.Interaction`, Implements: `IInteractable` - Global Namespace)。

### 統合時の課題
1. `PlayerInteractor` (DialogueSystem) は `InteractableReceiver` + `IInteractableProcessor` 構造を前提としている。
2. `InteractableNPC` は `IInteractable` しか実装しておらず、`PlayerInteractor` からは検出されない (InteractableReceiverが必要)。
3. `FishingSpotProcessor` は `IInteractableProcessor` なので `InteractableReceiver` 経由で動作可能。

### 推奨アクション
- **統一インターフェースの策定** (例: `IInteractableProcessor` を共通化し、`IInteractable` は廃止/エイリアス化)。
- `InteractableNPC` を `DialogueProcessor` + `InteractableReceiver` 構成へ移行、または `InteractableNPC` に `InteractableReceiver` 的な機能を持たせるアダプターを作成。

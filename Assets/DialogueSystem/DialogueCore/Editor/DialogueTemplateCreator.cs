using UnityEngine;
using UnityEditor;
using System.IO;

namespace DialogueSystem.Editor
{
    public static class DialogueTemplateCreator
    {
        [MenuItem("Assets/Create/Dialogue System/Custom Command Handler", false, 80)]
        public static void CreateCommandHandlerTemplate()
        {
            string template = @"/*
=========================================================
【AI生成用プロンプト・実装ルール】
AIアシスタントにこのスクリプトを渡して処理を実装させる場合は、以下のルールを厳守させてください。

1. [属性の付与] クラス直上に必ず `[HandlerInfo(""コマンドの説明"", ""使い方: [コマンド名:引数=値]"")]` を記述すること。
2. [必須実装] `IDialogueCommandHandler` インターフェースを実装すること。
3. [登録処理] `Start()` 内で `DialogueEventDispatcher.Instance.RegisterHandler(this);` を行うこと。
4. [引数取得] 引数は `command.GetString(""key"", default)` や `command.GetFloat()` などを用いて安全に取得すること。
5. [完了通知] 演出（コルーチンやTween等）が終了したら、最後に必ず `onComplete?.Invoke();` を呼び出すこと。
6. [スキップ対応] `ForceComplete()` 内では、即座に演出の最終状態を適用し、進行中のコルーチン等を停止すること（onCompleteの呼び出しは不要）。
=========================================================
*/
using System;
using System.Collections;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core; // HandlerInfoを使用するために必要

namespace Runtime.Dialogue.Commands
{
    [HandlerInfo(""ここにコマンドの説明を記述します"", ""使い方: [my_command:target=value,time=1.0]"")]
    public class CustomCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => ""my_command"";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string targetValue = command.GetString(""target"", ""default_value"");
            float timeValue = command.GetFloat(""time"", 1.0f);

            StartCoroutine(CommandRoutine(timeValue, targetValue, onComplete));
        }

        public void ForceComplete(DialogueCommand command)
        {
            StopAllCoroutines();
            // TODO: ここにオブジェクトの最終状態を適用する処理を書く（例: アルファ値を強制的に1にする など）
        }

        private IEnumerator CommandRoutine(float time, string target, Action onComplete)
        {
            Debug.Log($""[CustomCommand] 開始: target={target}, time={time}"");
            yield return new WaitForSeconds(time);
            Debug.Log(""[CustomCommand] 完了"");
            
            // 処理完了時に必ず呼ぶ
            onComplete?.Invoke();
        }
    }
}";
            CreateScriptAsset("CustomCommandHandler.cs", template);
        }

        [MenuItem("Assets/Create/Dialogue System/Custom Branch Handler", false, 81)]
        public static void CreateBranchHandlerTemplate()
        {
            string template = @"/*
=========================================================
【AI生成用プロンプト・実装ルール】
AIアシスタントにこのスクリプトを渡して処理を実装させる場合は、以下のルールを厳守させてください。

1. [属性の付与] クラス直上に必ず `[HandlerInfo(""分岐条件の説明"", ""使い方: ノードのBranch設定方法"")]` を記述すること。
2. [必須実装] `IDialogueBranchHandler` インターフェースを実装すること。
3. [登録処理] `Start()` 内で `DialogueBranchDispatcher.Instance.RegisterHandler(this);` を行うこと。
4. [分岐判定] `TryHandleBranch` 内で `choices[0].branchType != this.GetType().Name` の場合は処理せず `false` を返すこと。
5. [条件処理] `ChoiceData` の `conditionKey` や `conditionValue` を用いて条件判定を行うこと。
6. [決定通知] 条件を満たす選択肢が見つかった場合は `onBranchDecided?.Invoke(選択肢のtargetNodeID);` を呼び出し、`true` を返すこと。条件を満たさない場合は `false` を返すこと。
=========================================================
*/
using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Branching;

namespace Runtime.Dialogue.Branching
{
    [HandlerInfo(""ここに分岐ハンドラーの説明を記述します"", ""使い方: 選択肢のBranch Typeから選択し、必要に応じてKey/Valを設定します"")]
    public class CustomBranchHandler : MonoBehaviour, IDialogueBranchHandler
    {
        // 優先度（数値が大きいほど先に判定される）
        public int Priority => 50;

        private void Start()
        {
            if (DialogueBranchDispatcher.Instance != null)
            {
                DialogueBranchDispatcher.Instance.RegisterHandler(this);
            }
        }

        public bool TryHandleBranch(List<ChoiceData> choices, Action<string> onBranchDecided)
        {
            if (choices == null || choices.Count == 0) return false;

            // 自身のクラス名とエディタで選ばれた名前が一致するか判定
            if (choices[0].branchType != this.GetType().Name) return false;

            // TODO: ここに具体的な分岐条件のロジックを記述します
            // 例: 条件を満たした場合は onBranchDecided?.Invoke(choices[0].targetNodeID); を呼んで true を返す

            return false;
        }
    }
}";
            CreateScriptAsset("CustomBranchHandler.cs", template);
        }

        private static void CreateScriptAsset(string defaultName, string content)
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (Directory.Exists(path) == false)
            {
                path = Path.GetDirectoryName(path);
            }

            string fullPath = Path.Combine(path, defaultName);
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);

            File.WriteAllText(fullPath, content, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();

            Object obj = AssetDatabase.LoadAssetAtPath<Object>(fullPath);
            Selection.activeObject = obj;
            EditorUtility.FocusProjectWindow();
        }
    }
}
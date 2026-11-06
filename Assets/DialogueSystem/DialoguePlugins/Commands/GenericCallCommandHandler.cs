using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

namespace Runtime.Dialogue.Plugins.Commands
{
    [Serializable]
    public class DirectTargetMapping
    {
        [Tooltip("タグで指定する識別ID（例: CameraManager, SoundManager）")]
        public string targetID;
        [Tooltip("対象のGameObject")]
        public GameObject targetObject;
    }

    [HandlerInfo(
        description: @"任意のGameObjectにアタッチされたスクリプトのメソッドを、柔軟な指定方式（話しかけている対象・最近接・カスタムタグ・Inspector登録）で動的に実行するコマンドハンドラーです。
引数の有無や型（string/int/float/bool）に応じたオーバーロード解決に対応しており、呼び出し失敗時には原因（オブジェクト未検出、スクリプト不在、メソッド名違い、引数不一致、実行時例外など）を詳細にログ出力します。",
        usage: @"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【基本構文】
[call:mode=方式, script=スクリプト名, method=メソッド名, arg=引数]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【パラメータ一覧】
・script  : 【推奨】実行対象のスクリプト名（クラス名）。
            例: script=SceneLoader, script=EnemyController
・method  : 【必須】実行するメソッド名。引数0個または1個のメソッドが呼べます。
            例: method=LoadScene, method=PlayAnimation, method=TakeDamage
・arg     : 【任意】メソッドに渡す引数（省略時は引数なしメソッドを実行）。
            対応型: string, int, float, bool
            例: arg=Minigame1, arg=10, arg=1.5, arg=true
・mode    : 【任意】対象オブジェクトの特定方式（省略時のデフォルトは 'target'）。
            - target  : 現在話しかけている相手（NPC等）を自動指定【デフォルト】
            - nearest : 基準位置（MainCamera）から最も近いオブジェクトを自動検索
            - tag     : DialogueCustomTag の tag 名で検索
            - direct  : Inspectorの Direct Targets リストの登録名で指定

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【各 mode の詳細と使用例】

1. mode=target（話しかけている対象を自動指定 / 省略可）
   会話開始時（DialogueStarter等）に渡された対象、または会話ノードの話者名（Speaker Name）と
   同名のオブジェクトを自動解決します。
   ・シーン遷移（引数あり）:
     [call:script=SceneLoader,method=LoadScene,arg=Minigame1]
   ・デフォルトシーン遷移（引数なし）:
     [call:script=SceneLoader,method=LoadScene]
   ・NPCにアイテムを渡す:
     [call:script=NPCController,method=ReceiveItem,arg=Potion]

2. mode=nearest（最近接オブジェクト）
   プレイヤーやカメラに最も近い指定スクリプトを持つオブジェクトを実行します。
   ・最も近い敵を攻撃:
     [call:mode=nearest,script=EnemyController,method=Attack]

3. mode=tag（DialogueCustomTag で指定）
   オブジェクトに DialogueCustomTag をアタッチし、TagName で特定します。
   ・ボス演出の再生:
     [call:mode=tag,tag=Boss,method=PlayAnim,arg=Roar]

4. mode=direct（事前登録IDで指定）
   GenericCallCommandHandler の Inspector (Direct Targets) に登録した ID で実行します。
   ・カメラシェイクの実行:
     [call:mode=direct,target=CameraRig,script=CameraShake,method=Shake]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
【エラーハンドリング】
設定に誤りがある場合、Unityコンソールに以下の段階別エラーが出力されます：
1. [対象オブジェクト未検出] : 指定された相手がシーン内に見つからない
2. [スクリプト未検出]     : 対象にアタッチされているスクリプト一覧と要求スクリプトを比較表示
3. [メソッド未検出]       : 対象クラスに定義されているメソッド一覧と要求メソッドを比較表示
4. [引数の不適合]         : 定義されているオーバーロード一覧と渡された引数を比較表示
5. [メソッド内部の例外]   : 実行先メソッドで発生した例外とスタックトレースを表示"
    )]
    public class GenericCallCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "call";

        [Header("指定方式 (direct): Inspector登録リスト")]
        [SerializeField] private List<DirectTargetMapping> directTargets = new List<DirectTargetMapping>();

        [Header("距離判定の基準位置（空ならCamera.mainを使用）")]
        [SerializeField] private Transform referenceTransform;

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }

            if (referenceTransform == null && Camera.main != null)
            {
                referenceTransform = Camera.main.transform;
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            string mode = command.GetString("mode", "").ToLower();
            if (string.IsNullOrEmpty(mode))
            {
                if (command.Arguments.ContainsKey("target"))
                {
                    mode = "direct";
                }
                else if (command.Arguments.ContainsKey("tag"))
                {
                    mode = "tag";
                }
                else
                {
                    mode = "target"; // デフォルトで話しかけている対象
                }
            }

            string scriptName = command.GetString("script", "");
            string methodName = command.GetString("method", "");
            string arg = command.Arguments.TryGetValue("arg", out string val) ? val : null;

            // 1. 対象オブジェクトの探索
            GameObject targetObj = ResolveTargetObject(mode, command, scriptName, out string targetErrorDetail);
            if (targetObj == null)
            {
                Debug.LogError($"[GenericCall 失敗: 対象オブジェクト未検出]\n" +
                               $"【指定Mode】: '{mode}' | Script: '{(string.IsNullOrEmpty(scriptName) ? "(未指定)" : scriptName)}'\n" +
                               $"【失敗詳細】: {targetErrorDetail}\n" +
                               $"【対処法】: 対象オブジェクトがアクティブか、名前・タグ・Inspector登録設定を確認してください。");
                onComplete?.Invoke();
                return;
            }

            // 2. メソッド名の指定確認
            if (string.IsNullOrEmpty(methodName))
            {
                Debug.LogError($"[GenericCall 失敗: メソッド名未指定]\n" +
                               $"【対象オブジェクト】: '{targetObj.name}'\n" +
                               $"【対処法】: [call:method=実行したいメソッド名] を指定してください。");
                onComplete?.Invoke();
                return;
            }

            // 3. メソッド呼び出し（スクリプト検索・メソッド検索・オーバーロード解決・実行時例外捕捉）
            InvokeMethodWithFeedback(targetObj, scriptName, methodName, arg);

            onComplete?.Invoke();
        }

        /// <summary>
        /// 指定された mode に基づいて対象の GameObject を探索・解決します。
        /// 失敗時は errorDetail に詳細な理由を格納します。
        /// </summary>
        private GameObject ResolveTargetObject(string mode, DialogueCommand command, string scriptName, out string errorDetail)
        {
            errorDetail = string.Empty;
            GameObject targetObj = null;

            switch (mode)
            {
                case "target":
                case "current":
                case "interact":
                case "speaker":
                    targetObj = FindCurrentInteractTarget(scriptName, out errorDetail);
                    break;

                case "nearest":
                    targetObj = FindNearestObjectWithScript(scriptName, out errorDetail);
                    break;

                case "tag":
                    string customTag = command.GetString("tag", "");
                    if (string.IsNullOrEmpty(customTag))
                    {
                        errorDetail = "tagパラメータが指定されていません（例: [call:mode=tag,tag=Boss]）";
                        return null;
                    }
                    targetObj = FindObjectByCustomTag(customTag, out errorDetail);
                    break;

                case "direct":
                default:
                    string targetID = command.GetString("target", "");
                    if (string.IsNullOrEmpty(targetID))
                    {
                        errorDetail = "targetパラメータが指定されていません（例: [call:mode=direct,target=CameraManager]）";
                        return null;
                    }
                    var mapping = directTargets.Find(t => t.targetID.Equals(targetID, StringComparison.OrdinalIgnoreCase));
                    if (mapping == null)
                    {
                        var registeredIDs = directTargets.Where(t => !string.IsNullOrEmpty(t.targetID)).Select(t => $"'{t.targetID}'");
                        errorDetail = $"Inspector の Direct Targets リストに ID '{targetID}' が見つかりません。\n" +
                                      $"  ・登録済みID一覧: [{string.Join(", ", registeredIDs)}]";
                    }
                    else if (mapping.targetObject == null)
                    {
                        errorDetail = $"Direct Targets の ID '{targetID}' に紐づく GameObject が None（未アサイン）です。";
                    }
                    else
                    {
                        targetObj = mapping.targetObject;
                    }
                    break;
            }

            return targetObj;
        }

        /// <summary>
        /// 話しかけている対象（NPC等）を多段階で自動解決します。
        /// </summary>
        private GameObject FindCurrentInteractTarget(string scriptName, out string errorDetail)
        {
            // 1. DialogueManager に登録されている CurrentTarget を最優先
            if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentTarget != null)
            {
                errorDetail = string.Empty;
                return DialogueManager.Instance.CurrentTarget;
            }

            // 2. 現在のノードの話者名（CurrentSpeaker）と同名の GameObject を検索（非アクティブ含む）
            string speaker = DialogueManager.Instance != null ? DialogueManager.Instance.CurrentSpeaker : string.Empty;
            if (!string.IsNullOrEmpty(speaker))
            {
                var allObjs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var obj in allObjs)
                {
                    if (obj != null && obj.name.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(scriptName) || HasComponent(obj, scriptName))
                        {
                            errorDetail = string.Empty;
                            return obj;
                        }
                    }
                }
            }

            // 3. フォールバック: 該当スクリプトを持つ中で最も近いオブジェクト
            if (!string.IsNullOrEmpty(scriptName))
            {
                var nearest = FindNearestObjectWithScript(scriptName, out _);
                if (nearest != null)
                {
                    errorDetail = string.Empty;
                    return nearest;
                }
            }

            errorDetail = $"話しかけている対象を特定できませんでした。\n" +
                          $"  ・DialogueManager.CurrentTarget: {(DialogueManager.Instance?.CurrentTarget != null ? "設定済" : "未設定(null)")}\n" +
                          $"  ・現在の話者名: '{(string.IsNullOrEmpty(speaker) ? "(なし)" : speaker)}' (同名のGameObjectがシーン内に存在しないか、指定スクリプトを持っていません)\n" +
                          $"  ・スクリプト '{(string.IsNullOrEmpty(scriptName) ? "(未指定)" : scriptName)}' を持つオブジェクト: シーン内に見つかりませんでした。";
            return null;
        }

        private bool HasComponent(GameObject obj, string scriptName)
        {
            var comps = obj.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c != null && c.GetType().Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // 方式: 最近接オブジェクトの探索
        private GameObject FindNearestObjectWithScript(string scriptName, out string errorDetail)
        {
            errorDetail = string.Empty;
            if (string.IsNullOrEmpty(scriptName))
            {
                errorDetail = "scriptパラメータが指定されていません（例: [call:mode=nearest,script=EnemyController,method=Attack]）";
                return null;
            }

            if (referenceTransform == null && Camera.main != null)
            {
                referenceTransform = Camera.main.transform;
            }

            // Unity 6 対応: FindObjectsByType 使用
            var allMonoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            GameObject nearestObj = null;
            float minDistance = float.MaxValue;

            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null) continue;

                if (mb.GetType().Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase))
                {
                    if (referenceTransform != null)
                    {
                        float dist = Vector3.Distance(referenceTransform.position, mb.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            nearestObj = mb.gameObject;
                        }
                    }
                    else
                    {
                        nearestObj = mb.gameObject;
                        break;
                    }
                }
            }

            if (nearestObj == null)
            {
                errorDetail = $"シーン内にスクリプト '{scriptName}' を持つアクティブな GameObject が1つも見つかりませんでした。";
            }

            return nearestObj;
        }

        // 方式: 自前タグ（DialogueCustomTag）による検索
        private GameObject FindObjectByCustomTag(string tag, out string errorDetail)
        {
            errorDetail = string.Empty;
            if (string.IsNullOrEmpty(tag))
            {
                errorDetail = "tagパラメータが指定されていません。";
                return null;
            }

            // Unity 6 対応: FindObjectsByType 使用
            var tags = UnityEngine.Object.FindObjectsByType<DialogueCustomTag>(FindObjectsSortMode.None);

            foreach (var t in tags)
            {
                if (t != null && t.TagName.Equals(tag, StringComparison.OrdinalIgnoreCase))
                {
                    return t.gameObject;
                }
            }

            var existingTags = tags.Where(t => t != null).Select(t => $"'{t.TagName}'");
            errorDetail = $"シーン内に DialogueCustomTag.TagName が '{tag}' のオブジェクトが見つかりませんでした。\n" +
                          $"  ・シーン内に存在するタグ一覧: [{string.Join(", ", existingTags)}]";
            return null;
        }

        /// <summary>
        /// メソッドの解決と実行、および各段階での詳細なエラーフィードバックを行います。
        /// </summary>
        private void InvokeMethodWithFeedback(GameObject targetObj, string scriptName, string methodName, string arg)
        {
            var components = targetObj.GetComponents<MonoBehaviour>();

            // 1. スクリプト（Component）の探索
            List<MonoBehaviour> targetComponents = new List<MonoBehaviour>();
            if (!string.IsNullOrEmpty(scriptName))
            {
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetComponents.Add(comp);
                    }
                }

                if (targetComponents.Count == 0)
                {
                    var attachedScriptNames = components.Where(c => c != null).Select(c => $"'{c.GetType().Name}'");
                    Debug.LogError($"[GenericCall 失敗: スクリプト未検出]\n" +
                                   $"【対象オブジェクト】: '{targetObj.name}'\n" +
                                   $"【要求スクリプト】: '{scriptName}'\n" +
                                   $"【アタッチされているスクリプト一覧】: [{string.Join(", ", attachedScriptNames)}]\n" +
                                   $"【対処法】: スクリプト名のスペルミス、または対象オブジェクトにコンポーネントがアタッチされているか確認してください。");
                    return;
                }
            }
            else
            {
                // scriptName 未指定の場合はアタッチされている全コンポーネントを対象にする
                targetComponents.AddRange(components.Where(c => c != null));
            }

            // 2. メソッド（MethodInfo）の探索
            List<MethodInfo> candidates = new List<MethodInfo>();
            MonoBehaviour matchedComponent = null;

            foreach (var comp in targetComponents)
            {
                Type type = comp.GetType();
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var m in methods)
                {
                    if (m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(m);
                        matchedComponent = comp;
                    }
                }
                if (candidates.Count > 0) break;
            }

            if (candidates.Count == 0)
            {
                string compName = targetComponents.Count == 1 ? targetComponents[0].GetType().Name : $"{targetComponents.Count}個のコンポーネント";
                var availableMethods = targetComponents[0].GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(m => m.Name).Distinct().Take(12);

                Debug.LogError($"[GenericCall 失敗: メソッド未検出]\n" +
                               $"【対象オブジェクト】: '{targetObj.name}' (Script: {compName})\n" +
                               $"【要求メソッド】: '{methodName}'\n" +
                               $"【定義されている主なメソッド一覧】: [{string.Join(", ", availableMethods)}]\n" +
                               $"【対処法】: メソッド名 '{methodName}' のスペルミスや大文字小文字を確認してください。");
                return;
            }

            // 3. 引数の整合性チェックとオーバーロード解決
            MethodInfo targetMethod = null;
            object[] invokeArgs = null;
            bool hasArg = arg != null;
            string mismatchReason = string.Empty;

            if (hasArg)
            {
                // 引数が指定されている場合: 引数1個のメソッドを優先検索
                foreach (var m in candidates)
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length == 1)
                    {
                        if (TryConvertArgument(arg, parameters[0].ParameterType, out object converted))
                        {
                            targetMethod = m;
                            invokeArgs = new object[] { converted };
                            break;
                        }
                        else
                        {
                            mismatchReason = $"引数 '{arg}' をパラメータの型 '{parameters[0].ParameterType.Name}' に変換できませんでした。";
                        }
                    }
                }

                // 引数1個で見つからない場合、引数なしメソッドにフォールバック可能か確認
                if (targetMethod == null)
                {
                    foreach (var m in candidates)
                    {
                        if (m.GetParameters().Length == 0)
                        {
                            targetMethod = m;
                            invokeArgs = null;
                            break;
                        }
                    }
                }

                if (targetMethod == null && string.IsNullOrEmpty(mismatchReason))
                {
                    mismatchReason = "引数 (arg) が指定されていますが、受け取り可能な引数1個のメソッドが定義されていません。";
                }
            }
            else
            {
                // 引数が指定されていない場合: 引数0個（パラメータなし）のメソッドを優先検索
                foreach (var m in candidates)
                {
                    if (m.GetParameters().Length == 0)
                    {
                        targetMethod = m;
                        invokeArgs = null;
                        break;
                    }
                }

                // 引数0個がない場合、string 引数のメソッド（null渡し）にフォールバック
                if (targetMethod == null)
                {
                    foreach (var m in candidates)
                    {
                        var parameters = m.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                        {
                            targetMethod = m;
                            invokeArgs = new object[] { null };
                            break;
                        }
                    }
                }

                if (targetMethod == null)
                {
                    mismatchReason = "引数 (arg) が未指定ですが、引数なし（0個）で呼び出し可能なメソッドが定義されていません。";
                }
            }

            if (targetMethod == null)
            {
                var candidateSignatures = candidates.Select(m => {
                    var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    return $"{m.ReturnType.Name} {m.Name}({pars})";
                });

                Debug.LogError($"[GenericCall 失敗: 引数の不適合 / オーバーロード解決失敗]\n" +
                               $"【対象オブジェクト】: '{targetObj.name}' (Script: '{matchedComponent?.GetType().Name}')\n" +
                               $"【要求メソッド】: '{methodName}'\n" +
                               $"【渡された引数 (arg)】: {(hasArg ? $"'{arg}'" : "(未指定 / null)")}\n" +
                               $"【定義されているオーバーロード一覧】:\n" +
                               $"  - " + string.Join("\n  - ", candidateSignatures) + "\n" +
                               $"【不適合の理由】: {mismatchReason}\n" +
                               $"【対処法】: メソッド定義に合わせた引数を arg パラメータで指定（または省略）してください（2つ以上の引数は非対応です）。");
                return;
            }

            // 4. メソッドの実行と例外捕捉
            try
            {
                targetMethod.Invoke(matchedComponent, invokeArgs);
            }
            catch (TargetInvocationException tie)
            {
                Exception inner = tie.InnerException ?? tie;
                Debug.LogError($"[GenericCall 失敗: 呼び出し先メソッド内部で例外が発生しました]\n" +
                               $"【実行対象】: '{matchedComponent.GetType().Name}.{targetMethod.Name}' on '{targetObj.name}'\n" +
                               $"【例外型】: {inner.GetType().FullName}\n" +
                               $"【メッセージ】: {inner.Message}\n" +
                               $"【スタックトレース】:\n{inner.StackTrace}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GenericCall 失敗: リフレクション実行エラー]\n" +
                               $"【実行対象】: '{matchedComponent?.GetType().Name}.{targetMethod.Name}' on '{targetObj.name}'\n" +
                               $"【例外メッセージ】: {ex.Message}\n" +
                               $"【スタックトレース】:\n{ex.StackTrace}");
            }
        }

        private bool TryConvertArgument(string rawArg, Type targetType, out object result)
        {
            result = null;
            if (targetType == typeof(string))
            {
                result = rawArg;
                return true;
            }
            if (targetType == typeof(int) && int.TryParse(rawArg, out int intVal))
            {
                result = intVal;
                return true;
            }
            if (targetType == typeof(float) && float.TryParse(rawArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
            {
                result = floatVal;
                return true;
            }
            if (targetType == typeof(bool))
            {
                if (bool.TryParse(rawArg, out bool boolVal))
                {
                    result = boolVal;
                    return true;
                }
                if (rawArg.Equals("true", StringComparison.OrdinalIgnoreCase) || rawArg.Equals("1") || rawArg.Equals("t", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }
                if (rawArg.Equals("false", StringComparison.OrdinalIgnoreCase) || rawArg.Equals("0") || rawArg.Equals("f", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }
            }
            return false;
        }

        public void ForceComplete(DialogueCommand command)
        {
            Execute(command, null);
        }
    }
}

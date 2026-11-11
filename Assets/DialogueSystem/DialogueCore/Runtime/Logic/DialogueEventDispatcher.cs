using System;
using System.Collections.Generic;
using UnityEngine;
using Runtime.Dialogue.Core;

namespace Runtime.Dialogue
{
    /// <summary>
    /// 各コマンドの実行を、登録されたハンドラーに丸投げするディスパッチャ。
    /// - シーン上の既存ハンドラーの自動スキャン
    /// - 外部からの RegisterHandler による動的登録
    /// - 未登録コマンド到来時の自動型探索・自動インスタンス化（フォールバック）
    /// を備え、コマンドが未登録のまま取りこぼされるのを防ぎます。
    /// </summary>
    public class DialogueEventDispatcher : MonoBehaviour
    {
        public static DialogueEventDispatcher Instance { get; private set; }

        // コマンド名と、それを処理するクラス(ハンドラー)の辞書
        private Dictionary<string, IDialogueCommandHandler> handlers = new Dictionary<string, IDialogueCommandHandler>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ScanAndRegisterSceneHandlers();
        }

        private void Start()
        {
            ScanAndRegisterSceneHandlers();
        }

        /// <summary>
        /// シーン上に存在するすべての IDialogueCommandHandler を自動検出して登録します
        /// </summary>
        public void ScanAndRegisterSceneHandlers()
        {
            var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMonoBehaviours)
            {
                if (mb is IDialogueCommandHandler handler)
                {
                    RegisterHandler(handler);
                }
            }
        }

        /// <summary>
        /// 外部のスクリプトから新しい演出コマンドをシステムに登録する
        /// </summary>
        public void RegisterHandler(IDialogueCommandHandler handler)
        {
            if (handler == null || string.IsNullOrEmpty(handler.TargetCommandName)) return;

            string key = handler.TargetCommandName.ToLower();
            if (!handlers.ContainsKey(key))
            {
                handlers.Add(key, handler);
                Debug.Log($"[DialogueEventDispatcher] コマンドハンドラー登録: '{key}' -> {handler.GetType().Name}");
            }
        }

        public void ExecuteCommand(DialogueCommand command, Action onComplete)
        {
            if (command == null)
            {
                onComplete?.Invoke();
                return;
            }

            string key = command.CommandName?.ToLower() ?? string.Empty;

            if (EnsureHandlerRegistered(key, out var handler))
            {
                handler.Execute(command, () =>
                {
                    command.IsExecuted = true;
                    onComplete?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning($"[Dialogue] 未登録のコマンド '{key}' が来ました。");
                command.IsExecuted = true;
                onComplete?.Invoke();
            }
        }

        public void ForceCompleteCommand(DialogueCommand command)
        {
            if (command == null || command.IsExecuted) return;
            command.IsExecuted = true;

            string key = command.CommandName?.ToLower() ?? string.Empty;

            if (EnsureHandlerRegistered(key, out var handler))
            {
                handler.ForceComplete(command);
            }
        }

        /// <summary>
        /// ハンドラーが未登録の場合、シーン走査およびアセンブリ探索を行って確実に解決します
        /// </summary>
        private bool EnsureHandlerRegistered(string key, out IDialogueCommandHandler handler)
        {
            if (handlers.TryGetValue(key, out handler) && handler != null)
            {
                return true;
            }

            // 1. シーン全体を再走査
            ScanAndRegisterSceneHandlers();
            if (handlers.TryGetValue(key, out handler) && handler != null)
            {
                return true;
            }

            // 2. プロジェクトアセンブリから未配置のハンドラーを自動検出・アタッチ
            AutoDiscoverAndAttachHandler(key);
            if (handlers.TryGetValue(key, out handler) && handler != null)
            {
                return true;
            }

            return false;
        }

        private void AutoDiscoverAndAttachHandler(string commandKey)
        {
            try
            {
                var targetInterface = typeof(IDialogueCommandHandler);
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    string asmName = assembly.GetName().Name;
                    if (asmName.StartsWith("System") || asmName.StartsWith("UnityEngine") || asmName.StartsWith("UnityEditor") || asmName.StartsWith("mscorlib"))
                        continue;

                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch { continue; }

                    foreach (var type in types)
                    {
                        if (targetInterface.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type))
                        {
                            // 既にアタッチ済みか確認
                            var existing = GetComponent(type) as IDialogueCommandHandler;
                            if (existing == null)
                            {
                                existing = gameObject.AddComponent(type) as IDialogueCommandHandler;
                            }

                            if (existing != null)
                            {
                                RegisterHandler(existing);
                                if (existing.TargetCommandName.Equals(commandKey, StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.Log($"[DialogueEventDispatcher] コマンド '{commandKey}' に対応するハンドラー '{type.Name}' を自動アタッチ＆登録しました。");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DialogueEventDispatcher] コマンド自動解決中に例外: {ex.Message}");
            }
        }
    }
}

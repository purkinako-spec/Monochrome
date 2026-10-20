using Runtime.Dialogue.Core;
using Runtime.Dialogue.Audio;
using System;
using UnityEngine;

namespace Runtime.Dialogue.Commands
{
    /// <summary>
    /// [bgm:name=曲名] または [bgm:clip=曲名] を処理するコマンドハンドラー
    /// </summary>
    [HandlerInfo(
    description: @"BGMの再生や停止を行うコマンドハンドラーです[cite: 7]。内部で DialogueAudioManager を呼び出して制御します[cite: 7]。

【コンテナ・シーン側の事前準備】
1. シーン上に空のGameObjectを作成し、音源を管理するオーディオマネージャー（DialogueAudioManager等）と、このハンドラー（BGMCommandHandler）をアタッチします。
2. 再生したいBGMのオーディオクリップが適切なリソースフォルダやマネージャー側のリストに登録されていることを確認してください。

【基本パラメータ】
・曲名を指定して再生: [bgm:name=曲名] または [bgm:clip=曲名]
・BGMの停止: [bgm:stop=true]
・ループ設定の変更: [bgm:name=曲名, loop=false]",
    usage: @"【使用例】
・BGMを再生する: [bgm:name=MainTheme]
・BGMを停止する: [bgm:stop=true]"
)]
    public class BGMCommandHandler : MonoBehaviour, IDialogueCommandHandler
    {
        public string TargetCommandName => "bgm";

        private void Start()
        {
            if (DialogueEventDispatcher.Instance != null)
            {
                DialogueEventDispatcher.Instance.RegisterHandler(this);
            }
        }

        public void Execute(DialogueCommand command, Action onComplete)
        {
            PlayOrStopBGM(command);

            // BGMの切り替えは非同期で進行するため即座にコールバックを消化
            onComplete?.Invoke();
        }

        public void ForceComplete(DialogueCommand command)
        {
            // スキップ時もそのままBGMを正しく適用
            PlayOrStopBGM(command);
        }

        private void PlayOrStopBGM(DialogueCommand command)
        {
            // string として取得してから bool にパース（解析）する
            string stopStr = command.GetString("stop", "false");
            bool isStop = bool.TryParse(stopStr, out bool stopParsed) && stopParsed;

            if (isStop)
            {
                DialogueAudioManager.Instance?.StopBGM();
                return;
            }

            // "name" または "clip" からキーを取得
            string bgmKey = command.GetString("name", command.GetString("clip", ""));

            string loopStr = command.GetString("loop", "true");
            bool isLoop = !bool.TryParse(loopStr, out bool loopParsed) || loopParsed;

            if (!string.IsNullOrEmpty(bgmKey))
            {
                DialogueAudioManager.Instance?.PlayBGM(bgmKey, isLoop);
            }
        }
    }
}
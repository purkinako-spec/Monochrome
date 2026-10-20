using System;
using UnityEngine;
using Runtime.Dialogue;
using Runtime.Dialogue.Core;
using Runtime.Dialogue.Logic;

[HandlerInfo(
    description: @"立ち絵の表示・表情切り替え・移動・フェード演出を行うコマンドハンドラーです。
スロット枠を固定したまま、キャラクター固有の AnimatorController を動的に割り当ててアニメーション（motion）を発火できます。

【コンテナ・シーン側の事前準備】
1. シーン上の立ち絵用 UI スロット（Image）に Animator コンポーネントをアタッチし、StandardUIPortraitHandler の UI Slots に登録しておきます[cite: 1, 4]。
2. 各 CharacterProfile（ScriptableObject）の animatorController 欄に、キャラ固有の AnimatorController（Triggerを設定したもの）を割り当てます[cite: 1]。

【基本パラメータ】
[portrait:target=キャラID, exp=表情ID, pos=位置ID, fade=秒数, moveTime=秒数, motion=トリガー名]

【パラメータ解説】
・target: キャラクターID（clear を指定すると非表示処理）[cite: 4]
・exp: CharacterProfile に登録した表情ID[cite: 4]
・pos: 表示するスロット位置（left, center, right 等）[cite: 4]
・fade: フェードイン/アウトにかける時間（秒）[cite: 4]
・moveTime: 現在位置からデフォルト位置へ移動完了するまでの時間（秒）[cite: 4]
・motion: AnimatorController 内で発火させる Trigger 名[cite: 1, 4]",

    usage: @"【使用例】
・立ち絵の表示（フェード・移動・モーション付き）:
  [portrait:target=hero,exp=smile,pos=left,fade=0.5,moveTime=0.3,motion=nod]

・表情とモーションのみ変更:
  [portrait:target=hero,exp=angry,pos=left,motion=shake]

・指定スロットの立ち絵をフェードアウト消去:
  [portrait:target=clear,pos=left,fade=0.5]

・全スロットの立ち絵を消去:
  [portrait:target=clear,pos=all,fade=0.5]"
)]
public class PortraitCommandHandler : MonoBehaviour, IDialogueCommandHandler
{
    public string TargetCommandName => "portrait";

    private void Start()
    {
        if (DialogueEventDispatcher.Instance != null)
        {
            DialogueEventDispatcher.Instance.RegisterHandler(this);
        }
    }

    public void Execute(DialogueCommand command, Action onComplete)
    {
        string target = command.GetString("target", "");
        string exp = command.GetString("exp", "default");
        string pos = command.GetString("pos", "center");

        if (DialoguePortraitDispatcher.Instance != null)
        {
            bool handled = DialoguePortraitDispatcher.Instance.TryHandlePortrait(target, exp, pos, command.Arguments, onComplete);
            if (!handled) onComplete?.Invoke();
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void ForceComplete(DialogueCommand command)
    {
        StopAllCoroutines();

        string target = command.GetString("target", "");
        string exp = command.GetString("exp", "default");
        string pos = command.GetString("pos", "center");

        if (DialoguePortraitDispatcher.Instance != null)
        {
            DialoguePortraitDispatcher.Instance.ForceCompletePortrait(target, exp, pos, command.Arguments);
        }
    }
}
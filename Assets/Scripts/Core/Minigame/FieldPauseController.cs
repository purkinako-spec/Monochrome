using System;
using System.Collections.Generic;
using UnityEngine;
using Monochrome.Core.Character;
using Monochrome.Core.Input;

namespace Monochrome.Core.Minigame
{
    /// <summary>
    /// ミニゲーム中や全画面イベント中に、Time.timeScale を変更することなく
    /// フィールド側の画面描画・音声・プレイヤー入力を安全に完全休止・復帰させるコントローラー。
    /// </summary>
    public static class FieldPauseController
    {
        private static Camera disabledCamera;
        private static AudioListener disabledAudioListener;
        private static readonly List<AudioSource> pausedAudioSources = new List<AudioSource>();

        private static bool isPaused = false;
        public static bool IsPaused => isPaused;

        /// <summary>
        /// フィールドを休止状態にします（カメラOFF、BGM一時停止、入力ロック）
        /// </summary>
        public static void PauseField()
        {
            if (isPaused) return;
            isPaused = true;

            // 1. プレイヤー入力をロック
            if (PlayerController2D.LocalPlayer != null)
            {
                PlayerController2D.LocalPlayer.BlockState.AddBlocker(ActionBlocker.Minigame);
                PlayerController2D.LocalPlayer.Motor.StopImmediately();
            }

            // 2. メインカメラを一時無効化（ミニゲーム側のカメラのみを描画させる）
            disabledCamera = Camera.main;
            if (disabledCamera != null)
            {
                disabledCamera.enabled = false;
                disabledAudioListener = disabledCamera.GetComponent<AudioListener>();
                if (disabledAudioListener != null)
                {
                    disabledAudioListener.enabled = false;
                }
            }

            // 3. フィールドで現在鳴っている AudioSource を一時停止
            pausedAudioSources.Clear();
            var allAudio = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var audio in allAudio)
            {
                if (audio != null && audio.isPlaying)
                {
                    audio.Pause();
                    pausedAudioSources.Add(audio);
                }
            }

            Debug.Log("[FieldPauseController] フィールドを完全休止状態に移行しました。");
        }

        /// <summary>
        /// フィールドを通常状態に復帰させます
        /// </summary>
        public static void ResumeField()
        {
            if (!isPaused) return;
            isPaused = false;

            // 1. メインカメラと AudioListener を再開
            if (disabledCamera != null)
            {
                disabledCamera.enabled = true;
                if (disabledAudioListener != null)
                {
                    disabledAudioListener.enabled = true;
                }
                disabledCamera = null;
                disabledAudioListener = null;
            }

            // 2. 一時停止していた BGM / AudioSource を再開
            foreach (var audio in pausedAudioSources)
            {
                if (audio != null)
                {
                    audio.UnPause();
                }
            }
            pausedAudioSources.Clear();

            // 3. プレイヤー入力ロックを解除
            if (PlayerController2D.LocalPlayer != null)
            {
                PlayerController2D.LocalPlayer.BlockState.RemoveBlocker(ActionBlocker.Minigame);
            }

            Debug.Log("[FieldPauseController] フィールドを通常状態に復帰させました。");
        }
    }
}

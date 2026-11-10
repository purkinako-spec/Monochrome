using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Monochrome.Core.Input
{
    public enum InputDeviceType
    {
        KeyboardMouse,
        Gamepad,
        Touchscreen
    }

    /// <summary>
    /// 現在プレイヤーが使用している入力デバイス（Keyboard/Mouse, Gamepad, Touchscreen）を
    /// リアルタイムで自動判別し、UIプロンプトや操作ガイドの動的切り替えを支援する常駐シングルトン。
    /// </summary>
    [AddComponentMenu("Monochrome/Core/Input Device Manager")]
    [DisallowMultipleComponent]
    public class InputDeviceManager : MonoBehaviour
    {
        private static InputDeviceManager instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static InputDeviceManager Instance
        {
            get
            {
                if (instance == null || !instance)
                {
                    instance = FindFirstObjectByType<InputDeviceManager>();
                    if ((instance == null || !instance) && Application.isPlaying)
                    {
                        var go = new GameObject("[InputDeviceManager]");
                        instance = go.AddComponent<InputDeviceManager>();
                        if (go.transform.parent == null) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("Current Status")]
        [SerializeField] private InputDeviceType currentDeviceType = InputDeviceType.KeyboardMouse;
        public InputDeviceType CurrentDeviceType => currentDeviceType;

        /// <summary>
        /// 使用デバイスが切り替わったときに発火するイベント (newDeviceType)
        /// </summary>
        public event Action<InputDeviceType> OnDeviceChanged;

        private void Awake()
        {
            if (instance == null || !instance)
            {
                instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                DetectInitialDevice();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            InputSystem.onActionChange += HandleActionChange;
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= HandleActionChange;
        }

        private void DetectInitialDevice()
        {
            if (Application.isMobilePlatform && Touchscreen.current != null)
            {
                SetDevice(InputDeviceType.Touchscreen);
            }
            else if (Gamepad.current != null)
            {
                SetDevice(InputDeviceType.Gamepad);
            }
            else
            {
                SetDevice(InputDeviceType.KeyboardMouse);
            }
        }

        private void HandleActionChange(object obj, InputActionChange change)
        {
            if (change == InputActionChange.ActionPerformed && obj is InputAction action)
            {
                var control = action.activeControl;
                if (control != null)
                {
                    var device = control.device;
                    if (device is Gamepad)
                    {
                        SetDevice(InputDeviceType.Gamepad);
                    }
                    else if (device is Touchscreen)
                    {
                        SetDevice(InputDeviceType.Touchscreen);
                    }
                    else if (device is Keyboard || device is Mouse)
                    {
                        SetDevice(InputDeviceType.KeyboardMouse);
                    }
                }
            }
        }

        private void SetDevice(InputDeviceType newType)
        {
            if (currentDeviceType != newType)
            {
                currentDeviceType = newType;
                OnDeviceChanged?.Invoke(newType);
                Debug.Log($"[InputDeviceManager] 入力デバイスを検出・切り替えました: {newType}");
            }
        }
    }
}

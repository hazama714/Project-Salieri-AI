// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Expression.Voice
{
    public sealed class AndroidTtsVoiceOutput : MonoBehaviour, IVoiceOutput
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject tts;
        private bool initialized;
#endif

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaClass unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            tts = new AndroidJavaObject(
                "android.speech.tts.TextToSpeech",
                activity,
                new TtsInitListener(this)
            );
#endif
        }

        public void Speak(string text, string speakerName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (tts == null || !initialized)
                return;

            tts.Call<int>(
                "speak",
                text,
                0,
                null,
                "salieri_tts"
            );
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnTtsInitialized(int status)
        {
            initialized = status == 0;

            if (initialized && tts != null)
            {
                AndroidJavaObject locale =
                    new AndroidJavaClass("java.util.Locale")
                        .GetStatic<AndroidJavaObject>("JAPAN");

                tts.Call<int>("setLanguage", locale);
            }
        }

        private sealed class TtsInitListener : AndroidJavaProxy
        {
            private readonly AndroidTtsVoiceOutput owner;

            public TtsInitListener(AndroidTtsVoiceOutput owner)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                this.owner = owner;
            }

            public void onInit(int status)
            {
                owner.OnTtsInitialized(status);
            }
        }
#endif

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null)
            {
                tts.Call("stop");
                tts.Call("shutdown");
                tts.Dispose();
                tts = null;
            }
#endif
        }
    }
}
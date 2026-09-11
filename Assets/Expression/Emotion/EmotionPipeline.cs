// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// Version: Salieri.EmotionPipeline v1.0.4
// Timestamp (JST): 2025-10-15 01:58
// Comment: タイムアウト制御をExtractor側に一本化。Pipeline側の外側タイムアウト処理は撤去。
//          既存のAuto-bind・イベント設計・例外フォールバックは維持（最小変更）。

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Salieri.Emotion
{
    [Serializable] public sealed class EmotionResultEvent : UnityEvent<EmotionResult> { }

    public sealed class EmotionPipeline : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("LLMランタイムHost（GenerateSafeAsync を持つ）")]
        public MonoBehaviour runtimeHost; // LocalLLMRuntimeHost を推奨

        [Header("Events")]
        public EmotionResultEvent OnEmotionReady;

        private Func<string, Task<string>> _llmCall;
        private EmotionExtractor _extractor;

        void Awake()
        {
            if (!BindHost(runtimeHost))
            {
                TryAutoBind();
            }

            if (_llmCall == null)
            {
                Debug.LogError("[EmotionPipeline] runtimeHost.GenerateSafeAsync not found");
                return;
            }

            // タイムアウトは Extractor 内に統一（25s既定）
            _extractor = new EmotionExtractor(_llmCall);
        }

        private bool BindHost(MonoBehaviour host)
        {
            if (host == null) return false;

            // GenerateSafeAsync(string) : Task<string>
            var mi = host.GetType().GetMethod("GenerateSafeAsync",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            if (mi == null) return false;
            if (mi.ReturnType != typeof(Task<string>)) return false;

            _llmCall = (prompt) => (Task<string>)mi.Invoke(host, new object[] { prompt });
            return true;
        }

        private void TryAutoBind()
        {
            var all = FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            foreach (var mb in all)
            {
                if (BindHost(mb))
                {
                    runtimeHost = mb;
                    Debug.Log($"[EmotionPipeline] Auto-bound host: {mb.GetType().Name}");
                    return;
                }
            }

            var cand = all.FirstOrDefault(mb =>
                mb.GetType().Name.IndexOf("LocalLLMRuntimeHost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mb.gameObject.name.IndexOf("LocalLLMRuntimeHost", StringComparison.OrdinalIgnoreCase) >= 0);

            if (cand != null && BindHost(cand))
            {
                runtimeHost = cand;
                Debug.Log($"[EmotionPipeline] Auto-bound host by name: {cand.GetType().Name}");
            }
        }

        /// <summary>本文が得られた直後にUIから呼ぶ。感情を抽出し、イベントで通知。</summary>
        public async void AnalyzeAndApply(string text)
        {
            if (_extractor == null) return;

            try
            {
                var emo = await _extractor.ExtractAsync(text);
                OnEmotionReady?.Invoke(emo ?? new EmotionResult { primary = "Neutral", intensity = 0f, blendshapes = Array.Empty<Blendshape>() });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmotionPipeline] exception: {ex.GetType().Name} {ex.Message}");
                OnEmotionReady?.Invoke(new EmotionResult { primary = "Neutral", intensity = 0f, blendshapes = Array.Empty<Blendshape>() });
            }
        }
    }
}

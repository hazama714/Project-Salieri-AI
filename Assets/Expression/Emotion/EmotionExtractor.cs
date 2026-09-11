// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// Version: Salieri.EmotionExtractor v1.3.0 (Tag-Only)
// Timestamp (JST): 2025-10-15 02:15
// Comment:
// - 仕様を「感情タグ1つのみ」に縮退。
// - LLM出力は {"primary":"Joy|Angry|Sorrow|Fun|Neutral"} のみ。
// - 戻り値は EmotionResult 互換で返すが、intensity=0, blendshapes=[] に固定。
// - 過剰機能（口形/AIEOU, Blink, weight 等）は完全に無視。安定最優先。
// - タイムアウトは Extractor 内のみ（25s）。キー名のゆらぎ（emotion/sentiment）も吸収。

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Salieri.Emotion
{
    [Serializable]
    public sealed class EmotionResult
    {
        public string primary;          // "Joy" | "Angry" | "Sorrow" | "Fun" | "Neutral"
        public float intensity;         // 常に 0.0 固定（タグのみ仕様）
        public Blendshape[] blendshapes;// 常に空配列（タグのみ仕様）
    }

    [Serializable]
    public sealed class Blendshape
    {
        public string key;
        public float weight;
    }

    public sealed class EmotionExtractor
    {
        private readonly Func<string, Task<string>> _llmCall;
        private readonly int _timeoutMs; // Extractor内でのみタイムアウト制御

        // --- タグ専用・極小プロンプト ---
        private const string InstrTagOnly =
@"Output ONLY ONE JSON object and nothing else.
JSON must have exactly one field: {""primary"":""Joy|Angry|Sorrow|Fun|Neutral""}

TEXT:
{TEXT}
END

Return JSON only.";

        private const string RetryInstrSuffix =
@"IMPORTANT: Wrap the JSON by <json> and </json>. No extra text.
Example:
<json>{""primary"":""Neutral""}</json>";

        public EmotionExtractor(Func<string, Task<string>> llmCall, int timeoutMs = 25000)
        {
            _llmCall = llmCall;
            _timeoutMs = Math.Max(5000, timeoutMs); // 安全下限5s
        }

        public async Task<EmotionResult> ExtractAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return MakeResult("Neutral");

                // 1回目（極小）
                var prompt = InstrTagOnly.Replace("{TEXT}", text);
                var raw = await CallWithTimeout(prompt).ConfigureAwait(false);

                var primary = ParsePrimary(NormalizeJson(TryExtractJson(raw)));
                if (primary != null) return MakeResult(primary);

                // 2回目（厳格）
                var retryPrompt = prompt + RetryInstrSuffix;
                var raw2 = await CallWithTimeout(retryPrompt).ConfigureAwait(false);

                var primary2 = ParsePrimary(NormalizeJson(TryExtractJson(raw2)));
                if (primary2 != null) return MakeResult(primary2);

                Debug.LogWarning($"[EmotionExtractor] Tag parse failed twice. Raw1: {Trunc(raw)} | Raw2: {Trunc(raw2)}");
                return MakeResult("Neutral");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmotionExtractor] exception: {ex.GetType().Name} {ex.Message}");
                return MakeResult("Neutral");
            }
        }

        // ==== LLM呼び出し（Extractorのみでタイムアウト制御） ====
        private async Task<string> CallWithTimeout(string prompt)
        {
            try
            {
                var t = _llmCall(prompt);
                var done = await Task.WhenAny(t, Task.Delay(_timeoutMs));
                if (done != t)
                {
                    Debug.LogWarning("[EmotionExtractor] call timeout");
                    return "{}";
                }
                return t.Result ?? "{}";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EmotionExtractor] call failed: {ex.GetType().Name}");
                return "{}";
            }
        }

        // ==== JSON 抽出（安全版） ====
        private static string TryExtractJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "{}";

            // <json> ... </json>
            int s = raw.IndexOf("<json>", StringComparison.OrdinalIgnoreCase);
            int e = raw.LastIndexOf("</json>", StringComparison.OrdinalIgnoreCase);
            if (s >= 0 && e > s)
            {
                var inner = raw.Substring(s + 6, e - (s + 6)).Trim();
                var tagJson = Braced(inner);
                if (tagJson != null) return tagJson;
            }

            // ```json ... ``` フェンス
            const string fence = "```";
            int f1 = raw.IndexOf(fence, StringComparison.Ordinal);
            if (f1 >= 0)
            {
                int f2 = raw.IndexOf(fence, f1 + fence.Length, StringComparison.Ordinal);
                if (f2 > f1)
                {
                    var inner = raw.Substring(f1 + fence.Length, f2 - (f1 + fence.Length)).Trim();
                    if (inner.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                        inner = inner.Substring(4).Trim();
                    var fenceJson = Braced(inner);
                    if (fenceJson != null) return fenceJson;
                }
            }

            // { ... }
            var braces = Braced(raw);
            if (braces != null) return braces;

            return "{}";
        }

        private static string Braced(string s)
        {
            int i = s.IndexOf('{');
            int j = s.LastIndexOf('}');
            if (i >= 0 && j >= i) return s.Substring(i, j - i + 1);
            return null;
        }

        // ==== 正規化（全角/キー別名/余分テキスト排除） ====
        private static string NormalizeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s);
            // 全角 → 半角（最低限）
            sb.Replace('：', ':');
            sb.Replace('”', '"').Replace('“', '"').Replace('’', '"').Replace('‘', '"');

            var outStr = sb.ToString();

            // キー名の別名（emotion/sentiment → primary）
            outStr = RegexReplace(outStr, @"(?<q>""|\')(?<k>emotion|sentiment)(\k<q>)\s*:", m => "\"primary\":");

            // 余剰フィールドを粗く消す必要はない（タグのみを後で抽出）のでここまで
            return outStr;
        }

        // ==== primary の抽出（寛容） ====
        private static string ParsePrimary(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            // 1) 正規のJSONキー "primary":"xxx"
            var m = Regex.Match(json, @"""primary""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return CanonicalizePrimary(m.Groups[1].Value);
            }

            // 2) 万一 JSONが崩れていても単語を拾う（joy/angry/sorrow/fun/neutral）
            var t = json.ToLowerInvariant();
            if (t.Contains("joy") || t.Contains("happy") || t.Contains("嬉") || t.Contains("喜")) return "Joy";
            if (t.Contains("ang") || t.Contains("怒")) return "Angry";
            if (t.Contains("sorr") || t.Contains("sad") || t.Contains("哀") || t.Contains("悲")) return "Sorrow";
            if (t.Contains("fun") || t.Contains("楽") || t.Contains("面白")) return "Fun";
            if (t.Contains("neutral") || t.Contains("無") || t.Contains("平")) return "Neutral";

            return null;
        }

        private static string CanonicalizePrimary(string p)
        {
            if (string.IsNullOrEmpty(p)) return "Neutral";
            var t = p.Trim().ToLowerInvariant();
            if (t.StartsWith("joy") || t.Contains("happy") || t.Contains("嬉") || t.Contains("喜")) return "Joy";
            if (t.StartsWith("ang") || t.Contains("怒")) return "Angry";
            if (t.StartsWith("sorr") || t.Contains("sad") || t.Contains("哀") || t.Contains("悲")) return "Sorrow";
            if (t.StartsWith("fun") || t.Contains("楽") || t.Contains("面白")) return "Fun";
            if (t.StartsWith("neutral") || t.Contains("無") || t.Contains("平")) return "Neutral";
            return "Neutral";
        }

        // ==== 結果の構築（タグのみ仕様） ====
        private static EmotionResult MakeResult(string primary)
        {
            return new EmotionResult
            {
                primary = CanonicalizePrimary(primary),
                intensity = 0f,
                blendshapes = Array.Empty<Blendshape>() // 常に空
            };
        }

        private static string RegexReplace(string input, string pattern, MatchEvaluator eval)
        {
            try { return Regex.Replace(input, pattern, eval); }
            catch { return input; }
        }

        private static string Trunc(string s, int max = 160)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max) + " …";
        }
    }
}

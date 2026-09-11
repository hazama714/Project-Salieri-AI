// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SalieriAI.Core.LLM.Common;
using SalieriAI.Persona;

namespace SalieriAI.CloudLLM
{
    public sealed class CloudLLMClient : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private CloudLLMSettings settings;

        [Header("Persona")]
        [SerializeField] private PersonaPromptLoader personaPromptLoader;

        [Header("Output")]
        [SerializeField] private int actionMaxOutputTokens = 96;
        [SerializeField] private int speechMaxOutputTokens = 64;
        [SerializeField] private int conversationMaxOutputTokens = 96;

        private const string Endpoint = "https://api.openai.com/v1/responses";

        public async Task<LLMActionDecision> GenerateActionDecisionAsync(global::SelfStateSummary summary)
        {
            string prompt = CloudActionPromptBuilder.BuildCloudActionPrompt(summary);

            string result = await SendRequestAsync(
                prompt,
                CloudActionPromptBuilder.BuildCloudActionInstructions(personaPromptLoader),
                actionMaxOutputTokens > 0 ? actionMaxOutputTokens : GetDefaultMaxOutputTokens(),
                "ACTION"
            );

            LLMActionDecision decision = ParseActionDecision(result);
            decision.Normalize();
            return decision;
        }

        public async Task<string> GenerateConversationResponseJsonAsync(
            string userText
        )
        {
            return await GenerateConversationResponseJsonAsync(
                ConversationGenerationRequest.FromText(userText));
        }

        public async Task<string> GenerateConversationResponseJsonAsync(
            ConversationGenerationRequest request
        )
        {
            string prompt =
                CloudConversationPromptBuilder.BuildConversationPrompt(
                    request
                );

            string result = await SendRequestAsync(
                prompt,
                CloudConversationPromptBuilder.BuildConversationInstructions(
                    personaPromptLoader
                ),
                conversationMaxOutputTokens > 0
                    ? conversationMaxOutputTokens
                    : speechMaxOutputTokens,
                "CONVERSATION"
            );

            return result;
        }

        public async Task<string> GenerateSpeechForActionAsync(
            global::SelfStateSummary summary,
            LLMActionDecision actionDecision)
        {
            if (actionDecision == null)
                actionDecision = LLMActionDecision.SafeDefault("action_missing");

            actionDecision.Normalize();

            string prompt = CloudSpeechPromptBuilder.BuildCloudSpeechPrompt(summary, actionDecision);

            string result = await SendRequestAsync(
                prompt,
                CloudSpeechPromptBuilder.BuildCloudSpeechInstructions(personaPromptLoader),
                speechMaxOutputTokens,
                "SPEECH"
            );

            return SanitizeSpeech(result);
        }

        public async Task<string> GenerateActionAsync(string prompt)
        {
            string result = await SendRequestAsync(
                prompt,
                CloudActionPromptBuilder.BuildCloudActionInstructions(personaPromptLoader),
                GetDefaultMaxOutputTokens(),
                "ACTION_LEGACY"
            );

            if (string.IsNullOrWhiteSpace(result))
                return string.Empty;

            return SanitizeLegacyActionNumber(result);
        }

        public async Task<string> GenerateActionAsync(
            string prompt,
            global::SelfStateSummary state)
        {
            string cloudPrompt = PromptTextUtility.AppendCloudStateContext(prompt, state);
            return await GenerateActionAsync(cloudPrompt);
        }

        public async Task<string> GenerateSpeechAsync(string prompt)
        {
            string result = await SendRequestAsync(
                prompt,
                CloudSpeechPromptBuilder.BuildCloudSpeechInstructions(personaPromptLoader),
                speechMaxOutputTokens,
                "SPEECH_LEGACY"
            );

            return SanitizeSpeech(result);
        }

        public async Task<string> GenerateSpeechAsync(
            string prompt,
            global::SelfStateSummary state)
        {
            string cloudPrompt = PromptTextUtility.AppendCloudStateContext(prompt, state);
            return await GenerateSpeechAsync(cloudPrompt);
        }

        private int GetDefaultMaxOutputTokens()
        {
            if (settings == null)
                return 32;

            return Mathf.Max(1, settings.maxOutputTokens);
        }

        private async Task<string> SendRequestAsync(
            string prompt,
            string instructions,
            int maxOutputTokens,
            string label)
        {
            if (settings == null)
            {
                Debug.LogWarning("[CloudLLMClient] settings is null.");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(settings.apiKey))
            {
                Debug.LogWarning("[CloudLLMClient] apiKey is empty.");
                return string.Empty;
            }

            Debug.Log("[CloudLLMClient][" + label + "] FINAL PROMPT:\n" + prompt);

            string requestJson = BuildRequestJson(
                prompt,
                instructions,
                maxOutputTokens
            );

            using UnityWebRequest request = new UnityWebRequest(Endpoint, "POST");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = settings.timeoutSeconds;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + settings.apiKey);

            await SendAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    "[CloudLLMClient][" + label + "] ERROR: " +
                    request.responseCode +
                    " / " +
                    request.error +
                    "\n" +
                    request.downloadHandler.text
                );

                return string.Empty;
            }

            Debug.Log("[CloudLLMClient][" + label + "] RESPONSE CODE: " + request.responseCode);

            string rawJson = request.downloadHandler.text;

            Debug.Log("[CloudLLMClient][" + label + "] RAW JSON:\n" + rawJson);

            string text = ExtractText(rawJson);
            text = DecodeJsonStringEscapes(text);

            Debug.Log("[CloudLLMClient][" + label + "] RESULT: " + text);

            return text.Trim();
        }

        private string BuildRequestJson(
            string prompt,
            string instructions,
            int maxOutputTokens)
        {
            string model = settings != null ? settings.model : string.Empty;

            return "{"
                + "\"model\":\"" + EscapeJson(model) + "\","
                + "\"instructions\":\"" + EscapeJson(instructions) + "\","
                + "\"input\":\"" + EscapeJson(prompt) + "\","
                + "\"max_output_tokens\":" + Mathf.Max(1, maxOutputTokens) + ","
                + "\"store\":false"
                + "}";
        }

        private static async Task SendAsync(UnityWebRequest request)
        {
            UnityWebRequestAsyncOperation op = request.SendWebRequest();

            while (!op.isDone)
            {
                await Task.Yield();
            }
        }

        private static string ExtractText(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            MatchCollection matches = Regex.Matches(
                json,
                "\"text\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.Singleline
            );

            if (matches.Count <= 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();

            foreach (Match match in matches)
            {
                sb.Append(match.Groups[1].Value);
            }

            return sb.ToString().Trim();
        }

        private static LLMActionDecision ParseActionDecision(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LLMActionDecision.SafeDefault("cloud_empty");

            string result = DecodeJsonStringEscapes(value).Trim();

            string json = ExtractJsonObject(result);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    LLMActionDecision parsed = JsonUtility.FromJson<LLMActionDecision>(json);
                    if (parsed != null)
                    {
                        parsed.action = NormalizeActionName(parsed.action);
                        parsed.speech = SanitizeSpeech(parsed.speech);
                        parsed.reason = PromptTextUtility.SafeText(parsed.reason);
                        parsed.Normalize();
                        return parsed;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CloudLLMClient] Failed to parse action JSON: " + ex.Message);
                }
            }

            string legacyNumber = SanitizeLegacyActionNumber(result);

            switch (legacyNumber)
            {
                case "0":
                    return LLMActionDecision.SafeDefault("legacy_none");

                case "1":
                    return new LLMActionDecision
                    {
                        action = "lookAround",
                        speech = "",
                        reason = "legacy_lookAround",
                        confidence = 0.7f
                    };

                case "2":
                    return new LLMActionDecision
                    {
                        action = "speakShort",
                        speech = "",
                        reason = "legacy_speakShort",
                        confidence = 0.7f
                    };

                default:
                    return LLMActionDecision.SafeDefault("cloud_parse_failed");
            }
        }

        private static string NormalizeActionName(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return "none";

            switch (action.Trim())
            {
                case "none":
                    return "none";

                case "lookAround":
                case "search":
                case "search_front":
                    return "lookAround";

                case "returnCenter":
                case "look_front":
                case "hold_position":
                    return "returnCenter";

                case "idleNod":
                case "nod":
                    return "idleNod";

                case "speak":
                case "speakShort":
                case "speak_short":
                    return "speakShort";

                default:
                    return "none";
            }
        }

        private static string ExtractJsonObject(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            int start = value.IndexOf('{');
            int end = value.LastIndexOf('}');

            if (start < 0 || end < start)
                return string.Empty;

            return value.Substring(start, end - start + 1);
        }

        private static string SanitizeLegacyActionNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = DecodeJsonStringEscapes(value).Trim();

            Match match = Regex.Match(result, "[0-2]");
            if (!match.Success)
                return string.Empty;

            return match.Value;
        }

        private static string SanitizeSpeech(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = DecodeJsonStringEscapes(value).Trim();

            result = result.Replace("「", "").Replace("」", "");
            result = result.Replace("『", "").Replace("』", "");
            result = result.Replace("\"", "");
            result = result.Replace("'", "");

            int cut = result.IndexOf('\n');
            if (cut >= 0)
                result = result.Substring(0, cut);

            cut = result.IndexOf("===");
            if (cut >= 0)
                result = result.Substring(0, cut);

            cut = result.IndexOf("この指示");
            if (cut >= 0)
                result = result.Substring(0, cut);

            result = result.Trim();

            if (result.Length > 40)
                result = result.Substring(0, 40);

            return result.Trim();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", "\\n");
        }

        private static string DecodeJsonStringEscapes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string result = value;

            result = result
                .Replace("\\n", "\n")
                .Replace("\\r", "")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\/", "/");

            result = Regex.Replace(
                result,
                @"\\u([0-9a-fA-F]{4})",
                match =>
                {
                    string hex = match.Groups[1].Value;
                    int code = int.Parse(
                        hex,
                        System.Globalization.NumberStyles.HexNumber
                    );

                    return char.ConvertFromUtf32(code);
                }
            );

            result = result.Replace("\\\\", "\\");

            return result;
        }
    }
}

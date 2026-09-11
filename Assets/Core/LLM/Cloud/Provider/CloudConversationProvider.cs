// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;
using SalieriAI.Core.LLM.Common;
using UnityEngine;

namespace SalieriAI.CloudLLM
{
    /// <summary>
    /// Thin Cloud conversation provider.
    /// Prompt generation and API access stay inside CloudLLMClient.
    /// </summary>
    public sealed class CloudConversationProvider :
        MonoBehaviour,
        ILLMConversationProvider
    {
        [Header("Cloud")]
        [SerializeField]
        private CloudLLMClient cloudClient;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private void Awake()
        {
            if (cloudClient == null)
                cloudClient = FindObjectOfType<CloudLLMClient>();
        }

        public async Task<ConversationLLMResponse>
            GenerateConversationResponseAsync(string userText)
        {
            return await GenerateConversationResponseAsync(
                ConversationGenerationRequest.FromText(userText));
        }

        public async Task<ConversationLLMResponse>
            GenerateConversationResponseAsync(
                ConversationGenerationRequest request)
        {
            string text = NormalizeUserText(
                request != null ? request.UserText : string.Empty);

            if (string.IsNullOrWhiteSpace(text))
                return ConversationLLMResponse.Empty();

            ConversationGenerationRequest normalizedRequest =
                new ConversationGenerationRequest(
                    text,
                    request != null ? request.InputId : string.Empty,
                request != null ? request.InteractionId : string.Empty,
                request != null ? request.ObjectGrounding : null,
                request != null ? request.RecentDialogue : null,
                request != null ? request.SemanticBodySnapshot : null);

            if (cloudClient == null)
            {
                Debug.LogWarning(
                    "[CloudConversationProvider] cloudClient is null"
                );

                return ConversationLLMResponse.Empty();
            }

            try
            {
                    string raw =
                        await cloudClient
                        .GenerateConversationResponseJsonAsync(
                            normalizedRequest);

                ConversationLLMResponse response =
                    ParseConversationResponse(raw);

                if (verboseLog)
                {
                    Debug.Log(
                        "[CloudConversationProvider] done. " +
                        "speechLen=" + response.speech.Length +
                        " emotion=" + response.emotion
                    );
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[CloudConversationProvider] failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );

                return ConversationLLMResponse.Empty();
            }
        }

        private static ConversationLLMResponse ParseConversationResponse(
            string value
        )
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConversationLLMResponse.Empty();

            string json = ExtractJsonObject(value);

            if (string.IsNullOrWhiteSpace(json))
                return ConversationLLMResponse.Empty();

            try
            {
                ConversationLLMResponse response =
                    JsonUtility.FromJson<ConversationLLMResponse>(json);

                if (response == null)
                    return ConversationLLMResponse.Empty();

                response.Normalize();
                return response;
            }
            catch
            {
                return ConversationLLMResponse.Empty();
            }
        }

        private static string ExtractJsonObject(string value)
        {
            int start = value.IndexOf('{');
            int end = value.LastIndexOf('}');

            if (start < 0 || end <= start)
                return string.Empty;

            return value.Substring(start, end - start + 1).Trim();
        }

        private static string NormalizeUserText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (result.Length > 160)
                result = result.Substring(0, 160);

            return result;
        }
    }
}

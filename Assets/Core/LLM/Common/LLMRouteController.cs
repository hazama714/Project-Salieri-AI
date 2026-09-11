// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;
using SalieriAI.CloudLLM;
using SalieriAI.Runtime;
using UnityEngine;

namespace SalieriAI.Core.LLM.Common
{
    public enum LLMRouteMode
    {
        Disabled,
        CloudOnly,
        LocalOnly,
        CloudPrimaryLocalFallback
    }

    /// <summary>
    /// Central Cloud / Local router.
    /// Conversation, autonomous action and autonomous speech use separate providers.
    /// </summary>
    public sealed class LLMRouteController : MonoBehaviour
    {
        [Header("Runtime Connection Settings")]
        [SerializeField]
        private RuntimeConnectionSettings runtimeSettings;

        private const LLMRouteMode DefaultRouteWhenSettingsMissing =
            LLMRouteMode.Disabled;

        [Header("Action Providers")]
        [SerializeField]
        private MonoBehaviour cloudActionProviderBehaviour;

        [SerializeField]
        private MonoBehaviour localActionProviderBehaviour;

        [Header("Autonomous Speech Providers")]
        [SerializeField]
        private MonoBehaviour cloudSpeechProviderBehaviour;

        [SerializeField]
        private MonoBehaviour localSpeechProviderBehaviour;

        [Header("Conversation Providers")]
        [SerializeField]
        private MonoBehaviour cloudConversationProviderBehaviour;

        [SerializeField]
        private MonoBehaviour localConversationProviderBehaviour;

        [Header("Legacy Cloud Direct Client")]
        [Tooltip("Compatibility fallback only. New scenes should assign CloudConversationProvider.")]
        [SerializeField]
        private CloudLLMClient legacyCloudDirectClient;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private ILLMActionProvider CloudActionProvider =>
            cloudActionProviderBehaviour as ILLMActionProvider;

        private ILLMActionProvider LocalActionProvider =>
            localActionProviderBehaviour as ILLMActionProvider;

        private ILLMSpeechProvider CloudSpeechProvider =>
            cloudSpeechProviderBehaviour as ILLMSpeechProvider;

        private ILLMSpeechProvider LocalSpeechProvider =>
            localSpeechProviderBehaviour as ILLMSpeechProvider;

        private ILLMConversationProvider CloudConversationProvider =>
            cloudConversationProviderBehaviour as ILLMConversationProvider;

        private ILLMConversationProvider LocalConversationProvider =>
            localConversationProviderBehaviour as ILLMConversationProvider;

        private void Awake()
        {
            if (runtimeSettings == null)
                runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();

            if (legacyCloudDirectClient == null)
                legacyCloudDirectClient = FindObjectOfType<CloudLLMClient>();
        }

        // ============================================================
        // User Conversation Route
        // ============================================================

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

            LLMRouteMode route = GetConversationRouteMode();
            LogRoute("Conversation", route);

            switch (route)
            {
                case LLMRouteMode.CloudOnly:
                    return await GenerateConversationCloudAsync(
                        normalizedRequest);

                case LLMRouteMode.LocalOnly:
                    return await GenerateConversationLocalAsync(
                        normalizedRequest);

                case LLMRouteMode.CloudPrimaryLocalFallback:
                    return await GenerateConversationCloudWithFallbackAsync(
                        normalizedRequest
                    );

                default:
                    return ConversationLLMResponse.Empty();
            }
        }

        /// <summary>
        /// Compatibility wrapper for older callers.
        /// </summary>
        public async Task<string> GenerateSpeechAsync(string userText)
        {
            ConversationLLMResponse response =
                await GenerateConversationResponseAsync(userText);

            return response != null ? response.speech : string.Empty;
        }

        private async Task<ConversationLLMResponse>
            GenerateConversationCloudWithFallbackAsync(
                ConversationGenerationRequest request)
        {
            ConversationLLMResponse cloud =
                await GenerateConversationCloudAsync(request);

            if (cloud != null && cloud.HasSpeech())
                return cloud;

            LogWarning(
                "[LLMRouteController] Cloud conversation empty. " +
                "fallback local."
            );

            return await GenerateConversationLocalAsync(request);
        }

        private async Task<ConversationLLMResponse>
            GenerateConversationCloudAsync(
                ConversationGenerationRequest request)
        {
            if (!CanUseCloudForConversationNow())
                return ConversationLLMResponse.Empty();

            if (CloudConversationProvider != null)
            {
                try
                {
                    ConversationLLMResponse response =
                        await CloudConversationProvider
                            .GenerateConversationResponseAsync(request);

                    return NormalizeConversationResponse(response);
                }
                catch (Exception ex)
                {
                    LogWarning(
                        "[LLMRouteController] Cloud conversation failed: " +
                        ex.GetType().Name + ": " + ex.Message
                    );
                }
            }

            if (legacyCloudDirectClient != null)
            {
                try
                {
                    string raw =
                        await legacyCloudDirectClient
                            .GenerateConversationResponseJsonAsync(request);

                    return ParseConversationResponseJson(raw);
                }
                catch (Exception ex)
                {
                    LogWarning(
                        "[LLMRouteController] Legacy cloud conversation failed: " +
                        ex.GetType().Name + ": " + ex.Message
                    );
                }
            }

            LogWarning(
                "[LLMRouteController] Cloud conversation provider missing."
            );

            return ConversationLLMResponse.Empty();
        }

        private async Task<ConversationLLMResponse>
            GenerateConversationLocalAsync(
                ConversationGenerationRequest request)
        {
            if (!CanUseLocalForConversationNow())
                return ConversationLLMResponse.Empty();

            if (LocalConversationProvider == null)
            {
                LogWarning(
                    "[LLMRouteController] Local conversation provider missing."
                );

                return ConversationLLMResponse.Empty();
            }

            try
            {
                ConversationLLMResponse response =
                    await LocalConversationProvider
                        .GenerateConversationResponseAsync(request);

                return NormalizeConversationResponse(response);
            }
            catch (Exception ex)
            {
                LogWarning(
                    "[LLMRouteController] Local conversation failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );

                return ConversationLLMResponse.Empty();
            }
        }

        // ============================================================
        // Autonomous Action Route
        // ============================================================

        public async Task<LLMActionDecision> DecideActionAsync(
            SelfStateSummary summary
        )
        {
            if (summary == null)
                return LLMActionDecision.SafeDefault("summary_missing");

            LLMRouteMode route = GetCurrentRouteMode();
            LogRoute("Action", route);

            switch (route)
            {
                case LLMRouteMode.CloudOnly:
                    return await DecideActionCloudAsync(summary);

                case LLMRouteMode.LocalOnly:
                    return await DecideActionLocalAsync(summary);

                case LLMRouteMode.CloudPrimaryLocalFallback:
                    return await DecideActionCloudWithFallbackAsync(summary);

                default:
                    return LLMActionDecision.SafeDefault("route_disabled");
            }
        }

        private async Task<LLMActionDecision> DecideActionCloudAsync(
            SelfStateSummary summary
        )
        {
            if (!CanUseCloudNow() || CloudActionProvider == null)
                return LLMActionDecision.SafeDefault("cloud_action_missing");

            try
            {
                return NormalizeActionDecision(
                    await CloudActionProvider.DecideActionAsync(summary),
                    "cloud_action_empty"
                );
            }
            catch (Exception ex)
            {
                LogWarning(
                    "[LLMRouteController] Cloud action failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );

                return LLMActionDecision.SafeDefault(
                    "cloud_action_exception"
                );
            }
        }

        private async Task<LLMActionDecision> DecideActionLocalAsync(
            SelfStateSummary summary
        )
        {
            if (!CanUseLocalNow() || LocalActionProvider == null)
                return LLMActionDecision.SafeDefault("local_action_missing");

            try
            {
                return NormalizeActionDecision(
                    await LocalActionProvider.DecideActionAsync(summary),
                    "local_action_empty"
                );
            }
            catch (Exception ex)
            {
                LogWarning(
                    "[LLMRouteController] Local action failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );

                return LLMActionDecision.SafeDefault(
                    "local_action_exception"
                );
            }
        }

        private async Task<LLMActionDecision>
            DecideActionCloudWithFallbackAsync(SelfStateSummary summary)
        {
            LLMActionDecision cloud = await DecideActionCloudAsync(summary);

            if (cloud != null && !cloud.IsNone())
                return cloud;

            if (cloud != null &&
                cloud.reason != "cloud_action_missing" &&
                cloud.reason != "cloud_action_exception")
            {
                return cloud;
            }

            return await DecideActionLocalAsync(summary);
        }

        // ============================================================
        // Autonomous Speech Route
        // ============================================================

        public async Task<LLMSpeechDecision> DecideSpeechAsync(
            SelfStateSummary summary,
            LLMActionDecision actionDecision
        )
        {
            if (summary == null)
                return LLMSpeechDecision.Empty("summary_missing");

            if (actionDecision == null)
                actionDecision = LLMActionDecision.SafeDefault(
                    "action_missing"
                );

            LLMRouteMode route = GetCurrentRouteMode();
            LogRoute("Speech", route);

            switch (route)
            {
                case LLMRouteMode.CloudOnly:
                    return await DecideSpeechCloudAsync(
                        summary,
                        actionDecision
                    );

                case LLMRouteMode.LocalOnly:
                    return await DecideSpeechLocalAsync(
                        summary,
                        actionDecision
                    );

                case LLMRouteMode.CloudPrimaryLocalFallback:
                    return await DecideSpeechCloudWithFallbackAsync(
                        summary,
                        actionDecision
                    );

                default:
                    return LLMSpeechDecision.Empty("route_disabled");
            }
        }

        private async Task<LLMSpeechDecision> DecideSpeechCloudAsync(
            SelfStateSummary summary,
            LLMActionDecision actionDecision
        )
        {
            if (!CanUseCloudNow() || CloudSpeechProvider == null)
                return LLMSpeechDecision.Empty("cloud_speech_missing");

            try
            {
                return NormalizeSpeechDecision(
                    await CloudSpeechProvider.DecideSpeechAsync(
                        summary,
                        actionDecision
                    ),
                    "cloud_speech_empty"
                );
            }
            catch (Exception ex)
            {
                LogWarning(
                    "[LLMRouteController] Cloud speech failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );

                return LLMSpeechDecision.Empty(
                    "cloud_speech_exception"
                );
            }
        }

        private async Task<LLMSpeechDecision> DecideSpeechLocalAsync(
            SelfStateSummary summary,
            LLMActionDecision actionDecision
        )
        {
            if (!CanUseLocalNow() || LocalSpeechProvider == null)
                return LLMSpeechDecision.Empty("local_speech_missing");

            try
            {
                return NormalizeSpeechDecision(
                    await LocalSpeechProvider.DecideSpeechAsync(
                        summary,
                        actionDecision
                    ),
                    "local_speech_empty"
                );
            }
            catch (Exception ex)
            {
                LogWarning(
                    "[LLMRouteController] Local speech failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );

                return LLMSpeechDecision.Empty(
                    "local_speech_exception"
                );
            }
        }

        private async Task<LLMSpeechDecision>
            DecideSpeechCloudWithFallbackAsync(
                SelfStateSummary summary,
                LLMActionDecision actionDecision
            )
        {
            LLMSpeechDecision cloud = await DecideSpeechCloudAsync(
                summary,
                actionDecision
            );

            if (cloud != null && cloud.HasSpeech())
                return cloud;

            return await DecideSpeechLocalAsync(summary, actionDecision);
        }

        // ============================================================
        // Route Selection
        // ============================================================

        private LLMRouteMode GetCurrentRouteMode()
        {
            if (runtimeSettings == null)
                return DefaultRouteWhenSettingsMissing;

            bool cloudEnabled = runtimeSettings.useCloudLLM;
            bool localEnabled = runtimeSettings.useLocalLLM;

            switch (runtimeSettings.runtimeMode)
            {
                case RuntimeConnectionSettings.RuntimeMode.WifiCloudOnly:
                    return cloudEnabled
                        ? LLMRouteMode.CloudOnly
                        : LLMRouteMode.Disabled;

                case RuntimeConnectionSettings.RuntimeMode.WifiOffLocalOnly:
                    return localEnabled
                        ? LLMRouteMode.LocalOnly
                        : LLMRouteMode.Disabled;

                case RuntimeConnectionSettings.RuntimeMode.WifiCloudAndLocal:
                    if (cloudEnabled && runtimeSettings.ShouldUseCloudNow())
                    {
                        return localEnabled
                            ? LLMRouteMode.CloudPrimaryLocalFallback
                            : LLMRouteMode.CloudOnly;
                    }

                    return localEnabled
                        ? LLMRouteMode.LocalOnly
                        : LLMRouteMode.Disabled;

                default:
                    return DefaultRouteWhenSettingsMissing;
            }
        }

        private LLMRouteMode GetConversationRouteMode()
        {
            if (runtimeSettings == null)
                return DefaultRouteWhenSettingsMissing;

            bool cloudEnabled = runtimeSettings.useCloudLLM;
            bool localEnabled = runtimeSettings.useLocalLLM;

            switch (runtimeSettings.runtimeMode)
            {
                case RuntimeConnectionSettings.RuntimeMode.WifiCloudOnly:
                    return cloudEnabled
                        ? LLMRouteMode.CloudOnly
                        : LLMRouteMode.Disabled;

                case RuntimeConnectionSettings.RuntimeMode.WifiOffLocalOnly:
                    return localEnabled
                        ? LLMRouteMode.LocalOnly
                        : LLMRouteMode.Disabled;

                case RuntimeConnectionSettings.RuntimeMode.WifiCloudAndLocal:
                    if (cloudEnabled && localEnabled)
                        return LLMRouteMode.CloudPrimaryLocalFallback;

                    if (cloudEnabled)
                        return LLMRouteMode.CloudOnly;

                    if (localEnabled)
                        return LLMRouteMode.LocalOnly;

                    return LLMRouteMode.Disabled;

                default:
                    return DefaultRouteWhenSettingsMissing;
            }
        }

        private bool CanUseCloudNow()
        {
            return runtimeSettings != null &&
                   runtimeSettings.ShouldUseCloudNow();
        }

        private bool CanUseLocalNow()
        {
            return runtimeSettings != null &&
                   runtimeSettings.ShouldUseLocalNow();
        }

        private bool CanUseCloudForConversationNow()
        {
            return runtimeSettings != null &&
                   runtimeSettings.useCloudLLM &&
                   runtimeSettings.runtimeMode !=
                       RuntimeConnectionSettings.RuntimeMode
                           .WifiOffLocalOnly;
        }

        private bool CanUseLocalForConversationNow()
        {
            return runtimeSettings != null &&
                   runtimeSettings.useLocalLLM &&
                   runtimeSettings.runtimeMode !=
                       RuntimeConnectionSettings.RuntimeMode
                           .WifiCloudOnly;
        }

        // ============================================================
        // Normalize / Parse / Log
        // ============================================================

        private static LLMActionDecision NormalizeActionDecision(
            LLMActionDecision decision,
            string fallbackReason
        )
        {
            if (decision == null)
                return LLMActionDecision.SafeDefault(fallbackReason);

            decision.Normalize();
            return decision;
        }

        private static LLMSpeechDecision NormalizeSpeechDecision(
            LLMSpeechDecision decision,
            string fallbackReason
        )
        {
            if (decision == null)
                return LLMSpeechDecision.Empty(fallbackReason);

            decision.Normalize();
            return decision;
        }

        private static ConversationLLMResponse NormalizeConversationResponse(
            ConversationLLMResponse response
        )
        {
            if (response == null)
                return ConversationLLMResponse.Empty();

            response.Normalize();
            return response;
        }

        private static ConversationLLMResponse ParseConversationResponseJson(
            string value
        )
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConversationLLMResponse.Empty();

            int start = value.IndexOf('{');
            int end = value.LastIndexOf('}');

            if (start < 0 || end <= start)
                return ConversationLLMResponse.Empty();

            try
            {
                ConversationLLMResponse response =
                    JsonUtility.FromJson<ConversationLLMResponse>(
                        value.Substring(start, end - start + 1)
                    );

                return NormalizeConversationResponse(response);
            }
            catch
            {
                return ConversationLLMResponse.Empty();
            }
        }

        private static string NormalizeUserText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            return result.Length > 160
                ? result.Substring(0, 160)
                : result;
        }

        private void LogRoute(string caller, LLMRouteMode route)
        {
            if (!verboseLog)
                return;

            Debug.Log(
                "[LLMRouteController] route caller=" + caller +
                " mode=" + route +
                " runtime=" +
                (runtimeSettings != null
                    ? runtimeSettings.runtimeMode.ToString()
                    : "missing") +
                " cloudConversation=" +
                ObjectName(cloudConversationProviderBehaviour) +
                " localConversation=" +
                ObjectName(localConversationProviderBehaviour)
            );
        }

        private static string ObjectName(UnityEngine.Object value)
        {
            return value == null ? "None" : value.name;
        }

        private static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }
    }
}

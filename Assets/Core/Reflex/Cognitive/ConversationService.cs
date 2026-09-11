// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;
using UnityEngine;

using SalieriAI.Body.Semantics;
using SalieriAI.Core.LLM.Common;
using SalieriAI.Core.Input;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Language.Runtime;
using SalieriAI.Core.Reflex.Cognitive.Grounding;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.Semantics.Referents;

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// Phase 10-E / Phase 10-M-1:
    /// Conversation分類されたユーザー発話を、LLMRouteControllerへ渡す会話サービス。
    ///
    /// ConversationService は Cloud / Local を直接知らない。
    /// Cloud / Local の選択は LLMRouteController 側の責任。
    ///
    /// Phase 10-M-1:
    /// 会話応答を string のみではなく、
    /// speech + emotion を持つ ConversationLLMResponse として受け取る。
    ///
    /// 正しい流れ：
    /// ConversationReactionService
    /// → ConversationService
    /// → LLMRouteController
    /// → Cloud / Local
    /// → ConversationLLMResponse
    /// ├─ speech → ResponseBus → VoicePlaybackController
    /// └─ emotion → RuntimeExpressionController
    /// </summary>
    public class ConversationService : MonoBehaviour
    {
        [Header("LLM Route")]
        [SerializeField]
        private LLMRouteController llmRouteController;

        [Header("Expression Runtime")]
        [SerializeField]
        private RuntimeExpressionController runtimeExpressionController;

        [Tooltip("LLMRouteControllerを使って応答生成する。OFFの場合はfallbackのみ。")]
        [SerializeField]
        private bool useLLMRoute = true;

        [Tooltip("LLM応答が空、失敗、またはRoute無効の場合に使う固定応答。")]
        [SerializeField]
        private string fallbackConversationResponse = "うん、聞いてるよ。";

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private int requestSerial;
        private readonly ConversationGroundingService groundingService =
            new ConversationGroundingService();
        private readonly ConversationReferenceResolverV1
            targetReferenceResolver =
                new ConversationReferenceResolverV1();
        private ICurrentObjectReferentProvider objectReferentProvider;
        private Func<SemanticBodySnapshot> semanticBodySnapshotFactory;
        private Func<string> groundingIdFactory =
            () => CommunicationIdGenerator.Create("grounding");
        private Func<DateTime> groundingClock = () => DateTime.UtcNow;

        private static readonly Action<string> BodyIntentUnityLog =
            message => Debug.Log(message);
        private static readonly Action<string> BodyPoseUnityLog =
            message => Debug.Log(message);

        public ConversationTurnContext LastTurnContext { get; private set; }
        public ConversationGenerationRequest LastGenerationRequest
        { get; private set; }

        private void Awake()
        {
            if (llmRouteController == null)
                llmRouteController = FindObjectOfType<LLMRouteController>();

            if (runtimeExpressionController == null)
            {
                runtimeExpressionController =
                    FindObjectOfType<RuntimeExpressionController>();
            }

        }

        private void Start()
        {
            ResolveObjectReferentProvider();
        }

        internal void ConfigureGroundingForTests(
            ICurrentObjectReferentProvider provider,
            Func<string> idFactory,
            Func<DateTime> clock)
        {
            objectReferentProvider = provider;
            groundingIdFactory = idFactory ??
                (() => CommunicationIdGenerator.Create("grounding"));
            groundingClock = clock ?? (() => DateTime.UtcNow);
        }

        internal void ConfigureSemanticBodyForTests(
            Func<SemanticBodySnapshot> snapshotFactory)
        {
            semanticBodySnapshotFactory = snapshotFactory;
        }

        public void HandleConversation(string userText)
        {
            string normalized = NormalizeUserText(userText);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                Debug.LogWarning(
                    "[ConversationService] Conversation skipped: " +
                    "user text is empty."
                );
                return;
            }

            CommunicationInput communicationInput =
                CommunicationInput.CreateAccepted(
                    userText,
                    normalized,
                    UserInputSource.Unknown
                );

            ReactionDecision decision =
                ReactionDecision.Create(
                    communicationInput,
                    ReactionRouteIds.Conversation,
                    "CONVERSATION",
                    "legacy-direct-conversation-call"
                );

            InteractionTraceLogger.LogAdmission(communicationInput);
            InteractionTraceLogger.LogDecision(decision);

            HandleConversation(
                normalized,
                communicationInput,
                decision
            );
        }

        public void HandleConversation(
            string userText,
            CommunicationInput communicationInput,
            ReactionDecision decision
        )
        {
            string text = NormalizeUserText(userText);

            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationService] HandleConversation. " +
                    "route=conversation-service text=" +
                    SafeLog(text) +
                    " useLLMRoute=" +
                    useLLMRoute +
                    " fallback=" +
                    SafeLog(fallbackConversationResponse)
                );
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning(
                    "[ConversationService] Conversation skipped: " +
                    "user text is empty."
                );

                return;
            }

            if (communicationInput == null)
            {
                communicationInput =
                    CommunicationInput.CreateAccepted(
                        userText,
                        text,
                        UserInputSource.Unknown
                    );

                InteractionTraceLogger.LogAdmission(
                    communicationInput
                );
            }

            if (decision == null)
            {
                decision =
                    ReactionDecision.Create(
                        communicationInput,
                        ReactionRouteIds.Conversation,
                        "CONVERSATION",
                        "conversation-service-direct-call"
                    );

                InteractionTraceLogger.LogDecision(decision);
            }

            EnsureObjectReferentProvider();

            CurrentObjectReferent liveReferent =
                objectReferentProvider != null
                    ? objectReferentProvider.CurrentReferent
                    : null;

            DateTime turnStartedAtUtc = groundingClock();
            ConversationWorkingMemorySnapshot workingMemory =
                ConversationWorkingMemoryRuntime.GetSnapshot();
            FrozenObjectReferentSnapshot previousTurnReferent =
                workingMemory != null
                    ? workingMemory.LastObjectReferent
                    : null;
            ConversationReferenceResolution referenceResolution =
                targetReferenceResolver.Resolve(
                    text,
                    liveReferent,
                    previousTurnReferent);
            ConversationObjectGrounding objectGrounding =
                groundingService.Freeze(
                    referenceResolution,
                    liveReferent,
                    groundingIdFactory(),
                    communicationInput.InputId,
                    communicationInput.InteractionId,
                    turnStartedAtUtc);

            int serial = ++requestSerial;
            var turn = new ConversationTurnContext(
                serial,
                text,
                communicationInput,
                decision,
                objectGrounding,
                turnStartedAtUtc);

            LastTurnContext = turn;

            SemanticBodySnapshot semanticBodySnapshot =
                CaptureSemanticBodySnapshot(turnStartedAtUtc);

            ConversationGenerationRequest generationRequest =
                ConversationGenerationRequest.FromTurn(
                    turn,
                    workingMemory != null
                        ? workingMemory.RecentDialogue
                        : null,
                    semanticBodySnapshot);
            LastGenerationRequest = generationRequest;

            ConversationTurnLifecycleRuntime.BeginTurn(turn);

            _ = HandleConversationAsync(
                turn,
                generationRequest
            );
        }

        private SemanticBodySnapshot CaptureSemanticBodySnapshot(
            DateTime timestampUtc)
        {
            if (semanticBodySnapshotFactory != null)
            {
                SemanticBodySnapshot injected =
                    semanticBodySnapshotFactory();
                return injected ?? SemanticBodySnapshot.Unknown(timestampUtc);
            }

            return SemanticBodySnapshotRuntime.Capture(timestampUtc);
        }

        private async Task HandleConversationAsync(
            ConversationTurnContext turn,
            ConversationGenerationRequest generationRequest
        )
        {
            string userText = turn != null
                ? turn.UserText
                : string.Empty;
            int serial = turn != null ? turn.RequestSerial : 0;
            CommunicationInput communicationInput =
                turn != null ? turn.Input : null;
            ReactionDecision decision = turn != null
                ? turn.Decision
                : null;

            if (turn != null)
            {
                ConversationTurnLifecycleRuntime.MarkGenerationStarted(
                    turn.TurnId);
            }

            InteractionTraceLogger.LogRuntimeStage(
                "CONVERSATION_GENERATION_STARTED",
                communicationInput,
                "serial=" + serial
            );

            ConversationLLMResponse llmResponse =
                ConversationLLMResponse.Empty();

            if (CanUseLLMRoute())
            {
                llmResponse =
                    await GenerateViaRouteAsync(
                        generationRequest,
                        serial
                    );
            }
            else if (verboseLog)
            {
                Debug.Log(
                    "[ConversationService] LLM route skipped. " +
                    "reason=route-disabled-or-missing"
                );
            }

            if (serial != requestSerial)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationService] Response ignored: " +
                        "stale request. " +
                        "serial=" +
                        serial +
                        " current=" +
                        requestSerial
                    );
                }

                InteractionTraceLogger.LogRuntimeStage(
                    "CONVERSATION_STALE_RESULT_DROPPED",
                    communicationInput,
                    "serial=" + serial + " current=" + requestSerial
                );

                if (turn != null)
                {
                    ConversationTurnLifecycleRuntime.InterruptTurn(
                        turn.TurnId,
                        "generation-stale-request-serial");
                }

                return;
            }

            string response =
                llmResponse != null
                    ? llmResponse.speech
                    : string.Empty;

            string emotion =
                llmResponse != null
                    ? llmResponse.emotion
                    : "Neutral";

            bool usedFallbackConversationResponse = false;

            if (string.IsNullOrWhiteSpace(response))
            {
                response = fallbackConversationResponse;
                emotion = "Neutral";
                usedFallbackConversationResponse = true;

                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationService] Use fallback response. " +
                        "response=" +
                        SafeLog(response)
                    );
                }
            }

            response = SanitizeResponse(response);

            emotion =
                ConversationLLMResponse.NormalizeEmotion(
                    emotion
                );

            if (string.IsNullOrWhiteSpace(response))
            {
                Debug.LogWarning(
                    "[ConversationService] Conversation response skipped: " +
                    "both route response and fallback are empty. text=" +
                    SafeLog(userText)
                );

                InteractionTraceLogger.LogRuntimeStage(
                    "CONVERSATION_OUTPUT_SKIPPED",
                    communicationInput,
                    "empty-route-and-fallback-response"
                );

                if (turn != null)
                {
                    ConversationTurnLifecycleRuntime.FailTurn(
                        turn.TurnId,
                        "conversation-output-empty");
                }

                return;
            }

            EmitBodyExpressionIntentShadow(
                usedFallbackConversationResponse
                    ? ConversationLLMResponse.Empty()
                    : llmResponse,
                turn != null ? turn.TurnId : string.Empty,
                BodyIntentUnityLog);

            ConversationalHandTargetHandoffResult handTargetHandoff =
                ConversationalHandTargetProductionAdapter.Handle(
                    usedFallbackConversationResponse
                        ? ConversationLLMResponse.Empty()
                        : llmResponse,
                    turn != null ? turn.TurnId : string.Empty,
                    BodyPoseUnityLog);

            // A user-requested hand target is authoritative for this turn.
            // Conversational gesture mapping remains unchanged for responses
            // that select Keep for both hands.
            if (!handTargetHandoff.HasUserTargetDemand)
            {
                BodyExpressionIntentProductionAdapter.Handle(
                usedFallbackConversationResponse
                    ? ConversationLLMResponse.Empty()
                    : llmResponse,
                turn != null ? turn.TurnId : string.Empty,
                BodyPoseUnityLog);
            }

            Debug.Log(
                "[ConversationService] Response ready. " +
                "route=" +
                (CanUseLLMRoute()
                    ? "llm-route-or-fallback"
                    : "fallback") +
                " text=" +
                SafeLog(userText) +
                " response=" +
                SafeLog(response) +
                " emotion=" +
                SafeLog(emotion)
            );

            RequestConversationExpression(
                emotion,
                userText,
                response
            );

            ReactionOutcome outcome =
                ReactionOutcome.CreateOutputRequest(
                    communicationInput,
                    decision,
                    communicationInput.Source.ToString(),
                    "conversation",
                    response,
                    emotion,
                    "SUCCEEDED",
                    "ConversationService.HandleConversationAsync"
                );

            InteractionTraceLogger.LogOutcome(outcome);

            bool outputCorrelated =
                turn != null &&
                ConversationTurnLifecycleRuntime.MarkOutputRequested(
                    turn.TurnId,
                    outcome);
            if (outputCorrelated)
            {
                bool delivered = global::ResponseBus.Raise(
                    new global::ResponseEnvelope(
                        response,
                        outcome.OutputId,
                        turn.TurnId,
                        communicationInput != null
                            ? communicationInput.InteractionId
                            : string.Empty));
                if (!delivered)
                {
                    ConversationTurnLifecycleRuntime.FailTurn(
                        turn.TurnId,
                        "response-envelope-not-delivered");
                }
            }
            else
            {
                // Lifecycle diagnostics must never suppress the established
                // Production speech route.
                global::ResponseBus.Raise(response);
            }
        }

        private void RequestConversationExpression(
            string emotion,
            string userText,
            string responseText
        )
        {
            if (runtimeExpressionController == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationService] " +
                        "RuntimeExpressionController skipped: " +
                        "not assigned."
                    );
                }

                return;
            }

            runtimeExpressionController
                .RequestFromConversationEmotion(
                    emotion,
                    userText,
                    responseText,
                    "ConversationService.ResponseReady"
                );
        }

        internal static void EmitBodyExpressionIntentShadow(
            ConversationLLMResponse response,
            string turnId,
            Action<string> sink)
        {
            if (sink == null)
                return;

            sink(FormatBodyExpressionIntentShadow(response, turnId));
        }

        internal static string FormatBodyExpressionIntentShadow(
            ConversationLLMResponse response,
            string turnId)
        {
            ConversationLLMResponse safe =
                response ?? ConversationLLMResponse.Empty();
            safe.Normalize();

            return
                "[BodyExpressionIntent][SHADOW]" +
                " TurnId=" + SafeLog(turnId) +
                " Overall=" + safe.overallPoseIntent +
                " Right=" + safe.rightArmIntent +
                " Left=" + safe.leftArmIntent +
                " Head=" + safe.headIntent +
                " Target=" + safe.targetIntent +
                " MatchedPoseId=NONE" +
                " Execution=SHADOW_ONLY";
        }

        private async Task<ConversationLLMResponse>
            GenerateViaRouteAsync(
                ConversationGenerationRequest request,
                int serial
            )
        {
            string userText = request != null
                ? request.UserText
                : string.Empty;

            if (llmRouteController == null)
            {
                Debug.LogWarning(
                    "[ConversationService] LLM route skipped: " +
                    "llmRouteController is null."
                );

                return ConversationLLMResponse.Empty();
            }

            try
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationService] LLM route request start. " +
                        "serial=" +
                        serial +
                        " text=" +
                        SafeLog(userText)
                    );
                }

                ConversationLLMResponse result =
                    await llmRouteController
                        .GenerateConversationResponseAsync(
                            request
                        );

                if (result == null)
                {
                    result =
                        ConversationLLMResponse.Empty();
                }

                result.speech =
                    SanitizeResponse(
                        result.speech
                    );

                result.Normalize();

                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationService] " +
                        "LLM route response received. " +
                        "serial=" +
                        serial +
                        " response=" +
                        SafeLog(result.speech) +
                        " emotion=" +
                        SafeLog(result.emotion)
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[ConversationService] LLM route failed. " +
                    ex.GetType().Name +
                    ": " +
                    ex.Message
                );

                return ConversationLLMResponse.Empty();
            }
        }

        private void EnsureObjectReferentProvider()
        {
            if (objectReferentProvider == null)
                ResolveObjectReferentProvider();
        }

        private void ResolveObjectReferentProvider()
        {
            CurrentObjectReferentRuntimeHost[] providers =
                FindObjectsOfType<CurrentObjectReferentRuntimeHost>();

            if (providers != null && providers.Length == 1)
                objectReferentProvider = providers[0];
        }

        private bool CanUseLLMRoute()
        {
            if (!useLLMRoute)
                return false;

            if (llmRouteController == null)
                return false;

            return true;
        }

        private static string NormalizeUserText(
            string value
        )
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim();
        }

        private static string SanitizeResponse(
            string value
        )
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value.Trim();

            result =
                result
                    .Replace("「", "")
                    .Replace("」", "");

            result =
                result
                    .Replace("『", "")
                    .Replace("』", "");

            result =
                result
                    .Replace("\"", "")
                    .Replace("'", "");

            int cut = result.IndexOf('\n');

            if (cut >= 0)
                result = result.Substring(0, cut);

            cut =
                result.IndexOf(
                    "```",
                    StringComparison.Ordinal
                );

            if (cut >= 0)
                result = result.Substring(0, cut);

            cut =
                result.IndexOf(
                    "返答:",
                    StringComparison.Ordinal
                );

            if (cut >= 0)
            {
                result =
                    result.Substring(
                        cut + "返答:".Length
                    );
            }

            result = result.Trim();

            if (result.Length > 80)
                result = result.Substring(0, 80);

            return result.Trim();
        }

        private static string SafeLog(
            string value
        )
        {
            return string.IsNullOrEmpty(value)
                ? "<empty>"
                : value;
        }
    }
}

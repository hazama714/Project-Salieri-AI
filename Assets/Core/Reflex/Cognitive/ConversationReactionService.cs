// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SalieriAI.Core.Behavior.FindPointAsk;
using SalieriAI.Core.State;
using SalieriAI.Expression.Voice;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.Command.BodyCommand;
using SalieriAI.Core.Input;
using SalieriAI.Core.Experience.Observation;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Language.Cgl;
using SalieriAI.Core.Language.Runtime;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public class ConversationReactionService : MonoBehaviour
    {
        [Header("Persona Activation")]
        [SerializeField]
        private TextAsset personaActivationJson;

        [SerializeField]
        private string fallbackDisplayName = "アシス";

        [SerializeField]
        private string fallbackActivationAck = "はい";

        [SerializeField]
        private InteractionStateController interactionStateController;

        [Header("Wake Standby / Mic Standby")]
        [Tooltip("Idle / MicStandby中に起動語なし発話を無視した後、STTを再待機させる。")]
        [SerializeField]
        private global::SpeechInputController speechInputController;

        [Header("Activation Ack")]
        [Tooltip("ONにするとActivationAckだけVoiceControllerへ流す。通常会話発話ではない。")]
        [SerializeField]
        private bool speakActivationAck = false;

        [Header("Activation Ack Control")]
        [Tooltip("ONにすると「アシス こんにちは」のように起動語の後に本文がある場合、Ack発話を省略する。")]
        [SerializeField]
        private bool skipActivationAckWhenRemainingTextExists = true;

        [SerializeField]
        private VoiceController voiceController;

        [SerializeField]
        private string activationAckSpeakerName = "";

        [Header("Conversation Window")]
        [Tooltip("会話継続時間はSpeechInputController / RuntimeInteractionSettings側で管理する。ConversationReactionServiceは状態遷移とルーティングのみ担当する。")]
        [SerializeField]
        private bool deferConversationWindowToSpeechInputController = true;

        [Header("ProgrammedAction Observe Only")]
        [Tooltip("ONにするとActionIntentのRemainingTextからProgrammedAction候補をログだけ出す。実行はしない。")]
        [SerializeField]
        private bool observeProgrammedActionCandidate = true;

        [Header("ExecutionRequest Preparation")]
        [Tooltip("ONにするとProgrammedActionCandidateからExecutionRequestを作る。R-3以降は設定によりExecutionControllerへ渡す。")]
        [SerializeField]
        private bool prepareProgrammedActionExecutionRequest = true;

        [Header("ExecutionController Routing")]
        [Tooltip("ONにするとProgrammedAction由来のExecutionRequestをExecutionControllerへ渡す。実Action化するかはExecutionController側で判定する。")]
        [SerializeField]
        private bool routeProgrammedActionRequestToExecutionController = true;

        [SerializeField]
        private ExecutionController executionController;

        [Header("BodyAction Voice Rules")]
        [Tooltip("ONにすると、停止系と現行の非Legacy固定BodyAction音声をLLMへ送らずExecutionControllerへ渡す。旧Crawler移動ShortcutはSR-0で通常Speechから隔離する。")]
        [SerializeField]
        private bool enableFixedBodyActionVoiceRules = true;

        [Tooltip("ONにすると、固定BodyAction由来のExecutionRequestをExecutionControllerへ渡す。")]
        [SerializeField]
        private bool routeFixedBodyActionRequestToExecutionController = true;

        [Header("Fixed BodyAction Ack")]
        [Tooltip("ONにすると、通常の固定BodyActionを実行する前に短い了承応答を返す。停止系は安全優先で即実行する。")]
        [SerializeField]
        private bool speakFixedBodyActionAck = true;

        [SerializeField]
        private string fixedBodyActionAckText = "はい";

        [Tooltip("Ackを出してからBodyActionへ進むまでの最小待機秒。短すぎると返事が聞こえる前に動く。")]
        [SerializeField]
        private float fixedBodyActionAckDelaySeconds = 0.35f;

        [Tooltip("Ack発話中にBodyActionへ入らないための最大待機秒。VOICEVOXが詰まった場合は待ちすぎない。")]
        [SerializeField]
        private float fixedBodyActionAckMaxWaitSpeakingSeconds = 2.0f;

        [Header("Conversation Route")]
        [Tooltip("ONにするとConversation分類をConversationServiceへ渡す。LLMはまだ呼ばない。")]
        [SerializeField]
        private bool routeConversationToConversationService = true;

        [SerializeField]
        private ConversationService conversationService;

        [Header("Question Answer Routing")]
        [SerializeField]
        private FindPointAskBehaviorController questionAnswerController;

        [Tooltip("OneShot回答のBindingと保存が成功した後にだけ発行する固定Reaction。")]
        [SerializeField]
        private string questionAnswerSavedAckText = "覚えました。";

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private PersonaActivationProfile activationProfile;
        private Coroutine activationAckReturnCoroutine;
        private int activationAckReturnToken;
        private Coroutine fixedBodyActionAckCoroutine;
        private int fixedBodyActionAckRouteToken;
        private const int ConsumedAnswerInputCapacity = 64;
        private readonly Queue<string> consumedAnswerInputOrder =
            new Queue<string>();
        private readonly HashSet<string> consumedAnswerInputIds =
            new HashSet<string>(System.StringComparer.Ordinal);
        private FindPointAskBehaviorController subscribedAnswerController;

        internal int ConsumedAnswerInputCount =>
            consumedAnswerInputIds.Count;

        internal PostSaveMemoryContextRefreshResult
            LastPostSaveMemoryContextRefreshResult { get; private set; }

        private void Awake()
        {
            ResolveSpeechInputController();
            ResolveQuestionAnswerController();
            ReloadActivationProfile();
        }

        private void OnDisable()
        {
            UnsubscribeQuestionAnswerOutcome();
            CancelListeningTimeout("OnDisable");
            CancelActivationAckReturn("OnDisable");
            CancelFixedBodyActionAckRoute("OnDisable");
        }


        private void ResolveSpeechInputController()
        {
            if (speechInputController == null)
            {
                speechInputController = FindObjectOfType<global::SpeechInputController>();
            }
        }

        private void RequestWakeStandbyRelisten(string reason)
        {
            ResolveSpeechInputController();

            if (speechInputController == null)
            {
                if (verboseLog)
                {
                    Debug.LogWarning(
                        "[ConversationReactionService] Wake standby relisten skipped. " +
                        "reason=speech-input-controller-not-found source=" +
                        reason
                    );
                }

                return;
            }

            speechInputController.RequestRelistenForWakeStandby(reason);

            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationReactionService] Wake standby relisten requested. reason=" +
                    reason
                );
            }
        }

        [ContextMenu("Reload Persona Activation Profile")]
        public void ReloadActivationProfile()
        {
            activationProfile = PersonaActivationLoader.LoadOrDefault(
                personaActivationJson,
                fallbackDisplayName,
                fallbackActivationAck
            );

            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationReactionService] Activation profile loaded. " +
                    "displayName=" +
                    SafeLog(activationProfile.displayName) +
                    " aliases=" +
                    FormatAliasesForLog(activationProfile.activationAliases) +
                    " ack=" +
                    SafeLog(activationProfile.GetActivationAckOrDefault())
                );
            }
        }

        public string GetFirstValidActivationAlias()
        {
            EnsureActivationProfile();

            if (activationProfile == null ||
                activationProfile.activationAliases == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < activationProfile.activationAliases.Length; i++)
            {
                string alias = activationProfile.activationAliases[i];
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    return alias.Trim();
                }
            }

            return string.Empty;
        }

        public void ReactToUserSpeech(string userText)
        {
            if (string.IsNullOrWhiteSpace(userText))
                return;

            ReactToUserSpeech(
                CommunicationInput.CreateAccepted(
                    userText,
                    userText.Trim(),
                    UserInputSource.Unknown
                )
            );
        }

        public void ReactToUserSpeech(CommunicationInput communicationInput)
        {
            if (communicationInput == null ||
                string.IsNullOrWhiteSpace(communicationInput.NormalizedText))
            {
                return;
            }

            string trimmed = communicationInput.NormalizedText.Trim();

            InteractionTraceLogger.LogRuntimeStage(
                "REACTION_INPUT",
                communicationInput,
                "ConversationReactionService"
            );

            Debug.Log(
                "[ConversationReactionService] ReactToUserSpeech: " +
                trimmed +
                " inputId=" +
                communicationInput.InputId +
                " interactionId=" +
                communicationInput.InteractionId
            );

            // 新しいUserSpeechが来た時点で、前回Activation由来の待機Timeoutは無効化する。
            CancelListeningTimeout("new-user-speech");
            CancelFixedBodyActionAckRoute("new-user-speech");

            // Phase 9-U safety-first classification.
            // StopCommand / AdminEmergencyStop は名前呼び不要。
            UserSpeechClassificationResult wholeTextClass =
                UserSpeechClassifier.Classify(trimmed);

            if (wholeTextClass.SpeechClass == UserSpeechClass.AdminEmergencyStop ||
                wholeTextClass.SpeechClass == UserSpeechClass.StopCommand)
            {
                LogClassification("WholeText", wholeTextClass);

                TryRouteFixedBodyActionFromSpeech(
                    trimmed,
                    wholeTextClass,
                    "WholeText",
                    communicationInput
                );

                // Stop系は安全命令として扱う。
                // ExecutionController未設定などで実行できなかった場合でも、
                // 会話/LLMへ流して通常応答に変換しない。
                return;
            }

            EnsureActivationProfile();

            ActivationPhraseResult activation = ActivationPhraseDetector.Detect(
                trimmed,
                activationProfile
            );

            CommunicationInput answerInput = activation.IsActivated
                ? communicationInput.WithActivationMatched(true)
                : communicationInput;
            string answerCandidate = activation.IsActivated
                ? activation.RemainingText
                : trimmed;

            if (TryConsumeQuestionAnswer(answerCandidate, answerInput))
                return;

            if (!activation.IsActivated)
            {
                if (TryHandleContinuationWithoutActivation(
                        trimmed,
                        communicationInput
                    ))
                {
                    return;
                }

                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.Ignored,
                    string.Empty,
                    "activation-phrase-not-matched"
                );

                Debug.Log(
                    "[ConversationReactionService] No activation phrase. " +
                    "route=ignore-for-now text=" +
                    SafeLog(trimmed)
                );

                RequestWakeStandbyRelisten("no-activation-ignored");
                return;
            }

            HandleActivation(
                activation,
                answerInput
            );
        }

        private bool TryConsumeQuestionAnswer(
            string answerCandidate,
            CommunicationInput communicationInput)
        {
            if (communicationInput == null)
                return false;

            string inputId = communicationInput.InputId ?? string.Empty;
            if (inputId.Length > 0 &&
                consumedAnswerInputIds.Contains(inputId))
            {
                Debug.Log(
                    "[ConversationReactionService][QUESTION_ANSWER] " +
                    "Duplicate consumed input suppressed. inputId=" +
                    inputId
                );
                return true;
            }

            if (string.IsNullOrWhiteSpace(answerCandidate))
                return false;

            ResolveQuestionAnswerController();
            if (questionAnswerController == null ||
                !questionAnswerController.CanAcceptProductionAnswer)
            {
                return false;
            }

            if (!questionAnswerController.TrySubmitProductionAnswer(
                    answerCandidate,
                    communicationInput,
                    out OneShotAnswerOutcome outcome,
                    out string error))
            {
                Debug.LogWarning(
                    "[ConversationReactionService][QUESTION_ANSWER] " +
                    "Admission rejected after authoritative precheck. " +
                    "inputId=" + inputId +
                    " reason=" + SafeLog(error)
                );
                return false;
            }

            RememberConsumedAnswerInput(inputId);
            Debug.Log(
                "[ConversationReactionService][QUESTION_ANSWER] " +
                "Production answer accepted and consumed. " +
                "inputId=" + SafeLog(inputId) +
                " source=" + communicationInput.Source +
                " activationMatched=" + communicationInput.ActivationMatched
            );
            return true;
        }

        private void ReactToOneShotAnswerOutcome(
            OneShotAnswerOutcome outcome)
        {
            if (outcome == null || !outcome.Succeeded)
                return;

            PostSaveMemoryContextRefreshResult refresh =
                PostSaveMemoryContextRefreshRuntime.RefreshAfterSave(
                    outcome,
                    System.DateTime.UtcNow);
            LastPostSaveMemoryContextRefreshResult = refresh;
            Debug.Log(
                "[ConversationReactionService][POST_SAVE_MEMORY_REFRESH] " +
                "Status=" + refresh.Status +
                " TargetKey=" + SafeLog(refresh.TargetKey) +
                " TrackId=" + refresh.TrackId +
                " RecallStatus=" + refresh.RecallStatus +
                " KnownName=" + SafeLog(refresh.KnownName) +
                " ExperienceRecordId=" +
                SafeLog(refresh.ExperienceRecordId) +
                " Error=" + SafeLog(refresh.Error));

            string acknowledgement = questionAnswerSavedAckText == null
                ? string.Empty
                : questionAnswerSavedAckText.Trim();
            if (!questionAnswerController.TryBeginAcknowledgement(
                    outcome,
                    out string acknowledgementCorrelationId,
                    out string acknowledgementError))
            {
                Debug.LogWarning(
                    "[ConversationReactionService][QUESTION_ANSWER_REACTION] " +
                    "Acknowledgement lifecycle rejected. reason=" +
                    SafeLog(acknowledgementError));
                return;
            }
            if (acknowledgement.Length == 0)
            {
                questionAnswerController.ReportAcknowledgementDispatchFailure(
                    acknowledgementCorrelationId,
                    "Acknowledgement text is empty.");
                Debug.LogWarning(
                    "[ConversationReactionService][QUESTION_ANSWER_REACTION] " +
                    "Successful outcome has no acknowledgement text. " +
                    "questionId=" + SafeLog(outcome.QuestionId) +
                    " inputId=" + SafeLog(outcome.InputId)
                );
                return;
            }

            bool delivered = global::ResponseBus.Raise(
                global::ResponseEnvelope.OneShotAcknowledgement(
                    acknowledgement,
                    acknowledgementCorrelationId));
            if (!delivered)
            {
                questionAnswerController.ReportAcknowledgementDispatchFailure(
                    acknowledgementCorrelationId,
                    "No ResponseEnvelope consumer accepted the ACK request.");
            }
            Debug.Log(
                "[ConversationReactionService][QUESTION_ANSWER_REACTION] " +
                "Acknowledgement published. questionId=" +
                SafeLog(outcome.QuestionId) +
                " inputId=" + SafeLog(outcome.InputId) +
                " correlation=" + SafeLog(acknowledgementCorrelationId) +
                " binding=" + outcome.CorrelationStatus +
                " save=" + outcome.SaveStatus
            );
        }

        private void RememberConsumedAnswerInput(string inputId)
        {
            if (string.IsNullOrWhiteSpace(inputId) ||
                consumedAnswerInputIds.Contains(inputId))
            {
                return;
            }

            while (consumedAnswerInputOrder.Count >=
                   ConsumedAnswerInputCapacity)
            {
                string expired = consumedAnswerInputOrder.Dequeue();
                consumedAnswerInputIds.Remove(expired);
            }

            consumedAnswerInputOrder.Enqueue(inputId);
            consumedAnswerInputIds.Add(inputId);
        }

        private void ResolveQuestionAnswerController()
        {
            if (questionAnswerController == null)
            {
                questionAnswerController =
                    FindObjectOfType<FindPointAskBehaviorController>();
            }
            SubscribeQuestionAnswerOutcome();
        }

        private void SubscribeQuestionAnswerOutcome()
        {
            if (subscribedAnswerController == questionAnswerController)
                return;
            UnsubscribeQuestionAnswerOutcome();
            if (questionAnswerController == null)
                return;
            questionAnswerController.AnswerOutcomeReady +=
                ReactToOneShotAnswerOutcome;
            subscribedAnswerController = questionAnswerController;
        }

        private void UnsubscribeQuestionAnswerOutcome()
        {
            if (subscribedAnswerController == null)
                return;
            subscribedAnswerController.AnswerOutcomeReady -=
                ReactToOneShotAnswerOutcome;
            subscribedAnswerController = null;
        }

        private void HandleActivation(
            ActivationPhraseResult activation,
            CommunicationInput communicationInput
        )
        {
            string ack = activationProfile.GetActivationAckOrDefault();

            Debug.Log(
                "[ConversationReactionService] Activation matched. " +
                "alias=" +
                SafeLog(activation.MatchedAlias) +
                " remaining=" +
                SafeLog(activation.RemainingText) +
                " ack=" +
                SafeLog(ack)
            );

            bool hasRemainingText = !string.IsNullOrWhiteSpace(activation.RemainingText);

            bool ackSpeechRequested =
                EmitActivationAck(
                    ack,
                    activation.RemainingText,
                    hasRemainingText,
                    communicationInput
                );

            if (!hasRemainingText)
            {
                if (ackSpeechRequested)
                {
                    Debug.Log(
                        "[ConversationReactionService] Activation only. " +
                        "state=Speaking route=ack-speaking-then-wait-next-user-speech"
                    );

                    // Do not enter Listening immediately here.
                    // VoiceController / VoicePlaybackController owns the Speaking lifecycle.
                    // After the short Ack speech finishes, this service forces Listening
                    // so the next utterance can be accepted without repeating the activation phrase.
                    ScheduleActivationAckReturnToListening("activation-only-ack");

                    return;
                }

                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.ActivationAck,
                    string.Empty,
                    "activation-only-without-speech-request"
                );

                EnterListeningState();

                Debug.Log(
                    "[ConversationReactionService] Activation only. " +
                    "state=Listening route=wait-next-user-speech"
                );

                StartListeningTimeout("activation-only");
                return;
            }

            UserSpeechClassificationResult remainingClass =
                UserSpeechClassifier.Classify(activation.RemainingText);

            LogClassification("RemainingText", remainingClass);

            // Phase 10-J-3:
            // 固定BodyActionは会話待機ではなくAction実行として扱う。
            // 先にListeningへ入ると LimboPermission が CanStartAction=false になり、
            // 「こっち向いて」などが BodyActionExecutor で弾かれるため、
            // fixed body action 判定を Listening 遷移より前に行う。
            if (TryRouteFixedBodyActionFromSpeech(
                    activation.RemainingText,
                    remainingClass,
                    "RemainingText",
                    communicationInput
                ))
            {
                return;
            }

            EnterListeningState();

            if (TryHandleClarificationIfNeeded(
                    activation.RemainingText,
                    remainingClass,
                    communicationInput
                ))
            {
                return;
            }

            if (TryHandleConversationIfNeeded(
                    activation.RemainingText,
                    remainingClass,
                    communicationInput
                ))
            {
                return;
            }

            ProgrammedActionCandidate programmedActionCandidate =
                ResolveAndLogProgrammedActionCandidateIfNeeded(
                    activation.RemainingText,
                    remainingClass
                );

            PrepareAndLogProgrammedActionExecutionRequestIfNeeded(
                activation.RemainingText,
                remainingClass,
                programmedActionCandidate
            );

            LogReactionDecision(
                communicationInput,
                ReactionRouteIds.ProgrammedAction,
                string.Empty,
                programmedActionCandidate != null
                    ? programmedActionCandidate.Reason
                    : "programmed-action-candidate-unavailable",
                programmedActionCandidate != null
                    ? programmedActionCandidate.PrimaryAction.ToString()
                    : string.Empty
            );

            Debug.Log(
                "[ConversationReactionService] RemainingText handled. " +
                "route=programmed-action-pipeline"
            );

            if (deferConversationWindowToSpeechInputController)
            {
                StartListeningTimeout("remaining-text-processed");
            }
        }

        private bool TryHandleContinuationWithoutActivation(
            string text,
            CommunicationInput communicationInput
        )
        {
            if (interactionStateController == null)
            {
                return false;
            }

            if (interactionStateController.CurrentState != InteractionState.Listening)
            {
                return false;
            }

            Debug.Log(
                "[ConversationReactionService] Conversation continuation accepted. " +
                "state=Listening route=without-activation text=" +
                SafeLog(text)
            );

            UserSpeechClassificationResult continuationClass =
                UserSpeechClassifier.Classify(text);

            LogClassification("ContinuationText", continuationClass);

            if (TryRouteFixedBodyActionFromSpeech(
                    text,
                    continuationClass,
                    "ContinuationText",
                    communicationInput
                ))
            {
                if (deferConversationWindowToSpeechInputController)
                {
                    StartListeningTimeout("fixed-body-action-continuation-text");
                }

                return true;
            }

            if (TryHandleClarificationIfNeeded(
                    text,
                    continuationClass,
                    communicationInput
                ))
            {
                return true;
            }

            if (TryHandleConversationIfNeeded(
                    text,
                    continuationClass,
                    communicationInput
                ))
            {
                return true;
            }

            ProgrammedActionCandidate programmedActionCandidate =
                ResolveAndLogProgrammedActionCandidateIfNeeded(
                    text,
                    continuationClass
                );

            PrepareAndLogProgrammedActionExecutionRequestIfNeeded(
                text,
                continuationClass,
                programmedActionCandidate
            );

            LogReactionDecision(
                communicationInput,
                ReactionRouteIds.ProgrammedAction,
                string.Empty,
                programmedActionCandidate != null
                    ? programmedActionCandidate.Reason
                    : "programmed-action-candidate-unavailable",
                programmedActionCandidate != null
                    ? programmedActionCandidate.PrimaryAction.ToString()
                    : string.Empty
            );

            Debug.Log(
                "[ConversationReactionService] ContinuationText handled. " +
                "route=programmed-action-pipeline"
            );

            if (deferConversationWindowToSpeechInputController)
            {
                StartListeningTimeout("continuation-text-processed");
            }

            return true;
        }

        private bool TryHandleClarificationIfNeeded(
            string remainingText,
            UserSpeechClassificationResult classification,
            CommunicationInput communicationInput
        )
        {
            if (classification == null)
            {
                return false;
            }

            string responseText = string.Empty;
            string clarificationReason = string.Empty;

            if (classification.SpeechClass == UserSpeechClass.NeedsTarget)
            {
                responseText = "何を見る？";
                clarificationReason = "needs-target";
            }
            else if (classification.SpeechClass == UserSpeechClass.NeedsContext)
            {
                responseText = "どこを見る？";
                clarificationReason = "needs-context";
            }
            else
            {
                return false;
            }

            Debug.Log(
                "[ConversationReactionService] Clarification required. " +
                "class=" +
                classification.SpeechClass +
                " reason=" +
                SafeLog(classification.Reason) +
                " route=clarification" +
                " clarificationReason=" +
                clarificationReason +
                " text=" +
                SafeLog(remainingText) +
                " response=" +
                SafeLog(responseText)
            );

            ReactionDecision decision =
                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.Clarification,
                    string.Empty,
                    clarificationReason
                );

            LogOutputRequest(
                communicationInput,
                decision,
                "clarification",
                responseText,
                "Neutral",
                "not-executed",
                "ConversationReactionService.TryHandleClarificationIfNeeded"
            );

            global::ResponseBus.Raise(responseText);

            Debug.Log(
                "[ConversationReactionService] RemainingText handled. " +
                "route=clarification execution-request=not-created body-action=not-routed"
            );

            if (deferConversationWindowToSpeechInputController)
            {
                StartListeningTimeout("clarification-response");
            }

            return true;
        }

        private bool TryHandleConversationIfNeeded(
            string remainingText,
            UserSpeechClassificationResult classification,
            CommunicationInput communicationInput
        )
        {
            if (classification == null)
            {
                return false;
            }

            if (classification.SpeechClass != UserSpeechClass.Conversation)
            {
                return false;
            }

            Debug.Log(
                "[ConversationReactionService] Conversation classified. " +
                "class=" +
                classification.SpeechClass +
                " reason=" +
                SafeLog(classification.Reason) +
                " route=conversation-service" +
                " text=" +
                SafeLog(remainingText)
            );

            if (!routeConversationToConversationService)
            {
                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.Unavailable,
                    "CONVERSATION",
                    "conversation-route-disabled"
                );

                Debug.Log(
                    "[ConversationReactionService] RemainingText handled. " +
                    "route=conversation-service-disabled execution-request=not-created body-action=not-routed"
                );

                if (deferConversationWindowToSpeechInputController)
                {
                    StartListeningTimeout("conversation-route-disabled");
                }

                return true;
            }

            if (conversationService == null)
            {
                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.Unavailable,
                    "CONVERSATION",
                    "conversation-service-missing"
                );

                Debug.LogWarning(
                    "[ConversationReactionService] Conversation route skipped: " +
                    "ConversationService is not assigned. " +
                    "execution-request=not-created body-action=not-routed text=" +
                    SafeLog(remainingText)
                );

                if (deferConversationWindowToSpeechInputController)
                {
                    StartListeningTimeout("conversation-service-missing");
                }

                return true;
            }

            ReactionDecision decision =
                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.Conversation,
                    "CONVERSATION",
                    classification.Reason
                );

            conversationService.HandleConversation(
                remainingText,
                communicationInput,
                decision
            );

            Debug.Log(
                "[ConversationReactionService] RemainingText handled. " +
                "route=conversation-service execution-request=not-created body-action=not-routed"
            );

            if (deferConversationWindowToSpeechInputController)
            {
                Debug.Log(
                    "[ConversationReactionService] Listening timeout skipped after conversation-service-response. " +
                    "reason=handled-by-speech-input-controller"
                );
            }

            return true;
        }

        private bool EmitActivationAck(
            string ack,
            string remainingText,
            bool hasRemainingText,
            CommunicationInput communicationInput
        )
        {
            if (skipActivationAckWhenRemainingTextExists && hasRemainingText)
            {
                Debug.Log(
                    "[ConversationReactionService] ActivationAck skipped. " +
                    "reason=remaining-text-exists" +
                    " remaining=" +
                    SafeLog(remainingText) +
                    " speak=" +
                    speakActivationAck
                );
                return false;
            }

            Debug.Log(
                "[ConversationReactionService] ActivationAck: " +
                SafeLog(ack) +
                " speak=" +
                speakActivationAck
            );

            if (!speakActivationAck)
            {
                return false;
            }

            if (voiceController == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] ActivationAck speak skipped: " +
                    "VoiceController is not assigned."
                );
                return false;
            }

            ReactionDecision decision =
                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.ActivationAck,
                    string.Empty,
                    hasRemainingText
                        ? "activation-with-remaining-text"
                        : "activation-only"
                );

            LogOutputRequest(
                communicationInput,
                decision,
                "activation-ack",
                ack,
                "Neutral",
                "not-executed",
                "ConversationReactionService.EmitActivationAck"
            );

            voiceController.Speak(ack, activationAckSpeakerName);
            return true;
        }

        private void ScheduleActivationAckReturnToListening(string reason)
        {
            CancelActivationAckReturn("restart:" + reason);

            int token = ++activationAckReturnToken;
            activationAckReturnCoroutine = StartCoroutine(
                ActivationAckReturnToListeningRoutine(token, reason)
            );

            Debug.Log(
                "[ConversationReactionService] ActivationAck return scheduled. " +
                "route=return-to-listening-after-ack reason=" +
                reason +
                " token=" +
                token
            );
        }

        private IEnumerator ActivationAckReturnToListeningRoutine(
            int token,
            string reason
        )
        {
            // Wait one frame so VoicePlaybackController can enter Speaking first.
            yield return null;

            float startTime = Time.time;
            const float maxWaitSeconds = 10.0f;

            while (interactionStateController != null &&
                   interactionStateController.CurrentState == InteractionState.Speaking &&
                   Time.time - startTime < maxWaitSeconds)
            {
                yield return null;
            }

            activationAckReturnCoroutine = null;

            if (token != activationAckReturnToken)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationReactionService] ActivationAck return ignored: stale token. " +
                        "token=" +
                        token +
                        " current=" +
                        activationAckReturnToken
                    );
                }

                yield break;
            }

            if (interactionStateController == null)
            {
                yield break;
            }

            InteractionState current = interactionStateController.CurrentState;

            if (current == InteractionState.Listening)
            {
                Debug.Log(
                    "[ConversationReactionService] ActivationAck return skipped. " +
                    "already Listening reason=" +
                    reason
                );
                yield break;
            }

            if (current == InteractionState.Idle || current == InteractionState.Speaking)
            {
                Debug.Log(
                    "[ConversationReactionService] ActivationAck return. " +
                    current +
                    " -> Listening reason=" +
                    reason
                );

                EnterListeningState();
                yield break;
            }

            Debug.Log(
                "[ConversationReactionService] ActivationAck return skipped. " +
                "state=" +
                current +
                " reason=" +
                reason
            );
        }

        private void CancelActivationAckReturn(string reason)
        {
            activationAckReturnToken++;

            if (activationAckReturnCoroutine == null)
            {
                return;
            }

            StopCoroutine(activationAckReturnCoroutine);
            activationAckReturnCoroutine = null;

            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationReactionService] ActivationAck return cancelled. reason=" +
                    reason
                );
            }
        }

        private void EnterListeningState()
        {
            if (interactionStateController == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] Listening state skipped: " +
                    "InteractionStateController is not assigned."
                );
                return;
            }

            interactionStateController.SetListening();
        }

        private void StartListeningTimeout(string reason)
        {
            // Phase 10-F:
            // ConversationReactionService は会話内容の分類とルーティングだけを担当する。
            // 会話継続ウィンドウ、20秒timeout、MicStandby/Wake待機への復帰は
            // SpeechInputController + RuntimeInteractionSettings 側に集約する。
            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationReactionService] Listening timeout delegated. " +
                    "owner=SpeechInputController reason=" +
                    reason
                );
            }
        }

        private void CancelListeningTimeout(string reason)
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationReactionService] Listening timeout cancel delegated/no-op. " +
                    "owner=SpeechInputController reason=" +
                    reason
                );
            }
        }

        private bool TryRouteFixedBodyActionFromSpeech(
            string sourceText,
            UserSpeechClassificationResult classification,
            string source,
            CommunicationInput communicationInput
        )
        {
            if (!enableFixedBodyActionVoiceRules)
            {
                return false;
            }

            BodyCommandMatch match;

            if (!TryResolveFixedBodyAction(
                    sourceText,
                    classification,
                    out match
                ))
            {
                return false;
            }

            Debug.Log(
                "[ConversationReactionService] Body command matched. " +
                "source=" +
                SafeLog(source) +
                " action=" +
                SafeLog(match.ActionId) +
                " priority=" +
                match.Priority +
                " safetyStop=" +
                match.SafetyStop +
                " reason=" +
                SafeLog(match.Reason) +
                " text=" +
                SafeLog(sourceText)
            );

            ReactionDecision decision =
                LogReactionDecision(
                    communicationInput,
                    ReactionRouteIds.FixedBodyAction,
                    string.Empty,
                    match.Reason,
                    match.ActionId
                );

            if (ShouldAckBeforeFixedBodyAction(match))
            {
                RouteFixedBodyActionWithAck(
                    match,
                    sourceText,
                    source,
                    communicationInput,
                    decision
                );
                return true;
            }

            RouteResolvedFixedBodyAction(match, sourceText, source);
            return true;
        }

        private bool ShouldAckBeforeFixedBodyAction(BodyCommandMatch match)
        {
            if (!speakFixedBodyActionAck)
            {
                return false;
            }

            if (match.SafetyStop)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(fixedBodyActionAckText);
        }

        private void RouteFixedBodyActionWithAck(
            BodyCommandMatch match,
            string sourceText,
            string source,
            CommunicationInput communicationInput,
            ReactionDecision decision
        )
        {
            CancelFixedBodyActionAckRoute("restart");

            int token = ++fixedBodyActionAckRouteToken;
            fixedBodyActionAckCoroutine = StartCoroutine(
                FixedBodyActionAckThenRouteRoutine(
                    token,
                    match,
                    sourceText,
                    source,
                    communicationInput,
                    decision
                )
            );

            Debug.Log(
                "[ConversationReactionService] Fixed BodyAction ack scheduled. " +
                "ack=" +
                SafeLog(fixedBodyActionAckText) +
                " action=" +
                SafeLog(match.ActionId) +
                " source=" +
                SafeLog(source) +
                " text=" +
                SafeLog(sourceText) +
                " delay=" +
                fixedBodyActionAckDelaySeconds.ToString("0.00")
            );
        }

        private IEnumerator FixedBodyActionAckThenRouteRoutine(
            int token,
            BodyCommandMatch match,
            string sourceText,
            string source,
            CommunicationInput communicationInput,
            ReactionDecision decision
        )
        {
            Debug.Log(
                "[ConversationReactionService] Fixed BodyAction ack: " +
                SafeLog(fixedBodyActionAckText) +
                " action=" +
                SafeLog(match.ActionId)
            );

            LogOutputRequest(
                communicationInput,
                decision,
                "body-action-ack",
                fixedBodyActionAckText,
                "Neutral",
                "REQUEST_ACCEPTED",
                "ConversationReactionService.FixedBodyActionAckThenRouteRoutine"
            );

            global::ResponseBus.Raise(fixedBodyActionAckText);

            if (fixedBodyActionAckDelaySeconds > 0.0f)
            {
                yield return new WaitForSeconds(fixedBodyActionAckDelaySeconds);
            }
            else
            {
                yield return null;
            }

            float startTime = Time.time;
            float maxWait = Mathf.Max(0.0f, fixedBodyActionAckMaxWaitSpeakingSeconds);

            while (interactionStateController != null &&
                   interactionStateController.CurrentState == InteractionState.Speaking &&
                   Time.time - startTime < maxWait)
            {
                yield return null;
            }

            fixedBodyActionAckCoroutine = null;

            if (token != fixedBodyActionAckRouteToken)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationReactionService] Fixed BodyAction ack route ignored: stale token. " +
                        "token=" +
                        token +
                        " current=" +
                        fixedBodyActionAckRouteToken
                    );
                }

                yield break;
            }

            RouteResolvedFixedBodyAction(match, sourceText, source);
        }

        private void CancelFixedBodyActionAckRoute(string reason)
        {
            fixedBodyActionAckRouteToken++;

            if (fixedBodyActionAckCoroutine == null)
            {
                return;
            }

            StopCoroutine(fixedBodyActionAckCoroutine);
            fixedBodyActionAckCoroutine = null;

            if (verboseLog)
            {
                Debug.Log(
                    "[ConversationReactionService] Fixed BodyAction ack route cancelled. reason=" +
                    reason
                );
            }
        }

        private void RouteResolvedFixedBodyAction(
            BodyCommandMatch match,
            string sourceText,
            string source
        )
        {
            PrepareRuntimeStateForFixedBodyAction(
                match.ActionId,
                sourceText,
                source
            );

            global::ExecutionRequest request = CreateFixedBodyActionExecutionRequest(
                match.ActionId,
                sourceText,
                source,
                match.Reason,
                match.Priority
            );

            RouteFixedBodyActionExecutionRequestIfNeeded(request);
        }

        private void PrepareRuntimeStateForFixedBodyAction(
            string actionId,
            string sourceText,
            string source
        )
        {
            CancelListeningTimeout("fixed-body-action-before-execution");

            if (interactionStateController == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] Fixed BodyAction state prepare skipped: " +
                    "InteractionStateController is not assigned. action=" +
                    SafeLog(actionId)
                );
                return;
            }

            InteractionState currentState = interactionStateController.CurrentState;

            if (currentState != InteractionState.Listening)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationReactionService] Fixed BodyAction state prepare: " +
                        "no state change required state=" +
                        currentState +
                        " action=" +
                        SafeLog(actionId) +
                        " source=" +
                        SafeLog(source) +
                        " text=" +
                        SafeLog(sourceText)
                    );
                }
                return;
            }

            Debug.Log(
                "[ConversationReactionService] Fixed BodyAction state prepare: " +
                "Listening -> Idle before execution action=" +
                SafeLog(actionId) +
                " source=" +
                SafeLog(source) +
                " text=" +
                SafeLog(sourceText)
            );

            interactionStateController.SetIdle();
        }

        internal static bool TryResolveFixedBodyAction(
            string sourceText,
            UserSpeechClassificationResult classification,
            out BodyCommandMatch match
        )
        {
            if (classification != null &&
                classification.SpeechClass == UserSpeechClass.AdminEmergencyStop)
            {
                match = new BodyCommandMatch(
                    "emergency_stop",
                    "fixed-rule:classifier-admin-emergency-stop",
                    100,
                    true,
                    "classifier:AdminEmergencyStop"
                );
                return true;
            }

            if (classification != null &&
                classification.SpeechClass == UserSpeechClass.StopCommand)
            {
                match = new BodyCommandMatch(
                    "crawler_stop",
                    "fixed-rule:classifier-stop-command",
                    90,
                    true,
                    "classifier:StopCommand"
                );
                return true;
            }

            if (!BodyCommandRegistry.TryMatch(sourceText, out match))
            {
                return false;
            }

            // SR-0:
            // BodyCommandRegistryには将来のMobility Skill等から再利用する
            // Crawler実行語彙を残す。ただし通常Speech routeからは、旧来の
            // 方向語Shortcutだけを隔離し、Conversationを上流で奪わせない。
            if (IsLegacyCrawlerSpeechShortcut(match.ActionId))
            {
                match = default(BodyCommandMatch);
                return false;
            }

            return true;
        }

        private static bool IsLegacyCrawlerSpeechShortcut(string actionId)
        {
            return actionId == "crawler_forward_short" ||
                   actionId == "crawler_back_short" ||
                   actionId == "crawler_turn_left_short" ||
                   actionId == "crawler_turn_right_short";
        }

        private global::ExecutionRequest CreateFixedBodyActionExecutionRequest(
            string actionId,
            string sourceText,
            string source,
            string reason,
            int priority
        )
        {
            global::ExecutionRequest request = new global::ExecutionRequest();
            request.ActionId = actionId;
            request.SourceEventType = "UserSpeech.FixedBodyAction." + source;
            request.SourcePayload = sourceText;
            request.Priority = priority;
            request.RequiresBody = true;
            request.RequiresSpeech = false;
            request.RequiresExpression = false;
            request.RequiresDevice = false;
            request.Reason = reason;
            return request;
        }

        private void RouteFixedBodyActionExecutionRequestIfNeeded(
            global::ExecutionRequest request
        )
        {
            if (!routeFixedBodyActionRequestToExecutionController)
            {
                Debug.Log(
                    "[ConversationReactionService] Fixed BodyAction route skipped. " +
                    "reason=route-disabled action=" +
                    SafeLog(request != null ? request.ActionId : string.Empty)
                );
                return;
            }

            if (request == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] Fixed BodyAction route skipped: request is null."
                );
                return;
            }

            if (executionController == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] Fixed BodyAction route skipped: " +
                    "ExecutionController is not assigned. action=" +
                    SafeLog(request.ActionId)
                );
                return;
            }

            bool accepted = executionController.TryStartExecution(request);

            Debug.Log(
                "[ConversationReactionService] Fixed BodyAction routed. " +
                "action=" +
                SafeLog(request.ActionId) +
                " accepted=" +
                accepted +
                " route=execution-controller" +
                " source=" +
                SafeLog(request.SourceEventType) +
                " reason=" +
                SafeLog(request.Reason)
            );
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value) || needles == null)
            {
                return false;
            }

            for (int i = 0; i < needles.Length; i++)
            {
                string needle = needles[i];

                if (string.IsNullOrEmpty(needle))
                {
                    continue;
                }

                if (value.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactAny(string value, params string[] candidates)
        {
            if (string.IsNullOrEmpty(value) || candidates == null)
            {
                return false;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (value == candidates[i])
                {
                    return true;
                }
            }

            return false;
        }

        private ProgrammedActionCandidate ResolveAndLogProgrammedActionCandidateIfNeeded(
            string remainingText,
            UserSpeechClassificationResult classification
        )
        {
            if (!observeProgrammedActionCandidate)
            {
                return null;
            }

            if (classification == null)
            {
                return null;
            }

            if (classification.SpeechClass != UserSpeechClass.ActionIntent)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationReactionService] ProgrammedActionCandidate skipped. " +
                        "reason=classification-not-action-intent class=" +
                        classification.SpeechClass +
                        " text=" +
                        SafeLog(remainingText)
                    );
                }
                return null;
            }

            ProgrammedActionCandidate candidate =
                ProgrammedActionResolver.Resolve(remainingText);

            if (candidate == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] ProgrammedActionCandidate resolver returned null. " +
                    "text=" +
                    SafeLog(remainingText)
                );
                return null;
            }

            Debug.Log(
                "[ConversationReactionService] ProgrammedActionCandidate resolved. " +
                "primary=" +
                candidate.PrimaryAction +
                " secondary=" +
                candidate.SecondaryAction +
                " requiresContext=" +
                candidate.RequiresAdditionalContext +
                " reason=" +
                SafeLog(candidate.Reason) +
                " text=" +
                SafeLog(candidate.SourceText) +
                " normalized=" +
                SafeLog(candidate.NormalizedText) +
                " route=execution-request-pipeline"
            );

            return candidate;
        }

        private void PrepareAndLogProgrammedActionExecutionRequestIfNeeded(
            string remainingText,
            UserSpeechClassificationResult classification,
            ProgrammedActionCandidate candidate
        )
        {
            if (!prepareProgrammedActionExecutionRequest)
            {
                return;
            }

            if (classification == null)
            {
                return;
            }

            if (classification.SpeechClass != UserSpeechClass.ActionIntent)
            {
                return;
            }

            if (candidate == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationReactionService] ProgrammedAction ExecutionRequest skipped. " +
                        "reason=no-candidate text=" +
                        SafeLog(remainingText)
                    );
                }
                return;
            }

            global::ExecutionRequest request;
            string skipReason;

            bool created = ProgrammedActionExecutionMapper.TryCreate(
                candidate,
                remainingText,
                out request,
                out skipReason
            );

            if (!created || request == null)
            {
                Debug.Log(
                    "[ConversationReactionService] ProgrammedAction ExecutionRequest skipped. " +
                    "primary=" +
                    candidate.PrimaryAction +
                    " requiresContext=" +
                    candidate.RequiresAdditionalContext +
                    " reason=" +
                    SafeLog(skipReason) +
                    " text=" +
                    SafeLog(remainingText) +
                    " route=no-execution-request"
                );
                return;
            }

            Debug.Log(
                "[ConversationReactionService] ProgrammedAction ExecutionRequest prepared. " +
                "action=" +
                SafeLog(request.ActionId) +
                " source=" +
                SafeLog(request.SourceEventType) +
                " payload=" +
                SafeLog(request.SourcePayload) +
                " priority=" +
                request.Priority +
                " requiresBody=" +
                request.RequiresBody +
                " requiresSpeech=" +
                request.RequiresSpeech +
                " requiresExpression=" +
                request.RequiresExpression +
                " requiresDevice=" +
                request.RequiresDevice +
                " reason=" +
                SafeLog(request.Reason) +
                " route=execution-controller-candidate"
            );

            RouteProgrammedActionExecutionRequestIfNeeded(request);
        }

        private void RouteProgrammedActionExecutionRequestIfNeeded(global::ExecutionRequest request)
        {
            if (!routeProgrammedActionRequestToExecutionController)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ConversationReactionService] ProgrammedAction ExecutionRequest route skipped. " +
                        "reason=route-disabled action=" +
                        SafeLog(request != null ? request.ActionId : string.Empty)
                    );
                }

                return;
            }

            if (request == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] ProgrammedAction ExecutionRequest route skipped: request is null."
                );
                return;
            }

            if (executionController == null)
            {
                Debug.LogWarning(
                    "[ConversationReactionService] ProgrammedAction ExecutionRequest route skipped: " +
                    "ExecutionController is not assigned. action=" +
                    SafeLog(request.ActionId)
                );
                return;
            }

            bool accepted = executionController.TryStartExecution(request);

            Debug.Log(
                "[ConversationReactionService] ProgrammedAction ExecutionRequest routed. " +
                "action=" +
                SafeLog(request.ActionId) +
                " accepted=" +
                accepted +
                " route=execution-controller"
            );
        }

        private void LogClassification(
            string source,
            UserSpeechClassificationResult result
        )
        {
            if (result == null)
            {
                return;
            }

            Debug.Log(
                "[ConversationReactionService] UserSpeech classified. " +
                "source=" +
                source +
                " class=" +
                result.SpeechClass +
                " reason=" +
                SafeLog(result.Reason) +
                " text=" +
                SafeLog(result.Text)
            );
        }

        private void EnsureActivationProfile()
        {
            if (activationProfile != null)
            {
                return;
            }

            ReloadActivationProfile();
        }

        private static ReactionDecision LogReactionDecision(
            CommunicationInput communicationInput,
            string route,
            string selectedSkillId,
            string reason,
            string actionId = "",
            string directionCandidate = "",
            string targetCandidate = ""
        )
        {
            ReactionDecision decision =
                ReactionDecision.Create(
                    communicationInput,
                    route,
                    selectedSkillId,
                    reason,
                    actionId: actionId,
                    directionCandidate: directionCandidate,
                    targetCandidate: targetCandidate
                );

            InteractionTraceLogger.LogDecision(decision);
            CglLegacyComparisonService.TryCompareLegacyDecision(decision);
            return decision;
        }

        private static void LogOutputRequest(
            CommunicationInput communicationInput,
            ReactionDecision decision,
            string responseType,
            string text,
            string emotion,
            string skillStatus,
            string caller
        )
        {
            ReactionOutcome outcome =
                ReactionOutcome.CreateOutputRequest(
                    communicationInput,
                    decision,
                    communicationInput != null
                        ? communicationInput.Source.ToString()
                        : string.Empty,
                    responseType,
                    text,
                    emotion,
                    skillStatus,
                    caller
                );

            InteractionTraceLogger.LogOutcome(outcome);
        }

        private static string SafeLog(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }

        private static string FormatAliasesForLog(string[] aliases)
        {
            if (aliases == null || aliases.Length == 0)
            {
                return "<none>";
            }

            return string.Join(",", aliases);
        }
    }
}

// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

using SalieriAI.Core.Perception.Attention;
using SalieriAI.Core.Perception.ObjectTargeting;
using SalieriAI.Core.Perception.ObjectTracking;
using SalieriAI.Core.Perception.VisualSnapshots;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Experience.AnswerBinding;
using SalieriAI.Core.Experience.Recall;
using SalieriAI.Core.Experience.Storage;
using SalieriAI.Core.Behavior.FindPointAsk.PhysicalIntegration;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Limbo;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.Skills;
using SalieriAI.Core.Skills.Body.Pointing;
using SalieriAI.Core.Skills.Communication;
using SalieriAI.Core.Skills.Perception.ObjectTargeting;

using UnityEngine;

namespace SalieriAI.Core.Behavior.FindPointAsk
{
    /// <summary>
    /// Find -> Point -> Ask v0.1 の薄いProcedure controller。
    ///
    /// WAIT_TARGET -> TARGET_SELECTED -> LOOK -> POINT -> ASK -> WAIT_ANSWER
    ///
    /// 対象選択、共有AttentionTarget、腕IK Target、ResponseBus音声経路を再利用し、
    /// YOLO、VRM bone、VirtualBody、Servoを直接操作しない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FindPointAskBehaviorController : MonoBehaviour
    {
        private const string SourceKey = "behavior:find_point_ask:v0.1";
        private const float PointSoftwareSettleTimeoutSeconds =
            PointSoftwareSettleGate.DefaultTimeoutSeconds;

        [Header("Existing Target / LOOK Path")]
        [SerializeField] private ObservationTargetSelectionService selectionService;
        [SerializeField] private OrientationTargetResolver orientationResolver;
        [SerializeField] private OrientationPriorityRequestService priorityRequestService;
        [SerializeField] private OrientationResolutionTargetDriver targetDriver;

        [Header("Existing POINT Target Path")]
        [SerializeField] private AttentionTargetArmPointingController pointingController;

        [Header("Existing Speech Lifecycle Path")]
        [SerializeField] private global::VoicePlaybackController voicePlaybackController;

        [Header("Behavior v0.1")]
        [SerializeField] private string questionText = "これは何ですか？";
        [SerializeField, Min(0.1f)] private float targetWaitTimeoutSeconds = 12f;
        [SerializeField, Min(0.1f)] private float lookTimeoutSeconds = 5f;
        [SerializeField, Min(1f)] private float answerWaitTimeoutSeconds = 30f;
        [SerializeField, Min(1f)] private float acknowledgementTimeoutSeconds = 120f;
        [SerializeField, Range(0, 100)] private int orientationPriority = 80;

        [Header("Manual One-Shot Gate")]
        [Tooltip("初期値はOFF。Enable And Start One Shot操作でのみ1回有効化する。")]
        [SerializeField] private bool manualOneShotEnabled;

        [Header("M1 Editor Manual Answer")]
        [SerializeField, TextArea] private string editorManualAnswerText = "ボール";

        [Header("M3 Editor ReadBack")]
        [SerializeField] private string editorReadBackRecordId = string.Empty;

        [Header("Runtime Diagnostics (Read Only)")]
        [SerializeField] private string diagnosticState = "Idle";
        [SerializeField] private string diagnosticTargetKey = string.Empty;
        [SerializeField] private string diagnosticMessage = "Disabled by default.";
        [SerializeField] private bool oneShotConsumed;
        [SerializeField] private string diagnosticQuestionId = string.Empty;
        [SerializeField] private string diagnosticSpeechLifecycle = "None";
        [SerializeField] private bool diagnosticWaitAnswer;
        [SerializeField] private string diagnosticRecallStatus = "NoKey";
        [SerializeField] private string diagnosticRecallKey = string.Empty;
        [SerializeField] private string diagnosticRecallAnswer = string.Empty;

        [Header("M6 Physical Integration Diagnostics (Read Only)")]
        [SerializeField] private string diagnosticM6BehaviorRunId = string.Empty;
        [SerializeField] private string diagnosticM6FrozenTargetKey = string.Empty;
        [SerializeField] private bool diagnosticM6NeckCommandDispatched;
        [SerializeField] private bool diagnosticM6ArmCommandDispatched;
        [SerializeField] private string diagnosticM6NeckCompletion = "NotObserved";
        [SerializeField] private string diagnosticM6ArmCompletion = "NotObserved";
        [SerializeField] private string diagnosticM6PoseOwnership = "None";
        [SerializeField] private string diagnosticM6PoseReleaseReason = "None";

        private FindPointAskBehaviorFlow flow = new FindPointAskBehaviorFlow();
        private QuestionInteractionSession questionSession =
            new QuestionInteractionSession();
        private readonly AnswerBindingService answerBindingService =
            new AnswerBindingService();
        private IExperienceStore experienceStore;
        private ExperiencePersistenceService experiencePersistenceService;
        private Coroutine runningRoutine;
        private Coroutine answerWaitRoutine;
        private Coroutine acknowledgementTimeoutRoutine;
        private string behaviorRunId = string.Empty;
        private string pendingAcknowledgementCorrelationId = string.Empty;
        private string pendingAcknowledgementSpeechId = string.Empty;
        private bool acknowledgementStarted;
        private bool awaitingQuestionSpeechRequest;
        private bool speechLifecycleSubscribed;
        private FindPointAskPhysicalInteractionTracker physicalInteractionTracker;
        private FindPointAskPhysicalInteractionObserver physicalInteractionObserver;
        private SkillRuntime productionSkillRuntime;
        private LimboPermission limboPermission;
        private NeckActionExecutor recoveryNeckActionExecutor;
        private ByteTrackObjectTrackingService recoveryTrackingService;
        private AutonomousLookAroundPolicy recoveryAutonomousPolicy;
        private FindPointAskPreQuestionRecoveryGate activeRecoveryGate;
        private bool preQuestionRecoveryActive;
        private float preQuestionRecoveryStartedAt;

        private sealed class LookPointAttemptResult
        {
            public bool Completed;
            public bool RecoveryRequired;
            public FindPointAskRecoveryStage LostStage;
            public long LostObservationSourceFrameId;

            public void RequireRecovery(
                FindPointAskRecoveryStage stage,
                long sourceFrameId)
            {
                RecoveryRequired = true;
                LostStage = stage;
                LostObservationSourceFrameId = Math.Max(0L, sourceFrameId);
            }
        }

        private sealed class PreQuestionRecoveryResult
        {
            public bool Succeeded;
            public SelectObservationTargetResult Selected;
        }

        public FindPointAskBehaviorFlow.BehaviorState State => flow.State;
        public string CurrentTargetKey => flow.TargetKey;
        public string FailureReason => flow.FailureReason;
        public string QuestionText => questionText;
        public bool ManualOneShotEnabled => manualOneShotEnabled;
        public bool OneShotConsumed => oneShotConsumed;
        public string DiagnosticState => diagnosticState;
        public string DiagnosticMessage => diagnosticMessage;
        public QuestionContext CurrentQuestionContext =>
            questionSession.CurrentQuestion;
        public bool IsWaitingForAnswer => questionSession.IsWaitingForAnswer;
        public bool CanAcceptProductionAnswer =>
            flow.State == FindPointAskBehaviorFlow.BehaviorState.WaitAnswer &&
            questionSession.IsWaitingForAnswer;
        public EditorManualAnswerReceipt AcceptedEditorManualAnswer =>
            questionSession.AcceptedAnswer as EditorManualAnswerReceipt;
        public AnswerBindingResult LastAnswerBindingResult { get; private set; }
        public ExperienceSaveResult LastExperienceSaveResult { get; private set; }
        public OneShotAnswerOutcome LastAnswerOutcome { get; private set; }
        public ExperienceRecord LastReadExperienceRecord { get; private set; }
        public ExperienceRecallResult LastRecallResult { get; private set; }
        public string ExperienceStoragePath =>
            GetExperienceStoragePath();
        public FindPointAskPhysicalInteractionState PhysicalInteractionState =>
            physicalInteractionTracker != null
                ? physicalInteractionTracker.CurrentState
                : null;

        public event Action<FindPointAskBehaviorFlow.BehaviorState> StateChanged;
        public event Action<string, string> WaitingForAnswer;
        public event Action<EditorManualAnswerReceipt> AnswerReceived;
        public event Action<AnswerBindingResult> AnswerContextResolved;
        public event Action<OneShotAnswerOutcome> AnswerOutcomeReady;

        private void Awake()
        {
            ResolveVoicePlaybackController();
            limboPermission = FindObjectOfType<LimboPermission>();
            ResolvePreQuestionRecoveryDependencies();
        }

        private void OnEnable()
        {
            SubscribeSpeechLifecycle();
        }

        private void Update()
        {
            if (ObservePreQuestionRecoveryOverride())
                return;

            if (ObserveOutcomeLifecycleEmergency())
                return;

            if (ObserveQuestionRetentionOverride())
                return;

            if (physicalInteractionObserver == null ||
                physicalInteractionTracker == null)
            {
                return;
            }

            physicalInteractionObserver.Pump(physicalInteractionTracker);
            UpdateM6Diagnostics();
        }

        [ContextMenu("Enable And Start One Shot")]
        public void EnableAndStartOneShot()
        {
            if (runningRoutine != null ||
                IsBehaviorActive(flow.State))
            {
                SetDiagnostic("One-shot is already active or waiting for an answer.");
                return;
            }

            manualOneShotEnabled = true;
            oneShotConsumed = false;
            StartBehavior();
        }

        [ContextMenu("Start Find Point Ask v0.1")]
        public void StartBehavior()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FindPointAskBehavior] Play Mode only.", this);
                return;
            }

            if (!manualOneShotEnabled || oneShotConsumed)
            {
                SetDiagnostic(
                    "Disabled by default. Use Enable And Start One Shot.");
                return;
            }

            if (runningRoutine != null)
                return;

            if (!ValidateReferences(out string error))
            {
                Fail(error);
                return;
            }

            if (!EnsureProductionSelectSkillRuntime(out error))
            {
                Fail("Production Skill Runtime unavailable: " + error);
                return;
            }

            priorityRequestService.RemoveBySourceKey(SourceKey);
            if (!flow.Start())
            {
                SetDiagnostic("The behavior flow rejected a new start.");
                return;
            }
            questionSession = new QuestionInteractionSession();
            behaviorRunId = Guid.NewGuid().ToString("N");
            awaitingQuestionSpeechRequest = false;
            ClearAcknowledgementTracking();
            physicalInteractionTracker = null;
            physicalInteractionObserver = null;
            ResolveVoicePlaybackController();
            SubscribeSpeechLifecycle();
            manualOneShotEnabled = false;
            oneShotConsumed = true;
            NotifyState("Waiting for a stable ObservationTarget.");
            runningRoutine = StartCoroutine(RunBehavior());
        }

        [ContextMenu("Cancel Find Point Ask")]
        public void CancelBehavior()
        {
            CancelBehavior(
                FindPointAskPhysicalPoseReleaseReason.Interrupted,
                "One-shot was interrupted.");
        }

        private void CancelBehavior(
            FindPointAskPhysicalPoseReleaseReason releaseReason,
            string message)
        {
            StopAnswerWaitTimeout();
            StopAcknowledgementTimeout();
            ClearAcknowledgementTracking();
            if (runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                runningRoutine = null;
            }

            questionSession.TryCancel(out _);
            awaitingQuestionSpeechRequest = false;
            if (flow.Cancel())
                NotifyState(message);
            else
                UpdateQuestionDiagnostics();
            ReleaseOwnedOutputs(releaseReason);
        }

        private void OnDisable()
        {
            CancelBehavior(
                FindPointAskPhysicalPoseReleaseReason.Disabled,
                "One-shot was disabled.");
            UnsubscribeSpeechLifecycle();
        }

        [ContextMenu("M1 Editor: Simulate Speech Started")]
        public void SimulateEditorSpeechStarted()
        {
            ApplyQuestionSpeechLifecycle(
                questionSession.SpeechInstanceId,
                QuestionSpeechLifecycleState.Started,
                "Editor simulated SpeechStarted.");
        }

        [ContextMenu("M1 Editor: Simulate Speech Completed")]
        public void SimulateEditorSpeechCompleted()
        {
            ApplyQuestionSpeechLifecycle(
                questionSession.SpeechInstanceId,
                QuestionSpeechLifecycleState.Completed,
                "Editor simulated SpeechCompleted.");
        }

        [ContextMenu("M1 Editor: Submit Manual Answer")]
        public void SubmitConfiguredEditorManualAnswer()
        {
            SubmitEditorManualAnswer(editorManualAnswerText);
        }

        public bool SubmitEditorManualAnswer(string text)
        {
            if (flow.State !=
                FindPointAskBehaviorFlow.BehaviorState.WaitAnswer)
            {
                SetDiagnostic(
                    "Editor manual answer rejected: behavior is not WAIT_ANSWER.");
                return false;
            }

            if (!questionSession.TryAcceptEditorManualAnswer(
                    text,
                    DateTime.UtcNow,
                    out EditorManualAnswerReceipt receipt,
                    out string error))
            {
                SetDiagnostic("Editor manual answer rejected: " + error);
                return false;
            }

            return CompleteAcceptedAnswer(receipt, true, out _);
        }

        public bool TrySubmitProductionAnswer(
            string answerCandidate,
            CommunicationInput communicationInput,
            out OneShotAnswerOutcome outcome,
            out string error)
        {
            outcome = null;
            error = string.Empty;
            if (communicationInput == null)
            {
                error = "CommunicationInput is missing.";
                return false;
            }

            if (flow.State !=
                FindPointAskBehaviorFlow.BehaviorState.WaitAnswer)
            {
                error = "Behavior is not WAIT_ANSWER.";
                return false;
            }

            if (!questionSession.IsWaitingForAnswer)
            {
                error = "No Active completed QuestionContext is waiting.";
                return false;
            }

            if (!questionSession.TryAcceptAnswer(
                    answerCandidate,
                    communicationInput.Source,
                    communicationInput.InputId,
                    communicationInput.TimestampUtc,
                    out QuestionAnswerReceipt receipt,
                    out error))
            {
                return false;
            }

            if (!CompleteAcceptedAnswer(receipt, false, out outcome))
            {
                error = "Accepted answer could not enter the outcome lifecycle.";
                return false;
            }
            return true;
        }

        private bool CompleteAcceptedAnswer(
            QuestionAnswerReceipt receipt,
            bool raiseEditorManualEvent,
            out OneShotAnswerOutcome outcome)
        {
            outcome = null;
            StopAnswerWaitTimeout();
            if (receipt == null || !flow.AcceptAnswer())
            {
                SetDiagnostic(
                    "Accepted answer could not enter AnswerAccepted.");
                return false;
            }

            NotifyState(
                "[OneShotLifecycle][ANSWER_ACCEPTED] QuestionId=" + receipt.QuestionId +
                " InputSource=" + receipt.InputSource +
                " InputId=" + receipt.InputId);
            ReleaseOwnedOutputs(
                FindPointAskPhysicalPoseReleaseReason.AnswerReceived);
            if (raiseEditorManualEvent &&
                receipt is EditorManualAnswerReceipt editorReceipt)
            {
                AnswerReceived?.Invoke(editorReceipt);
            }

            if (!flow.BeginBinding())
            {
                Fail("Answer lifecycle could not enter Binding.");
                return false;
            }
            NotifyState("[OneShotLifecycle][BINDING]");
            BindAnswerReceived(receipt);

            if (!flow.BeginSaving())
            {
                Fail("Answer lifecycle could not enter Saving.");
                return false;
            }
            NotifyState("[OneShotLifecycle][SAVE_PENDING]");
            PersistMatchedAnswer(LastAnswerBindingResult);

            outcome = OneShotAnswerOutcome.FromCompletedAnswer(
                receipt,
                LastAnswerBindingResult,
                LastExperienceSaveResult);
            LastAnswerOutcome = outcome;

            if (!outcome.BindingMatched)
            {
                Fail("Binding terminal: " + outcome.CorrelationStatus);
            }
            else if (!outcome.Succeeded)
            {
                Fail("Save terminal: " + outcome.SaveStatus);
            }
            else
            {
                NotifyState(
                    "[OneShotLifecycle][SAVE_SAVED] OutcomeId=" +
                    outcome.OutcomeId);
            }
            AnswerOutcomeReady?.Invoke(outcome);
            return true;
        }

        private void BindAnswerReceived(QuestionAnswerReceipt receipt)
        {
            LastAnswerBindingResult = answerBindingService.Bind(receipt);
            Debug.Log(
                "[FindPointAskBehavior][ANSWER_BINDING] " +
                "QuestionId=" + receipt.QuestionId +
                " CorrelationStatus=" + LastAnswerBindingResult.Status +
                " CanProceed=" + LastAnswerBindingResult.CanProceedToExperience,
                this);
            AnswerContextResolved?.Invoke(LastAnswerBindingResult);
        }

        public bool TryBeginAcknowledgement(
            OneShotAnswerOutcome outcome,
            out string correlationId,
            out string error)
        {
            correlationId = string.Empty;
            error = string.Empty;
            if (outcome == null || !outcome.Succeeded ||
                LastAnswerOutcome == null ||
                !string.Equals(
                    outcome.OutcomeId,
                    LastAnswerOutcome.OutcomeId,
                    StringComparison.Ordinal))
            {
                error = "ACK outcome does not match the saved OneShot outcome.";
                return false;
            }
            if (!flow.BeginAcknowledgement())
            {
                error = "Behavior is not waiting for ACK after Save Saved.";
                return false;
            }

            pendingAcknowledgementCorrelationId = outcome.OutcomeId;
            pendingAcknowledgementSpeechId = string.Empty;
            acknowledgementStarted = false;
            correlationId = pendingAcknowledgementCorrelationId;
            if (Application.isPlaying)
            {
                acknowledgementTimeoutRoutine =
                    StartCoroutine(WaitForAcknowledgementTimeout());
            }
            NotifyState(
                "[OneShotLifecycle][ACK_REQUESTED] Correlation=" +
                correlationId);
            return true;
        }

        public void ReportAcknowledgementDispatchFailure(
            string correlationId,
            string reason)
        {
            if (!IsPendingAcknowledgement(correlationId))
                return;
            FailAcknowledgement("DispatchFailed", reason);
        }

        private void PersistMatchedAnswer(AnswerBindingResult binding)
        {
            EnsureExperiencePersistence();
            LastExperienceSaveResult = experiencePersistenceService.SaveMatched(
                binding,
                DateTime.UtcNow);
            if (LastExperienceSaveResult.Record != null)
                editorReadBackRecordId = LastExperienceSaveResult.Record.RecordId;

            Debug.Log(
                "[FindPointAskBehavior][EXPERIENCE_SAVE] " +
                "Status=" + LastExperienceSaveResult.Status +
                " RecordId=" +
                (LastExperienceSaveResult.Record != null
                    ? LastExperienceSaveResult.Record.RecordId
                    : string.Empty) +
                " Path=" + GetExperienceStoragePath() +
                " Error=" + LastExperienceSaveResult.Error,
                this);
        }

        [ContextMenu("M3 Editor: Read Experience By RecordId")]
        public void ReadConfiguredExperienceRecord()
        {
            if (!TryReadExperienceByRecordId(
                    editorReadBackRecordId,
                    out ExperienceRecord record,
                    out string error))
            {
                Debug.LogWarning(
                    "[FindPointAskBehavior][EXPERIENCE_READ] " + error,
                    this);
                return;
            }

            Debug.Log(
                "[FindPointAskBehavior][EXPERIENCE_READ] " +
                "RecordId=" + record.RecordId +
                " QuestionId=" + record.QuestionId +
                " BehaviorRunId=" + record.BehaviorRunId +
                " TargetKey=" + record.TargetKey +
                " TrackId=" + record.TrackId +
                " RawAnswer=" + record.RawAnswer +
                " NormalizedAnswer=" + record.NormalizedAnswer +
                " InputSource=" + record.InputSource,
                this);
        }

        public bool TryReadExperienceByRecordId(
            string recordId,
            out ExperienceRecord record,
            out string error)
        {
            EnsureExperiencePersistence();
            bool found = experienceStore.TryReadByRecordId(
                recordId,
                out record,
                out error);
            LastReadExperienceRecord = found ? record : null;
            return found;
        }

        private ExperienceRecallResult ResolveRecall(string recallKey)
        {
            EnsureExperiencePersistence();
            LastRecallResult = new ExperienceRecallResolver(experienceStore)
                .Resolve(recallKey);
            diagnosticRecallStatus = LastRecallResult.Status.ToString();
            diagnosticRecallKey = LastRecallResult.RecallKey;
            diagnosticRecallAnswer = LastRecallResult.NormalizedAnswer;
            Debug.Log(
                "[FindPointAskBehavior][RECALL] Status=" +
                LastRecallResult.Status +
                " RecallKey=" + LastRecallResult.RecallKey +
                " Answer=" + LastRecallResult.NormalizedAnswer +
                " Matches=" + LastRecallResult.MatchedRecordIds.Count +
                " Error=" + LastRecallResult.Error,
                this);
            return LastRecallResult;
        }

        private void EnsureExperiencePersistence()
        {
            if (experienceStore != null && experiencePersistenceService != null)
                return;
            experienceStore = new JsonFileExperienceStore(
                GetExperienceStoragePath());
            experiencePersistenceService =
                new ExperiencePersistenceService(experienceStore);
        }

        private static string GetExperienceStoragePath()
        {
            return ExperienceProductionStorePath.Resolve();
        }

        private IEnumerator RunBehavior()
        {
            float targetDeadline = Time.unscaledTime + targetWaitTimeoutSeconds;
            while (Time.unscaledTime < targetDeadline &&
                   (selectionService.CurrentTarget == null ||
                    selectionService.CurrentTarget.TrackSnapshot == null))
            {
                yield return null;
            }

            if (selectionService.CurrentTarget == null)
            {
                Fail("No stable ObservationTarget became available.");
                yield break;
            }

            Task<SkillExecutionResult> selectTask =
                productionSkillRuntime.ExecuteAsync(
                SelectObservationTargetSkill.Id,
                SelectObservationTargetSkill.CurrentVersion,
                new SelectObservationTargetRequest(),
                CreateSkillContext("select_target"));
            yield return WaitForTask(selectTask);

            SkillExecutionResult selectResult = GetCompletedResult(selectTask);
            SelectObservationTargetResult selected =
                selectResult != null
                    ? selectResult.Payload as SelectObservationTargetResult
                    : null;
            if (selectResult == null || !selectResult.IsSuccess || selected == null ||
                !flow.SelectTarget(selected.TargetKey))
            {
                Fail(selectResult != null
                    ? selectResult.Message
                    : "ObservationTarget Skill failed.");
                yield break;
            }

            if (!TryFreezeSelectedVisualCrop(
                    selected,
                    out VisualCropSnapshot selectedVisualCrop,
                    out string visualError))
            {
                Fail("Selected target visual snapshot unavailable: " + visualError);
                yield break;
            }

            if (!TryBeginPhysicalInteractionTracking(
                    selected,
                    out string physicalTrackingError))
            {
                Fail(physicalTrackingError);
                yield break;
            }


            string recallKeyCandidate = string.Empty;
            if (RecallKeyV0Builder.TryBuild(
                    selected.ClassId,
                    selectedVisualCrop,
                    out RecallKeyV0 recallKey,
                    out string recallBuildError))
            {
                recallKeyCandidate = recallKey.Serialized;
            }
            else
            {
                Debug.LogWarning(
                    "[FindPointAskBehavior][RECALL_KEY] Unavailable: " +
                    recallBuildError,
                    this);
            }

            ExperienceRecallResult recall = ResolveRecall(recallKeyCandidate);
            if (recall.IsKnown)
            {
                if (!flow.CompleteKnownWithoutQuestion())
                {
                    Fail("Known recall could not complete from TARGET_SELECTED.");
                    yield break;
                }

                runningRoutine = null;
                physicalInteractionTracker.TryRecordKnownSuppressed(
                    flow.TargetKey);
                NotifyState(
                    "Known experience recalled; ASK suppressed. Answer=" +
                    recall.NormalizedAnswer);
                ReleaseOwnedOutputs(
                    FindPointAskPhysicalPoseReleaseReason.KnownSuppressed);
                yield break;
            }
            NotifyState("ObservationTarget selected.");

            if (!flow.BeginLook())
            {
                Fail("Invalid TARGET_SELECTED transition.");
                yield break;
            }

            SelectObservationTargetResult motionSelected = selected;
            while (true)
            {
                var attempt = new LookPointAttemptResult();
                yield return RunLookPointAttempt(motionSelected, attempt);
                if (attempt.Completed)
                    break;

                if (!attempt.RecoveryRequired)
                    yield break;

                var recovery = new PreQuestionRecoveryResult();
                yield return RecoverPreQuestionTarget(
                    motionSelected,
                    attempt.LostStage,
                    attempt.LostObservationSourceFrameId,
                    recovery);
                if (!recovery.Succeeded || recovery.Selected == null)
                    yield break;

                motionSelected = recovery.Selected;
            }

            QuestionContext questionContext =
                QuestionContext.CreateWithVisualEvidence(
                Guid.NewGuid().ToString("N"),
                behaviorRunId,
                selected.TargetKey,
                selected.TrackId,
                BuildObservationReference(selected),
                QuestionType.IdentifyObject,
                DateTime.UtcNow,
                selectedVisualCrop,
                recallKeyCandidate);
            if (!questionSession.TryCreateQuestion(
                    questionContext,
                    out string questionError))
            {
                Fail("QuestionContext creation failed: " + questionError);
                yield break;
            }
            if (!physicalInteractionTracker.TryRecordQuestion(
                    questionContext.TargetKey,
                    questionContext.QuestionId))
            {
                Fail("M6 QuestionContext target did not match the frozen target.");
                yield break;
            }
            UpdateQuestionDiagnostics();
            NotifyState("QuestionContext created before ASK.");

            AskQuestionSkill askSkill = new AskQuestionSkill(
                new ResponseBusQuestionSpeechRequestSink());
            if (!physicalInteractionTracker.TryRecordAskRequested(
                    flow.TargetKey))
            {
                Fail("M6 ASK target did not match the frozen target.");
                yield break;
            }
            awaitingQuestionSpeechRequest = true;
            Task<SkillExecutionResult> askTask = askSkill.ExecuteUntypedAsync(
                new AskQuestionRequest(questionText),
                CreateSkillContext("ask"));
            yield return WaitForTask(askTask);
            awaitingQuestionSpeechRequest = false;
            SkillExecutionResult askResult = GetCompletedResult(askTask);
            if (askResult == null || !askResult.IsSuccess)
            {
                Fail(askResult != null
                    ? askResult.Message
                    : "ASK Skill failed.");
                yield break;
            }

            if (questionSession.SpeechState ==
                QuestionSpeechLifecycleState.None)
            {
                string editorSpeechInstanceId =
                    "question-speech:" + questionContext.QuestionId;
                if (!questionSession.TryObserveSpeech(
                        editorSpeechInstanceId,
                        QuestionSpeechLifecycleState.Requested,
                        out string requestedError))
                {
                    Fail("SpeechRequested was not recorded: " + requestedError);
                    yield break;
                }
                physicalInteractionTracker.TryRecordSpeechRequested(
                    editorSpeechInstanceId);
            }

            UpdateQuestionDiagnostics();
            NotifyState(
                "Question speech requested; WAIT_ANSWER remains false until playback completion.");
            runningRoutine = null;
        }

        private IEnumerator RunLookPointAttempt(
            SelectObservationTargetResult selected,
            LookPointAttemptResult result)
        {
            long lastFrozenTargetSourceFrameId =
                GetRecoveryBaselineSourceFrameId(selected.SourceFrameId);
            var lookSettleGate = new LookVisualSoftwareSettleGate(
                flow.TargetKey,
                GetLookStartedSourceFrameId(selected.SourceFrameId));

            if (!SubmitOrientation(
                    OrientationPriorityDirective.PreferObject,
                    out string lookError))
            {
                Fail(lookError);
                yield break;
            }
            if (!physicalInteractionTracker.TryRecordLookRequested(
                    flow.TargetKey))
            {
                Fail("M6 LOOK causal target could not be recorded.");
                yield break;
            }
            NotifyState("LOOK priority request submitted.");

            bool frozenTargetWasResolvedByResolver =
                IsFrozenTargetResolvedByResolver();
            float lookSettleStartedAt = Time.unscaledTime;
            LookVisualSoftwareSettleDecision lastLoggedLookDecision =
                (LookVisualSoftwareSettleDecision)(-1);
            while (true)
            {
                LookVisualSoftwareSettleObservation lookObservation =
                    GetLookVisualSoftwareSettleObservation();
                if (lookObservation.HasDirectObjectObservation &&
                    string.Equals(
                        lookObservation.ResolverTargetKey,
                        flow.TargetKey,
                        StringComparison.Ordinal))
                {
                    frozenTargetWasResolvedByResolver = true;
                    lastFrozenTargetSourceFrameId = Math.Max(
                        lastFrozenTargetSourceFrameId,
                        lookObservation.SourceFrameId);
                }

                bool resolverStillHasFrozenTarget =
                    IsFrozenTargetResolvedByResolver();
                if (FindPointAskPreQuestionRecoveryGate
                    .RequiresRecoveryFromResolverState(
                        frozenTargetWasResolvedByResolver,
                        resolverStillHasFrozenTarget,
                        IsCurrentSelectionFrozenTarget()))
                {
                    physicalInteractionTracker.TryRecordTargetLost(false);
                    result.RequireRecovery(
                        FindPointAskRecoveryStage.Look,
                        GetRecoveryBaselineSourceFrameId(
                            lastFrozenTargetSourceFrameId));
                    yield break;
                }

                LookVisualSoftwareSettleDecision lookDecision =
                    lookSettleGate.Evaluate(
                        lookObservation,
                        Time.unscaledTime - lookSettleStartedAt,
                        lookTimeoutSeconds);

                if (lookDecision != lastLoggedLookDecision)
                {
                    LogLookSettleTransition(
                        lookDecision,
                        lookObservation,
                        lookSettleGate.LastReason);
                    lastLoggedLookDecision = lookDecision;
                }

                if (lookDecision ==
                    LookVisualSoftwareSettleDecision.Settled)
                {
                    if (!lookSettleGate.TryConsumePointEntry())
                    {
                        Fail("LOOK software settle POINT entry was already consumed.");
                        yield break;
                    }

                    break;
                }

                if (lookDecision ==
                    LookVisualSoftwareSettleDecision.TimedOut)
                {
                    physicalInteractionTracker.TryRecordTargetLost(false);
                    Fail(
                        "LOOK visual/software settle timed out. " +
                        lookSettleGate.LastReason);
                    yield break;
                }

                yield return null;
            }

            if (!SubmitOrientation(
                    OrientationPriorityDirective.HoldPrevious,
                    flow.TargetKey,
                    out string holdError))
            {
                Fail(holdError);
                yield break;
            }

            if (!flow.ConfirmLook())
            {
                Fail("Invalid LOOK transition.");
                yield break;
            }
            if (!physicalInteractionTracker.TryRecordLookLogicalConfirmed(
                    flow.TargetKey))
            {
                Fail("M6 LOOK logical correlation did not match the frozen target.");
                yield break;
            }
            NotifyState("LOOK software settled; POINT starting.");

            if (!IsFrozenTargetResolvedByResolver())
            {
                physicalInteractionTracker.TryRecordTargetLost(true);
                result.RequireRecovery(
                    FindPointAskRecoveryStage.Point,
                    GetRecoveryBaselineSourceFrameId(
                        lastFrozenTargetSourceFrameId));
                yield break;
            }

            PointAttentionTargetSkill pointSkill =
                new PointAttentionTargetSkill(pointingController);
            Task<SkillExecutionResult> pointTask = pointSkill.ExecuteUntypedAsync(
                new PointAttentionTargetRequest(),
                CreateSkillContext("point"));
            yield return WaitForTask(pointTask);
            SkillExecutionResult pointResult = GetCompletedResult(pointTask);
            if (pointResult == null || !pointResult.IsSuccess)
            {
                if (!IsFrozenTargetResolvedByResolver())
                {
                    physicalInteractionTracker.TryRecordTargetLost(true);
                    result.RequireRecovery(
                        FindPointAskRecoveryStage.Point,
                        GetRecoveryBaselineSourceFrameId(
                            lastFrozenTargetSourceFrameId));
                    yield break;
                }

                Fail(pointResult != null
                    ? pointResult.Message
                    : "POINT Skill failed.");
                yield break;
            }

            if (!IsFrozenTargetResolvedByResolver())
            {
                physicalInteractionTracker.TryRecordTargetLost(true);
                result.RequireRecovery(
                    FindPointAskRecoveryStage.Point,
                    GetRecoveryBaselineSourceFrameId(
                        lastFrozenTargetSourceFrameId));
                yield break;
            }

            physicalInteractionObserver.BeginPoint();
            Vector3 acceptedPointTarget =
                pointingController.AcceptedAttentionTargetPosition;
            if (!pointingController.HasAcceptedPointTarget ||
                !physicalInteractionTracker.TryRecordPointRequested(
                    flow.TargetKey,
                    pointingController.ActiveArmId,
                    acceptedPointTarget.x,
                    acceptedPointTarget.y,
                    acceptedPointTarget.z))
            {
                Fail("M6 POINT causal target could not be recorded.");
                yield break;
            }

            var pointSettleGate = new PointSoftwareSettleGate();
            float pointSettleStartedAt = Time.unscaledTime;
            bool pointSettleWaitLogged = false;
            while (true)
            {
                long currentTargetFrame = GetCurrentFrozenTargetSourceFrameId();
                lastFrozenTargetSourceFrameId = Math.Max(
                    lastFrozenTargetSourceFrameId,
                    currentTargetFrame);
                if (!IsFrozenTargetResolvedByResolver())
                {
                    physicalInteractionTracker.TryRecordTargetLost(true);
                    result.RequireRecovery(
                        FindPointAskRecoveryStage.Point,
                        GetRecoveryBaselineSourceFrameId(
                            lastFrozenTargetSourceFrameId));
                    yield break;
                }

                if (!pointingController.IsPointing)
                {
                    Fail("POINT output stopped before software settle.");
                    yield break;
                }

                PointSoftwareSettleObservation settleObservation =
                    pointingController.GetSoftwareSettleObservation();
                PointSoftwareSettleDecision settleDecision =
                    pointSettleGate.Evaluate(
                        settleObservation,
                        Time.unscaledTime - pointSettleStartedAt,
                        PointSoftwareSettleTimeoutSeconds);

                if (!pointSettleWaitLogged &&
                    settleDecision == PointSoftwareSettleDecision.Waiting)
                {
                    pointSettleWaitLogged = true;
                    Debug.Log(
                        "[OneShotPointSettle][WAIT] " +
                        "Target=" + flow.TargetKey + " " +
                        "Reason=" + settleObservation.WaitingReason,
                        this);
                }

                if (settleDecision == PointSoftwareSettleDecision.Settled)
                {
                    if (!pointSettleGate.TryConsumeAskEntry())
                    {
                        Fail("POINT software settle ASK entry was already consumed.");
                        yield break;
                    }

                    Debug.Log(
                        "[OneShotPointSettle][SETTLED] " +
                        "Target=" + flow.TargetKey + " " +
                        "HandError=" +
                        settleObservation.HandTargetErrorMeters.ToString("F4") +
                        " IkError=" +
                        settleObservation.IkErrorMeters.ToString("F4") + " " +
                        "PhysicalCompletion=Unverified",
                        this);
                    break;
                }

                if (settleDecision == PointSoftwareSettleDecision.TimedOut)
                {
                    Debug.LogWarning(
                        "[OneShotPointSettle][TIMEOUT] " +
                        "Target=" + flow.TargetKey + " " +
                        "Reason=" + settleObservation.WaitingReason,
                        this);
                    Fail(
                        "POINT software settle timed out. " +
                        settleObservation.WaitingReason);
                    yield break;
                }

                yield return null;
            }

            if (!flow.ConfirmPoint())
            {
                Fail("Invalid POINT transition.");
                yield break;
            }
            if (!physicalInteractionTracker.TryRecordPointLogicalConfirmed(
                    flow.TargetKey))
            {
                Fail("M6 POINT logical correlation did not match the frozen target.");
                yield break;
            }
            NotifyState("POINT software settled; ASK starting.");
            result.Completed = true;
        }

        private IEnumerator RecoverPreQuestionTarget(
            SelectObservationTargetResult frozenTarget,
            FindPointAskRecoveryStage lostStage,
            long lostObservationSourceFrameId,
            PreQuestionRecoveryResult result)
        {
            ReleaseOwnedOutputs(
                FindPointAskPhysicalPoseReleaseReason.PreQuestionRecovery);
            if (!flow.RestartLookAfterPreQuestionRecovery())
            {
                Fail("Pre-question recovery could not restart LOOK lifecycle.");
                yield break;
            }

            ResolvePreQuestionRecoveryDependencies();
            if (recoveryNeckActionExecutor == null)
            {
                Fail("Existing NeckActionExecutor Search is unavailable.");
                yield break;
            }

            long recoveryBaseline = GetRecoveryBaselineSourceFrameId(
                lostObservationSourceFrameId);
            activeRecoveryGate = new FindPointAskPreQuestionRecoveryGate();
            FindPointAskPreQuestionRecoveryDecision begin =
                activeRecoveryGate.Begin(
                    lostStage,
                    frozenTarget.TargetKey,
                    frozenTarget.TrackId,
                    frozenTarget.SessionId,
                    recoveryBaseline,
                    Time.unscaledTimeAsDouble,
                    lookTimeoutSeconds,
                    out string beginReason);
            if (begin != FindPointAskPreQuestionRecoveryDecision.SearchStarted)
            {
                Fail("Pre-question recovery rejected: " + beginReason);
                yield break;
            }

            preQuestionRecoveryActive = true;
            preQuestionRecoveryStartedAt = Time.time;
            Debug.Log(
                "[OneShotRecovery][ENTER] Target=" + flow.TargetKey +
                " Stage=" + lostStage,
                this);

            long lastSearchStartSourceFrameId = recoveryBaseline;
            if (!TryStartExistingRecoverySearch(
                    (float)Math.Max(
                        0.001,
                        activeRecoveryGate.DeadlineSeconds -
                        Time.unscaledTimeAsDouble),
                    out string searchError))
            {
                Fail("Existing recovery Search could not start: " + searchError);
                yield break;
            }

            Debug.Log(
                "[OneShotRecovery][SEARCH] Target=" + flow.TargetKey +
                " Session=" + frozenTarget.SessionId,
                this);

            while (preQuestionRecoveryActive && activeRecoveryGate.IsActive)
            {
                ObservationTarget current = selectionService.CurrentTarget;
                FindPointAskPreQuestionRecoveryDecision decision =
                    activeRecoveryGate.Observe(
                        current != null,
                        current != null ? current.TargetKey : string.Empty,
                        current != null ? current.TrackId : -1,
                        current != null ? current.SessionId : string.Empty,
                        current != null ? current.SourceFrameId : 0L,
                        Time.unscaledTimeAsDouble,
                        out string decisionReason);

                if (decision ==
                    FindPointAskPreQuestionRecoveryDecision.SameTargetReacquired)
                {
                    StopPreQuestionRecoverySearch(
                        "Frozen target reacquired; releasing Search ownership.");

                    Task<SkillExecutionResult> selectTask =
                        productionSkillRuntime.ExecuteAsync(
                            SelectObservationTargetSkill.Id,
                            SelectObservationTargetSkill.CurrentVersion,
                            new SelectObservationTargetRequest(),
                            CreateSkillContext("recovery_select_target"));
                    yield return WaitForTask(selectTask);
                    SkillExecutionResult selectResult =
                        GetCompletedResult(selectTask);
                    SelectObservationTargetResult reacquired =
                        selectResult != null
                            ? selectResult.Payload as SelectObservationTargetResult
                            : null;
                    if (selectResult == null || !selectResult.IsSuccess ||
                        !IsSameShortTermTarget(frozenTarget, reacquired))
                    {
                        Fail(
                            "Recovery target changed before fresh Skill selection; " +
                            "B was not adopted as A.");
                        yield break;
                    }

                    if (!TryBeginPhysicalInteractionTracking(
                            reacquired,
                            out string trackingError))
                    {
                        Fail(trackingError);
                        yield break;
                    }

                    Debug.Log(
                        "[OneShotRecovery][REACQUIRED] Target=" +
                        reacquired.TargetKey + " SourceFrame=" +
                        reacquired.SourceFrameId,
                        this);
                    Debug.Log(
                        "[OneShotRecovery][RESUME_LOOK] Target=" +
                        reacquired.TargetKey,
                        this);
                    NotifyState(
                        "Target reacquired; LOOK lifecycle restarting from a fresh baseline.");
                    result.Selected = reacquired;
                    result.Succeeded = true;
                    yield break;
                }

                if (decision ==
                    FindPointAskPreQuestionRecoveryDecision.TimedOut)
                {
                    Debug.LogWarning(
                        "[OneShotRecovery][FAILED] Reason=Timeout Target=" +
                        flow.TargetKey,
                        this);
                    Fail("Pre-question target recovery timed out. " + decisionReason);
                    yield break;
                }

                if (!recoveryNeckActionExecutor.IsLookAroundSearchActive &&
                    current == null)
                {
                    long latestFrame = GetLatestTrackingSourceFrameId();
                    if (latestFrame > lastSearchStartSourceFrameId)
                    {
                        float remainingSeconds = (float)Math.Max(
                            0.001,
                            activeRecoveryGate.DeadlineSeconds -
                            Time.unscaledTimeAsDouble);
                        if (TryStartExistingRecoverySearch(
                                remainingSeconds,
                                out _))
                        {
                            lastSearchStartSourceFrameId = latestFrame;
                        }
                    }
                }

                yield return null;
            }
        }

        private bool TryStartExistingRecoverySearch(
            float remainingSeconds,
            out string error)
        {
            error = string.Empty;
            ResolvePreQuestionRecoveryDependencies();
            if (recoveryNeckActionExecutor == null)
            {
                error = "NeckActionExecutor is unavailable.";
                return false;
            }

            bool accepted = recoveryNeckActionExecutor.TryExecute(
                "lookAround",
                SourceKey,
                "Natural OneShot pre-question target recovery",
                remainingSeconds);
            if (!accepted)
                error = "lookAround request was rejected.";
            return accepted;
        }

        private void StopPreQuestionRecoverySearch(string reason)
        {
            if (activeRecoveryGate != null && activeRecoveryGate.IsActive)
                activeRecoveryGate.Interrupt(reason);
            if (recoveryNeckActionExecutor != null)
                recoveryNeckActionExecutor.InterruptLookAroundSearch(reason);
            preQuestionRecoveryActive = false;
            preQuestionRecoveryStartedAt = 0f;
        }

        private void ResolvePreQuestionRecoveryDependencies()
        {
            if (recoveryNeckActionExecutor == null)
                recoveryNeckActionExecutor = FindObjectOfType<NeckActionExecutor>();
            if (recoveryTrackingService == null)
            {
                recoveryTrackingService =
                    FindObjectOfType<ByteTrackObjectTrackingService>();
            }
            if (recoveryAutonomousPolicy == null)
            {
                recoveryAutonomousPolicy =
                    FindObjectOfType<AutonomousLookAroundPolicy>();
            }
        }

        private long GetRecoveryBaselineSourceFrameId(long knownSourceFrameId)
        {
            return Math.Max(
                Math.Max(0L, knownSourceFrameId),
                GetLatestTrackingSourceFrameId());
        }

        private long GetLatestTrackingSourceFrameId()
        {
            ResolvePreQuestionRecoveryDependencies();
            return recoveryTrackingService != null &&
                recoveryTrackingService.LatestSnapshot != null
                    ? Math.Max(
                        0L,
                        recoveryTrackingService.LatestSnapshot.SourceFrameId)
                    : 0L;
        }

        private long GetCurrentFrozenTargetSourceFrameId()
        {
            ObservationTarget current = selectionService != null
                ? selectionService.CurrentTarget
                : null;
            return current != null &&
                string.Equals(
                    current.TargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal)
                        ? Math.Max(0L, current.SourceFrameId)
                        : 0L;
        }

        private bool IsFrozenTargetResolvedByResolver()
        {
            OrientationResolution resolution = orientationResolver != null
                ? orientationResolver.CurrentResolution
                : null;
            return resolution != null && resolution.HasTarget &&
                resolution.Target != null &&
                string.Equals(
                    resolution.Target.TargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal);
        }

        private bool IsCurrentSelectionFrozenTarget()
        {
            ObservationTarget selected = selectionService != null
                ? selectionService.CurrentTarget
                : null;
            return selected != null &&
                string.Equals(
                    selected.TargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal);
        }

        private static bool IsSameShortTermTarget(
            SelectObservationTargetResult frozen,
            SelectObservationTargetResult candidate)
        {
            return frozen != null && candidate != null &&
                frozen.TrackId == candidate.TrackId &&
                string.Equals(
                    frozen.TargetKey,
                    candidate.TargetKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    frozen.SessionId,
                    candidate.SessionId,
                    StringComparison.Ordinal) &&
                candidate.SourceFrameId > frozen.SourceFrameId;
        }

        private void OnSpeechLifecycleObserved(
            global::SpeechPlaybackRuntimeFact fact)
        {
            if (fact == null)
                return;

            if (TryObserveAcknowledgementSpeech(fact))
                return;

            if (questionSession.CurrentQuestion == null)
                return;

            if (fact.Lifecycle == SpeechAdapterLifecycle.RequestAccepted)
            {
                if (!awaitingQuestionSpeechRequest)
                    return;
                ApplyQuestionSpeechLifecycle(
                    fact.RuntimeSpeechId,
                    QuestionSpeechLifecycleState.Requested,
                    "SpeechRequested observed.");
                return;
            }

            if (!string.Equals(
                    fact.RuntimeSpeechId,
                    questionSession.SpeechInstanceId,
                    StringComparison.Ordinal))
            {
                return;
            }

            switch (fact.Lifecycle)
            {
                case SpeechAdapterLifecycle.PlaybackStarted:
                    ApplyQuestionSpeechLifecycle(
                        fact.RuntimeSpeechId,
                        QuestionSpeechLifecycleState.Started,
                        "SpeechStarted observed.");
                    break;

                case SpeechAdapterLifecycle.PlaybackCompleted:
                    ApplyQuestionSpeechLifecycle(
                        fact.RuntimeSpeechId,
                        QuestionSpeechLifecycleState.Completed,
                        "SpeechCompleted observed.");
                    break;

                case SpeechAdapterLifecycle.PlaybackFailed:
                    ApplyQuestionSpeechLifecycle(
                        fact.RuntimeSpeechId,
                        QuestionSpeechLifecycleState.Failed,
                        "Question speech failed: " + fact.FailureReason);
                    break;

                case SpeechAdapterLifecycle.PlaybackInterrupted:
                    ApplyQuestionSpeechLifecycle(
                        fact.RuntimeSpeechId,
                        QuestionSpeechLifecycleState.Cancelled,
                        "Question speech was interrupted: " + fact.FailureReason);
                    break;
            }
        }

        private bool ApplyQuestionSpeechLifecycle(
            string speechInstanceId,
            QuestionSpeechLifecycleState lifecycle,
            string message)
        {
            if (!questionSession.TryObserveSpeech(
                    speechInstanceId,
                    lifecycle,
                    out string error))
            {
                SetDiagnostic(
                    "Speech lifecycle ignored: " + lifecycle + " | " + error);
                return false;
            }

            UpdateQuestionDiagnostics();
            if (physicalInteractionTracker != null)
            {
                switch (lifecycle)
                {
                    case QuestionSpeechLifecycleState.Requested:
                        physicalInteractionTracker.TryRecordSpeechRequested(
                            speechInstanceId);
                        break;
                    case QuestionSpeechLifecycleState.Started:
                        physicalInteractionTracker.TryRecordSpeechStarted(
                            speechInstanceId);
                        break;
                }
            }
            if (lifecycle == QuestionSpeechLifecycleState.Completed)
            {
                if (!flow.ConfirmAsk())
                {
                    Fail("Speech completed outside ASK state.");
                    return false;
                }

                if (physicalInteractionTracker != null &&
                    !physicalInteractionTracker
                        .TryRecordQuestionSpeechCompletedAndRetain(
                            speechInstanceId))
                {
                    Fail(
                        "M6 Question SpeechCompleted correlation was rejected.");
                    return false;
                }

                if (!EnterQuestionPostureRetention(out string retentionError))
                {
                    Fail(retentionError);
                    return false;
                }

                answerWaitRoutine = StartCoroutine(WaitForAnswerTimeout());
                NotifyState(
                    "SpeechCompleted; QuestionContext Active; waiting for an answer.");
                WaitingForAnswer?.Invoke(flow.TargetKey, questionText.Trim());
                return true;
            }

            if (lifecycle == QuestionSpeechLifecycleState.Failed ||
                lifecycle == QuestionSpeechLifecycleState.Cancelled)
            {
                Fail(message);
                return true;
            }

            NotifyState(message);
            return true;
        }

        private bool SubmitOrientation(
            OrientationPriorityDirective directive,
            out string error)
        {
            return SubmitOrientation(
                directive,
                string.Empty,
                out error);
        }

        private bool SubmitOrientation(
            OrientationPriorityDirective directive,
            string heldTargetKey,
            out string error)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            OrientationPriorityRequest request = new OrientationPriorityRequest(
                Guid.NewGuid().ToString("N"),
                SourceKey,
                directive,
                OrientationPriorityReason.ObserveObject,
                orientationPriority,
                now,
                0,
                null,
                heldTargetKey);
            return priorityRequestService.SubmitOrReplace(request, out error);
        }

        private bool IsLockedTargetResolved()
        {
            OrientationResolution resolution = orientationResolver.CurrentResolution;
            return resolution != null &&
                resolution.HasTarget &&
                resolution.Target != null &&
                string.Equals(
                    resolution.Target.TargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal) &&
                targetDriver.HasOutputPosition &&
                string.Equals(
                    targetDriver.OutputTargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal);
        }

        private long GetLookStartedSourceFrameId(long selectedSourceFrameId)
        {
            long sourceFrameId = Math.Max(0L, selectedSourceFrameId);
            OrientationResolution resolution = orientationResolver.CurrentResolution;
            if (resolution != null &&
                resolution.HasTarget &&
                resolution.Target != null &&
                string.Equals(
                    resolution.Target.TargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal))
            {
                sourceFrameId = Math.Max(
                    sourceFrameId,
                    resolution.Target.SourceFrameId);
            }

            if (targetDriver.HasOutputPosition &&
                string.Equals(
                    targetDriver.OutputTargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal))
            {
                sourceFrameId = Math.Max(
                    sourceFrameId,
                    targetDriver.LastAcceptedObjectSourceFrameId);
            }

            return sourceFrameId;
        }

        private LookVisualSoftwareSettleObservation
            GetLookVisualSoftwareSettleObservation()
        {
            OrientationResolution resolution =
                orientationResolver.CurrentResolution;
            OrientationTargetCandidate target =
                resolution != null && resolution.HasTarget
                    ? resolution.Target
                    : null;
            bool directObject =
                resolution != null &&
                resolution.Kind == OrientationResolutionKind.Object &&
                resolution.Provenance ==
                    OrientationAttentionProvenance.DirectObservation &&
                target != null &&
                target.Kind == OrientationTargetCandidateKind.Object;

            string waitingReason;
            if (!directObject)
            {
                waitingReason =
                    resolution != null &&
                    !string.IsNullOrWhiteSpace(resolution.Reason)
                        ? resolution.Reason
                        : "Direct Object observation is unavailable.";
            }
            else if (!string.Equals(
                         target.TargetKey,
                         flow.TargetKey,
                         StringComparison.Ordinal))
            {
                waitingReason = "Resolver target changed from the frozen LOOK target.";
            }
            else if (!targetDriver.HasOutputPosition)
            {
                waitingReason = "Orientation target driver has no output position.";
            }
            else
            {
                waitingReason = string.Empty;
            }

            return new LookVisualSoftwareSettleObservation(
                directObject,
                target != null ? target.TargetKey : string.Empty,
                targetDriver.HasOutputPosition
                    ? targetDriver.OutputTargetKey
                    : string.Empty,
                target != null ? target.SourceFrameId : 0L,
                targetDriver.LastAcceptedObjectSourceFrameId,
                target != null ? target.CenterX : float.NaN,
                target != null ? target.CenterY : float.NaN,
                waitingReason);
        }

        private void LogLookSettleTransition(
            LookVisualSoftwareSettleDecision decision,
            LookVisualSoftwareSettleObservation observation,
            string reason)
        {
            string detail =
                "Target=" + flow.TargetKey + " " +
                "SourceFrame=" + observation.SourceFrameId + " " +
                "Center=(" +
                observation.NormalizedCenterX.ToString("F3") + "," +
                observation.NormalizedCenterY.ToString("F3") + ")";

            switch (decision)
            {
                case LookVisualSoftwareSettleDecision.Waiting:
                    Debug.Log(
                        "[OneShotLookSettle][WAIT] " + detail + " " +
                        "Reason=" + reason,
                        this);
                    break;

                case LookVisualSoftwareSettleDecision.Tracking:
                    Debug.Log(
                        "[OneShotLookSettle][TRACKING] " + detail + " " +
                        "Tolerance=" +
                        LookVisualSoftwareSettleGate
                            .DefaultNormalizedCenterTolerance.ToString("F3"),
                        this);
                    break;

                case LookVisualSoftwareSettleDecision.Settled:
                    Debug.Log(
                        "[OneShotLookSettle][SETTLED] " + detail + " " +
                        "PhysicalCompletion=Unverified",
                        this);
                    break;

                case LookVisualSoftwareSettleDecision.TimedOut:
                    Debug.LogWarning(
                        "[OneShotLookSettle][TIMEOUT] " + detail + " " +
                        "Reason=" + reason,
                        this);
                    break;
            }
        }

        private SkillExecutionContext CreateSkillContext(string stepId)
        {
            string runId = string.IsNullOrEmpty(behaviorRunId)
                ? Guid.NewGuid().ToString("N")
                : behaviorRunId;
            return new SkillExecutionContext(
                runId,
                "find_point_ask_v0_1:" + runId,
                stepId,
                DateTime.UtcNow,
                Application.platform.ToString(),
                CancellationToken.None);
        }

        private bool EnsureProductionSelectSkillRuntime(out string error)
        {
            error = string.Empty;

            if (productionSkillRuntime != null)
                return true;

            SkillPayloadTypeRegistry payloadRegistry =
                new SkillPayloadTypeRegistry();
            SkillImplementationRegistry implementationRegistry =
                new SkillImplementationRegistry();

            if (!ObservationTargetSkillRegistration.Register(
                    payloadRegistry,
                    implementationRegistry,
                    selectionService,
                    out error))
            {
                return false;
            }

            productionSkillRuntime = new SkillRuntime(
                implementationRegistry);
            return true;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (task != null && !task.IsCompleted)
                yield return null;
        }

        private static SkillExecutionResult GetCompletedResult(
            Task<SkillExecutionResult> task)
        {
            if (task == null || task.IsCanceled || task.IsFaulted)
                return null;
            return task.Result;
        }

        private static string BuildObservationReference(
            SelectObservationTargetResult selected)
        {
            return "observation:" +
                (selected.SessionId ?? string.Empty) +
                ":frame:" + selected.SourceFrameId +
                ":captured:" + selected.CapturedBySkillAtUnixMilliseconds;
        }

        private bool TryFreezeSelectedVisualCrop(
            SelectObservationTargetResult selected,
            out VisualCropSnapshot crop,
            out string error)
        {
            crop = null;
            error = string.Empty;
            if (selected == null || selectionService == null)
            {
                error = "Selected target or selection service is unavailable.";
                return false;
            }

            if (!selectionService.TryGetVisualFrameSnapshot(
                    selected.SessionId,
                    selected.SourceFrameId,
                    DateTime.UtcNow,
                    out VisualFrameSnapshot frame,
                    out error))
            {
                return false;
            }

            if (!string.Equals(
                    frame.SessionId,
                    selected.SessionId,
                    StringComparison.Ordinal) ||
                frame.SourceFrameId != selected.SourceFrameId)
            {
                error = "Resolved visual snapshot correlation does not match target.";
                return false;
            }

            var boundingBox = new VisualNormalizedBoundingBox(
                selected.NormalizedX1,
                selected.NormalizedY1,
                selected.NormalizedX2,
                selected.NormalizedY2);
            return frame.TryCreateCrop(boundingBox, out crop, out error);
        }

        private void ResolveVoicePlaybackController()
        {
            if (voicePlaybackController == null)
            {
                voicePlaybackController =
                    FindObjectOfType<global::VoicePlaybackController>();
            }
        }

        private void SubscribeSpeechLifecycle()
        {
            if (speechLifecycleSubscribed)
                return;
            ResolveVoicePlaybackController();
            if (voicePlaybackController == null)
                return;

            voicePlaybackController.SpeechLifecycleObserved +=
                OnSpeechLifecycleObserved;
            speechLifecycleSubscribed = true;
        }

        private void UnsubscribeSpeechLifecycle()
        {
            if (!speechLifecycleSubscribed || voicePlaybackController == null)
                return;

            voicePlaybackController.SpeechLifecycleObserved -=
                OnSpeechLifecycleObserved;
            speechLifecycleSubscribed = false;
        }

        private static bool IsBehaviorActive(
            FindPointAskBehaviorFlow.BehaviorState state)
        {
            return state == FindPointAskBehaviorFlow.BehaviorState.WaitTarget ||
                state == FindPointAskBehaviorFlow.BehaviorState.TargetSelected ||
                state == FindPointAskBehaviorFlow.BehaviorState.Look ||
                state == FindPointAskBehaviorFlow.BehaviorState.Point ||
                state == FindPointAskBehaviorFlow.BehaviorState.Ask ||
                state == FindPointAskBehaviorFlow.BehaviorState.WaitAnswer ||
                state == FindPointAskBehaviorFlow.BehaviorState.AnswerAccepted ||
                state == FindPointAskBehaviorFlow.BehaviorState.Binding ||
                state == FindPointAskBehaviorFlow.BehaviorState.Saving ||
                state == FindPointAskBehaviorFlow.BehaviorState.Acknowledging;
        }

        private static bool IsOutcomeLifecycleActive(
            FindPointAskBehaviorFlow.BehaviorState state)
        {
            return state == FindPointAskBehaviorFlow.BehaviorState.AnswerAccepted ||
                state == FindPointAskBehaviorFlow.BehaviorState.Binding ||
                state == FindPointAskBehaviorFlow.BehaviorState.Saving ||
                state == FindPointAskBehaviorFlow.BehaviorState.Acknowledging;
        }

        private bool ValidateReferences(out string error)
        {
            error = string.Empty;
            if (selectionService == null) error = "SelectionService is not assigned.";
            else if (orientationResolver == null) error = "OrientationTargetResolver is not assigned.";
            else if (priorityRequestService == null) error = "OrientationPriorityRequestService is not assigned.";
            else if (targetDriver == null) error = "OrientationResolutionTargetDriver is not assigned.";
            else if (pointingController == null) error = "AttentionTargetArmPointingController is not assigned.";
            else if (string.IsNullOrWhiteSpace(questionText)) error = "Question text is empty.";
            return error.Length == 0;
        }

        private bool TryBeginPhysicalInteractionTracking(
            SelectObservationTargetResult selected,
            out string error)
        {
            error = string.Empty;
            try
            {
                var runTarget = new FindPointAskPhysicalRunTarget(
                    behaviorRunId,
                    selected.TargetKey,
                    selected.TrackId,
                    selected.SessionId,
                    selected.SourceFrameId,
                    DateTime.UtcNow);
                physicalInteractionTracker =
                    new FindPointAskPhysicalInteractionTracker(runTarget);
                physicalInteractionObserver =
                    FindPointAskPhysicalInteractionObserver.ResolveFromScene();
                physicalInteractionObserver.BeginRun();
                UpdateM6Diagnostics();
                return true;
            }
            catch (Exception exception)
            {
                physicalInteractionTracker = null;
                physicalInteractionObserver = null;
                error = "M6 physical tracking could not start: " +
                    exception.Message;
                return false;
            }
        }

        private bool EnterQuestionPostureRetention(out string error)
        {
            error = string.Empty;
            QuestionContext question = questionSession.CurrentQuestion;
            if (question == null || !questionSession.IsWaitingForAnswer ||
                !string.Equals(
                    question.TargetKey,
                    flow.TargetKey,
                    StringComparison.Ordinal))
            {
                error = "WAIT_ANSWER retention target does not match the Active question.";
                return false;
            }

            if (physicalInteractionTracker == null ||
                !physicalInteractionTracker.CurrentState
                    .WaitAnswerRetentionActive)
            {
                error = "WAIT_ANSWER physical ownership was not retained.";
                return false;
            }

            if (pointingController == null ||
                !pointingController.RetainAcceptedPointPose())
            {
                error = "POINT pose could not be frozen for WAIT_ANSWER.";
                return false;
            }

            Debug.Log(
                "[OneShotWaitRetention][ENTER] " +
                "Target=" + question.TargetKey + " " +
                "LookRetained=true PointRetained=true",
                this);
            return true;
        }

        private IEnumerator WaitForAnswerTimeout()
        {
            float deadline =
                Time.unscaledTime + answerWaitTimeoutSeconds;
            while (flow.State ==
                    FindPointAskBehaviorFlow.BehaviorState.WaitAnswer &&
                questionSession.IsWaitingForAnswer &&
                Time.unscaledTime < deadline)
            {
                yield return null;
            }

            answerWaitRoutine = null;
            if (flow.State !=
                    FindPointAskBehaviorFlow.BehaviorState.WaitAnswer ||
                !questionSession.IsWaitingForAnswer)
            {
                yield break;
            }

            if (!questionSession.TryExpire(out string error))
            {
                Fail("Question timeout transition failed: " + error);
                yield break;
            }

            awaitingQuestionSpeechRequest = false;
            flow.Fail("Question answer wait timed out.");
            ReleaseOwnedOutputs(
                FindPointAskPhysicalPoseReleaseReason.QuestionTimedOut);
            NotifyState("Question answer wait timed out; retained outputs released.");
        }

        private IEnumerator WaitForAcknowledgementTimeout()
        {
            float deadline =
                Time.unscaledTime + acknowledgementTimeoutSeconds;
            while (flow.State ==
                    FindPointAskBehaviorFlow.BehaviorState.Acknowledging &&
                pendingAcknowledgementCorrelationId.Length > 0 &&
                Time.unscaledTime < deadline)
            {
                yield return null;
            }

            acknowledgementTimeoutRoutine = null;
            if (flow.State !=
                    FindPointAskBehaviorFlow.BehaviorState.Acknowledging ||
                pendingAcknowledgementCorrelationId.Length == 0)
            {
                yield break;
            }

            FailAcknowledgement(
                "Timeout",
                "ACK speech lifecycle did not reach a terminal result.");
        }

        private bool TryObserveAcknowledgementSpeech(
            global::SpeechPlaybackRuntimeFact fact)
        {
            if (!IsPendingAcknowledgement(fact.ResponseCorrelationId))
                return false;

            if (fact.Lifecycle == SpeechAdapterLifecycle.RequestAccepted)
            {
                if (pendingAcknowledgementSpeechId.Length > 0)
                    return true;
                if (string.IsNullOrWhiteSpace(fact.RuntimeSpeechId))
                {
                    FailAcknowledgement(
                        "AttachFailed",
                        "ACK RequestAccepted has no RuntimeSpeechId.");
                    return true;
                }

                pendingAcknowledgementSpeechId = fact.RuntimeSpeechId;
                Debug.Log(
                    "[OneShotLifecycle][ACK_ATTACHED] Correlation=" +
                    pendingAcknowledgementCorrelationId +
                    " RuntimeSpeechId=" + pendingAcknowledgementSpeechId,
                    this);
                return true;
            }

            if (!string.Equals(
                    fact.RuntimeSpeechId,
                    pendingAcknowledgementSpeechId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            switch (fact.Lifecycle)
            {
                case SpeechAdapterLifecycle.PlaybackStarted:
                    acknowledgementStarted = true;
                    Debug.Log(
                        "[OneShotLifecycle][ACK_STARTED] RuntimeSpeechId=" +
                        pendingAcknowledgementSpeechId,
                        this);
                    break;

                case SpeechAdapterLifecycle.PlaybackCompleted:
                    if (!acknowledgementStarted)
                    {
                        FailAcknowledgement(
                            "InvalidOrder",
                            "ACK completed before PlaybackStarted.");
                        break;
                    }
                    CompleteAcknowledgement();
                    break;

                case SpeechAdapterLifecycle.PlaybackFailed:
                    FailAcknowledgement("Failed", fact.FailureReason);
                    break;

                case SpeechAdapterLifecycle.PlaybackInterrupted:
                    FailAcknowledgement("Interrupted", fact.FailureReason);
                    break;
            }
            return true;
        }

        private bool IsPendingAcknowledgement(string correlationId)
        {
            return flow.State ==
                    FindPointAskBehaviorFlow.BehaviorState.Acknowledging &&
                pendingAcknowledgementCorrelationId.Length > 0 &&
                string.Equals(
                    pendingAcknowledgementCorrelationId,
                    correlationId,
                    StringComparison.Ordinal);
        }

        private void CompleteAcknowledgement()
        {
            string speechId = pendingAcknowledgementSpeechId;
            StopAcknowledgementTimeout();
            ClearAcknowledgementTracking();
            if (!flow.CompleteAcknowledgement())
                return;
            NotifyState(
                "[OneShotLifecycle][ACK_COMPLETED] RuntimeSpeechId=" +
                speechId + " [OneShotLifecycle][COMPLETED]");
        }

        private void FailAcknowledgement(string status, string reason)
        {
            StopAcknowledgementTimeout();
            ClearAcknowledgementTracking();
            Fail(
                "[OneShotLifecycle][ACK_TERMINAL] Status=" + status +
                " MemorySaved=true Reason=" + (reason ?? string.Empty));
        }

        private void StopAcknowledgementTimeout()
        {
            if (acknowledgementTimeoutRoutine == null)
                return;
            StopCoroutine(acknowledgementTimeoutRoutine);
            acknowledgementTimeoutRoutine = null;
        }

        private void ClearAcknowledgementTracking()
        {
            pendingAcknowledgementCorrelationId = string.Empty;
            pendingAcknowledgementSpeechId = string.Empty;
            acknowledgementStarted = false;
        }

        private void StopAnswerWaitTimeout()
        {
            if (answerWaitRoutine == null)
                return;

            StopCoroutine(answerWaitRoutine);
            answerWaitRoutine = null;
        }

        private bool ObserveQuestionRetentionOverride()
        {
            if (physicalInteractionTracker == null ||
                !physicalInteractionTracker.CurrentState
                    .WaitAnswerRetentionActive)
            {
                return false;
            }

            if (limboPermission == null)
                limboPermission = FindObjectOfType<LimboPermission>();

            if (limboPermission != null &&
                limboPermission.IsEmergencyMode)
            {
                TerminateQuestionRetention(
                    FindPointAskPhysicalPoseReleaseReason.Emergency,
                    "Emergency interrupted WAIT_ANSWER retention.");
                return true;
            }

            OrientationPriorityRequest active =
                priorityRequestService != null
                    ? priorityRequestService.ActiveRequest
                    : null;
            if (active != null &&
                active.Directive == OrientationPriorityDirective.Neutral &&
                !string.Equals(
                    active.SourceKey,
                    SourceKey,
                    StringComparison.Ordinal))
            {
                FindPointAskPhysicalPoseReleaseReason reason =
                    active.Reason == OrientationPriorityReason.Emergency
                        ? FindPointAskPhysicalPoseReleaseReason.Emergency
                        : FindPointAskPhysicalPoseReleaseReason.ExplicitNeutral;
                TerminateQuestionRetention(
                    reason,
                    reason == FindPointAskPhysicalPoseReleaseReason.Emergency
                        ? "Emergency Neutral interrupted WAIT_ANSWER retention."
                        : "Explicit Neutral interrupted WAIT_ANSWER retention.");
                return true;
            }

            return false;
        }

        private bool ObservePreQuestionRecoveryOverride()
        {
            if (!preQuestionRecoveryActive)
                return false;

            if (recoveryAutonomousPolicy == null)
                recoveryAutonomousPolicy = FindObjectOfType<AutonomousLookAroundPolicy>();
            if (recoveryAutonomousPolicy != null &&
                recoveryAutonomousPolicy.LastUserSpeechEventTime >
                    preQuestionRecoveryStartedAt)
            {
                CancelBehavior(
                    FindPointAskPhysicalPoseReleaseReason.Interrupted,
                    "Explicit user input interrupted pre-question recovery.");
                return true;
            }

            if (limboPermission == null)
                limboPermission = FindObjectOfType<LimboPermission>();
            if (limboPermission != null && limboPermission.IsEmergencyMode)
            {
                CancelBehavior(
                    FindPointAskPhysicalPoseReleaseReason.Emergency,
                    "Emergency interrupted pre-question recovery.");
                return true;
            }

            OrientationPriorityRequest active =
                priorityRequestService != null
                    ? priorityRequestService.ActiveRequest
                    : null;
            if (active != null &&
                active.Directive == OrientationPriorityDirective.Neutral &&
                !string.Equals(
                    active.SourceKey,
                    SourceKey,
                    StringComparison.Ordinal))
            {
                FindPointAskPhysicalPoseReleaseReason reason =
                    active.Reason == OrientationPriorityReason.Emergency
                        ? FindPointAskPhysicalPoseReleaseReason.Emergency
                        : FindPointAskPhysicalPoseReleaseReason.ExplicitNeutral;
                CancelBehavior(
                    reason,
                    reason == FindPointAskPhysicalPoseReleaseReason.Emergency
                        ? "Emergency Neutral interrupted pre-question recovery."
                        : "Explicit Neutral interrupted pre-question recovery.");
                return true;
            }

            return false;
        }

        private bool ObserveOutcomeLifecycleEmergency()
        {
            if (!IsOutcomeLifecycleActive(flow.State))
                return false;

            if (limboPermission == null)
                limboPermission = FindObjectOfType<LimboPermission>();
            if (limboPermission == null || !limboPermission.IsEmergencyMode)
                return false;

            CancelBehavior(
                FindPointAskPhysicalPoseReleaseReason.Emergency,
                "Emergency interrupted the OneShot outcome lifecycle. " +
                "Saved memory, if any, remains committed.");
            return true;
        }

        private void TerminateQuestionRetention(
            FindPointAskPhysicalPoseReleaseReason reason,
            string message)
        {
            StopAnswerWaitTimeout();
            questionSession.TryCancel(out _);
            awaitingQuestionSpeechRequest = false;
            flow.Cancel();
            ReleaseOwnedOutputs(reason);
            NotifyState(message);
        }

        private void Fail(string reason)
        {
            StopAnswerWaitTimeout();
            StopAcknowledgementTimeout();
            ClearAcknowledgementTracking();
            runningRoutine = null;
            awaitingQuestionSpeechRequest = false;
            questionSession.TryFail(out _);
            flow.Fail(reason);
            UpdateQuestionDiagnostics();
            NotifyState("Stopped at " + flow.State + ": " + flow.FailureReason);
            ReleaseOwnedOutputs(
                FindPointAskPhysicalPoseReleaseReason.Failed);
            Debug.LogError("[FindPointAskBehavior][FAILED] " + flow.FailureReason, this);
        }

        private void ReleaseOwnedOutputs(
            FindPointAskPhysicalPoseReleaseReason reason)
        {
            if (preQuestionRecoveryActive)
            {
                StopPreQuestionRecoverySearch(
                    "OneShot output release: " + reason);
            }

            bool retentionWasActive =
                physicalInteractionTracker != null &&
                physicalInteractionTracker.CurrentState
                    .WaitAnswerRetentionActive;
            string retainedTarget = retentionWasActive
                ? physicalInteractionTracker.CurrentState.RunTarget.TargetKey
                : string.Empty;

            if (priorityRequestService != null)
                priorityRequestService.RemoveBySourceKey(SourceKey);
            if (pointingController != null)
                pointingController.StopPointing(true);
            if (physicalInteractionTracker != null)
                physicalInteractionTracker.TryReleasePose(reason);
            UpdateM6Diagnostics();

            if (retentionWasActive)
            {
                Debug.Log(
                    "[OneShotWaitRetention][RELEASE] " +
                    "Target=" + retainedTarget + " " +
                    "Reason=" + reason,
                    this);
            }
        }

        private void NotifyState(string message)
        {
            UpdateQuestionDiagnostics();
            diagnosticState = flow.State.ToString();
            diagnosticTargetKey = flow.TargetKey ?? string.Empty;
            diagnosticMessage = message ?? string.Empty;
            Debug.Log(
                "[FindPointAskBehavior][STATE] " +
                "State=" + diagnosticState +
                " TargetKey=" + diagnosticTargetKey +
                " Message=" + diagnosticMessage,
                this);
            StateChanged?.Invoke(flow.State);
        }

        private void UpdateM6Diagnostics()
        {
            FindPointAskPhysicalInteractionState state =
                physicalInteractionTracker != null
                    ? physicalInteractionTracker.CurrentState
                    : null;
            diagnosticM6BehaviorRunId = state != null
                ? state.RunTarget.BehaviorRunId
                : string.Empty;
            diagnosticM6FrozenTargetKey = state != null
                ? state.RunTarget.TargetKey
                : string.Empty;
            diagnosticM6NeckCommandDispatched =
                state != null && state.NeckCommandDispatched;
            diagnosticM6ArmCommandDispatched =
                state != null && state.ArmCommandDispatched;
            diagnosticM6NeckCompletion = state != null
                ? state.NeckCompletion.ToString()
                : FindPointAskPhysicalCompletionState.NotObserved.ToString();
            diagnosticM6ArmCompletion = state != null
                ? state.ArmCompletion.ToString()
                : FindPointAskPhysicalCompletionState.NotObserved.ToString();
            diagnosticM6PoseOwnership = state != null
                ? state.PoseOwnership.ToString()
                : FindPointAskPhysicalPoseOwnershipState.None.ToString();
            diagnosticM6PoseReleaseReason = state != null
                ? state.PoseReleaseReason.ToString()
                : FindPointAskPhysicalPoseReleaseReason.None.ToString();
        }

        private void UpdateQuestionDiagnostics()
        {
            QuestionContext context = questionSession.CurrentQuestion;
            diagnosticQuestionId = context != null
                ? context.QuestionId
                : string.Empty;
            diagnosticSpeechLifecycle = questionSession.SpeechState.ToString();
            diagnosticWaitAnswer = questionSession.IsWaitingForAnswer;
        }

        private void SetDiagnostic(string message)
        {
            diagnosticState = flow.State.ToString();
            diagnosticTargetKey = flow.TargetKey ?? string.Empty;
            diagnosticMessage = message ?? string.Empty;
            Debug.LogWarning(
                "[FindPointAskBehavior][ONE_SHOT] " + diagnosticMessage,
                this);
        }
    }

    /// <summary>
    /// OneShot回答のBindingと永続化が完了した時点のimmutable outcome。
    /// 人間向けReactionは所有せず、Conversation / Reaction層へ結果だけを返す。
    /// </summary>
    public sealed class OneShotAnswerOutcome
    {
        public string OutcomeId { get; }
        public string QuestionId { get; }
        public string InputId { get; }
        public CorrelationStatus CorrelationStatus { get; }
        public ExperienceSaveStatus SaveStatus { get; }
        public string Answer { get; }
        public TargetContext TargetContext { get; }

        public bool BindingMatched =>
            CorrelationStatus == CorrelationStatus.Matched;

        public bool Succeeded =>
            BindingMatched &&
            SaveStatus == ExperienceSaveStatus.Saved;

        public OneShotAnswerOutcome(
            string questionId,
            string inputId,
            CorrelationStatus correlationStatus,
            ExperienceSaveStatus saveStatus,
            string answer,
            TargetContext targetContext)
        {
            QuestionId = Normalize(questionId);
            InputId = Normalize(inputId);
            OutcomeId = "oneshot-answer:" + QuestionId + ":" + InputId;
            CorrelationStatus = correlationStatus;
            SaveStatus = saveStatus;
            Answer = answer ?? string.Empty;
            TargetContext = targetContext;
        }

        public static OneShotAnswerOutcome FromCompletedAnswer(
            QuestionAnswerReceipt receipt,
            AnswerBindingResult binding,
            ExperienceSaveResult save)
        {
            AnswerContext answer = binding != null
                ? binding.AnswerContext
                : null;
            return new OneShotAnswerOutcome(
                receipt != null ? receipt.QuestionId : string.Empty,
                receipt != null ? receipt.InputId : string.Empty,
                binding != null
                    ? binding.Status
                    : CorrelationStatus.InvalidAnswer,
                save != null
                    ? save.Status
                    : ExperienceSaveStatus.StorageFailed,
                answer != null ? answer.NormalizedAnswer : string.Empty,
                answer != null ? answer.TargetContext : null);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}

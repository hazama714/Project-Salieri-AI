// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Body.Command;
using SalieriAI.Body.Joint;
using SalieriAI.Runtime;

namespace SalieriAI.Body.Servo.Bridge
{
    /// <summary>
    /// 仮想リグ上の BodyJointConstraint と ServoControlUnit を接続する Bridge。
    ///
    /// IK Solver は探索のために仮想関節角度を即時更新する。
    /// この Bridge は最終的に観測された AppliedAngle を実機入力角度へ変換し、
    /// 必要に応じて実機出力だけを補間する。
    ///
    /// これにより、IK探索を壊さずにサーボの急激な跳ねを抑える。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class BodyJointServoBridge : MonoBehaviour
    {
        [Header("Joint Source")]

        [Tooltip("通常は同じ Axis Empty に付いている BodyJointConstraint を指定する。")]
        [SerializeField]
        private BodyJointConstraint jointConstraint;

        [Header("Sender")]

        [Tooltip("通常運用時は BodyCommandCoordinator を指定する。")]
        [SerializeField]
        private MonoBehaviour senderBehaviour;

        [Header("Runtime Settings")]

        [Tooltip("LAB Scene では未設定でもよい。MainScene では RuntimeConnectionSettings を指定できる。")]
        [SerializeField]
        private RuntimeConnectionSettings runtimeSettings;

        [Header("Physical Servo")]

        [Tooltip("Servo ID、実機側 min/max、invert、offset を設定する。")]
        [SerializeField]
        private ServoControlUnit servo =
            new ServoControlUnit();

        [Header("Virtual To Servo Conversion")]

        [Tooltip("仮想関節角度が 0 度の時に ServoControlUnit へ渡す入力角度。")]
        [SerializeField]
        private float servoNeutralInputAngle = 90f;

        [Tooltip("仮想角度へ掛ける倍率。通常は 1。逆方向へ変換する場合は -1。")]
        [SerializeField]
        private float virtualAngleMultiplier = 1f;

        [Header("Output Control")]

        [Tooltip("ON の時だけ通常送信する。")]
        [SerializeField]
        private bool outputEnabled = false;

        [Tooltip("ON の場合、Play 開始時から腕サーボ出力を許可する。OFF の場合は ArmOutput() が必要。")]
        [SerializeField]
        private bool armOutputOnStart = false;

        [Tooltip("ON の場合、Play 開始後に現在角度を1回送信する。")]
        [SerializeField]
        private bool sendInitialAngleOnStart = false;

        [Tooltip("ON の場合、AppliedAngle の変化を監視して自動送信する。")]
        [SerializeField]
        private bool sendWhenAppliedAngleChanges = true;

        [Tooltip("この角度未満の細かな変化は送信しない。")]
        [SerializeField]
        private float minimumVirtualAngleDelta = 0.5f;

        [Header("Startup Baseline")]

        [Tooltip(
            "ON の場合、Play開始時にはサーボ命令を送らず、" +
            "実機がこの角度にあるものとして内部状態だけを初期化する。"
        )]
        [SerializeField]
        private bool initializeStartupBaselineWithoutSending = true;

        [Tooltip(
            "Play開始時に実機がいると仮定するサーボ入力角度。" +
            "LABでは実機Neutral値を設定する。"
        )]
        [SerializeField]
        private int assumedServoInputAngleOnStartup = 90;

        [Header("Servo Output Interpolation")]

        [Tooltip("ON の場合、実機サーボ出力だけを現在値から目標値へ滑らかに補間する。")]
        [SerializeField]
        private bool interpolateServoOutput = true;

        [Tooltip("現在の実機出力値から新しい目標値へ移動する時間。")]
        [SerializeField]
        private float interpolationDurationSeconds = 1.0f;

        [Tooltip(
            "補間中に中間角度を送る間隔。4軸同時使用時は 0.10 秒程度から開始する。"
        )]
        [SerializeField]
        private float interpolationSendIntervalSeconds = 0.10f;

        [Header("Diagnostics")]

        [SerializeField]
        private bool logConversion = true;

        [SerializeField]
        private bool logInterpolation = true;

        private ICommandSender sender;

        private bool initialized;

        private float lastObservedAppliedAngle =
            float.NaN;

        private int lastRequestedServoInputAngle =
            int.MinValue;

        private bool hasCurrentServoInputAngle;

        private int currentServoInputAngle;

        private int interpolationStartServoInputAngle;

        private int interpolationTargetServoInputAngle;

        private float interpolationStartTime;

        private float lastInterpolationSendTime =
            -999f;

        private bool interpolationActive;

        private bool startupBaselineInitialized;

        private bool startupBaselinePending;

        private bool startupSynchronizationPending;

        private bool runtimeOutputArmed;

        public BodyJointConstraint JointConstraint =>
            jointConstraint;

        public ServoControlUnit Servo =>
            servo;

        public bool OutputEnabled =>
            outputEnabled;

        public bool IsOutputArmed
        {
            get { return runtimeOutputArmed; }
        }

        public int ServoId
        {
            get { return servo != null ? servo.servoIndex : -1; }
        }

        public bool IsStartupBaselinePending
        {
            get { return startupBaselinePending; }
        }

        public bool HasCompletedStartupBaseline
        {
            get
            {
                return startupBaselineInitialized &&
                    !startupBaselinePending;
            }
        }

        public bool IsStartupSynchronizationPending =>
            startupSynchronizationPending;

        public bool IsOutputReady
        {
            get
            {
                return runtimeOutputArmed &&
                    startupBaselineInitialized &&
                    !startupBaselinePending &&
                    !startupSynchronizationPending &&
                    outputEnabled &&
                    isActiveAndEnabled &&
                    jointConstraint != null &&
                    servo != null;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveSender();
        }

        private void Start()
        {
            runtimeOutputArmed = armOutputOnStart;

            if (initializeStartupBaselineWithoutSending &&
                (!runtimeOutputArmed || !sendInitialAngleOnStart))
            {
                startupBaselinePending = true;
            }

            if (runtimeOutputArmed && sendInitialAngleOnStart)
            {
                SendCurrentAngleNow();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (startupBaselinePending)
            {
                InitializeStartupBaselineWithoutSending();
                startupBaselinePending = false;
                return;
            }

            if (startupSynchronizationPending)
            {
                return;
            }

            if (!runtimeOutputArmed)
            {
                return;
            }

            if (!outputEnabled)
            {
                return;
            }

            if (sendWhenAppliedAngleChanges)
            {
                TryScheduleCurrentAngle(
                    forceImmediate: false
                );
            }

            UpdateInterpolatedOutput();
        }

        private void OnDisable()
        {
            runtimeOutputArmed = false;
            interpolationActive = false;
            startupBaselinePending = false;
            startupBaselineInitialized = false;
            startupSynchronizationPending = false;
        }

        private void OnValidate()
        {
            if (minimumVirtualAngleDelta < 0f)
            {
                minimumVirtualAngleDelta = 0f;
            }

            if (interpolationDurationSeconds < 0f)
            {
                interpolationDurationSeconds = 0f;
            }

            if (interpolationSendIntervalSeconds < 0.01f)
            {
                interpolationSendIntervalSeconds = 0.01f;
            }

            assumedServoInputAngleOnStartup =
                Mathf.Clamp(
                    assumedServoInputAngleOnStartup,
                    0,
                    180
                );

            ResolveReferences();
        }

        /// <summary>
        /// Inspector 参照を補完する。
        /// </summary>
        [ContextMenu("Resolve References")]
        public void ResolveReferences()
        {
            if (jointConstraint == null)
            {
                jointConstraint =
                    GetComponent<BodyJointConstraint>();
            }

            if (runtimeSettings == null)
            {
                runtimeSettings =
                    FindObjectOfType<RuntimeConnectionSettings>();
            }

            if (servo == null)
            {
                servo =
                    new ServoControlUnit();
            }
        }

        /// <summary>
        /// Sender を取得し、ServoControlUnit を初期化する。
        /// Sender 未設定時は Scene 内の BodyCommandCoordinator を自動検索する。
        /// </summary>
        [ContextMenu("Resolve Sender")]
        public void ResolveSender()
        {
            ResolveReferences();

            sender =
                senderBehaviour as ICommandSender;

            if (sender == null)
            {
                BodyCommandCoordinator coordinator =
                    FindObjectOfType<BodyCommandCoordinator>();

                if (coordinator != null)
                {
                    senderBehaviour = coordinator;
                    sender = coordinator;
                }
            }

            if (sender == null)
            {
                initialized = false;

                Debug.LogWarning(
                    $"[BodyJointServoBridge] " +
                    $"ICommandSender is not assigned. " +
                    $"GameObject={gameObject.name}",
                    this
                );

                return;
            }

            servo.Initialize(
                sender,
                runtimeSettings
            );

            initialized = true;

            Debug.Log(
                $"[BodyJointServoBridge] " +
                $"Sender resolved. " +
                $"Joint={GetJointName()} " +
                $"ServoID={servo.servoIndex} " +
                $"Sender={senderBehaviour.name}",
                this
            );
        }

        /// <summary>
        /// 腕サーボ出力を許可し、次の LateUpdate で現在姿勢を
        /// 送信なしの Baseline として再取得する。
        /// </summary>
        public void ArmOutput()
        {
            if (runtimeOutputArmed)
            {
                return;
            }

            interpolationActive = false;
            startupBaselineInitialized = false;
            startupBaselinePending = true;
            startupSynchronizationPending = true;
            runtimeOutputArmed = true;

            Debug.Log(
                $"[BodyJointServoBridge] " +
                $"Arm output requested. " +
                $"Joint={GetJointName()}",
                this
            );
        }

        /// <summary>
        /// 腕サーボ出力を停止し、補間と保留中の Baseline 取得を破棄する。
        /// </summary>
        public void DisarmOutput()
        {
            runtimeOutputArmed = false;
            interpolationActive = false;
            startupBaselinePending = false;
            startupBaselineInitialized = false;
            startupSynchronizationPending = false;

            Debug.Log(
                $"[BodyJointServoBridge] " +
                $"Output disarmed. " +
                $"Joint={GetJointName()}",
                this
            );
        }

        /// <summary>
        /// 現在角度を即時送信する。
        /// 初期位置確認、単体メンテナンス、緊急的な手動確認用。
        /// </summary>
        [ContextMenu("Send Current Angle Now")]
        public void SendCurrentAngleNow()
        {
            TryScheduleCurrentAngle(
                forceImmediate: true
            );
        }

        /// <summary>
        /// 現在の変換結果だけを Console へ表示する。
        /// </summary>
        [ContextMenu("Log Current Conversion")]
        public void LogCurrentConversion()
        {
            if (jointConstraint == null)
            {
                Debug.LogWarning(
                    $"[BodyJointServoBridge] " +
                    $"BodyJointConstraint is null. " +
                    $"GameObject={gameObject.name}",
                    this
                );

                return;
            }

            float appliedAngle =
                jointConstraint.AppliedAngle;

            int servoInputAngle =
                ConvertToServoInputAngle(
                    appliedAngle
                );

            Debug.Log(
                $"[BodyJointServoBridge] " +
                $"Conversion preview. " +
                $"Joint={GetJointName()} " +
                $"AppliedAngle={appliedAngle:F1} " +
                $"Neutral={servoNeutralInputAngle:F1} " +
                $"Multiplier={virtualAngleMultiplier:F1} " +
                $"ServoInput={servoInputAngle} " +
                $"ServoID={servo.servoIndex}",
                this
            );
        }

        /// <summary>
        /// Play開始時の実機角度を、送信せずに内部状態へ登録する。
        ///
        /// Unity上の初期AppliedAngleと実機Neutralが一致しない軸でも、
        /// 起動直後に誤った initial-immediate 命令を送らない。
        /// </summary>
        [ContextMenu("Initialize Startup Baseline Without Sending")]
        public void InitializeStartupBaselineWithoutSending()
        {
            ResolveReferences();

            if (jointConstraint == null)
            {
                Debug.LogWarning(
                    $"[BodyJointServoBridge] " +
                    $"Startup baseline skipped: " +
                    $"BodyJointConstraint is null. " +
                    $"GameObject={gameObject.name}",
                    this
                );

                return;
            }

            float appliedAngle =
                jointConstraint.AppliedAngle;

            int convertedServoInputAngle =
                ConvertToServoInputAngle(
                    appliedAngle
                );

            currentServoInputAngle =
                Mathf.Clamp(
                    assumedServoInputAngleOnStartup,
                    0,
                    180
                );

            hasCurrentServoInputAngle =
                true;

            lastObservedAppliedAngle =
                appliedAngle;

            lastRequestedServoInputAngle =
                convertedServoInputAngle;

            interpolationStartServoInputAngle =
                currentServoInputAngle;

            interpolationTargetServoInputAngle =
                currentServoInputAngle;

            interpolationStartTime =
                0f;

            lastInterpolationSendTime =
                -999f;

            interpolationActive =
                false;

            startupBaselineInitialized =
                true;

            if (logInterpolation)
            {
                Debug.Log(
                    $"[BodyJointServoBridge][STARTUP_BASELINE] " +
                    $"Joint={GetJointName()} " +
                    $"ServoID={servo.servoIndex} " +
                    $"AssumedServoInput={currentServoInputAngle} " +
                    $"ObservedAppliedAngle={appliedAngle:F1} " +
                    $"ConvertedInputWithoutSend={convertedServoInputAngle}",
                    this
                );
            }
        }

        /// <summary>
        /// Explicit ARM lifecycle only: sends the current canonical joint pose
        /// through the existing conversion and sender path. Success means that
        /// the sender accepted the command; it does not mean physical arrival.
        /// </summary>
        public bool TrySendStartupSynchronizationCommand(
            out int servoInputAngle,
            out string reason
        )
        {
            servoInputAngle = 0;

            if (!runtimeOutputArmed)
            {
                reason = "bridge_output_disarmed";
                return false;
            }

            if (!startupSynchronizationPending)
            {
                reason = "startup_synchronization_not_pending";
                return false;
            }

            if (startupBaselinePending ||
                !startupBaselineInitialized)
            {
                reason = "startup_baseline_not_ready";
                return false;
            }

            if (!outputEnabled)
            {
                reason = "bridge_output_disabled";
                return false;
            }

            if (!EnsureInitialized())
            {
                reason = "bridge_sender_not_initialized";
                return false;
            }

            if (jointConstraint == null || servo == null)
            {
                reason = "bridge_source_missing";
                return false;
            }

            float appliedAngle = jointConstraint.AppliedAngle;
            servoInputAngle = ConvertToServoInputAngle(appliedAngle);

            if (!servo.TrySetAngle(
                servoInputAngle,
                forceSend: true,
                out int finalAngle,
                out reason))
            {
                return false;
            }

            lastObservedAppliedAngle = appliedAngle;
            lastRequestedServoInputAngle = servoInputAngle;
            currentServoInputAngle = servoInputAngle;
            hasCurrentServoInputAngle = true;
            interpolationActive = false;

            Debug.Log(
                $"[BodyJointServoBridge][STARTUP_SYNC_REQUESTED] " +
                $"Joint={GetJointName()} ServoID={servo.servoIndex} " +
                $"AppliedAngle={appliedAngle:F1} " +
                $"ServoInput={servoInputAngle} Final={finalAngle}",
                this
            );

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Opens normal output only after the controller has observed dispatch
        /// for every arm servo and completed its estimated settle interval.
        /// </summary>
        public bool CompleteStartupSynchronization(out string reason)
        {
            if (!runtimeOutputArmed ||
                startupBaselinePending ||
                !startupBaselineInitialized ||
                !startupSynchronizationPending)
            {
                reason = "startup_synchronization_not_completable";
                return false;
            }

            startupSynchronizationPending = false;
            reason = string.Empty;

            Debug.Log(
                $"[BodyJointServoBridge][STARTUP_SYNC_COMPLETE] " +
                $"Joint={GetJointName()} ServoID={servo.servoIndex}",
                this
            );

            return true;
        }

        /// <summary>
        /// Bridge 内部の送信履歴と補間状態をリセットする。
        /// </summary>
        [ContextMenu("Reset Bridge Send History")]
        public void ResetBridgeSendHistory()
        {
            lastObservedAppliedAngle =
                float.NaN;

            lastRequestedServoInputAngle =
                int.MinValue;

            hasCurrentServoInputAngle =
                false;

            currentServoInputAngle =
                0;

            interpolationStartServoInputAngle =
                0;

            interpolationTargetServoInputAngle =
                0;

            interpolationStartTime =
                0f;

            lastInterpolationSendTime =
                -999f;

            interpolationActive =
                false;

            startupBaselineInitialized =
                false;

            Debug.Log(
                $"[BodyJointServoBridge] " +
                $"Send history reset. " +
                $"Joint={GetJointName()}",
                this
            );
        }

        /// <summary>
        /// 現在の仮想角度を観測し、実機出力の目標値を更新する。
        ///
        /// forceImmediate = true:
        ///     補間せず即時送信する。
        ///
        /// forceImmediate = false:
        ///     初回のみ即時送信し、2回目以降は補間を開始する。
        /// </summary>
        private bool TryScheduleCurrentAngle(
            bool forceImmediate
        )
        {
            if (!runtimeOutputArmed)
            {
                Debug.Log(
                    $"[BodyJointServoBridge] " +
                    $"Send skipped: Output Disarmed. " +
                    $"Joint={GetJointName()}",
                    this
                );

                return false;
            }

            if (startupBaselinePending)
            {
                Debug.Log(
                    $"[BodyJointServoBridge] " +
                    $"Send skipped: Startup Baseline Pending. " +
                    $"Joint={GetJointName()}",
                    this
                );

                return false;
            }

            if (!outputEnabled)
            {
                Debug.Log(
                    $"[BodyJointServoBridge] " +
                    $"Send skipped: Output Disabled. " +
                    $"Joint={GetJointName()}",
                    this
                );

                return false;
            }

            if (initializeStartupBaselineWithoutSending &&
                !startupBaselineInitialized)
            {
                InitializeStartupBaselineWithoutSending();
            }

            if (!EnsureInitialized())
            {
                return false;
            }

            if (jointConstraint == null)
            {
                Debug.LogWarning(
                    $"[BodyJointServoBridge] " +
                    $"BodyJointConstraint is null. " +
                    $"GameObject={gameObject.name}",
                    this
                );

                return false;
            }

            float appliedAngle =
                jointConstraint.AppliedAngle;

            int servoInputAngle =
                ConvertToServoInputAngle(
                    appliedAngle
                );

            if (!forceImmediate &&
                !float.IsNaN(lastObservedAppliedAngle))
            {
                float angleDelta =
                    Mathf.Abs(
                        appliedAngle -
                        lastObservedAppliedAngle
                    );

                bool sameInputAngle =
                    servoInputAngle ==
                    lastRequestedServoInputAngle;

                if (angleDelta <
                    minimumVirtualAngleDelta &&
                    sameInputAngle)
                {
                    return false;
                }
            }

            lastObservedAppliedAngle =
                appliedAngle;

            lastRequestedServoInputAngle =
                servoInputAngle;

            if (forceImmediate ||
                !interpolateServoOutput ||
                interpolationDurationSeconds <= 0f ||
                !hasCurrentServoInputAngle)
            {
                SendServoInputImmediate(
                    servoInputAngle,
                    forceImmediate
                        ? "manual-immediate"
                        : "initial-immediate"
                );

                return true;
            }

            StartInterpolation(
                servoInputAngle
            );

            return true;
        }

        /// <summary>
        /// 現在出力値から新しい目標値へ補間を開始する。
        /// 補間中に次の目標値が来た場合は、最後に送った実機値から再開する。
        /// </summary>
        private void StartInterpolation(
            int targetServoInputAngle
        )
        {
            if (!hasCurrentServoInputAngle)
            {
                SendServoInputImmediate(
                    targetServoInputAngle,
                    "fallback-immediate"
                );

                return;
            }

            if (currentServoInputAngle ==
                targetServoInputAngle)
            {
                interpolationActive =
                    false;

                return;
            }

            interpolationStartServoInputAngle =
                currentServoInputAngle;

            interpolationTargetServoInputAngle =
                targetServoInputAngle;

            interpolationStartTime =
                Time.unscaledTime;

            lastInterpolationSendTime =
                -999f;

            interpolationActive =
                true;

            if (logInterpolation)
            {
                Debug.Log(
                    $"[BodyJointServoBridge][INTERPOLATION_START] " +
                    $"Joint={GetJointName()} " +
                    $"ServoID={servo.servoIndex} " +
                    $"From={interpolationStartServoInputAngle} " +
                    $"To={interpolationTargetServoInputAngle} " +
                    $"Duration={interpolationDurationSeconds:F2}s",
                    this
                );
            }
        }

        /// <summary>
        /// SmoothStep 補間で中間角度を送信する。
        /// </summary>
        private void UpdateInterpolatedOutput()
        {
            if (!interpolationActive)
            {
                return;
            }

            float now =
                Time.unscaledTime;

            if (now -
                lastInterpolationSendTime <
                interpolationSendIntervalSeconds)
            {
                return;
            }

            float elapsed =
                now -
                interpolationStartTime;

            float normalizedTime =
                interpolationDurationSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed /
                        interpolationDurationSeconds
                    );

            float smoothTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );

            int nextServoInputAngle =
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        interpolationStartServoInputAngle,
                        interpolationTargetServoInputAngle,
                        smoothTime
                    )
                );

            lastInterpolationSendTime =
                now;

            bool reachedTarget =
                normalizedTime >= 1f;

            if (nextServoInputAngle !=
                    currentServoInputAngle ||
                reachedTarget)
            {
                SendServoInputImmediate(
                    nextServoInputAngle,
                    reachedTarget
                        ? "interpolation-finished"
                        : "interpolation-step"
                );
            }

            if (reachedTarget)
            {
                interpolationActive =
                    false;

                if (logInterpolation)
                {
                    Debug.Log(
                        $"[BodyJointServoBridge][INTERPOLATION_END] " +
                        $"Joint={GetJointName()} " +
                        $"ServoID={servo.servoIndex} " +
                        $"Angle={currentServoInputAngle}",
                        this
                    );
                }
            }
        }

        /// <summary>
        /// ServoControlUnit へ1回だけ送信する。
        /// </summary>
        private void SendServoInputImmediate(
            int servoInputAngle,
            string reason
        )
        {
            if (logConversion)
            {
                Debug.Log(
                    $"[BodyJointServoBridge] " +
                    $"Send request. " +
                    $"Joint={GetJointName()} " +
                    $"ServoInput={servoInputAngle} " +
                    $"ServoID={servo.servoIndex} " +
                    $"Reason={reason}",
                    this
                );
            }

            currentServoInputAngle =
                servoInputAngle;

            hasCurrentServoInputAngle =
                true;

            servo.SetAngle(
                servoInputAngle
            );
        }

        /// <summary>
        /// 上位Action層から動作速度を変更するための入口。
        /// AIは安全なプリセットを選び、ActionControllerが秒数へ変換して渡す。
        /// </summary>
        public void SetInterpolationDurationSeconds(
            float durationSeconds
        )
        {
            interpolationDurationSeconds =
                Mathf.Max(
                    0f,
                    durationSeconds
                );
        }

        /// <summary>
        /// 上位Action層から補間条件をまとめて変更するための入口。
        /// </summary>
        public void ConfigureInterpolation(
            float durationSeconds,
            float sendIntervalSeconds
        )
        {
            interpolationDurationSeconds =
                Mathf.Max(
                    0f,
                    durationSeconds
                );

            interpolationSendIntervalSeconds =
                Mathf.Max(
                    0.01f,
                    sendIntervalSeconds
                );
        }

        private bool EnsureInitialized()
        {
            if (initialized &&
                sender != null)
            {
                return true;
            }

            ResolveSender();

            return initialized &&
                sender != null;
        }

        private int ConvertToServoInputAngle(
            float appliedAngle
        )
        {
            float convertedAngle =
                servoNeutralInputAngle +
                appliedAngle *
                virtualAngleMultiplier;

            convertedAngle +=
                CalculateAutomaticRangeOffset();

            return Mathf.RoundToInt(
                convertedAngle
            );
        }

        /// <summary>
        /// 仮想関節の変換後レンジ全体が実機サーボ範囲の外側にあり、
        /// かつ倍率変更を行わず平行移動だけで収まる場合に限り、
        /// 自動的に入力範囲へ移し替える。
        ///
        /// 右肘:
        ///     仮想範囲 -180～0
        ///     実機範囲    0～180
        ///     自動補正  +180
        ///
        /// 既存の -90～90 軸など、範囲が実機下限をまたぐ軸には
        /// 自動補正を適用しない。
        /// </summary>
        private float CalculateAutomaticRangeOffset()
        {
            if (jointConstraint == null ||
                servo == null)
            {
                return 0f;
            }

            float convertedEndpointA =
                servoNeutralInputAngle +
                jointConstraint.MinAngle *
                virtualAngleMultiplier;

            float convertedEndpointB =
                servoNeutralInputAngle +
                jointConstraint.MaxAngle *
                virtualAngleMultiplier;

            float convertedMin =
                Mathf.Min(
                    convertedEndpointA,
                    convertedEndpointB
                );

            float convertedMax =
                Mathf.Max(
                    convertedEndpointA,
                    convertedEndpointB
                );

            float servoMin =
                servo.minAngle;

            float servoMax =
                servo.maxAngle;

            float convertedRange =
                convertedMax -
                convertedMin;

            float servoRange =
                servoMax -
                servoMin;

            const float tolerance =
                0.001f;

            if (convertedRange >
                servoRange +
                tolerance)
            {
                return 0f;
            }

            if (convertedMax <=
                    servoMin +
                    tolerance &&
                convertedMin <
                    servoMin -
                    tolerance)
            {
                return
                    servoMin -
                    convertedMin;
            }

            if (convertedMin >=
                    servoMax -
                    tolerance &&
                convertedMax >
                    servoMax +
                    tolerance)
            {
                return
                    servoMax -
                    convertedMax;
            }

            return 0f;
        }

        private string GetJointName()
        {
            if (jointConstraint == null)
            {
                return gameObject.name;
            }

            return jointConstraint.JointId;
        }
    }
}

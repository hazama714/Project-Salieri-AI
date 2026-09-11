// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.State;
using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution
{
    /// <summary>
    /// ExecutionResourceManager
    ///
    /// InteractionState ごとに StateExecutionProfile を適用し、
    /// その状態で各実行資源をどの程度使えるかを管理する。
    ///
    /// Phase 5-A:
    /// AutonomousClock など上位Runtimeから参照できるように、
    /// CanRunAutonomousThink / CanUseVoice / CanUseFaceTracking などの
    /// 問い合わせAPIを追加する。
    ///
    /// このクラスは「資源の許可状態」を返すだけで、
    /// LLM / Voice / Servo / Action を直接起動しない。
    /// </summary>
    public sealed class ExecutionResourceManager : MonoBehaviour
    {
        [Header("Profiles")]
        [SerializeField]
        private StateExecutionProfile[] profiles;

        [Header("Tracking")]
        [SerializeField]
        private MonoBehaviour faceTrackingBehaviour;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private StateExecutionProfile currentProfile;

        /// <summary>
        /// 現在適用中の StateExecutionProfile。
        /// 外部から内容確認だけできるようにする。
        /// </summary>
        public StateExecutionProfile CurrentProfile
        {
            get { return currentProfile; }
        }

        /// <summary>
        /// 現在の LLM 実行レベル。
        /// Profile未適用時は Normal 扱いにする。
        /// </summary>
        public ExecutionLevel CurrentLlmLevel
        {
            get { return GetLevelOrDefault(ResourceKind.Llm); }
        }

        /// <summary>
        /// 現在の FaceTracking 実行レベル。
        /// Profile未適用時は Normal 扱いにする。
        /// </summary>
        public ExecutionLevel CurrentFaceTrackingLevel
        {
            get { return GetLevelOrDefault(ResourceKind.FaceTracking); }
        }

        /// <summary>
        /// 現在の ServoTracking 実行レベル。
        /// Profile未適用時は Normal 扱いにする。
        /// </summary>
        public ExecutionLevel CurrentServoTrackingLevel
        {
            get { return GetLevelOrDefault(ResourceKind.ServoTracking); }
        }

        /// <summary>
        /// 現在の Voice 実行レベル。
        /// Profile未適用時は Normal 扱いにする。
        /// </summary>
        public ExecutionLevel CurrentVoiceLevel
        {
            get { return GetLevelOrDefault(ResourceKind.Voice); }
        }

        /// <summary>
        /// 現在の Expression 実行レベル。
        /// Profile未適用時は Normal 扱いにする。
        /// </summary>
        public ExecutionLevel CurrentExpressionLevel
        {
            get { return GetLevelOrDefault(ResourceKind.Expression); }
        }

        /// <summary>
        /// 現在の Camera 実行レベル。
        /// Profile未適用時は Normal 扱いにする。
        /// </summary>
        public ExecutionLevel CurrentCameraLevel
        {
            get { return GetLevelOrDefault(ResourceKind.Camera); }
        }

        /// <summary>
        /// InteractionState に対応する StateExecutionProfile を適用する。
        /// </summary>
        public void ApplyProfile(InteractionState state)
        {
            if (verboseLog)
            {
                Debug.Log($"[ExecutionResourceManager] ApplyProfile: {state}");
            }

            currentProfile = null;

            if (profiles == null || profiles.Length == 0)
            {
                Debug.LogWarning("[ExecutionResourceManager] profiles is empty.");
                ApplyTracking();
                return;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] != null && profiles[i].state == state)
                {
                    currentProfile = profiles[i];
                    break;
                }
            }

            if (currentProfile == null)
            {
                Debug.LogWarning($"[ExecutionResourceManager] Profile not found: {state}");
                ApplyTracking();
                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    $"[ExecutionResourceManager] Profile found: {state}, " +
                    $"Camera={currentProfile.camera}, " +
                    $"FaceTracking={currentProfile.faceTracking}, " +
                    $"ServoTracking={currentProfile.servoTracking}, " +
                    $"LLM={currentProfile.llm}, " +
                    $"Voice={currentProfile.voice}, " +
                    $"Expression={currentProfile.expression}"
                );
            }

            ApplyTracking();
        }

        /// <summary>
        /// AutonomousClock が IdleTick / 自律思考を起動してよいかを見るためのAPI。
        ///
        /// Phase 5-Aでは llm が Off のときだけ false。
        /// Light / Normal / Full は許可する。
        /// </summary>
        public bool CanRunAutonomousThink()
        {
            bool result = IsResourceEnabled(CurrentLlmLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanRunAutonomousThink=" +
                    result +
                    " llm=" +
                    CurrentLlmLevel
                );
            }

            return result;
        }

        /// <summary>
        /// LLM資源を使ってよいか。
        /// 現時点では CanRunAutonomousThink と同じ判定。
        /// </summary>
        public bool CanUseLlm()
        {
            bool result = IsResourceEnabled(CurrentLlmLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanUseLlm=" +
                    result +
                    " llm=" +
                    CurrentLlmLevel
                );
            }

            return result;
        }

        /// <summary>
        /// 顔追従を使ってよいか。
        /// </summary>
        public bool CanUseFaceTracking()
        {
            bool result = IsResourceEnabled(CurrentFaceTrackingLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanUseFaceTracking=" +
                    result +
                    " faceTracking=" +
                    CurrentFaceTrackingLevel
                );
            }

            return result;
        }

        /// <summary>
        /// サーボ追従を使ってよいか。
        /// </summary>
        public bool CanUseServoTracking()
        {
            bool result = IsResourceEnabled(CurrentServoTrackingLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanUseServoTracking=" +
                    result +
                    " servoTracking=" +
                    CurrentServoTrackingLevel
                );
            }

            return result;
        }

        /// <summary>
        /// 音声出力を使ってよいか。
        /// </summary>
        public bool CanUseVoice()
        {
            bool result = IsResourceEnabled(CurrentVoiceLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanUseVoice=" +
                    result +
                    " voice=" +
                    CurrentVoiceLevel
                );
            }

            return result;
        }

        /// <summary>
        /// 表情制御を使ってよいか。
        /// </summary>
        public bool CanUseExpression()
        {
            bool result = IsResourceEnabled(CurrentExpressionLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanUseExpression=" +
                    result +
                    " expression=" +
                    CurrentExpressionLevel
                );
            }

            return result;
        }

        /// <summary>
        /// Camera資源を使ってよいか。
        /// </summary>
        public bool CanUseCamera()
        {
            bool result = IsResourceEnabled(CurrentCameraLevel);

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionResourceManager] CanUseCamera=" +
                    result +
                    " camera=" +
                    CurrentCameraLevel
                );
            }

            return result;
        }

        /// <summary>
        /// Phase 3C-3 read-only profile query.
        ///
        /// This reports only whether the current InteractionState profile
        /// enables the capability represented by an orchestration resource.
        /// It does not acquire ownership, inspect lease conflicts, reserve
        /// hardware, or prove that a device is ready.
        /// </summary>
        public bool TryGetResourceAvailability(
            string resourceId,
            out bool available,
            out string reason)
        {
            available = false;
            reason = string.Empty;

            ExecutionLevel level;
            string profileCapability;
            switch (resourceId)
            {
                case ExecutionResourceIds.SpeechOutput:
                    level = CurrentVoiceLevel;
                    profileCapability = "Voice";
                    break;
                case ExecutionResourceIds.LlmGeneration:
                    level = CurrentLlmLevel;
                    profileCapability = "Llm";
                    break;
                case ExecutionResourceIds.VisionLight:
                case ExecutionResourceIds.VisionHeavy:
                    level = CurrentCameraLevel;
                    profileCapability = "Camera";
                    break;
                case ExecutionResourceIds.HeadMotion:
                case ExecutionResourceIds.ArmMotion:
                case ExecutionResourceIds.CrawlerMotion:
                case ExecutionResourceIds.VirtualBody:
                case ExecutionResourceIds.PhysicalTransport:
                    level = CurrentServoTrackingLevel;
                    profileCapability = "ServoTracking";
                    break;
                default:
                    reason = "RESOURCE_PROFILE_MAPPING_UNKNOWN:" +
                        (resourceId ?? string.Empty);
                    return false;
            }

            available = IsResourceEnabled(level);
            reason = (available
                ? "RESOURCE_PROFILE_ENABLED:"
                : "RESOURCE_PROFILE_DISABLED:") + profileCapability;
            return true;
        }

        /// <summary>
        /// 現在Profileの faceTracking 設定を実際のBehaviour enabledへ反映する。
        /// </summary>
        private void ApplyTracking()
        {
            if (verboseLog)
            {
                Debug.Log("[ExecutionResourceManager] ApplyTracking ENTER");
            }

            if (faceTrackingBehaviour == null)
            {
                Debug.LogWarning("[ExecutionResourceManager] faceTrackingBehaviour is null.");
                return;
            }

            bool enableTracking = CanUseFaceTracking();

            faceTrackingBehaviour.enabled = enableTracking;

            if (verboseLog)
            {
                Debug.Log($"[ExecutionResourceManager] FaceTracking = {enableTracking}");
            }
        }

        /// <summary>
        /// ExecutionLevel が Off 以外なら使用可能とみなす。
        /// Light / Normal / Full は許可。
        /// </summary>
        private bool IsResourceEnabled(ExecutionLevel level)
        {
            return level != ExecutionLevel.Off;
        }

        /// <summary>
        /// currentProfile が未適用の場合は Normal として扱う。
        ///
        /// 理由:
        /// Profile未適用の起動直後に、ExecutionResourceManagerだけが原因で
        /// 既存Runtimeを止めないため。
        /// </summary>
        private ExecutionLevel GetLevelOrDefault(ResourceKind kind)
        {
            if (currentProfile == null)
            {
                return ExecutionLevel.Normal;
            }

            switch (kind)
            {
                case ResourceKind.Camera:
                    return currentProfile.camera;

                case ResourceKind.FaceTracking:
                    return currentProfile.faceTracking;

                case ResourceKind.ServoTracking:
                    return currentProfile.servoTracking;

                case ResourceKind.Llm:
                    return currentProfile.llm;

                case ResourceKind.Voice:
                    return currentProfile.voice;

                case ResourceKind.Expression:
                    return currentProfile.expression;

                default:
                    return ExecutionLevel.Normal;
            }
        }

        private enum ResourceKind
        {
            Camera,
            FaceTracking,
            ServoTracking,
            Llm,
            Voice,
            Expression
        }
    }
}

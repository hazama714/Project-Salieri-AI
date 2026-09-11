// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Body.Joint;
using UnityEngine;
using SalieriAI.Core.Diagnostics.Performance;

namespace SalieriAI.Body.IK
{
    /// <summary>
    /// 実機準拠 Transform Rig 用の左右共通 Arm IK Solver。
    ///
    /// HandTarget の位置へ HandEndEffector を近づける。
    ///
    /// Phase 3-D:
    /// - ShoulderOpen / ShoulderLift / Elbow の複数軸を
    ///   組み合わせて位置探索する。
    /// - ElbowPoleTarget を使用し、肘の向きを評価する。
    /// - Pole Target 使用時のみ、必要に応じて ShoulderTwist も
    ///   探索へ含める。
    /// - 各角度は必ず BodyJointConstraint.SetTargetAngle() を通す。
    ///
    /// Humanoid Bone、VRM、実サーボ送信には依存しない。
    /// </summary>
    [ExecuteAlways]
    public sealed class RobotArmIKSolver : MonoBehaviour
    {
        [Header("Arm Identity")]

        [Tooltip("ログ識別用。右腕は Right、左腕は Left など")]
        [SerializeField]
        private string armLabel = "Right";

        [Header("IK Target")]

        [Tooltip("手先を近づけたい目標位置")]
        [SerializeField]
        private Transform handTarget;

        [Tooltip("実際に動く手先モデルの Transform")]
        [SerializeField]
        private Transform handEndEffector;

        [Header("Elbow Pole Target")]

        [Tooltip("肘方向の制御を使用する")]
        [SerializeField]
        private bool useElbowPole = false;

        [Tooltip("肘を向けたい方向を示す Pole Target")]
        [SerializeField]
        private Transform elbowPoleTarget;

        [Tooltip("肘方向計算の起点。対象腕の肩付近の Transform を指定する")]
        [SerializeField]
        private Transform shoulderReference;

        [Tooltip("実際の肘位置。対象腕の ElbowAxis を指定する")]
        [SerializeField]
        private Transform elbowReference;

        [Tooltip(
            "手先位置に対して、肘方向をどの程度重視するか。" +
            "最初は 0.03 推奨。"
        )]
        [SerializeField]
        [Min(0f)]
        private float elbowPoleWeight = 0.03f;

        [Tooltip(
            "肘方向が到達したとみなす誤差。" +
            "0.10 はおおむね 18度程度。"
        )]
        [SerializeField]
        [Range(0.001f, 1f)]
        private float poleDirectionTolerance = 0.10f;

        [Tooltip(
            "Pole方向を改善するために許容する手先位置の微小な後退量。"
        )]
        [SerializeField]
        [Min(0f)]
        private float polePositionSlack = 0.003f;

        [Tooltip(
            "Pole Target 使用時に ShoulderTwist を探索へ含める。" +
            "肘の向きを変えるため、通常は ON 推奨。"
        )]
        [SerializeField]
        private bool includeShoulderTwistForPoleSolve = true;

        [Header("Arm Joints")]

        [Tooltip("上腕ねじり")]
        [SerializeField]
        private BodyJointConstraint shoulderTwist;

        [Tooltip("肩の横方向の開閉")]
        [SerializeField]
        private BodyJointConstraint shoulderOpen;

        [Tooltip("肩の前後方向・持ち上げ")]
        [SerializeField]
        private BodyJointConstraint shoulderLift;

        [Tooltip("肘の曲げ伸ばし")]
        [SerializeField]
        private BodyJointConstraint elbow;

        [Header("Solver Settings")]

        [Tooltip("1回の Solve で繰り返す最大回数")]
        [SerializeField]
        [Min(1)]
        private int maxIterations = 32;

        [Tooltip("探索開始時の角度刻み")]
        [SerializeField]
        [Min(0.1f)]
        private float initialStepAngle = 6f;

        [Tooltip("探索を終了する最小角度刻み")]
        [SerializeField]
        [Min(0.01f)]
        private float minimumStepAngle = 0.25f;

        [Tooltip("Target へ到達したとみなす距離")]
        [SerializeField]
        [Min(0.0001f)]
        private float positionTolerance = 0.015f;

        [Tooltip("微小な数値誤差を改善として誤認しないための距離差")]
        [SerializeField]
        [Min(0.000001f)]
        private float improvementEpsilon = 0.00005f;

        // ============================================================
        // Shoulder Zone Limit Test
        // ============================================================

        [Header("Shoulder Zone Limit Test")]

        [Tooltip(
            "実験用の Shoulder Zone Axis Limit を使用する。" +
            "OFF の場合は従来の IK 探索と同じ動作になる。"
        )]
        [SerializeField]
        private bool useShoulderZoneLimits = false;

        [Tooltip(
            "ShoulderLift（振り）の Zone 最小角。" +
            "BodyJointConstraint の物理的な min/max は変更しない。"
        )]
        [SerializeField]
        private float zoneLiftMin = -90f;

        [Tooltip(
            "ShoulderLift（振り）の Zone 最大角。" +
            "BodyJointConstraint の物理的な min/max は変更しない。"
        )]
        [SerializeField]
        private float zoneLiftMax = 90f;

        [Tooltip(
            "ShoulderOpen（開き）の Zone 最小角。" +
            "BodyJointConstraint の物理的な min/max は変更しない。"
        )]
        [SerializeField]
        private float zoneOpenMin = -90f;

        [Tooltip(
            "ShoulderOpen（開き）の Zone 最大角。" +
            "BodyJointConstraint の物理的な min/max は変更しない。"
        )]
        [SerializeField]
        private float zoneOpenMax = 90f;

        [Header("Shoulder Twist Policy")]

        [Tooltip(
            "Pole Target を使わない通常の位置探索にも、" +
            "上腕ねじりを含める。初期検証では OFF 推奨。"
        )]
        [SerializeField]
        private bool includeShoulderTwistInPositionSolve = false;

        [Header("Continuous Solve")]

        [Tooltip("Play中に毎フレーム IK を更新する。初期検証では OFF 推奨")]
        [SerializeField]
        private bool solveContinuously = false;

        [Header("Diagnostics")]

        [SerializeField]
        private bool logSolveResult = true;

        public float LastDistance { get; private set; }

        public float LastPoleDirectionError { get; private set; }

        public float LastScore { get; private set; }

        public bool LastSolveReachedTarget { get; private set; }

        public string ArmLabel => armLabel;

        public float PositionTolerance => positionTolerance;

        public bool IsContinuousSolveEnabled => solveContinuously;

        public bool LastSolveResultValid { get; private set; }

        public int LastSolveFrame { get; private set; } = -1;

        private readonly float[] searchDirections =
        {
            -1f,
            0f,
            1f
        };

        private void OnValidate()
        {
            if (maxIterations < 1)
            {
                maxIterations = 1;
            }

            if (minimumStepAngle < 0.01f)
            {
                minimumStepAngle = 0.01f;
            }

            if (initialStepAngle < minimumStepAngle)
            {
                initialStepAngle = minimumStepAngle;
            }

            if (positionTolerance < 0.0001f)
            {
                positionTolerance = 0.0001f;
            }

            if (improvementEpsilon < 0.000001f)
            {
                improvementEpsilon = 0.000001f;
            }

            if (elbowPoleWeight < 0f)
            {
                elbowPoleWeight = 0f;
            }

            if (polePositionSlack < 0f)
            {
                polePositionSlack = 0f;
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!solveContinuously)
            {
                return;
            }

            long virtualBodyStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            try
            {
                SolveOnce();
            }
            finally
            {
                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.VirtualBody,
                    virtualBodyStarted);
            }
        }

        /// <summary>
        /// 現在姿勢を起点として、手先を HandTarget へ近づける。
        ///
        /// Pole Target が有効な場合は、
        /// 手先位置と肘方向の両方を評価する。
        /// </summary>
        [ContextMenu("Solve Once")]
        public void SolveOnce()
        {
            LastSolveResultValid = false;
            LastSolveReachedTarget = false;
            LastSolveFrame = Time.frameCount;

            if (!ValidateReferences())
            {
                return;
            }

            PoseAngles originalPose = CaptureCurrentPose();

            PoseEvaluation originalEvaluation =
                EvaluateCurrentPose();

            PoseEvaluation currentEvaluation =
                originalEvaluation;

            float stepAngle = initialStepAngle;

            for (int iteration = 0;
                 iteration < maxIterations;
                 iteration++)
            {
                if (IsGoalReached(currentEvaluation))
                {
                    break;
                }

                PoseAngles currentPose =
                    CaptureCurrentPose();

                PoseAngles bestPose =
                    currentPose;

                PoseEvaluation bestEvaluation =
                    currentEvaluation;

                bool improved =
                    TryFindBetterCombinedPose(
                        currentPose,
                        stepAngle,
                        ref bestPose,
                        ref bestEvaluation
                    );

                if (improved)
                {
                    ApplyPose(bestPose);

                    currentEvaluation =
                        EvaluateCurrentPose();
                }
                else
                {
                    ApplyPose(currentPose);

                    stepAngle *= 0.5f;

                    if (stepAngle < minimumStepAngle)
                    {
                        break;
                    }
                }
            }

            PoseEvaluation finalEvaluation =
                EvaluateCurrentPose();

            bool originalPoseRestored = false;

            if (finalEvaluation.handDistance >
                originalEvaluation.handDistance +
                polePositionSlack +
                improvementEpsilon)
            {
                originalPoseRestored = true;
                ApplyPose(originalPose);

                finalEvaluation =
                    EvaluateCurrentPose();

                Debug.LogWarning(
                    "[RobotArmIKSolver] " +
                    "Solve result moved the hand too far away. " +
                    "Original pose restored.",
                    this
                );
            }

            LastDistance =
                finalEvaluation.handDistance;

            LastPoleDirectionError =
                finalEvaluation.poleDirectionError;

            LastScore =
                finalEvaluation.score;

            LastSolveResultValid = !originalPoseRestored;

            LastSolveReachedTarget =
                LastSolveResultValid && IsGoalReached(finalEvaluation);

            if (logSolveResult)
            {
                PoseAngles resultPose =
                    CaptureCurrentPose();

                Debug.Log(
                    $"[RobotArmIKSolver:{armLabel}] Solve completed. " +
                    $"initialDistance=" +
                    $"{originalEvaluation.handDistance:F4}, " +
                    $"distance={LastDistance:F4}, " +
                    $"poleError={LastPoleDirectionError:F4}, " +
                    $"signedPoleAngle={GetSignedPoleAngle():F1}, " +
                    $"poleTarget={GetPoleTargetLogText()}, " +
                    $"score={LastScore:F4}, " +
                    $"reached={LastSolveReachedTarget}, " +
                    $"target={handTarget.position}, " +
                    $"hand={handEndEffector.position}, " +
                    $"twist={resultPose.twist:F1}, " +
                    $"open={resultPose.open:F1}, " +
                    $"lift={resultPose.lift:F1}, " +
                    $"elbow={resultPose.elbow:F1}, " +
                    $"useElbowPole={useElbowPole}, " +
                    $"twistInSolve={ShouldIncludeTwistInSolve()}, " +
                    $"zoneLimits={useShoulderZoneLimits}",
                    this
                );
            }
        }

        /// <summary>
        /// 現在姿勢の周辺にある複数軸の組み合わせを評価する。
        /// </summary>
        private bool TryFindBetterCombinedPose(
            PoseAngles currentPose,
            float stepAngle,
            ref PoseAngles bestPose,
            ref PoseEvaluation bestEvaluation
        )
        {
            bool improved = false;

            if (ShouldIncludeTwistInSolve())
            {
                foreach (float twistDirection in searchDirections)
                {
                    improved |=
                        ExplorePositionJointCombinations(
                            currentPose,
                            currentPose.twist +
                            twistDirection * stepAngle,
                            stepAngle,
                            ref bestPose,
                            ref bestEvaluation
                        );
                }

                return improved;
            }

            improved |=
                ExplorePositionJointCombinations(
                    currentPose,
                    currentPose.twist,
                    stepAngle,
                    ref bestPose,
                    ref bestEvaluation
                );

            return improved;
        }

        /// <summary>
        /// ShoulderOpen / ShoulderLift / Elbow の
        /// 組み合わせを評価する。
        /// </summary>
        private bool ExplorePositionJointCombinations(
            PoseAngles currentPose,
            float twistAngle,
            float stepAngle,
            ref PoseAngles bestPose,
            ref PoseEvaluation bestEvaluation
        )
        {
            bool improved = false;

            foreach (float openDirection in searchDirections)
            {
                foreach (float liftDirection in searchDirections)
                {
                    foreach (float elbowDirection in searchDirections)
                    {
                        bool isOriginalPose =
                            Mathf.Approximately(
                                twistAngle,
                                currentPose.twist
                            ) &&
                            Mathf.Approximately(
                                openDirection,
                                0f
                            ) &&
                            Mathf.Approximately(
                                liftDirection,
                                0f
                            ) &&
                            Mathf.Approximately(
                                elbowDirection,
                                0f
                            );

                        if (isOriginalPose)
                        {
                            continue;
                        }

                        PoseAngles candidatePose =
                            new PoseAngles
                            {
                                twist = twistAngle,

                                open =
                                    currentPose.open +
                                    openDirection *
                                    stepAngle,

                                lift =
                                    currentPose.lift +
                                    liftDirection *
                                    stepAngle,

                                elbow =
                                    currentPose.elbow +
                                    elbowDirection *
                                    stepAngle
                            };

                        // Zone Limit は候補探索時だけ適用する。
                        //
                        // BodyJointConstraint の min/max は変更しない。
                        // Zone 外の候補は Clamp せず、その候補自体を
                        // 探索対象から除外する。
                        if (!IsWithinShoulderZone(candidatePose))
                        {
                            continue;
                        }

                        ApplyPose(candidatePose);

                        PoseEvaluation candidateEvaluation =
                            EvaluateCurrentPose();

                        if (!IsBetterCandidate(
                                candidateEvaluation,
                                bestEvaluation
                            ))
                        {
                            continue;
                        }

                        bestEvaluation =
                            candidateEvaluation;

                        bestPose =
                            CaptureCurrentPose();

                        improved = true;
                    }
                }
            }

            ApplyPose(currentPose);

            return improved;
        }

        /// <summary>
        /// 実験用 Shoulder Zone 内に候補姿勢が存在するかを判定する。
        ///
        /// Zone Limit は ShoulderLift（振り）と
        /// ShoulderOpen（開き）の探索候補だけを制限する。
        ///
        /// BodyJointConstraint の物理的 min/max は変更しない。
        /// </summary>
        private bool IsWithinShoulderZone(
            PoseAngles pose
        )
        {
            if (!useShoulderZoneLimits)
            {
                return true;
            }

            if (pose.lift < zoneLiftMin ||
                pose.lift > zoneLiftMax)
            {
                return false;
            }

            if (pose.open < zoneOpenMin ||
                pose.open > zoneOpenMax)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 候補姿勢が現在の最良姿勢より良いかを判定する。
        ///
        /// 基本は手先距離を優先する。
        /// Pole Target が有効な場合のみ、手先位置を大きく損なわない
        /// 範囲で肘方向の改善も採用する。
        /// </summary>
        private bool IsBetterCandidate(
            PoseEvaluation candidate,
            PoseEvaluation best
        )
        {
            bool handClearlyImproved =
                candidate.handDistance +
                improvementEpsilon <
                best.handDistance;

            if (handClearlyImproved)
            {
                return true;
            }

            if (!useElbowPole)
            {
                return false;
            }

            float allowedHandDistance =
                Mathf.Max(
                    positionTolerance,
                    best.handDistance +
                    polePositionSlack
                );

            if (candidate.handDistance >
                allowedHandDistance)
            {
                return false;
            }

            return candidate.score +
                   improvementEpsilon <
                   best.score;
        }

        /// <summary>
        /// 対象腕4軸を Home 角度へ戻す。
        /// </summary>
        [ContextMenu("Return Arm Home")]
        public void ReturnArmHome()
        {
            LastSolveResultValid = false;
            LastSolveReachedTarget = false;

            if (!ValidateJointReferences())
            {
                return;
            }

            shoulderTwist.ReturnHome();
            shoulderOpen.ReturnHome();
            shoulderLift.ReturnHome();
            elbow.ReturnHome();

            if (handTarget != null &&
                handEndEffector != null)
            {
                PoseEvaluation evaluation =
                    EvaluateCurrentPose();

                LastDistance =
                    evaluation.handDistance;

                LastPoleDirectionError =
                    evaluation.poleDirectionError;

                LastScore =
                    evaluation.score;
            }
            else
            {
                LastDistance = 0f;
                LastPoleDirectionError = 0f;
                LastScore = 0f;
            }

            Debug.Log(
                $"[RobotArmIKSolver:{armLabel}] Arm returned home.",
                this
            );
        }

        /// <summary>
        /// 旧右腕専用名との互換用。新規処理では ReturnArmHome() を使う。
        /// </summary>
        public void ReturnRightArmHome()
        {
            ReturnArmHome();
        }

        /// <summary>
        /// 現在の対象腕IK状態を Console へ表示する。
        /// </summary>
        [ContextMenu("Log Current IK State")]
        public void LogCurrentIkState()
        {
            if (!ValidateReferences())
            {
                return;
            }

            PoseAngles pose =
                CaptureCurrentPose();

            PoseEvaluation evaluation =
                EvaluateCurrentPose();

            Debug.Log(
                $"[RobotArmIKSolver:{armLabel}] " +
                $"distance={evaluation.handDistance:F4}, " +
                $"poleError={evaluation.poleDirectionError:F4}, " +
                $"signedPoleAngle={GetSignedPoleAngle():F1}, " +
                $"poleTarget={GetPoleTargetLogText()}, " +
                $"score={evaluation.score:F4}, " +
                $"target={handTarget.position}, " +
                $"hand={handEndEffector.position}, " +
                $"twist={pose.twist:F1}, " +
                $"open={pose.open:F1}, " +
                $"lift={pose.lift:F1}, " +
                $"elbow={pose.elbow:F1}, " +
                $"useElbowPole={useElbowPole}, " +
                $"zoneLimits={useShoulderZoneLimits}",
                this
            );
        }

        /// <summary>
        /// 現在適用されている角度を取得する。
        /// </summary>
        private PoseAngles CaptureCurrentPose()
        {
            return new PoseAngles
            {
                twist =
                    shoulderTwist.AppliedAngle,

                open =
                    shoulderOpen.AppliedAngle,

                lift =
                    shoulderLift.AppliedAngle,

                elbow =
                    elbow.AppliedAngle
            };
        }

        /// <summary>
        /// 対象腕4軸へ角度を適用する。
        ///
        /// 各 BodyJointConstraint が min / max Clamp と
        /// 単軸制限を担当する。
        /// </summary>
        private void ApplyPose(PoseAngles pose)
        {
            shoulderTwist.SetTargetAngle(
                pose.twist
            );

            shoulderOpen.SetTargetAngle(
                pose.open
            );

            shoulderLift.SetTargetAngle(
                pose.lift
            );

            elbow.SetTargetAngle(
                pose.elbow
            );
        }

        /// <summary>
        /// 現在姿勢の手先距離、Pole方向誤差、総合Scoreを取得する。
        /// </summary>
        private PoseEvaluation EvaluateCurrentPose()
        {
            float handDistance =
                Vector3.Distance(
                    handEndEffector.position,
                    handTarget.position
                );

            float poleDirectionError =
                useElbowPole
                    ? GetPoleDirectionError()
                    : 0f;

            float score =
                handDistance +
                poleDirectionError *
                elbowPoleWeight;

            return new PoseEvaluation
            {
                handDistance =
                    handDistance,

                poleDirectionError =
                    poleDirectionError,

                score =
                    score
            };
        }

        /// <summary>
        /// Pole Target が未接続でも診断ログを安全に出せるようにする。
        /// </summary>
        private string GetPoleTargetLogText()
        {
            return elbowPoleTarget != null
                ? elbowPoleTarget.position.ToString()
                : "unassigned";
        }

        /// <summary>
        /// 肩から手先へ向かう軸を基準として、
        /// 実際の肘方向と Pole Target 方向の符号付き角度を返す。
        ///
        /// 正負によって Pole の前後方向を区別する。
        /// </summary>
        private float GetSignedPoleAngle()
        {
            if (!useElbowPole ||
                elbowPoleTarget == null ||
                shoulderReference == null ||
                elbowReference == null ||
                handEndEffector == null)
            {
                return 0f;
            }

            Vector3 shoulderPosition =
                shoulderReference.position;

            Vector3 shoulderToHand =
                handEndEffector.position -
                shoulderPosition;

            if (shoulderToHand.sqrMagnitude <
                0.0000001f)
            {
                return 0f;
            }

            Vector3 shoulderToHandAxis =
                shoulderToHand.normalized;

            Vector3 shoulderToElbow =
                elbowReference.position -
                shoulderPosition;

            Vector3 shoulderToPole =
                elbowPoleTarget.position -
                shoulderPosition;

            Vector3 projectedElbowDirection =
                Vector3.ProjectOnPlane(
                    shoulderToElbow,
                    shoulderToHandAxis
                );

            Vector3 projectedPoleDirection =
                Vector3.ProjectOnPlane(
                    shoulderToPole,
                    shoulderToHandAxis
                );

            if (projectedElbowDirection.sqrMagnitude <
                    0.0000001f ||
                projectedPoleDirection.sqrMagnitude <
                    0.0000001f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(
                projectedElbowDirection,
                projectedPoleDirection,
                shoulderToHandAxis
            );
        }

        /// <summary>
        /// 肩から手先へ向かう軸に対して、
        /// 肘方向と Pole Target 方向を平面投影して比較する。
        ///
        /// 戻り値:
        /// 0.0 = Pole方向と一致
        /// 1.0 = おおむね反対方向
        /// </summary>
        private float GetPoleDirectionError()
        {
            Vector3 shoulderPosition =
                shoulderReference.position;

            Vector3 shoulderToHand =
                handEndEffector.position -
                shoulderPosition;

            if (shoulderToHand.sqrMagnitude <
                0.0000001f)
            {
                return 0f;
            }

            Vector3 shoulderToHandAxis =
                shoulderToHand.normalized;

            Vector3 shoulderToElbow =
                elbowReference.position -
                shoulderPosition;

            Vector3 shoulderToPole =
                elbowPoleTarget.position -
                shoulderPosition;

            Vector3 projectedElbowDirection =
                Vector3.ProjectOnPlane(
                    shoulderToElbow,
                    shoulderToHandAxis
                );

            Vector3 projectedPoleDirection =
                Vector3.ProjectOnPlane(
                    shoulderToPole,
                    shoulderToHandAxis
                );

            if (projectedElbowDirection.sqrMagnitude <
                    0.0000001f ||
                projectedPoleDirection.sqrMagnitude <
                    0.0000001f)
            {
                return 0f;
            }

            float angle =
                Vector3.Angle(
                    projectedElbowDirection,
                    projectedPoleDirection
                );

            return angle / 180f;
        }

        private bool IsGoalReached(
            PoseEvaluation evaluation
        )
        {
            if (evaluation.handDistance >
                positionTolerance)
            {
                return false;
            }

            if (!useElbowPole)
            {
                return true;
            }

            return evaluation.poleDirectionError <=
                   poleDirectionTolerance;
        }

        private bool ShouldIncludeTwistInSolve()
        {
            if (includeShoulderTwistInPositionSolve)
            {
                return true;
            }

            return useElbowPole &&
                   includeShoulderTwistForPoleSolve;
        }

        private bool ValidateReferences()
        {
            if (!ValidateJointReferences())
            {
                return false;
            }

            if (handTarget == null)
            {
                Debug.LogError(
                    "[RobotArmIKSolver] " +
                    "HandTarget is not assigned.",
                    this
                );

                return false;
            }

            if (handEndEffector == null)
            {
                Debug.LogError(
                    "[RobotArmIKSolver] " +
                    "HandEndEffector is not assigned.",
                    this
                );

                return false;
            }

            if (useElbowPole &&
                !ValidatePoleReferences())
            {
                return false;
            }

            return true;
        }

        private bool ValidateJointReferences()
        {
            if (shoulderTwist == null ||
                shoulderOpen == null ||
                shoulderLift == null ||
                elbow == null)
            {
                Debug.LogError(
                    "[RobotArmIKSolver] " +
                    "One or more arm joints " +
                    "are not assigned.",
                    this
                );

                return false;
            }

            return true;
        }

        private bool ValidatePoleReferences()
        {
            if (elbowPoleTarget == null)
            {
                Debug.LogError(
                    "[RobotArmIKSolver] " +
                    "ElbowPoleTarget is not assigned.",
                    this
                );

                return false;
            }

            if (shoulderReference == null)
            {
                Debug.LogError(
                    "[RobotArmIKSolver] " +
                    "ShoulderReference is not assigned.",
                    this
                );

                return false;
            }

            if (elbowReference == null)
            {
                Debug.LogError(
                    "[RobotArmIKSolver] " +
                    "ElbowReference is not assigned.",
                    this
                );

                return false;
            }

            return true;
        }

        private struct PoseAngles
        {
            public float twist;
            public float open;
            public float lift;
            public float elbow;
        }

        private struct PoseEvaluation
        {
            public float handDistance;
            public float poleDirectionError;
            public float score;
        }
    }
}

// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Body.SpatialTarget;
using SalieriAI.Core.Perception.Attention;

using UnityEngine;

namespace SalieriAI.Body.FreePose.Debugging
{
    /// <summary>
    /// Thin Play Mode-only composition and manual entry point for validating
    /// the existing FreePoseExecutor against MainScene Production services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FreePoseManualDebugDriver : MonoBehaviour
    {
        [Header("Production Services")]
        [SerializeField]
        private SpatialTargetRegistry spatialTargetRegistry;

        [SerializeField]
        private HandTargetAuthority handTargetAuthority;

        [SerializeField]
        private OrientationPriorityRequestService orientationRequestService;

        [SerializeField]
        private FaceOrientationTargetSource faceSource;

        [SerializeField]
        private ObjectOrientationTargetSource objectSource;

        [Header("Motion")]
        [SerializeField, Min(0f)]
        private float armTransitionDurationSeconds =
            FreePoseArmExecutor.DefaultTransitionDurationSeconds;

        private FreePoseArmExecutor armExecutor;
        private FreePoseHeadAdapter headAdapter;
        private FreePoseExecutor executor;
        private ProductionPoseRequestService productionPoseRequestService;

        private void Awake()
        {
            EnsureComposed(out _);
        }

        private void OnEnable()
        {
            if (!EnsureComposed(out string reason))
            {
                Debug.LogError(
                    "[FreePoseManualDebugDriver][PRODUCTION_RUNTIME_FAILED] " +
                    "Reason=" + reason,
                    this);
                return;
            }

            if (!ProductionPoseRequestRuntime.TryRegister(
                    productionPoseRequestService))
            {
                Debug.LogError(
                    "[FreePoseManualDebugDriver][PRODUCTION_RUNTIME_FAILED] " +
                    "Reason=production_pose_runtime_already_registered",
                    this);
            }
        }

        private void Update()
        {
            if (executor == null ||
                !executor.IsArmTransitioning)
            {
                return;
            }

            if (!executor.Tick(Time.unscaledDeltaTime, out string reason))
            {
                Debug.LogError(
                    "[FreePoseManualDebugDriver][TRANSITION_FAILED] " +
                    "Reason=" + reason,
                    this);
            }
        }

        private void OnDisable()
        {
            ProductionPoseRequestRuntime.Unregister(
                productionPoseRequestService);
            executor?.ReleasePose(out _);
        }

        private void OnDestroy()
        {
            ProductionPoseRequestRuntime.Unregister(
                productionPoseRequestService);
            productionPoseRequestService = null;
            executor?.Dispose();
            executor = null;
            headAdapter = null;
            armExecutor = null;
        }

        [ContextMenu("Start Debug Right Chest")]
        private void StartDebugRightChest()
        {
            StartPose(new FreePoseDefinition(
                "debug_right_chest",
                FreePoseArmDirective.ForSpatialTarget(
                    FreePoseSpatialTargetIds.RightChest),
                FreePoseArmDirective.Keep(),
                FreePoseHeadIntent.Keep));
        }

        [ContextMenu("Start Debug Left Side + Front")]
        private void StartDebugLeftSideFront()
        {
            StartPose(new FreePoseDefinition(
                "debug_left_side_front",
                FreePoseArmDirective.Keep(),
                FreePoseArmDirective.ForSpatialTarget(
                    FreePoseSpatialTargetIds.LeftSide),
                FreePoseHeadIntent.Front));
        }

        [ContextMenu("Start Debug Both Side")]
        private void StartDebugBothSide()
        {
            StartPose(new FreePoseDefinition(
                "debug_both_side",
                FreePoseArmDirective.ForSpatialTarget(
                    FreePoseSpatialTargetIds.RightSide),
                FreePoseArmDirective.ForSpatialTarget(
                    FreePoseSpatialTargetIds.LeftSide),
                FreePoseHeadIntent.Keep));
        }

        [ContextMenu("Start Debug Head Front")]
        private void StartDebugHeadFront()
        {
            StartPose(new FreePoseDefinition(
                "debug_head_front",
                FreePoseArmDirective.Keep(),
                FreePoseArmDirective.Keep(),
                FreePoseHeadIntent.Front));
        }

        [ContextMenu("Start Debug All Keep")]
        private void StartDebugAllKeep()
        {
            StartPose(new FreePoseDefinition(
                "debug_all_keep",
                FreePoseArmDirective.Keep(),
                FreePoseArmDirective.Keep(),
                FreePoseHeadIntent.Keep));
        }

        [ContextMenu("Start Debug Conversation Partner")]
        private void StartDebugConversationPartner()
        {
            StartPose(new FreePoseDefinition(
                "debug_conversation_partner",
                FreePoseArmDirective.Keep(),
                FreePoseArmDirective.Keep(),
                FreePoseHeadIntent.ConversationPartner));
        }

        [ContextMenu("Start Debug Current Object")]
        private void StartDebugCurrentObject()
        {
            StartPose(new FreePoseDefinition(
                "debug_current_object",
                FreePoseArmDirective.Keep(),
                FreePoseArmDirective.Keep(),
                FreePoseHeadIntent.CurrentObject));
        }

        [ContextMenu("Release Free Pose")]
        private void ReleaseFreePose()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[FreePoseManualDebugDriver] Play Mode is required.",
                    this);
                return;
            }

            if (executor == null)
            {
                Debug.Log(
                    "[FreePoseManualDebugDriver] No composed executor to release.",
                    this);
                return;
            }

            bool released = executor.ReleasePose(out string reason);
            Debug.Log(
                "[FreePoseManualDebugDriver][RELEASE] " +
                "Success=" + released + " Reason=" + reason,
                this);
        }

        private void StartPose(FreePoseDefinition definition)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[FreePoseManualDebugDriver] Play Mode is required.",
                    this);
                return;
            }

            if (!EnsureComposed(out string composeError))
            {
                Debug.LogError(
                    "[FreePoseManualDebugDriver][COMPOSE_FAILED] " +
                    composeError,
                    this);
                return;
            }

            bool started = executor.TryApplyPose(definition, out string reason);
            Debug.Log(
                "[FreePoseManualDebugDriver][START] " +
                "PoseId=" + definition.PoseId +
                " Success=" + started +
                " Active=" + executor.IsActive +
                " Reason=" + reason,
                this);
        }

        private bool EnsureComposed(out string reason)
        {
            if (executor != null)
            {
                reason = string.Empty;
                return true;
            }

            if (spatialTargetRegistry == null ||
                handTargetAuthority == null ||
                orientationRequestService == null ||
                faceSource == null ||
                objectSource == null)
            {
                reason = "required_production_reference_missing";
                return false;
            }

            armExecutor = new FreePoseArmExecutor(
                spatialTargetRegistry,
                handTargetAuthority,
                armTransitionDurationSeconds);
            headAdapter = new FreePoseHeadAdapter(
                orientationRequestService,
                faceSource,
                objectSource);
            executor = new FreePoseExecutor(armExecutor, headAdapter);
            productionPoseRequestService =
                new ProductionPoseRequestService(
                    executor,
                    message => Debug.Log(message, this));
            reason = string.Empty;
            return true;
        }
    }
}

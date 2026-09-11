// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Body.Frames;
using SalieriAI.Body.UpperBody.Shadow;

using UnityEngine;

namespace SalieriAI.Body.UpperBody.Actuation.Production
{
    /// <summary>
    /// Scene-independent production candidate adapter. It performs canonical
    /// input conversion and delegates all distribution math to the existing
    /// pure UpperBodyShadowSolver implementation.
    /// </summary>
    public sealed class UpperBodyChestProductionCandidateEvaluator
    {
        private readonly UpperBodyShadowSolver solver =
            new UpperBodyShadowSolver();

        private static readonly IReadOnlyDictionary<BodySegment, bool>
            ChestOnlyAvailability = new Dictionary<BodySegment, bool>
            {
                { BodySegment.Pelvis, false },
                { BodySegment.Spine, false },
                { BodySegment.Chest, true },
                { BodySegment.UpperChest, false },
                { BodySegment.Neck, false },
                { BodySegment.Head, false }
            };

        public UpperBodyShadowSolution Evaluate(
            BodyOrientationState bodyOrientation,
            Vector3 bodyOrientationWorldPosition,
            Vector3 resolvedTargetWorldPosition,
            string targetKey,
            int frame,
            DateTime evaluatedAtUtc,
            UpperBodyChestProductionActuationProfile profile)
        {
            string profileId = profile?.SolverProfile?.ProfileId ?? string.Empty;
            string profileVersion =
                profile?.SolverProfile?.Version ?? string.Empty;
            if (evaluatedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "Evaluation time must be UTC.", nameof(evaluatedAtUtc));
            if (bodyOrientation == null || !bodyOrientation.Valid ||
                profile == null)
            {
                return UpperBodyShadowSolution.CreateInvalid(
                    targetKey,
                    frame,
                    evaluatedAtUtc,
                    profileId,
                    profileVersion,
                    "Canonical body orientation or production profile unavailable.");
            }

            Vector3 worldDirection =
                resolvedTargetWorldPosition - bodyOrientationWorldPosition;
            if (!BodyRelativeTargetDirection.TryCreate(
                    bodyOrientation,
                    worldDirection,
                    out BodyRelativeTargetDirection targetDirection,
                    out string targetError))
            {
                return UpperBodyShadowSolution.CreateInvalid(
                    targetKey,
                    frame,
                    evaluatedAtUtc,
                    profileId,
                    profileVersion,
                    targetError);
            }

            return solver.Solve(
                bodyOrientation,
                targetDirection,
                targetKey,
                frame,
                evaluatedAtUtc,
                profile.SolverProfile,
                ChestOnlyAvailability);
        }
    }
}

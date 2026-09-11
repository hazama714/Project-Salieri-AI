// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Core.Limbo;
using SalieriAI.Core.RelationIntent;
using SalieriAI.Core.Spatial;

namespace SalieriAI.Core.Affordance
{
    public sealed class AffordanceResolver
    {
        public AffordanceCandidate Resolve(
            RelationalIntentRequest intent,
            SpatialContext spatial,
            BodyCapability capability,
            LimboPermission permission)
        {
            if (permission == null || permission.IsEmergencyMode)
                return AffordanceCandidate.None("Emergency mode.");

            if (intent == null || intent.IntentType == RelationalIntentType.None)
                return AffordanceCandidate.None("No relational intent.");

            if (!permission.CanStartAction)
                return AffordanceCandidate.None("Action is not permitted by Limbo.");

            if (!spatial.HasTarget)
            {
                if (intent.IntentType == RelationalIntentType.Approach && permission.CanSearch)
                    return new AffordanceCandidate(
                        AffordanceType.Search,
                        "search_front",
                        "Target is missing, search first."
                    );

                return AffordanceCandidate.None("No target.");
            }

            switch (intent.IntentType)
            {
                case RelationalIntentType.Approach:
                    return ResolveApproach(spatial, capability, permission);

                case RelationalIntentType.Withdraw:
                    return ResolveWithdraw(spatial, capability, permission);

                case RelationalIntentType.Observe:
                    return ResolveObserve(spatial, capability, permission);

                case RelationalIntentType.Hold:
                    return new AffordanceCandidate(
                        AffordanceType.Hold,
                        "hold_position",
                        "Maintain current relation."
                    );

                case RelationalIntentType.Ignore:
                    return AffordanceCandidate.None("Intent is ignore.");

                default:
                    return AffordanceCandidate.None("Unhandled intent.");
            }
        }

        private AffordanceCandidate ResolveApproach(
            SpatialContext spatial,
            BodyCapability capability,
            LimboPermission permission)
        {
            if (!permission.CanMoveServo)
                return AffordanceCandidate.None("Servo movement is not permitted.");

            if (spatial.IsReachable && capability.HasArm)
            {
                return new AffordanceCandidate(
                    AffordanceType.Reach,
                    "reach_front",
                    "Target is reachable by arm."
                );
            }

            if (spatial.Distance == TargetDistance.Near && capability.HasTorso)
            {
                return new AffordanceCandidate(
                    AffordanceType.Lean,
                    "lean_front_small",
                    "Target is near, lean forward."
                );
            }

            if (capability.CanMoveBase && spatial.Distance == TargetDistance.Far)
            {
                return new AffordanceCandidate(
                    AffordanceType.Lean,
                    "approach_forward",
                    "Target is far, move base forward."
                );
            }

            if (capability.HasNeck)
            {
                return new AffordanceCandidate(
                    AffordanceType.Look,
                    "look_front",
                    "Cannot physically approach, look toward target."
                );
            }

            return AffordanceCandidate.None("No body capability for approach.");
        }

        private AffordanceCandidate ResolveWithdraw(
            SpatialContext spatial,
            BodyCapability capability,
            LimboPermission permission)
        {
            if (!permission.CanMoveServo)
                return AffordanceCandidate.None("Servo movement is not permitted.");

            if (capability.CanMoveBase)
            {
                return new AffordanceCandidate(
                    AffordanceType.Withdraw,
                    "move_back_small",
                    "Move base away from target."
                );
            }

            if (capability.HasTorso)
            {
                return new AffordanceCandidate(
                    AffordanceType.Withdraw,
                    "lean_back_small",
                    "Lean away from target."
                );
            }

            if (capability.HasNeck)
            {
                return new AffordanceCandidate(
                    AffordanceType.Look,
                    "look_away_small",
                    "Cannot withdraw body, avert gaze."
                );
            }

            return AffordanceCandidate.None("No body capability for withdraw.");
        }

        private AffordanceCandidate ResolveObserve(
            SpatialContext spatial,
            BodyCapability capability,
            LimboPermission permission)
        {
            if (!permission.CanTrackFace && !permission.CanMoveServo)
                return AffordanceCandidate.None("Observation movement is not permitted.");

            if (capability.HasNeck)
            {
                return new AffordanceCandidate(
                    AffordanceType.Look,
                    "look_at_target",
                    "Observe target with neck."
                );
            }

            return new AffordanceCandidate(
                AffordanceType.Speak,
                "speak_short",
                "No neck capability, respond by speech."
            );
        }
    }
}
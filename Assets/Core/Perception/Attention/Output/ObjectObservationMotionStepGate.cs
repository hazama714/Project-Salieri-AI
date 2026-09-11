// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Core.Perception.Attention
{
    public enum ObjectObservationMotionStepDecision
    {
        InvalidObservation = 0,
        FreshStepAccepted = 1,
        AwaitFreshObservationHold = 2
    }

    /// <summary>
    /// Stateful, Object-only perception-to-motion admission contract.
    /// One (TargetKey, SourceFrameId) token can authorize exactly one bounded
    /// body-relative direction correction. Re-evaluating the token only
    /// returns the previously accepted intermediate direction.
    /// </summary>
    public sealed class ObjectObservationMotionStepGate
    {
        private const float MinimumDirectionSqrMagnitude = 0.000001f;

        public bool HasAcceptedStep { get; private set; }
        public string LastTargetKey { get; private set; } = string.Empty;
        public long LastSourceFrameId { get; private set; }
        public Vector3 AcceptedBodyDirection { get; private set; } =
            Vector3.forward;
        public int FreshStepCount { get; private set; }

        public ObjectObservationMotionStepDecision Evaluate(
            string targetKey,
            long sourceFrameId,
            Vector3 requestedBodyDirection,
            Vector3 initialBodyDirection,
            float maximumStepDegrees,
            out Vector3 acceptedBodyDirection,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(targetKey) ||
                sourceFrameId <= 0 ||
                !IsUsableDirection(requestedBodyDirection) ||
                !IsUsableDirection(initialBodyDirection) ||
                !IsFinite(maximumStepDegrees) ||
                maximumStepDegrees <= 0f)
            {
                acceptedBodyDirection = HasAcceptedStep
                    ? AcceptedBodyDirection
                    : NormalizeOrForward(initialBodyDirection);
                reason = "Object observation provenance or direction is invalid.";
                return ObjectObservationMotionStepDecision.InvalidObservation;
            }

            if (HasAcceptedStep &&
                LastSourceFrameId == sourceFrameId &&
                string.Equals(
                    LastTargetKey,
                    targetKey,
                    StringComparison.Ordinal))
            {
                acceptedBodyDirection = AcceptedBodyDirection;
                reason = "Awaiting a fresh Object observation.";
                return ObjectObservationMotionStepDecision
                    .AwaitFreshObservationHold;
            }

            Vector3 startDirection = HasAcceptedStep
                ? AcceptedBodyDirection
                : initialBodyDirection.normalized;
            Vector3 requestedDirection = requestedBodyDirection.normalized;
            Vector3 nextDirection = Vector3.RotateTowards(
                startDirection,
                requestedDirection,
                maximumStepDegrees * Mathf.Deg2Rad,
                0f).normalized;

            HasAcceptedStep = true;
            LastTargetKey = targetKey;
            LastSourceFrameId = sourceFrameId;
            AcceptedBodyDirection = nextDirection;
            FreshStepCount++;

            acceptedBodyDirection = nextDirection;
            reason = "Fresh Object observation accepted one bounded correction.";
            return ObjectObservationMotionStepDecision.FreshStepAccepted;
        }

        public void Reset()
        {
            HasAcceptedStep = false;
            LastTargetKey = string.Empty;
            LastSourceFrameId = 0;
            AcceptedBodyDirection = Vector3.forward;
            FreshStepCount = 0;
        }

        /// <summary>
        /// Aligns the next Object correction with an intervening authoritative
        /// Face, TargetDirection, or Neutral output without forgetting the
        /// last consumed Object observation token. Therefore returning to the
        /// same stale Object frame cannot authorize another correction.
        /// </summary>
        public bool TryRebaseAcceptedDirection(Vector3 bodyDirection)
        {
            if (!HasAcceptedStep || !IsUsableDirection(bodyDirection))
                return false;

            AcceptedBodyDirection = bodyDirection.normalized;
            return true;
        }

        private static Vector3 NormalizeOrForward(Vector3 value)
        {
            return IsUsableDirection(value)
                ? value.normalized
                : Vector3.forward;
        }

        private static bool IsUsableDirection(Vector3 value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z) &&
                value.sqrMagnitude >= MinimumDirectionSqrMagnitude;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

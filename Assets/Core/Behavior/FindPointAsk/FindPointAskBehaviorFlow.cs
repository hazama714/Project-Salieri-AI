// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Behavior.FindPointAsk
{
    /// <summary>
    /// Find -> Point -> Ask v0.1 の手順だけを所有する純粋な状態機械。
    /// Targetの選択、LOOK、POINT、SPEAKの実処理は各既存境界が所有する。
    /// </summary>
    public sealed class FindPointAskBehaviorFlow
    {
        public enum BehaviorState
        {
            Idle = 0,
            WaitTarget = 10,
            TargetSelected = 20,
            Look = 30,
            Point = 40,
            Ask = 50,
            WaitAnswer = 60,
            AnswerAccepted = 61,
            Binding = 62,
            Saving = 63,
            Acknowledging = 64,
            Completed = 70,
            Failed = 80,
            Cancelled = 90
        }

        public BehaviorState State { get; private set; }

        public string TargetKey { get; private set; }

        public string FailureReason { get; private set; }

        public FindPointAskBehaviorFlow()
        {
            State = BehaviorState.Idle;
            TargetKey = string.Empty;
            FailureReason = string.Empty;
        }

        public bool Start()
        {
            if (State != BehaviorState.Idle &&
                State != BehaviorState.Completed &&
                State != BehaviorState.Failed &&
                State != BehaviorState.Cancelled)
            {
                return false;
            }

            TargetKey = string.Empty;
            FailureReason = string.Empty;
            State = BehaviorState.WaitTarget;
            return true;
        }

        public bool SelectTarget(string targetKey)
        {
            if (State != BehaviorState.WaitTarget ||
                string.IsNullOrWhiteSpace(targetKey))
            {
                return false;
            }

            TargetKey = targetKey.Trim();
            State = BehaviorState.TargetSelected;
            return true;
        }

        public bool BeginLook()
        {
            return Move(
                BehaviorState.TargetSelected,
                BehaviorState.Look);
        }

        public bool ConfirmLook()
        {
            return Move(
                BehaviorState.Look,
                BehaviorState.Point);
        }

        public bool RestartLookAfterPreQuestionRecovery()
        {
            if (State != BehaviorState.Look &&
                State != BehaviorState.Point)
            {
                return false;
            }

            State = BehaviorState.Look;
            return true;
        }

        public bool ConfirmPoint()
        {
            return Move(
                BehaviorState.Point,
                BehaviorState.Ask);
        }

        public bool ConfirmAsk()
        {
            return Move(
                BehaviorState.Ask,
                BehaviorState.WaitAnswer);
        }

        public bool AcceptAnswer()
        {
            return Move(
                BehaviorState.WaitAnswer,
                BehaviorState.AnswerAccepted);
        }

        public bool BeginBinding()
        {
            return Move(
                BehaviorState.AnswerAccepted,
                BehaviorState.Binding);
        }

        public bool BeginSaving()
        {
            return Move(
                BehaviorState.Binding,
                BehaviorState.Saving);
        }

        public bool BeginAcknowledgement()
        {
            return Move(
                BehaviorState.Saving,
                BehaviorState.Acknowledging);
        }

        public bool CompleteAcknowledgement()
        {
            return Move(
                BehaviorState.Acknowledging,
                BehaviorState.Completed);
        }

        public bool CompleteKnownWithoutQuestion()
        {
            return Move(
                BehaviorState.TargetSelected,
                BehaviorState.Completed);
        }

        public bool Fail(string reason)
        {
            if (State == BehaviorState.Completed ||
                State == BehaviorState.Cancelled)
            {
                return false;
            }

            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "find_point_ask_failed"
                : reason.Trim();
            State = BehaviorState.Failed;
            return true;
        }

        public bool Cancel()
        {
            if (State == BehaviorState.Idle ||
                State == BehaviorState.Completed ||
                State == BehaviorState.Failed ||
                State == BehaviorState.Cancelled)
            {
                return false;
            }

            State = BehaviorState.Cancelled;
            return true;
        }

        private bool Move(
            BehaviorState expected,
            BehaviorState next)
        {
            if (State != expected)
                return false;

            State = next;
            return true;
        }
    }
}

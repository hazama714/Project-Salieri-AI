// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// Phase 9-R-1 observe-only.
    ///
    /// UserSpeechClassifier が ActionIntent と判定した RemainingText を、
    /// どの ProgrammedAction 候補として読めるかを保持する。
    ///
    /// ここではまだ実行しない。
    /// </summary>
    public sealed class ProgrammedActionCandidate
    {
        public ProgrammedActionType PrimaryAction { get; private set; }
        public ProgrammedActionType SecondaryAction { get; private set; }
        public string SourceText { get; private set; }
        public string NormalizedText { get; private set; }
        public string Reason { get; private set; }
        public bool RequiresAdditionalContext { get; private set; }

        public bool HasCandidate
        {
            get { return PrimaryAction != ProgrammedActionType.Unknown; }
        }

        public ProgrammedActionCandidate(
            ProgrammedActionType primaryAction,
            ProgrammedActionType secondaryAction,
            string sourceText,
            string normalizedText,
            string reason,
            bool requiresAdditionalContext
        )
        {
            PrimaryAction = primaryAction;
            SecondaryAction = secondaryAction;
            SourceText = sourceText ?? string.Empty;
            NormalizedText = normalizedText ?? string.Empty;
            Reason = reason ?? string.Empty;
            RequiresAdditionalContext = requiresAdditionalContext;
        }

        public static ProgrammedActionCandidate Unknown(
            string sourceText,
            string normalizedText,
            string reason
        )
        {
            return new ProgrammedActionCandidate(
                ProgrammedActionType.Unknown,
                ProgrammedActionType.Unknown,
                sourceText,
                normalizedText,
                reason,
                true
            );
        }
    }
}

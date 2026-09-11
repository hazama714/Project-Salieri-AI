// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Memory.WorldModel.Contracts
{
    public enum WorldMemoryEntityType
    {
        Unknown = 0,
        Object = 1,
        Person = 2,
        Place = 3
    }

    public enum WorldMemoryEntityStatus
    {
        Unknown = 0,
        Active = 1,
        Merged = 2,
        Retired = 3,
        Deleted = 4
    }

    public enum WorldMemoryFactValueKind
    {
        Unknown = 0,
        EntityReference = 1,
        Literal = 2
    }

    public enum WorldMemoryLiteralType
    {
        Unknown = 0,
        String = 1,
        Number = 2,
        Boolean = 3,
        DateTime = 4,
        Duration = 5,
        Uri = 6
    }

    public enum WorldMemoryFactStatus
    {
        Unknown = 0,
        Proposed = 1,
        Active = 2,
        Disputed = 3,
        Superseded = 4,
        Invalidated = 5,
        Retracted = 6
    }

    public enum WorldMemoryEvidenceType
    {
        Unknown = 0,
        DirectObservation = 1,
        HumanTeaching = 2,
        Hearsay = 3,
        WebSource = 4,
        ImportedKnowledge = 5,
        Inference = 6,
        RecognitionDerived = 7,
        ExperienceDerived = 8
    }

    public enum WorldMemoryEvidenceStance
    {
        Unknown = 0,
        Supports = 1,
        Challenges = 2,
        Neutral = 3
    }

    public enum WorldMemoryEvidenceStatus
    {
        Unknown = 0,
        Active = 1,
        Superseded = 2,
        Invalidated = 3,
        Retracted = 4
    }

    public enum WorldMemorySourceType
    {
        Unknown = 0,
        Sensor = 1,
        IdentifiedHuman = 2,
        UnidentifiedSpeaker = 3,
        WebResource = 4,
        ImportedDataset = 5,
        Experience = 6,
        InferenceProcess = 7,
        SystemComponent = 8
    }

    public enum WorldMemorySourceStatus
    {
        Unknown = 0,
        Active = 1,
        Retired = 2,
        Invalidated = 3
    }

    public enum WorldMemoryObservationModality
    {
        Unknown = 0,
        Vision = 1,
        Audio = 2,
        Manual = 3,
        Other = 4
    }

    public enum WorldMemoryRecognitionType
    {
        Unknown = 0,
        VisualSignature = 1,
        VisualEmbedding = 2,
        FaceEmbedding = 3,
        Ocr = 4,
        VoiceSignature = 5,
        Marker = 6,
        ManualIdentityAssertion = 7,
        Other = 8
    }

    public enum WorldMemoryRecognitionEvidenceStatus
    {
        Unknown = 0,
        Active = 1,
        Rejected = 2,
        Superseded = 3,
        Invalidated = 4
    }

    public enum WorldMemoryResolutionStatus
    {
        Unknown = 0,
        Resolved = 1,
        Ambiguous = 2,
        Unresolved = 3,
        Rejected = 4
    }

    public enum WorldMemoryResolutionMethod
    {
        Unknown = 0,
        ManualConfirmation = 1,
        HumanTeaching = 2,
        RecognitionEvidence = 3,
        ImportedMapping = 4,
        Inference = 5
    }
}

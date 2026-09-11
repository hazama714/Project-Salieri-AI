// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Perception.ObjectTargeting;

namespace SalieriAI.Core.Skills.Perception.ObjectTargeting
{
    /// <summary>
    /// Production側のSkill bootstrapから呼び出す明示的登録処理。
    /// Core RegistryにScene探索や自動登録を持ち込まない。
    /// </summary>
    public static class ObservationTargetSkillRegistration
    {
        public static bool Register(
            SkillPayloadTypeRegistry payloadRegistry,
            SkillImplementationRegistry implementationRegistry,
            ObservationTargetSelectionService selectionService,
            out string error)
        {
            error = string.Empty;

            if (payloadRegistry == null)
            {
                error = "SkillPayloadTypeRegistry is null.";
                return false;
            }

            if (implementationRegistry == null)
            {
                error = "SkillImplementationRegistry is null.";
                return false;
            }

            if (selectionService == null)
            {
                error =
                    "ObservationTargetSelectionService is null.";
                return false;
            }

            if (!EnsurePayloadRegistered<
                    SelectObservationTargetRequest>(
                    payloadRegistry,
                    SelectObservationTargetSkill.InputTypeId,
                    out error))
            {
                return false;
            }

            if (!EnsurePayloadRegistered<
                    SelectObservationTargetResult>(
                    payloadRegistry,
                    SelectObservationTargetSkill.OutputTypeId,
                    out error))
            {
                return false;
            }

            if (implementationRegistry.TryGet(
                    SelectObservationTargetSkill.Id,
                    SelectObservationTargetSkill.CurrentVersion,
                    out ISkillHandler existingHandler))
            {
                if (existingHandler is
                    SelectObservationTargetSkill)
                {
                    return true;
                }

                error =
                    "A different Handler is already registered for " +
                    SelectObservationTargetSkill.Id + "@" +
                    SelectObservationTargetSkill.CurrentVersion + ".";

                return false;
            }

            return implementationRegistry.Register(
                new SelectObservationTargetSkill(
                    selectionService),
                out error
            );
        }

        private static bool EnsurePayloadRegistered<TPayload>(
            SkillPayloadTypeRegistry registry,
            string typeId,
            out string error)
            where TPayload : ISkillPayload
        {
            error = string.Empty;

            if (registry.IsRegistered(
                    typeId,
                    typeof(TPayload)))
            {
                return true;
            }

            if (registry.TryResolve(
                    typeId,
                    out Type existingType))
            {
                error =
                    "Payload Type ID is already mapped to another type. " +
                    "typeId=" + typeId +
                    " existing=" + existingType.FullName;

                return false;
            }

            if (registry.TryGetTypeId(
                    typeof(TPayload),
                    out string existingTypeId))
            {
                error =
                    "Payload C# type is already mapped to another ID. " +
                    "type=" + typeof(TPayload).FullName +
                    " existingTypeId=" + existingTypeId;

                return false;
            }

            return registry.Register<TPayload>(
                typeId,
                out error
            );
        }
    }
}

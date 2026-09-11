// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Skills
{
    /// <summary>
    /// Explicit one-to-one mapping between safe JSON type IDs and C# payload types.
    /// Type.GetType and assembly-qualified names are intentionally not supported.
    /// </summary>
    public sealed class SkillPayloadTypeRegistry
    {
        private readonly Dictionary<string, Type> typesById =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        private readonly Dictionary<Type, string> idsByType =
            new Dictionary<Type, string>();

        public int Count => typesById.Count;

        public bool Register<TPayload>(string typeId, out string error)
            where TPayload : ISkillPayload
        {
            return RegisterInternal(typeId, typeof(TPayload), out error);
        }

        public bool TryResolve(string typeId, out Type payloadType)
        {
            payloadType = null;

            string normalizedId = NormalizeTypeId(typeId);
            if (normalizedId.Length == 0)
                return false;

            return typesById.TryGetValue(normalizedId, out payloadType);
        }

        public bool TryGetTypeId(Type payloadType, out string typeId)
        {
            typeId = string.Empty;

            if (payloadType == null)
                return false;

            return idsByType.TryGetValue(payloadType, out typeId);
        }

        public bool IsRegistered(string typeId, Type expectedType)
        {
            return TryResolve(typeId, out Type actualType) &&
                   actualType == expectedType;
        }

        private bool RegisterInternal(
            string typeId,
            Type payloadType,
            out string error)
        {
            error = string.Empty;

            string normalizedId = NormalizeTypeId(typeId);
            if (normalizedId.Length == 0)
            {
                error = "Payload Type ID is empty.";
                return false;
            }

            if (payloadType == null ||
                !typeof(ISkillPayload).IsAssignableFrom(payloadType))
            {
                error = "Payload type must implement ISkillPayload.";
                return false;
            }

            if (typesById.ContainsKey(normalizedId))
            {
                error = "Duplicate Payload Type ID: " + normalizedId;
                return false;
            }

            if (idsByType.ContainsKey(payloadType))
            {
                error = "Payload C# type is already registered: " +
                        payloadType.FullName;
                return false;
            }

            typesById.Add(normalizedId, payloadType);
            idsByType.Add(payloadType, normalizedId);
            return true;
        }

        private static string NormalizeTypeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}

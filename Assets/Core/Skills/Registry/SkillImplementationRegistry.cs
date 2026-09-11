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
    /// Explicit allow-list of C# Skill implementations.
    /// No reflection or Scene-wide discovery is performed.
    /// </summary>
    public sealed class SkillImplementationRegistry
    {
        private readonly Dictionary<SkillKey, ISkillHandler> handlers =
            new Dictionary<SkillKey, ISkillHandler>();

        public int Count => handlers.Count;

        public bool Register(ISkillHandler handler, out string error)
        {
            error = string.Empty;

            if (handler == null)
            {
                error = "Handler is null.";
                return false;
            }

            string skillId = NormalizeRequired(handler.SkillId);
            string inputTypeId = NormalizeRequired(handler.InputPayloadTypeId);
            string outputTypeId = NormalizeRequired(handler.OutputPayloadTypeId);

            if (skillId.Length == 0)
            {
                error = "Handler SkillId is empty.";
                return false;
            }

            if (handler.Version <= 0)
            {
                error = "Handler Version must be greater than zero.";
                return false;
            }

            if (inputTypeId.Length == 0 || outputTypeId.Length == 0)
            {
                error = "Handler payload type IDs must not be empty.";
                return false;
            }

            if (handler.InputPayloadType == null ||
                !typeof(ISkillPayload).IsAssignableFrom(handler.InputPayloadType))
            {
                error = "Handler input type must implement ISkillPayload.";
                return false;
            }

            if (handler.OutputPayloadType == null ||
                !typeof(ISkillPayload).IsAssignableFrom(handler.OutputPayloadType))
            {
                error = "Handler output type must implement ISkillPayload.";
                return false;
            }

            SkillKey key = new SkillKey(skillId, handler.Version);

            if (handlers.ContainsKey(key))
            {
                error = "Duplicate Skill handler registration: " + key;
                return false;
            }

            handlers.Add(key, handler);
            return true;
        }

        public bool TryGet(
            string skillId,
            int version,
            out ISkillHandler handler)
        {
            handler = null;

            string normalizedId = NormalizeRequired(skillId);
            if (normalizedId.Length == 0 || version <= 0)
                return false;

            return handlers.TryGetValue(
                new SkillKey(normalizedId, version),
                out handler
            );
        }

        public IReadOnlyList<ISkillHandler> GetRegisteredHandlers()
        {
            return new List<ISkillHandler>(handlers.Values).AsReadOnly();
        }

        private static string NormalizeRequired(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private readonly struct SkillKey : IEquatable<SkillKey>
        {
            private readonly string skillId;
            private readonly int version;

            public SkillKey(string skillId, int version)
            {
                this.skillId = skillId;
                this.version = version;
            }

            public bool Equals(SkillKey other)
            {
                return version == other.version &&
                       string.Equals(
                           skillId,
                           other.skillId,
                           StringComparison.Ordinal
                       );
            }

            public override bool Equals(object obj)
            {
                return obj is SkillKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((skillId != null
                        ? StringComparer.Ordinal.GetHashCode(skillId)
                        : 0) * 397) ^ version;
                }
            }

            public override string ToString()
            {
                return skillId + "@" + version;
            }
        }
    }
}

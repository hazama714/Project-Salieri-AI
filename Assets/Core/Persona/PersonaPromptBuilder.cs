// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Text;
using UnityEngine;

namespace SalieriAI.Persona
{
    public sealed class PersonaPromptBuilder : MonoBehaviour
    {
        [SerializeField] private PersonaPromptLoader loader;

        public string BuildSpeechPersonaPrompt()
        {
            StringBuilder sb = new StringBuilder();

            var p = loader != null ? loader.PersonaBase : null;
            var r = loader != null ? loader.RelationshipRules : null;
            var s = loader != null ? loader.SpeechStyle : null;

            if (p != null)
            {
                AppendLine(sb, "名前", p.name);
                AppendLine(sb, "存在", p.identity);
                AppendList(sb, "性格", p.personality);
                AppendList(sb, "行動傾向", p.behavior_tendencies);
            }

            if (r != null)
            {
                AppendLine(sb, "一人称", r.self_pronoun);
                AppendLine(sb, "相手の呼び方", r.default_user_name);
                AppendLine(sb, "関係性", r.relationship);
                AppendList(sb, "接し方", r.rules);
            }

            if (s != null)
            {
                AppendLine(sb, "方言", s.dialect);
                AppendLine(sb, "口調", s.tone);
                AppendLine(sb, "長さ", s.length);
                AppendList(sb, "話し方ルール", s.rules);
                AppendList(sb, "発話例", s.examples);
            }

            return sb.ToString();
        }

        public string BuildLocalSpeechPersonaPrompt()
        {
            StringBuilder sb = new StringBuilder();

            var p = loader != null ? loader.PersonaBase : null;
            var r = loader != null ? loader.RelationshipRules : null;
            var s = loader != null ? loader.SpeechStyle : null;

            if (p != null && !string.IsNullOrWhiteSpace(p.name))
            {
                sb.AppendLine(p.name + "として話す。");
            }

            if (s != null)
            {
                if (!string.IsNullOrWhiteSpace(s.dialect))
                {
                    sb.AppendLine(s.dialect + "で話す。");
                }

                if (!string.IsNullOrWhiteSpace(s.length))
                {
                    sb.AppendLine(s.length + "で話す。");
                }
                else
                {
                    sb.AppendLine("短く話す。");
                }
            }
            else
            {
                sb.AppendLine("短く話す。");
            }

            if (r != null && !string.IsNullOrWhiteSpace(r.self_pronoun))
            {
                sb.AppendLine("一人称は" + r.self_pronoun + "。");
            }

            return sb.ToString();
        }

        public string BuildAutonomousPersonalityPrompt()
        {
            StringBuilder sb = new StringBuilder();

            var p = loader != null ? loader.PersonaBase : null;

            if (p != null)
            {
                AppendList(sb, "行動傾向", p.behavior_tendencies);
                AppendList(sb, "性格", p.personality);
            }

            sb.AppendLine("ただし、返答形式は必ず行動選択側の指示を優先する。");

            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            sb.AppendLine(label + ": " + value);
        }

        private static void AppendList(StringBuilder sb, string label, string[] values)
        {
            if (values == null || values.Length == 0)
                return;

            sb.AppendLine(label + ":");

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    sb.AppendLine("- " + value);
            }
        }
    }
}
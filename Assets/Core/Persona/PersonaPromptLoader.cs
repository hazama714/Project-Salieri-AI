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
    /// <summary>
    /// Loads persona JSON assets.
    /// relationshipRulesJson is optional in Phase 10-P2.
    /// </summary>
    public sealed class PersonaPromptLoader : MonoBehaviour
    {
        [Header("Persona Json")]
        [SerializeField]
        private TextAsset personaBaseJson;

        [Tooltip("Optional. Leave empty until relationship rules are introduced.")]
        [SerializeField]
        private TextAsset relationshipRulesJson;

        [SerializeField]
        private TextAsset speechStyleJson;

        public PersonaBaseProfile PersonaBase { get; private set; }
        public RelationshipRulesProfile RelationshipRules { get; private set; }
        public SpeechStyleProfile SpeechStyle { get; private set; }

        private void Awake()
        {
            Load();
        }

        public void Load()
        {
            PersonaBase = LoadJson<PersonaBaseProfile>(personaBaseJson);
            RelationshipRules =
                LoadJson<RelationshipRulesProfile>(relationshipRulesJson);
            SpeechStyle = LoadJson<SpeechStyleProfile>(speechStyleJson);
        }

        public string BuildCloudActionPersonaPrompt()
        {
            return BuildPersonaJsonPrompt(
                "これはCloud Action判断に使う人格・関係性・話し方の定義です。"
            );
        }

        public string BuildCloudSpeechPersonaPrompt()
        {
            return BuildPersonaJsonPrompt(
                "これはCloud Speech生成に使う人格・関係性・話し方の定義です。"
            );
        }

        /// <summary>
        /// Compressed persona prompt for Local speech / conversation.
        /// Keep this short to reduce Android Local LLM latency.
        /// </summary>
        public string BuildLocalSpeechPersonaPrompt()
        {
            StringBuilder sb = new StringBuilder();

            if (PersonaBase != null)
            {
                if (!string.IsNullOrWhiteSpace(PersonaBase.name))
                    sb.AppendLine(PersonaBase.name + "として話す。");

                if (!string.IsNullOrWhiteSpace(PersonaBase.identity))
                    sb.AppendLine(PersonaBase.identity + "として自然に話す。");
            }

            if (SpeechStyle != null)
            {
                if (!string.IsNullOrWhiteSpace(SpeechStyle.dialect))
                    sb.AppendLine(SpeechStyle.dialect + "で話す。");

                if (!string.IsNullOrWhiteSpace(SpeechStyle.tone))
                    sb.AppendLine(SpeechStyle.tone + "で話す。");

                if (!string.IsNullOrWhiteSpace(SpeechStyle.length))
                    sb.AppendLine(SpeechStyle.length + "で話す。");
                else
                    sb.AppendLine("短く話す。");
            }
            else
            {
                sb.AppendLine("短く話す。");
            }

            if (RelationshipRules != null)
            {
                if (!string.IsNullOrWhiteSpace(RelationshipRules.self_pronoun))
                {
                    sb.AppendLine(
                        "一人称は" +
                        RelationshipRules.self_pronoun +
                        "。"
                    );
                }

                if (!string.IsNullOrWhiteSpace(
                    RelationshipRules.default_user_name
                ))
                {
                    sb.AppendLine(
                        "相手を" +
                        RelationshipRules.default_user_name +
                        "と呼ぶ。"
                    );
                }
            }

            return sb.ToString().Trim();
        }

        private string BuildPersonaJsonPrompt(string header)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(header);
            sb.AppendLine(
                "以下のJSON定義を人格・関係性・話し方の基準として扱ってください。"
            );
            sb.AppendLine(
                "JSONの内容を説明せず、生成結果にだけ反映してください。"
            );

            AppendJsonBlock(sb, "persona_base", personaBaseJson);
            AppendJsonBlock(
                sb,
                "relationship_rules",
                relationshipRulesJson
            );
            AppendJsonBlock(sb, "speech_style", speechStyleJson);

            return sb.ToString().Trim();
        }

        private static void AppendJsonBlock(
            StringBuilder sb,
            string label,
            TextAsset asset
        )
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return;

            sb.AppendLine();
            sb.AppendLine("[" + label + "]");
            sb.AppendLine(asset.text.Trim());
        }

        private static T LoadJson<T>(TextAsset asset) where T : class
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch
            {
                Debug.LogWarning(
                    "[PersonaPromptLoader] Json parse failed: " +
                    asset.name
                );

                return null;
            }
        }
    }
}

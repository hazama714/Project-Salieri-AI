// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Text;
using SalieriAI.Persona;

namespace SalieriAI.CloudLLM
{
    public static class CloudSpeechPromptBuilder
    {
        public static string BuildCloudSpeechPrompt(
            global::SelfStateSummary summary,
            global::LLMActionDecision actionDecision)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("現在の状態と直前の行動判断をもとに、短い発話を1つだけ作ってください。");
            sb.AppendLine("説明文ではなく、実際にロボットがその場で口にする自然な一言にしてください。");
            sb.AppendLine();

            sb.AppendLine("状態:");
            PromptTextUtility.AppendStateLines(sb, summary);
            sb.AppendLine();

            sb.AppendLine("直前の行動判断:");
            sb.AppendLine($"action: {PromptTextUtility.SafeText(actionDecision.action)}");
            sb.AppendLine($"reason: {PromptTextUtility.SafeText(actionDecision.reason)}");

            if (actionDecision.action == "speakShort")
            {
                sb.AppendLine("短く自然に話しかけてください。");
            }

            return sb.ToString();
        }

        public static string BuildCloudSpeechInstructions(PersonaPromptLoader personaPromptLoader)
        {
            string personaPrompt = personaPromptLoader != null
                ? personaPromptLoader.BuildCloudSpeechPersonaPrompt()
                : string.Empty;

            return
                personaPrompt + "\n\n" +
                "あなたはAndroid上で動く小さなAIロボットの発話生成層です。\n" +
                "役割は、現在状態と直前の行動判断に合わせて、短い自然な発話を1つだけ作ることです。\n" +
                "行動、サーボ角度、Bluetooth命令、制御命令は出してはいけません。\n" +
                "自然な短い日本語の発話だけを返してください。\n" +
                "説明、理由、JSON、Markdown、記号付きリストは禁止です。\n" +
                "長く話さないでください。\n" +
                "同じ意味や同じ言い回しを繰り返さないでください。\n" +
                "独り言のような自然な短文でも構いません。\n" +
                "状況説明ではなく、その場で自然に口から出る言葉にしてください。\n";
        }
    }
}
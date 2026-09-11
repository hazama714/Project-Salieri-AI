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
    public static class CloudActionPromptBuilder
    {
        public static string BuildCloudActionPrompt(global::SelfStateSummary summary)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("現在のロボット状態を読み取り、次に行う行動を1つだけ選んでください。");
            sb.AppendLine("サーボ角度や通信命令は出さないでください。");
            sb.AppendLine("行動しない判断も自然な選択です。");
            sb.AppendLine();

            sb.AppendLine("使用可能な行動:");
            sb.AppendLine("- none: 何もしない");
            sb.AppendLine("- lookAround: 周囲を見る、相手を探す");
            sb.AppendLine("- returnCenter: 正面へ戻る");
            sb.AppendLine("- idleNod: 小さくうなずく");
            sb.AppendLine("- speakShort: 短く話しかける");
            sb.AppendLine();

            PromptTextUtility.AppendStateLines(sb, summary);

            return sb.ToString();
        }

        public static string BuildCloudActionInstructions(PersonaPromptLoader personaPromptLoader)
        {
            string personaPrompt = personaPromptLoader != null
                ? personaPromptLoader.BuildCloudActionPersonaPrompt()
                : string.Empty;

            return
                personaPrompt + "\n\n" +
                "あなたはAndroid上で動くAIロボットの高度判断層です。\n" +
                "役割は、現在状態を読んで、次に行う行動を1つだけ選ぶことです。\n" +
                "あなたは身体を直接制御しません。サーボ角度、ID、Bluetooth命令は出してはいけません。\n" +
                "行動は必ず次のいずれかにしてください: none, lookAround, returnCenter, idleNod, speakShort。\n" +
                "人格や雰囲気は反映してよいですが、固定台詞や分類器のような単純反応にしないでください。\n" +
                "長時間誰も見つからない場合は、短く独り言のように speakShort を選んでも構いません。\n" +
                "lookAround を繰り返した直後は none や returnCenter を選んでも自然です。\n" +
                "同じ行動を短時間に繰り返しすぎないでください。\n" +
                "必要がなければ none を選んでください。\n" +
                "返答は必ず次のJSONだけにしてください。\n" +
                "{\"action\":\"none\",\"reason\":\"短い理由\",\"confidence\":0.0}\n" +
                "発話文は出力しないでください。発話内容は別のSpeech生成層が作成します。\n" +
                "JSON以外の文章、説明、Markdown、コードブロックは禁止です。";
        }
    }
}
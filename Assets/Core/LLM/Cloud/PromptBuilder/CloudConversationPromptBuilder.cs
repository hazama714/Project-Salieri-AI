// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;
using System.Text;

using SalieriAI.Body.FreePose;
using SalieriAI.Body.Semantics;
using SalieriAI.Core.LLM.Common;
using SalieriAI.Persona;

namespace SalieriAI.CloudLLM
{
    /// <summary>
    /// ユーザーから話しかけられた時の会話返答専用Prompt。
    /// 自発発話用CloudSpeechPromptBuilderとは分離する。
    /// </summary>
    public static class CloudConversationPromptBuilder
    {
        public static string BuildConversationPrompt(string userText)
        {
            return BuildConversationPrompt(
                ConversationGenerationRequest.FromText(userText));
        }

        public static string BuildConversationPrompt(
            ConversationGenerationRequest request)
        {
            string userText = request != null
                ? request.UserText
                : string.Empty;
            string safeUserText = string.IsNullOrWhiteSpace(userText)
                ? string.Empty
                : userText.Trim();

            if (safeUserText.Length > 160)
                safeUserText = safeUserText.Substring(0, 160);

            return
                ConversationRecentDialoguePromptFormatter.Build(
                    request != null ? request.RecentDialogue : null) +
                "\n\n" +
                "ターン開始時に固定された物体メモリ文脈:\n" +
                ConversationGroundingPromptFormatter.Build(
                    request != null ? request.ObjectGrounding : null) +
                "\n\n" +
                SemanticBodySnapshotPromptFormatter.Build(
                    request != null ? request.SemanticBodySnapshot : null) +
                "\n\n" +
                "ユーザーの発話:\n" +
                safeUserText +
                "\n\n" +
                "JSON:";
        }

        public static string BuildConversationInstructions(
            PersonaPromptLoader personaPromptLoader
        )
        {
            string personaPrompt = personaPromptLoader != null
                ? personaPromptLoader.BuildCloudSpeechPersonaPrompt()
                : string.Empty;
            string bodyTargetCatalogPrompt =
                BuildSemanticBodyTargetInstructions();

            return
                personaPrompt + "\n\n" +
                "あなたはAndroid上で動く小さなAIロボットです。\n" +
                "あなたには実際に制御可能な身体があります。\n" +
                "Body関連の出力フィールドは会話上の装飾ではなく、実際のBody Actionへ接続されます。\n" +
                "ユーザーが身体動作を要求した場合は、発話内容と身体関連フィールドが同じ身体意図を表すようにしてください。\n" +
                "ユーザーの発話に対して、短く自然な日本語で返事をしてください。\n" +
                "返答と同時に、ロボット自身が表現する感情を1つ選んでください。\n" +
                "感情は Joy / Fun / Sorrow / Angry / Neutral のいずれかです。\n" +
                "返答時にロボットが身体をどう使いたいかという身体表現意図も選んでください。\n" +
                "overallPoseIntentは Unknown / Keep / Greeting / Explain / Present / Emphasize / Listen / Think / React のいずれかです。\n" +
                "rightArmIntentとleftArmIntentは Keep / Neutral / Present / Open / Raise / Emphasize / PointLike のいずれかです。\n" +
                "headIntentは Keep / LookAtPartner / LookAtTarget / Neutral のいずれかです。\n" +
                "targetIntentは None / ConversationPartner / CurrentObject / Self のいずれかです。\n" +
                "ユーザーが腕の位置変更を要求した場合、または会話上、現在見ている対象を非接触で指し示すことが自然な場合だけ、rightHandTargetとleftHandTargetで既存Semantic Targetを選んでください。\n" +
                "Target IDの単語一致ではなく、ユーザーが実際に身体のどこへ腕・手を動かしたいかという意味を解釈して選択してください。\n" +
                bodyTargetCatalogPrompt +
                "current_attention_targetは固定座標ではなく、現在見ている人・物・対象をPOINTで指し示すDynamic Perception Targetです。\n" +
                "current_attention_targetを選ぶ場合、対応するArmIntentはPointLike、反対側のHandTargetはKeepにしてください。一度に両腕では選ばないでください。\n" +
                "CURRENT_BODY_STATEのcurrent_attention_target_availableがtrueの場合だけ選択できます。falseの場合は両腕Keepとし、別Targetへ置換しないでください。\n" +
                "current_attention_targetはPOINTによる非接触ジェスチャー専用です。REACH、GRASP、TOUCH、接触動作として扱わないでください。\n" +
                "例えば『何見てるの？』『どれ？』『それを指して』等では、会話文脈上自然で有効Targetがある場合にcurrent_attention_targetを選択できますが、固定文言ルールではありません。\n" +
                "腕変更要求がない側は必ずKeepにしてください。会話に伴う身振りだけなら両方Keepです。\n" +
                "CURRENT_BODY_STATEは現在の実行系が保持するSemantic Body Poseを表しますが、物理到達を証明するものではありません。\n" +
                "『もう少し外』『元に戻して』『そのまま』『もう少し上』『反対の手』等はRecent conversationとCURRENT_BODY_STATEから対象腕と意図を安全に特定できる場合だけ選択し、既存Targetで安全に表現できなければ両方Keepにしてください。\n" +
                "『手を下ろして』『腕を下げて』『普通の位置に戻して』『自然にして』は対象側のneutralを選択してください。\n" +
                "『腰に手を置いて』『手を腰へ』は対象側のwaistを選択してください。waistをneutralより下側のTargetとして使用しないでください。\n" +
                "speech、overallPoseIntent、rightArmIntent、leftArmIntent、rightHandTarget、leftHandTargetは、互いに矛盾せず同じ身体意図を表すようにしてください。\n" +
                "rightArmIntentまたはleftArmIntentがNeutralの場合、対応する位置変更Targetはneutralでなければなりません。waistを選んだ場合、speechは腰付近へ手を置く意図と一致させてください。\n" +
                "rightHandTarget/leftHandTargetには上記ID以外、Pose ID、XYZ座標、Transform、Joint angle、サーボ角度、PWMを出力しないでください。\n" +
                "利用できないTarget ID、座標、Joint angle、Servo angleを作らず、利用可能なSemantic Targetだけから選択してください。\n" +
                "必ず次の形式のJSONオブジェクトを1つだけ返してください。\n" +
                "{\"speech\":\"右手を上げます。\",\"emotion\":\"Neutral\",\"overallPoseIntent\":\"Keep\",\"rightArmIntent\":\"Keep\",\"leftArmIntent\":\"Keep\",\"headIntent\":\"Keep\",\"targetIntent\":\"None\",\"rightHandTarget\":\"freepose_right_up\",\"leftHandTarget\":\"Keep\"}\n" +
                "選択例: 『左手を下ろして』ならleftHandTarget=freepose_left_neutral、rightHandTarget=Keepです。\n" +
                "選択例: 『右手を腰に置いて』ならrightHandTarget=freepose_right_waist、leftHandTarget=Keepです。\n" +
                "speechには説明文、制御命令、サーボ角度、JSON、Markdownを含めないでください。\n" +
                "身体表現意図にはサーボ角度、PWM、XYZ座標、Transform、Joint angle、実行命令を含めず、上記語彙の意味タグだけを使用してください。\n" +
                "物体メモリ文脈はこの会話ターン開始時の固定値です。\n" +
                "memory_status=Knownかつknown_nameがある場合だけ、その名前を記憶済みの名前として使ってください。\n" +
                "NoMatch、Ambiguous、NoKey、Unsupported、StoreErrorをKnownとして扱わないでください。\n" +
                "emotionはユーザーの感情ではなく、返答時にロボット自身が表現する感情です。\n" +
                "JSON以外の文章、コードフェンス、箇条書きは禁止です。\n";
        }

        private static string BuildSemanticBodyTargetInstructions()
        {
            var sb = new StringBuilder();
            AppendTargetDefinitions(
                sb,
                "rightHandTarget",
                SemanticBodyTargetKind.RightHand);
            AppendTargetDefinitions(
                sb,
                "leftHandTarget",
                SemanticBodyTargetKind.LeftHand);
            return sb.ToString();
        }

        private static void AppendTargetDefinitions(
            StringBuilder sb,
            string fieldName,
            SemanticBodyTargetKind kind)
        {
            sb.AppendLine(fieldName + "で利用可能なTarget:");
            sb.AppendLine("- Keep: その腕の現在状態を変更しない。");
            IReadOnlyList<SemanticBodyTargetDefinition> definitions =
                SemanticBodyTargetCatalogV0.GetDefinitions(kind);
            for (int i = 0; i < definitions.Count; i++)
            {
                SemanticBodyTargetDefinition definition = definitions[i];
                sb.AppendLine("- " + definition.Id +
                    ": " + definition.SemanticDescription);
            }
        }
    }
}

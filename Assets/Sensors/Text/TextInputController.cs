// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.Reflex.Cognitive;
using SalieriAI.Core.Runtime;
using SalieriAI.Sensors.Audio;

namespace SalieriAI.Sensors.Text
{
    /// <summary>
    /// Minimal text-input entry for Project Salieri AI.
    ///
    /// Runtime route:
    /// TextInputController
    /// -> UserSpeechRouter
    /// -> UserSpeechInputBuffer
    /// -> ExternalInputBuffer(UserSpeech)
    /// -> RuntimeProcessor
    ///
    /// The built-in OnGUI panel is intentionally simple.
    /// It is a temporary/public-sample UI for runtime verification and can be
    /// replaced later without changing the runtime input route.
    /// </summary>
    public class TextInputController : MonoBehaviour
    {
        private const string InputControlName = "TextInputController.InputField";

        [Header("Runtime Route")]
        [SerializeField]
        private UserSpeechRouter userSpeechRouter;

        [SerializeField]
        private RuntimeInteractionSettings interactionSettings;

        private ConversationReactionService conversationReactionService;

        [Header("Phase 10-F Direct Text Input")]
        [Tooltip("Runtime側がまだsource付きPayloadを見ていない場合の暫定互換。TextInputをTrigger済み入力として本流へ入れる。")]
        [SerializeField]
        private bool prependTriggerForCurrentStringRoute = true;

        [Header("Simple Android Text Input Panel")]
        [Tooltip("画面下部中央に、簡易的な入力欄とSENDボタンを表示する。公開版では用途に応じて差し替え可能。")]
        [SerializeField]
        private bool showTextInputPanel = true;

        [Tooltip("BodyActionExecutorDebugTester と同様に、通常は画面下部へ配置する。OFFで画面上部へ配置。")]
        [SerializeField]
        private bool useBottomLayout = true;

        [Tooltip("BodyActionExecutorDebugTester と同じ基準でUI全体を拡大・縮小する。")]
        [SerializeField]
        private float uiScale = 1.0f;

        [SerializeField]
        private string panelTitle = "TEXT INPUT";

        [SerializeField]
        private string sendButtonLabel = "SEND";

        [Tooltip("送信成功後に入力欄を空にする。")]
        [SerializeField]
        private bool clearAfterSend = true;

        [Tooltip("入力欄にフォーカスがある時、Enterキーでも送信する。")]
        [SerializeField]
        private bool submitWithEnterKey = true;

        [Tooltip("過度に長い入力を防ぐための簡易制限。")]
        [SerializeField]
        private int maxInputLength = 500;

        private string uiText = string.Empty;

        private GUIStyle titleStyle;
        private GUIStyle inputFieldStyle;
        private GUIStyle sendButtonStyle;
        private bool requestInputFocus;

        private void Awake()
        {
            ResolveReferencesIfNeeded();
        }

        private void OnGUI()
        {
            if (!showTextInputPanel)
                return;

            EnsureStyles();

            float scale = Mathf.Max(0.7f, uiScale);
            float margin = 18f * scale;
            float gap = 14f * scale;
            float titleHeight = 42f * scale;
            float inputHeight = 76f * scale;
            float sendButtonWidth = 170f * scale;

            float panelWidth = Mathf.Min(Screen.width - margin * 2f, 900f * scale);
            float panelHeight = margin + titleHeight + gap + inputHeight + margin;

            float x = (Screen.width - panelWidth) * 0.5f;
            float y = useBottomLayout
                ? Mathf.Max(margin, Screen.height - panelHeight - margin)
                : margin;

            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none);

            float innerX = x + margin;
            float innerY = y + margin;
            float innerWidth = panelWidth - margin * 2f;
            float inputWidth = Mathf.Max(1f, innerWidth - gap - sendButtonWidth);

            bool textInputEnabled = IsTextInputEnabled();
            string title = textInputEnabled
                ? panelTitle
                : panelTitle + "  [DISABLED]";

            GUI.Label(
                new Rect(innerX, innerY, innerWidth, titleHeight),
                title,
                titleStyle
            );

            innerY += titleHeight + gap;

            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = textInputEnabled;

            GUI.SetNextControlName(InputControlName);
            uiText = GUI.TextField(
                new Rect(innerX, innerY, inputWidth, inputHeight),
                uiText,
                Mathf.Max(1, maxInputLength),
                inputFieldStyle
            );

            if (GUI.Button(
                    new Rect(innerX + inputWidth + gap, innerY, sendButtonWidth, inputHeight),
                    sendButtonLabel,
                    sendButtonStyle))
            {
                SubmitCurrentUIInput();
            }

            GUI.enabled = previousGuiEnabled;

            HandleEnterKeySubmit();

            if (requestInputFocus)
            {
                GUI.FocusControl(InputControlName);
                requestInputFocus = false;
            }
        }

        /// <summary>
        /// Sends the text currently typed into the simple OnGUI panel.
        /// This method can also be called from another temporary UI button.
        /// </summary>
        public void SubmitCurrentUIInput()
        {
            if (!TrySubmitTextInput(uiText))
                return;

            if (clearAfterSend)
                uiText = string.Empty;

            requestInputFocus = true;
        }

        /// <summary>
        /// Public entry for text-input UI implementations.
        /// Keep this route when replacing the simple built-in panel later.
        /// </summary>
        public void SubmitText(string text)
        {
            TrySubmitTextInput(text);
        }

        /// <summary>
        /// Debug-only entry. This intentionally bypasses the TextInput enable flag
        /// and does not prepend the wake trigger, preserving the existing behavior.
        /// </summary>
        public void SubmitDebugText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            ResolveReferencesIfNeeded();

            if (userSpeechRouter == null)
            {
                Debug.LogWarning("[TextInputController] UserSpeechRouter not found. Debug input was not routed.");
                return;
            }

            Debug.Log(
                "[TextInputController] SubmitDebugText: " +
                text
            );

            userSpeechRouter.OnUserInputReceived(
                text.Trim(),
                UserInputSource.DebugInput
            );
        }

        private bool TrySubmitTextInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!IsTextInputEnabled())
            {
                Debug.Log("[TextInputController] TextInput disabled by RuntimeInteractionSettings.");
                return false;
            }

            ResolveReferencesIfNeeded();

            if (userSpeechRouter == null)
            {
                Debug.LogWarning("[TextInputController] UserSpeechRouter not found. Text input was not routed.");
                return false;
            }

            string routedText = BuildRoutedText(text);

            Debug.Log(
                "[TextInputController] SubmitText: " +
                text.Trim() +
                " routed=" +
                routedText
            );

            userSpeechRouter.OnUserInputReceived(
                routedText,
                UserInputSource.TextInput
            );

            return true;
        }

        private void HandleEnterKeySubmit()
        {
            if (!submitWithEnterKey)
                return;

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
                return;

            bool isEnter =
                currentEvent.keyCode == KeyCode.Return ||
                currentEvent.keyCode == KeyCode.KeypadEnter;

            if (!isEnter)
                return;

            if (GUI.GetNameOfFocusedControl() != InputControlName)
                return;

            SubmitCurrentUIInput();
            currentEvent.Use();
        }

        private bool IsTextInputEnabled()
        {
            if (interactionSettings == null)
                return true;

            return interactionSettings.EnableTextInput;
        }

        private string BuildRoutedText(string text)
        {
            string trimmed = text.Trim();

            if (!prependTriggerForCurrentStringRoute)
                return trimmed;

            string trigger = conversationReactionService != null
                ? conversationReactionService.GetFirstValidActivationAlias()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(trigger))
                return trimmed;

            if (trimmed.StartsWith(trigger, System.StringComparison.Ordinal))
                return trimmed;

            return trigger + " " + trimmed;
        }

        private void ResolveReferencesIfNeeded()
        {
            if (interactionSettings == null)
                interactionSettings = FindObjectOfType<RuntimeInteractionSettings>();

            if (conversationReactionService == null)
                conversationReactionService = FindObjectOfType<ConversationReactionService>();

            if (userSpeechRouter == null)
                userSpeechRouter = FindObjectOfType<UserSpeechRouter>();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            float scale = Mathf.Max(0.8f, uiScale);
            int titleFontSize = Mathf.RoundToInt(24f * scale);
            int inputFontSize = Mathf.RoundToInt(28f * scale);
            int buttonFontSize = Mathf.RoundToInt(22f * scale);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = titleFontSize,
                fontStyle = FontStyle.Bold
            };

            inputFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = inputFontSize,
                padding = new RectOffset(
                    Mathf.RoundToInt(16f * scale),
                    Mathf.RoundToInt(16f * scale),
                    Mathf.RoundToInt(8f * scale),
                    Mathf.RoundToInt(8f * scale)
                )
            };

            sendButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = buttonFontSize,
                fontStyle = FontStyle.Bold
            };
        }
    }
}

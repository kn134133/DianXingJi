using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DianXingJi.Data;
using DianXingJi.NPC;
using DianXingJi.Puzzle;

namespace DianXingJi.UI
{
    /// <summary>
    /// UI管理器 - 全局UI控制，管理所有游戏界面
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("主界面面板")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject gameHUDPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject loadingPanel;

        [Header("游戏HUD组件")]
        [SerializeField] private TextMeshProUGUI storyProgressText;
        [SerializeField] private Slider storyProgressSlider;
        [SerializeField] private TextMeshProUGUI interactionPromptText;
        [SerializeField] private GameObject interactionPromptPanel;
        [SerializeField] private TextMeshProUGUI toastText;
        [SerializeField] private GameObject toastPanel;

        [Header("加载界面")]
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TextMeshProUGUI loadingText;

        [Header("对话界面")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TextMeshProUGUI npcNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Transform dialogueOptionsContainer;
        [SerializeField] private GameObject dialogueOptionButtonPrefab;

        [Header("谜题界面")]
        [SerializeField] private GameObject puzzlePanel;
        [SerializeField] private TextMeshProUGUI puzzleTitleText;
        [SerializeField] private TextMeshProUGUI puzzleDescText;
        [SerializeField] private TextMeshProUGUI puzzleQuestionText;
        [SerializeField] private Transform puzzleOptionsContainer;
        [SerializeField] private GameObject puzzleOptionButtonPrefab;
        [SerializeField] private TextMeshProUGUI puzzleHintText;
        [SerializeField] private Button hintButton;

        [Header("文化知识界面")]
        [SerializeField] private GameObject cultureKnowledgePanel;
        [SerializeField] private TextMeshProUGUI cultureNameText;
        [SerializeField] private TextMeshProUGUI cultureDescText;
        [SerializeField] private TextMeshProUGUI cultureContentText;
        [SerializeField] private Image cultureImage;
        [SerializeField] private Button cultureAudioButton;

        [Header("线索通知")]
        [SerializeField] private GameObject clueNotificationPanel;
        [SerializeField] private TextMeshProUGUI clueNotificationText;

        [Header("关卡完成界面")]
        [SerializeField] private GameObject levelCompletePanel;
        [SerializeField] private TextMeshProUGUI levelCompleteText;

        private Coroutine _toastCoroutine;
        private PuzzleData _currentPuzzle;
        private Action<NpcDialogueOption> _dialogueCallback;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ==================== 加载界面 ====================

        public void ShowLoadingScreen(bool show, string message = "")
        {
            if (loadingPanel != null) loadingPanel.SetActive(show);
            if (loadingText != null) loadingText.text = message;
            if (loadingProgressBar != null) loadingProgressBar.value = 0;
        }

        public void UpdateLoadingProgress(float progress, string message = "")
        {
            if (loadingProgressBar != null) loadingProgressBar.value = progress;
            if (loadingText != null && !string.IsNullOrEmpty(message))
                loadingText.text = message;
        }

        // ==================== 剧情进度 ====================

        public void UpdateStoryProgress(int solved, int total)
        {
            if (storyProgressText != null)
                storyProgressText.text = $"探索进度 {solved}/{total}";
            if (storyProgressSlider != null)
                storyProgressSlider.value = total > 0 ? (float)solved / total : 0;
        }

        // ==================== 交互提示 ====================

        public void ShowInteractionPrompt(string prompt)
        {
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(true);
            if (interactionPromptText != null) interactionPromptText.text = prompt;
        }

        public void HideInteractionPrompt()
        {
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(false);
        }

        // ==================== Toast通知 ====================

        public void ShowToast(string message, float duration = 2f)
        {
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ShowToastCoroutine(message, duration));
        }

        private System.Collections.IEnumerator ShowToastCoroutine(string message, float duration)
        {
            if (toastPanel != null) toastPanel.SetActive(true);
            if (toastText != null) toastText.text = message;
            yield return new WaitForSeconds(duration);
            if (toastPanel != null) toastPanel.SetActive(false);
        }

        // ==================== 线索通知 ====================

        public void ShowClueNotification(ClueData clue)
        {
            if (clueNotificationPanel != null) clueNotificationPanel.SetActive(true);
            if (clueNotificationText != null)
                clueNotificationText.text = $"获得线索：{clue.Type}\n{clue.Content}";

            StartCoroutine(HideAfterDelay(clueNotificationPanel, 3f));
        }

        // ==================== 谜题可用提示 ====================

        public void ShowPuzzleAvailable(PuzzleData puzzle)
        {
            ShowToast($"新谜题可解锁：{puzzle.Title}", 3f);
        }

        // ==================== 对话界面 ====================

        public void ShowDialogue(NpcData npc, NpcDialogueNode node, Action<NpcDialogueOption> callback)
        {
            _dialogueCallback = callback;
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (npcNameText != null) npcNameText.text = npc.Name;
            UpdateDialogue(node, callback);
        }

        public void UpdateDialogue(NpcDialogueNode node, Action<NpcDialogueOption> callback)
        {
            _dialogueCallback = callback;
            if (dialogueText != null) dialogueText.text = node.Text;

            // 清空旧选项
            if (dialogueOptionsContainer != null)
            {
                foreach (Transform child in dialogueOptionsContainer)
                    Destroy(child.gameObject);
            }

            // 生成选项按钮
            if (node.Options != null && dialogueOptionsContainer != null)
            {
                foreach (var option in node.Options)
                {
                    NpcDialogueOption capturedOption = option;
                    CreateDialogueOptionButton(option.Text, () => _dialogueCallback?.Invoke(capturedOption));
                }
            }
        }

        public void HideDialogue()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
        }

        private void CreateDialogueOptionButton(string text, Action onClick)
        {
            if (dialogueOptionButtonPrefab == null || dialogueOptionsContainer == null) return;
            GameObject btn = Instantiate(dialogueOptionButtonPrefab, dialogueOptionsContainer);
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = text;
            Button button = btn.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke());
        }

        // ==================== 谜题界面 ====================

        public void ShowPuzzle(PuzzleData puzzle)
        {
            _currentPuzzle = puzzle;
            if (puzzlePanel != null) puzzlePanel.SetActive(true);
            if (puzzleTitleText != null) puzzleTitleText.text = puzzle.Title;
            if (puzzleDescText != null) puzzleDescText.text = puzzle.Description;
            if (puzzleQuestionText != null) puzzleQuestionText.text = puzzle.Question;
            if (puzzleHintText != null) puzzleHintText.text = "";

            if (puzzleOptionsContainer != null)
            {
                foreach (Transform child in puzzleOptionsContainer)
                    Destroy(child.gameObject);

                if (puzzle.Options != null)
                {
                    foreach (string option in puzzle.Options)
                    {
                        string capturedOption = option;
                        CreatePuzzleOptionButton(option, () => OnPuzzleOptionSelected(capturedOption));
                    }
                }
            }
        }

        private void OnPuzzleOptionSelected(string answer)
        {
            if (_currentPuzzle == null) return;
            PuzzleResult result = PuzzleManager.Instance?.SubmitAnswer(_currentPuzzle.Id, answer)
                ?? PuzzleResult.NotFound;

            switch (result)
            {
                case PuzzleResult.Correct:
                    ShowToast("答对了！文化知识已解锁！", 3f);
                    if (puzzlePanel != null) puzzlePanel.SetActive(false);
                    break;
                case PuzzleResult.Wrong:
                    ShowToast("答案不对，再仔细想想...", 2f);
                    break;
                case PuzzleResult.AlreadySolved:
                    if (puzzlePanel != null) puzzlePanel.SetActive(false);
                    break;
            }
        }

        private void CreatePuzzleOptionButton(string text, Action onClick)
        {
            if (puzzleOptionButtonPrefab == null || puzzleOptionsContainer == null) return;
            GameObject btn = Instantiate(puzzleOptionButtonPrefab, puzzleOptionsContainer);
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = text;
            Button button = btn.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke());
        }

        public void OnHintButtonClicked()
        {
            if (_currentPuzzle == null) return;
            HintResult hint = PuzzleManager.Instance?.GetHint(_currentPuzzle.Id);
            if (hint == null) return;

            if (hint.Success)
            {
                if (puzzleHintText != null) puzzleHintText.text = $"提示：{hint.HintText}";
            }
            else
            {
                ShowToast(hint.Message);
            }
        }

        // ==================== 文化知识界面 ====================

        public void ShowCultureKnowledge(CultureResource resource)
        {
            if (cultureKnowledgePanel != null) cultureKnowledgePanel.SetActive(true);
            if (cultureNameText != null) cultureNameText.text = resource.Name;
            if (cultureDescText != null) cultureDescText.text = resource.Description;
            if (cultureContentText != null) cultureContentText.text = resource.Content;

            // 加载图片（需要异步加载资源）
            if (!string.IsNullOrEmpty(resource.ImageUrl))
                StartCoroutine(LoadCultureImage(resource.ImageUrl));
        }

        public void HideCultureKnowledge()
        {
            if (cultureKnowledgePanel != null) cultureKnowledgePanel.SetActive(false);
        }

        private System.Collections.IEnumerator LoadCultureImage(string imageUrl)
        {
            // 从Resources目录加载
            Sprite sprite = Resources.Load<Sprite>(imageUrl.Replace(".jpg", "").Replace(".png", ""));
            if (sprite != null && cultureImage != null)
                cultureImage.sprite = sprite;
            yield return null;
        }

        // ==================== 关卡完成 ====================

        public void ShowLevelComplete()
        {
            if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
            var pm = PuzzleManager.Instance;
            string msg = pm != null
                ? $"探索完成！\n解开了 {pm.GetSolvedPuzzleCount()}/{pm.GetTotalPuzzleCount()} 个谜题\n" +
                  $"解锁了 {Core.CultureKnowledgeManager.Instance?.GetUnlockedCount() ?? 0} 项文化知识"
                : "关卡完成！";
            if (levelCompleteText != null) levelCompleteText.text = msg;
        }

        // ==================== 工具方法 ====================

        private System.Collections.IEnumerator HideAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DianXingJi.Data;
using DianXingJi.NPC;
using DianXingJi.Puzzle;

namespace DianXingJi.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject gameHUDPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject loadingPanel;

        [SerializeField] private Text storyProgressText;
        [SerializeField] private Slider storyProgressSlider;
        [SerializeField] private Text interactionPromptText;
        [SerializeField] private GameObject interactionPromptPanel;
        [SerializeField] private Text toastText;
        [SerializeField] private GameObject toastPanel;

        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private Text loadingText;

        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private Text npcNameText;
        [SerializeField] private Text dialogueText;
        [SerializeField] private Transform dialogueOptionsContainer;
        [SerializeField] private GameObject dialogueOptionButtonPrefab;

        [SerializeField] private GameObject puzzlePanel;
        [SerializeField] private Text puzzleTitleText;
        [SerializeField] private Text puzzleDescText;
        [SerializeField] private Text puzzleQuestionText;
        [SerializeField] private Transform puzzleOptionsContainer;
        [SerializeField] private GameObject puzzleOptionButtonPrefab;
        [SerializeField] private Text puzzleHintText;
        [SerializeField] private Button hintButton;

        [SerializeField] private GameObject cultureKnowledgePanel;
        [SerializeField] private Text cultureNameText;
        [SerializeField] private Text cultureDescText;
        [SerializeField] private Text cultureContentText;
        [SerializeField] private Image cultureImage;
        [SerializeField] private Button cultureAudioButton;

        [SerializeField] private GameObject clueNotificationPanel;
        [SerializeField] private Text clueNotificationText;

        [SerializeField] private GameObject levelCompletePanel;
        [SerializeField] private Text levelCompleteText;

        private Coroutine _toastCoroutine;
        private PuzzleData _currentPuzzle;
        private Action<NpcDialogueOption> _dialogueCallback;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ShowLoadingScreen(bool show, string message = "")
        {
            if (loadingPanel != null) loadingPanel.SetActive(show);
            if (loadingText != null) loadingText.text = message;
            if (loadingProgressBar != null) loadingProgressBar.value = 0;
        }

        public void UpdateLoadingProgress(float progress, string message = "")
        {
            if (loadingProgressBar != null) loadingProgressBar.value = progress;
            if (loadingText != null && !string.IsNullOrEmpty(message)) loadingText.text = message;
        }

        public void UpdateStoryProgress(int solved, int total)
        {
            if (storyProgressText != null) storyProgressText.text = $"探索进度 {solved}/{total}";
            if (storyProgressSlider != null) storyProgressSlider.value = total > 0 ? (float)solved / total : 0;
        }

        public void ShowInteractionPrompt(string prompt)
        {
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(true);
            if (interactionPromptText != null) interactionPromptText.text = prompt;
        }

        public void HideInteractionPrompt()
        {
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(false);
        }

        public void ShowToast(string message, float duration = 2f)
        {
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ShowToastCoroutine(message, duration));
        }

        private IEnumerator ShowToastCoroutine(string message, float duration)
        {
            if (toastPanel != null) toastPanel.SetActive(true);
            if (toastText != null) toastText.text = message;
            yield return new WaitForSeconds(duration);
            if (toastPanel != null) toastPanel.SetActive(false);
        }

        public void ShowClueNotification(ClueData clue)
        {
            if (clueNotificationPanel != null) clueNotificationPanel.SetActive(true);
            if (clueNotificationText != null) clueNotificationText.text = $"获得线索：{clue.Type}\n{clue.Content}";
            StartCoroutine(HideAfterDelay(clueNotificationPanel, 3f));
        }

        public void ShowPuzzleAvailable(PuzzleData puzzle)
        {
            ShowToast($"新谜题可解锁：{puzzle.Title}", 3f);
        }

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
            if (dialogueOptionsContainer != null)
                foreach (Transform child in dialogueOptionsContainer) Destroy(child.gameObject);
            if (node.Options != null && dialogueOptionsContainer != null)
                foreach (var option in node.Options)
                {
                    NpcDialogueOption captured = option;
                    CreateDialogueOptionButton(option.Text, () => _dialogueCallback?.Invoke(captured));
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
            Text label = btn.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
            Button button = btn.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke());
        }

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
                foreach (Transform child in puzzleOptionsContainer) Destroy(child.gameObject);
                if (puzzle.Options != null)
                    foreach (string option in puzzle.Options)
                    {
                        string captured = option;
                        CreatePuzzleOptionButton(option, () => OnPuzzleOptionSelected(captured));
                    }
            }
        }

        private void OnPuzzleOptionSelected(string answer)
        {
            if (_currentPuzzle == null) return;
            PuzzleResult result = PuzzleManager.Instance?.SubmitAnswer(_currentPuzzle.Id, answer) ?? PuzzleResult.NotFound;
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
            Text label = btn.GetComponentInChildren<Text>();
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
            else ShowToast(hint.Message);
        }

        public void ShowCultureKnowledge(CultureResource resource)
        {
            if (cultureKnowledgePanel != null) cultureKnowledgePanel.SetActive(true);
            if (cultureNameText != null) cultureNameText.text = resource.Name;
            if (cultureDescText != null) cultureDescText.text = resource.Description;
            if (cultureContentText != null) cultureContentText.text = resource.Content;
            if (!string.IsNullOrEmpty(resource.ImageUrl))
                StartCoroutine(LoadCultureImage(resource.ImageUrl));
        }

        public void HideCultureKnowledge()
        {
            if (cultureKnowledgePanel != null) cultureKnowledgePanel.SetActive(false);
        }

        private IEnumerator LoadCultureImage(string imageUrl)
        {
            Sprite sprite = Resources.Load<Sprite>(imageUrl.Replace(".jpg", "").Replace(".png", ""));
            if (sprite != null && cultureImage != null) cultureImage.sprite = sprite;
            yield return null;
        }

        public void ShowLevelComplete()
        {
            if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
            var pm = PuzzleManager.Instance;
            string msg = pm != null
                ? $"探索完成！\n解开了 {pm.GetSolvedPuzzleCount()}/{pm.GetTotalPuzzleCount()} 个谜题"
                : "关卡完成！";
            if (levelCompleteText != null) levelCompleteText.text = msg;
        }

        private IEnumerator HideAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }
    }
}

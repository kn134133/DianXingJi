using System;
using System.Collections.Generic;
using UnityEngine;
using DianXingJi.Data;
using DianXingJi.Core;
using DianXingJi.UI;

namespace DianXingJi.Puzzle
{
    /// <summary>
    /// 谜题管理器 - 控制谜题生成、线索验证、剧情推进
    /// 实现"场景探索-线索收集-谜题破解-剧情推进-文化解锁"的核心玩法闭环
    /// </summary>
    public class PuzzleManager : MonoBehaviour
    {
        public static PuzzleManager Instance { get; private set; }

        [Header("谜题配置")]
        [SerializeField] private int maxHintCount = 3;
        [SerializeField] private float hintCooldown = 30f;

        private List<PuzzleData> _currentPuzzles = new List<PuzzleData>();
        private List<ClueData> _collectedClues = new List<ClueData>();
        private int _storyProgress;
        private float _hintCooldownTimer;

        public event Action<PuzzleData> OnPuzzleActivated;
        public event Action<PuzzleData> OnPuzzleSolved;
        public event Action<ClueData> OnClueCollected;
        public event Action<int> OnStoryProgressAdvanced;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (_hintCooldownTimer > 0)
                _hintCooldownTimer -= Time.deltaTime;
        }

        // ==================== 初始化 ====================

        public void InitializePuzzles(LevelData levelData)
        {
            _currentPuzzles = levelData.Puzzles ?? new List<PuzzleData>();
            _collectedClues.Clear();
            _storyProgress = 0;

            // 恢复已有进度
            var savedState = GameManager.Instance?.CurrentProgress?.CurrentLevelState;
            if (savedState != null)
            {
                foreach (int id in savedState.SolvedPuzzleIds)
                {
                    var puzzle = _currentPuzzles.Find(p => p.Id == id);
                    if (puzzle != null) puzzle.IsSolved = true;
                }
            }

            Debug.Log($"[PuzzleManager] 初始化 {_currentPuzzles.Count} 个谜题");
        }

        // ==================== 线索收集 ====================

        public void CollectClue(ClueData clue)
        {
            if (clue.IsCollected) return;
            clue.IsCollected = true;
            _collectedClues.Add(clue);
            OnClueCollected?.Invoke(clue);

            UIManager.Instance?.ShowClueNotification(clue);

            // 检查是否有足够线索解锁谜题
            CheckPuzzleUnlock(clue.RelatedPuzzleId);

            Debug.Log($"[PuzzleManager] 收集线索: {clue.Id} - {clue.Type}");
        }

        private void CheckPuzzleUnlock(int puzzleId)
        {
            PuzzleData puzzle = _currentPuzzles.Find(p => p.Id == puzzleId);
            if (puzzle == null || puzzle.IsSolved) return;

            int relatedClues = _collectedClues.FindAll(c => c.RelatedPuzzleId == puzzleId).Count;
            if (relatedClues >= 1)
            {
                OnPuzzleActivated?.Invoke(puzzle);
                UIManager.Instance?.ShowPuzzleAvailable(puzzle);
            }
        }

        // ==================== 谜题验证 ====================

        public PuzzleResult SubmitAnswer(int puzzleId, string answer)
        {
            PuzzleData puzzle = _currentPuzzles.Find(p => p.Id == puzzleId);
            if (puzzle == null) return PuzzleResult.NotFound;
            if (puzzle.IsSolved) return PuzzleResult.AlreadySolved;

            bool isCorrect = string.Equals(
                answer.Trim(), puzzle.CorrectAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                puzzle.IsSolved = true;
                OnPuzzleSolved?.Invoke(puzzle);
                HandlePuzzleSolved(puzzle);
                return PuzzleResult.Correct;
            }

            return PuzzleResult.Wrong;
        }

        private void HandlePuzzleSolved(PuzzleData puzzle)
        {
            // 解锁关联文化内容
            if (puzzle.CultureResourceId > 0)
                GameManager.Instance?.UnlockCulturalContent(puzzle.CultureResourceId);

            // 推进剧情
            AdvanceStory();

            // 检查是否全部谜题完成
            int solvedCount = _currentPuzzles.FindAll(p => p.IsSolved).Count;
            if (solvedCount >= _currentPuzzles.Count)
            {
                HandleLevelComplete();
            }

            Debug.Log($"[PuzzleManager] 谜题 {puzzle.Id} 解决！ 已解锁文化资源 {puzzle.CultureResourceId}");
        }

        private void AdvanceStory()
        {
            _storyProgress++;
            OnStoryProgressAdvanced?.Invoke(_storyProgress);
            UIManager.Instance?.UpdateStoryProgress(_storyProgress, _currentPuzzles.Count);
        }

        private void HandleLevelComplete()
        {
            Debug.Log("[PuzzleManager] 关卡全部谜题完成！");
            UIManager.Instance?.ShowLevelComplete();

            // 触发下一关生成
            int nextLevel = (GameManager.Instance?.CurrentLevel?.LevelIndex ?? 0) + 1;
            GameManager.Instance?.LoadLevel(nextLevel);
        }

        // ==================== 提示系统 ====================

        public HintResult GetHint(int puzzleId)
        {
            if (_hintCooldownTimer > 0)
                return new HintResult { Success = false, Message = $"请等待 {(int)_hintCooldownTimer} 秒后再获取提示" };

            PuzzleData puzzle = _currentPuzzles.Find(p => p.Id == puzzleId);
            if (puzzle == null) return new HintResult { Success = false, Message = "谜题不存在" };
            if (puzzle.IsSolved) return new HintResult { Success = false, Message = "此谜题已解决" };

            if (puzzle.HintCount >= puzzle.Hints.Count)
                return new HintResult { Success = false, Message = "已无更多提示" };

            string hint = puzzle.Hints[puzzle.HintCount];
            puzzle.HintCount++;
            _hintCooldownTimer = hintCooldown;

            return new HintResult { Success = true, HintText = hint };
        }

        // ==================== 状态查询 ====================

        public LevelStateData GetCurrentState()
        {
            LevelStateData state = new LevelStateData
            {
                SolvedPuzzleIds = new List<int>(),
                CollectedClueIds = new List<int>(),
                CurrentStoryProgress = _storyProgress
            };

            foreach (var p in _currentPuzzles)
                if (p.IsSolved) state.SolvedPuzzleIds.Add(p.Id);

            foreach (var c in _collectedClues)
                state.CollectedClueIds.Add(c.Id);

            return state;
        }

        public List<PuzzleData> GetActivePuzzles()
        {
            return _currentPuzzles.FindAll(p => !p.IsSolved);
        }

        public int GetTotalPuzzleCount() => _currentPuzzles.Count;
        public int GetSolvedPuzzleCount() => _currentPuzzles.FindAll(p => p.IsSolved).Count;
    }

    public enum PuzzleResult
    {
        Correct,
        Wrong,
        NotFound,
        AlreadySolved
    }

    [System.Serializable]
    public class HintResult
    {
        public bool Success;
        public string Message;
        public string HintText;
    }
}

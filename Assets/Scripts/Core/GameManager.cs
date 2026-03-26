using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DianXingJi.Network;
using DianXingJi.Data;
using DianXingJi.Level;
using DianXingJi.UI;

namespace DianXingJi.Core
{
    /// <summary>
    /// 游戏管理器 - 全局单例，控制游戏主流程、存档管理、关卡切换
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("游戏状态")]
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        [Header("当前玩家信息")]
        public PlayerData CurrentPlayer { get; private set; }

        [Header("当前关卡信息")]
        public LevelData CurrentLevel { get; private set; }

        [Header("游戏进度")]
        public GameProgress CurrentProgress { get; private set; }

        [Header("配置")]
        [SerializeField] private float autoSaveInterval = 60f;
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameScene = "GameScene";

        private LevelGenerator _levelGenerator;
        private float _autoSaveTimer;
        private bool _isLoading;

        public event Action<GameState> OnGameStateChanged;
        public event Action<LevelData> OnLevelLoaded;
        public event Action<GameProgress> OnProgressSaved;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }

        private void InitializeGame()
        {
            _levelGenerator = GetComponent<LevelGenerator>();
            if (_levelGenerator == null)
                _levelGenerator = gameObject.AddComponent<LevelGenerator>();

            CurrentProgress = new GameProgress();
            Debug.Log("[GameManager] 滇行记游戏初始化完成");
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing)
            {
                _autoSaveTimer += Time.deltaTime;
                if (_autoSaveTimer >= autoSaveInterval)
                {
                    _autoSaveTimer = 0f;
                    AutoSave();
                }
            }
        }

        // ==================== 游戏状态管理 ====================

        public void SetGameState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] 游戏状态切换: {newState}");
        }

        // ==================== 玩家管理 ====================

        public void SetCurrentPlayer(PlayerData player)
        {
            CurrentPlayer = player;
            Debug.Log($"[GameManager] 当前玩家: {player.Username}");
        }

        // ==================== 关卡管理 ====================

        /// <summary>
        /// 加载新关卡（触发PCG算法）
        /// </summary>
        public void LoadLevel(int levelIndex, string culturalTheme = "lijiang")
        {
            if (_isLoading) return;
            StartCoroutine(LoadLevelCoroutine(levelIndex, culturalTheme));
        }

        private IEnumerator LoadLevelCoroutine(int levelIndex, string culturalTheme)
        {
            _isLoading = true;
            SetGameState(GameState.Loading);

            UIManager.Instance?.ShowLoadingScreen(true, "正在生成关卡...");

            // 向后端请求文化约束规则
            LevelRule rule = null;
            yield return StartCoroutine(NetworkManager.Instance.GetLevelRule(culturalTheme, levelIndex,
                (r) => rule = r));

            if (rule == null)
            {
                Debug.LogWarning("[GameManager] 无法获取关卡规则，使用默认规则");
                rule = LevelRule.GetDefaultRule(culturalTheme, levelIndex);
            }

            // 执行PCG算法生成关卡
            UIManager.Instance?.UpdateLoadingProgress(0.3f, "生成文化场景...");
            LevelData levelData = null;
            yield return StartCoroutine(_levelGenerator.GenerateLevel(rule, (data) => levelData = data));

            CurrentLevel = levelData;

            UIManager.Instance?.UpdateLoadingProgress(0.7f, "加载场景资源...");

            // 加载Unity场景
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(gameScene);
            while (!sceneLoad.isDone)
            {
                float progress = Mathf.Lerp(0.7f, 1.0f, sceneLoad.progress);
                UIManager.Instance?.UpdateLoadingProgress(progress, "加载场景...");
                yield return null;
            }

            UIManager.Instance?.ShowLoadingScreen(false);
            SetGameState(GameState.Playing);
            OnLevelLoaded?.Invoke(CurrentLevel);

            _isLoading = false;
            Debug.Log($"[GameManager] 关卡 {levelIndex} ({culturalTheme}) 加载完成");
        }

        // ==================== 存档管理 ====================

        public void AutoSave()
        {
            if (CurrentPlayer == null || CurrentLevel == null) return;
            StartCoroutine(SaveProgressCoroutine(false));
        }

        public void ManualSave()
        {
            StartCoroutine(SaveProgressCoroutine(true));
        }

        private IEnumerator SaveProgressCoroutine(bool isManual)
        {
            if (CurrentPlayer == null) yield break;

            CurrentProgress.PlayerId = CurrentPlayer.Id;
            CurrentProgress.LevelId = CurrentLevel?.Id ?? 0;
            CurrentProgress.CurrentLevelState = GetCurrentLevelState();
            CurrentProgress.SaveTime = DateTime.Now;

            // 同步到后端
            bool success = false;
            yield return StartCoroutine(NetworkManager.Instance.SaveProgress(CurrentProgress, (s) => success = s));

            if (success)
            {
                OnProgressSaved?.Invoke(CurrentProgress);
                if (isManual)
                    UIManager.Instance?.ShowToast("游戏进度已保存");
                Debug.Log("[GameManager] 游戏进度保存成功");
            }
            else
            {
                // 本地备份存档
                SaveToLocal();
                Debug.LogWarning("[GameManager] 后端存档失败，已保存到本地");
            }
        }

        public void LoadProgress(GameProgress progress)
        {
            CurrentProgress = progress;
            LoadLevel(progress.LevelId);
        }

        private void SaveToLocal()
        {
            string json = JsonUtility.ToJson(CurrentProgress);
            PlayerPrefs.SetString("local_save", json);
            PlayerPrefs.Save();
        }

        public GameProgress LoadFromLocal()
        {
            string json = PlayerPrefs.GetString("local_save", "");
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<GameProgress>(json);
        }

        private LevelStateData GetCurrentLevelState()
        {
            return PuzzleManager.Instance?.GetCurrentState() ?? new LevelStateData();
        }

        // ==================== 场景切换 ====================

        public void GoToMainMenu()
        {
            SetGameState(GameState.MainMenu);
            SceneManager.LoadScene(mainMenuScene);
        }

        public void QuitGame()
        {
            ManualSave();
            Application.Quit();
        }

        // ==================== 文化内容解锁 ====================

        public void UnlockCulturalContent(int cultureResourceId)
        {
            if (CurrentProgress.UnlockedCultureIds.Contains(cultureResourceId)) return;
            CurrentProgress.UnlockedCultureIds.Add(cultureResourceId);

            StartCoroutine(NetworkManager.Instance.RecordCultureUnlock(
                CurrentPlayer.Id, cultureResourceId, (s) => { }));

            CultureKnowledgeManager.Instance?.ShowCultureDetail(cultureResourceId);
        }
    }

    public enum GameState
    {
        MainMenu,
        Loading,
        Playing,
        Paused,
        GameOver,
        Cutscene
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DianXingJi.Data
{
    // ==================== 玩家数据 ====================
    [System.Serializable]
    public class PlayerData
    {
        public int Id;
        public string Username;
        public string Email;
        public string Avatar;
        public int TotalScore;
        public int CurrentLevel;
        public string Token;
        public long CreatedAt;
    }

    // ==================== 文化资源数据 ====================
    [System.Serializable]
    public class CultureResource
    {
        public int Id;
        public string Name;
        public string Type;          // symbol/scene/behavior
        public string Description;
        public string ImageUrl;
        public string AudioUrl;
        public string Content;       // 详细文化内涵
        public string Category;      // dongba/gudian/minsu 东巴/古滇/民俗
        public string CulturalTheme; // lijiang/dali/xishuangbanna
        public bool IsUnlocked;
        public int SortOrder;
    }

    // ==================== 关卡规则数据（后端下发）====================
    [System.Serializable]
    public class LevelRule
    {
        public int Id;
        public string CulturalTheme;    // 文化主题（丽江/大理/西双版纳）
        public int LevelIndex;
        public float DifficultyFactor;  // 难度系数 0.1-1.0
        public string SceneLayoutRule;  // 场景布局规则JSON
        public string PuzzleRule;       // 谜题生成规则JSON
        public string NpcDialogueRule;  // NPC对话规则JSON
        public List<int> RequiredCultureIds; // 必须包含的文化元素ID
        public int MaxClueCount;
        public int MinPuzzleCount;
        public int MaxPuzzleCount;
        public string Status;

        public static LevelRule GetDefaultRule(string theme, int levelIndex)
        {
            return new LevelRule
            {
                Id = -1,
                CulturalTheme = theme,
                LevelIndex = levelIndex,
                DifficultyFactor = 0.3f + levelIndex * 0.1f,
                SceneLayoutRule = "{\"waterway\":true,\"buildingDensity\":0.6}",
                PuzzleRule = "{\"type\":\"dongbaSymbol\",\"clueCount\":3}",
                NpcDialogueRule = "{\"npcType\":\"naxi_elder\",\"dialogueDepth\":2}",
                RequiredCultureIds = new List<int> { 1, 2, 3 },
                MaxClueCount = 5,
                MinPuzzleCount = 2,
                MaxPuzzleCount = 4,
                Status = "active"
            };
        }
    }

    // ==================== 关卡数据（PCG生成）====================
    [System.Serializable]
    public class LevelData
    {
        public int Id;
        public string CulturalTheme;
        public int LevelIndex;
        public SceneLayoutData SceneLayout;
        public List<PuzzleData> Puzzles;
        public List<ClueData> Clues;
        public List<NpcData> Npcs;
        public List<int> CultureResourceIds; // 本关涉及的文化资源
        public LevelRule SourceRule;
    }

    // ==================== 场景布局数据 ====================
    [System.Serializable]
    public class SceneLayoutData
    {
        public int GridWidth;
        public int GridHeight;
        public List<BuildingData> Buildings;
        public List<WaterwayData> Waterways;
        public List<PathData> Paths;
        public List<CulturalDecorationData> Decorations;
        public Vector3 PlayerSpawnPoint;
        public List<Vector3> NpcSpawnPoints;
        public List<Vector3> ClueSpawnPoints;
    }

    [System.Serializable]
    public class BuildingData
    {
        public string Type;           // naxi_house/dongba_temple/market
        public Vector3 Position;
        public Vector3 Scale;
        public float Rotation;
        public string PrefabPath;
        public int CultureResourceId; // 关联的文化资源
    }

    [System.Serializable]
    public class WaterwayData
    {
        public List<Vector3> Points;
        public float Width;
        public string FlowDirection; // north/south/east/west
    }

    [System.Serializable]
    public class PathData
    {
        public List<Vector3> Points;
        public string Material; // stone/earth/wood
    }

    [System.Serializable]
    public class CulturalDecorationData
    {
        public string Type;
        public Vector3 Position;
        public int CultureResourceId;
    }

    // ==================== 谜题数据 ====================
    [System.Serializable]
    public class PuzzleData
    {
        public int Id;
        public string Type;            // symbol_match/riddle/item_combine/sequence
        public string Title;
        public string Description;
        public string Question;
        public string CorrectAnswer;
        public List<string> Options;
        public int CultureResourceId;  // 关联文化资源
        public bool IsSolved;
        public int HintCount;          // 已使用提示次数
        public List<string> Hints;
    }

    // ==================== 线索数据 ====================
    [System.Serializable]
    public class ClueData
    {
        public int Id;
        public string Type;            // text/image/audio/object
        public string Content;
        public string ResourcePath;
        public int RelatedPuzzleId;
        public int CultureResourceId;
        public Vector3 WorldPosition;
        public bool IsCollected;
    }

    // ==================== NPC数据 ====================
    [System.Serializable]
    public class NpcData
    {
        public int Id;
        public string Name;
        public string NpcType;         // naxi_elder/dongba_priest/market_vendor
        public string PrefabPath;
        public Vector3 SpawnPosition;
        public List<string> PatrolPoints;
        public NpcDialogueTree DialogueTree;
        public float InteractionRange;
        public int RelatedCultureId;
    }

    [System.Serializable]
    public class NpcDialogueTree
    {
        public List<NpcDialogueNode> Nodes;
        public int StartNodeId;
    }

    [System.Serializable]
    public class NpcDialogueNode
    {
        public int Id;
        public string Text;
        public string Condition;      // player_progress/item_collected/puzzle_solved
        public List<NpcDialogueOption> Options;
        public int CultureResourceId; // 揭示的文化内涵
    }

    [System.Serializable]
    public class NpcDialogueOption
    {
        public string Text;
        public int NextNodeId;
        public string Action;         // give_clue/unlock_culture/reveal_puzzle
    }

    // ==================== 游戏进度数据 ====================
    [System.Serializable]
    public class GameProgress
    {
        public int Id;
        public int PlayerId;
        public int LevelId;
        public LevelStateData CurrentLevelState;
        public List<int> UnlockedCultureIds = new List<int>();
        public System.DateTime SaveTime;
        public int TotalScore;
        public float TotalPlayTime;
    }

    [System.Serializable]
    public class LevelStateData
    {
        public List<int> SolvedPuzzleIds = new List<int>();
        public List<int> CollectedClueIds = new List<int>();
        public Vector3 PlayerPosition;
        public int CurrentStoryProgress;
    }
}

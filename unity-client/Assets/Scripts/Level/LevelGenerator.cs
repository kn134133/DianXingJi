using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DianXingJi.Data;
using DianXingJi.Network;

namespace DianXingJi.Level
{
    /// <summary>
    /// 关卡生成器 - 文化语义约束的程序化关卡生成算法（PCG）核心实现
    /// 算法流程：
    ///   1. 从后端获取文化约束规则
    ///   2. 基于分形算法生成符合文化场景布局的地形与建筑
    ///   3. 基于文化符号语义生成解谜线索与谜题逻辑
    ///   4. 发送后端进行文化逻辑校验
    ///   5. 校验通过后输出完整关卡数据
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [Header("场景生成参数")]
        [SerializeField] private int gridWidth = 20;
        [SerializeField] private int gridHeight = 20;
        [SerializeField] private float cellSize = 5f;
        [SerializeField] private int maxGenerationRetries = 3;

        [Header("建筑预制体路径")]
        [SerializeField] private string prefabBasePath = "Prefabs/Buildings/";

        // 建筑类型权重表（体现云南文化特色）
        private static readonly Dictionary<string, BuildingWeight[]> ThemeBuildingWeights =
            new Dictionary<string, BuildingWeight[]>
        {
            ["lijiang"] = new[]
            {
                new BuildingWeight("naxi_house", 0.45f, "naxi_house_01"),
                new BuildingWeight("dongba_temple", 0.15f, "dongba_temple_01"),
                new BuildingWeight("market", 0.20f, "lijiang_market"),
                new BuildingWeight("watermill", 0.10f, "watermill_01"),
                new BuildingWeight("bridge", 0.10f, "stone_bridge"),
            },
            ["dali"] = new[]
            {
                new BuildingWeight("bai_house", 0.50f, "bai_house_01"),
                new BuildingWeight("chongsheng_pagoda", 0.10f, "pagoda_01"),
                new BuildingWeight("market", 0.20f, "dali_market"),
                new BuildingWeight("gallery", 0.20f, "art_gallery"),
            },
            ["xishuangbanna"] = new[]
            {
                new BuildingWeight("dai_bamboo_house", 0.60f, "dai_house_01"),
                new BuildingWeight("buddha_temple", 0.15f, "temple_01"),
                new BuildingWeight("elephant_pavilion", 0.10f, "pavilion"),
                new BuildingWeight("market", 0.15f, "tropical_market"),
            }
        };

        // 文化谜题类型映射
        private static readonly Dictionary<string, string[]> ThemePuzzleTypes =
            new Dictionary<string, string[]>
        {
            ["lijiang"] = new[] { "dongba_symbol_match", "naxi_music_riddle", "waterway_navigation", "ancient_script_decode" },
            ["dali"] = new[] { "bai_pattern_match", "tie_dye_sequence", "legend_riddle", "pagoda_symbol" },
            ["xishuangbanna"] = new[] { "dai_calendar_puzzle", "tropical_plant_riddle", "water_festival_sequence", "ethnic_costume_match" }
        };

        public IEnumerator GenerateLevel(LevelRule rule, Action<LevelData> callback)
        {
            LevelData levelData = null;
            int retries = 0;

            while (retries < maxGenerationRetries && levelData == null)
            {
                levelData = GenerateLevelInternal(rule);

                // 发送后端进行文化逻辑校验
                bool isValid = false;
                yield return StartCoroutine(
                    NetworkManager.Instance.ValidateLevelCulture(levelData, (v) => isValid = v));

                if (!isValid)
                {
                    Debug.LogWarning($"[LevelGenerator] 关卡文化逻辑校验失败，第{retries + 1}次重新生成");
                    levelData = null;
                    retries++;
                }
            }

            if (levelData == null)
            {
                // 降级：使用本地校验通过的基础关卡
                levelData = GenerateFallbackLevel(rule);
                Debug.LogWarning("[LevelGenerator] 使用降级关卡方案");
            }

            callback?.Invoke(levelData);
        }

        // ==================== 核心PCG算法 ====================

        private LevelData GenerateLevelInternal(LevelRule rule)
        {
            LevelData data = new LevelData
            {
                CulturalTheme = rule.CulturalTheme,
                LevelIndex = rule.LevelIndex,
                SourceRule = rule,
                Puzzles = new List<PuzzleData>(),
                Clues = new List<ClueData>(),
                Npcs = new List<NpcData>(),
                CultureResourceIds = new List<int>()
            };

            // Step 1: 生成场景布局
            data.SceneLayout = GenerateSceneLayout(rule);

            // Step 2: 基于文化语义生成谜题
            data.Puzzles = GeneratePuzzles(rule, data.SceneLayout);

            // Step 3: 生成场景线索
            data.Clues = GenerateClues(rule, data.Puzzles, data.SceneLayout);

            // Step 4: 生成NPC配置
            data.Npcs = GenerateNpcs(rule, data.SceneLayout);

            // Step 5: 收集涉及的文化资源ID
            data.CultureResourceIds.AddRange(rule.RequiredCultureIds ?? new List<int>());

            return data;
        }

        // ==================== 场景布局生成（分形约束算法）====================

        private SceneLayoutData GenerateSceneLayout(LevelRule rule)
        {
            SceneLayoutData layout = new SceneLayoutData
            {
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                Buildings = new List<BuildingData>(),
                Waterways = new List<WaterwayData>(),
                Paths = new List<PathData>(),
                Decorations = new List<CulturalDecorationData>()
            };

            // 解析文化约束规则
            SceneLayoutRule sceneRule = ParseSceneRule(rule.SceneLayoutRule);

            // 丽江水系约束：遵循"三山夹两城，一水绕全城"布局
            if (sceneRule.HasWaterway)
                GenerateWaterwayLayout(layout, rule.CulturalTheme);

            // 建筑布局：基于文化主题权重随机排布
            GenerateBuildingLayout(layout, rule.CulturalTheme, rule.DifficultyFactor, sceneRule);

            // 路径连通性生成（确保玩家可到达所有关键点）
            GeneratePathLayout(layout);

            // 玩家出生点（场景入口）
            layout.PlayerSpawnPoint = new Vector3(gridWidth * cellSize * 0.1f, 0, gridHeight * cellSize * 0.5f);

            // NPC站位
            GenerateNpcSpawnPoints(layout);

            // 线索隐藏点（符合文化习惯，如藏在神龛、水边、古树旁）
            GenerateClueSpawnPoints(layout, rule.CulturalTheme);

            return layout;
        }

        private void GenerateWaterwayLayout(SceneLayoutData layout, string theme)
        {
            // 丽江古城：水系从西北到东南流经城中，形成"Y"型水网
            if (theme == "lijiang")
            {
                // 主水道
                layout.Waterways.Add(new WaterwayData
                {
                    Points = GenerateMeanderingPath(
                        new Vector3(0, 0, gridHeight * cellSize * 0.5f),
                        new Vector3(gridWidth * cellSize, 0, gridHeight * cellSize * 0.3f),
                        5),
                    Width = 3f,
                    FlowDirection = "east"
                });
                // 支流
                layout.Waterways.Add(new WaterwayData
                {
                    Points = GenerateMeanderingPath(
                        new Vector3(gridWidth * cellSize * 0.3f, 0, gridHeight * cellSize),
                        new Vector3(gridWidth * cellSize * 0.5f, 0, gridHeight * cellSize * 0.5f),
                        3),
                    Width = 1.5f,
                    FlowDirection = "south"
                });
            }
        }

        private void GenerateBuildingLayout(SceneLayoutData layout, string theme,
            float difficulty, SceneLayoutRule sceneRule)
        {
            if (!ThemeBuildingWeights.TryGetValue(theme, out BuildingWeight[] weights))
                weights = ThemeBuildingWeights["lijiang"];

            int buildingCount = Mathf.RoundToInt(
                Mathf.Lerp(8, 20, sceneRule.BuildingDensity));

            // 使用泊松圆盘采样确保建筑间距合理
            List<Vector2> positions = PoissonDiskSampling(
                gridWidth * cellSize, gridHeight * cellSize,
                cellSize * 1.5f, buildingCount);

            foreach (Vector2 pos2d in positions)
            {
                // 确保建筑不在水道上
                Vector3 pos3d = new Vector3(pos2d.x, 0, pos2d.y);
                if (IsOnWaterway(layout.Waterways, pos3d)) continue;

                string buildingType = WeightedRandom(weights);
                BuildingWeight bw = System.Array.Find(weights, w => w.Type == buildingType);

                layout.Buildings.Add(new BuildingData
                {
                    Type = buildingType,
                    Position = pos3d,
                    Scale = Vector3.one * UnityEngine.Random.Range(0.9f, 1.1f),
                    Rotation = UnityEngine.Random.Range(0, 4) * 90f,
                    PrefabPath = prefabBasePath + bw.PrefabName
                });
            }
        }

        private void GeneratePathLayout(SceneLayoutData layout)
        {
            // 连接所有建筑到最近道路（最小生成树算法）
            List<Vector3> keyPoints = new List<Vector3> { layout.PlayerSpawnPoint };
            foreach (var b in layout.Buildings)
                keyPoints.Add(b.Position);

            // 简化：生成主干道 + 支路
            layout.Paths.Add(new PathData
            {
                Points = new List<Vector3>
                {
                    layout.PlayerSpawnPoint,
                    new Vector3(gridWidth * cellSize * 0.5f, 0, gridHeight * cellSize * 0.5f),
                    new Vector3(gridWidth * cellSize * 0.9f, 0, gridHeight * cellSize * 0.5f)
                },
                Material = "stone"
            });
        }

        private void GenerateNpcSpawnPoints(SceneLayoutData layout)
        {
            layout.NpcSpawnPoints = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 point = new Vector3(
                    UnityEngine.Random.Range(cellSize * 2, (gridWidth - 2) * cellSize),
                    0,
                    UnityEngine.Random.Range(cellSize * 2, (gridHeight - 2) * cellSize));
                layout.NpcSpawnPoints.Add(point);
            }
        }

        private void GenerateClueSpawnPoints(SceneLayoutData layout, string theme)
        {
            layout.ClueSpawnPoints = new List<Vector3>();
            // 线索藏在建筑附近（文化语义约束：神龛旁、水边、古树下）
            foreach (var building in layout.Buildings)
            {
                if (building.Type == "dongba_temple" || building.Type == "naxi_house")
                {
                    Vector3 cluePos = building.Position + new Vector3(
                        UnityEngine.Random.Range(-2f, 2f), 0, UnityEngine.Random.Range(-2f, 2f));
                    layout.ClueSpawnPoints.Add(cluePos);
                }
            }
            // 水边线索
            if (layout.Waterways.Count > 0 && layout.Waterways[0].Points.Count > 1)
            {
                int mid = layout.Waterways[0].Points.Count / 2;
                layout.ClueSpawnPoints.Add(layout.Waterways[0].Points[mid]);
            }
        }

        // ==================== 谜题生成（文化语义约束）====================

        private List<PuzzleData> GeneratePuzzles(LevelRule rule, SceneLayoutData layout)
        {
            List<PuzzleData> puzzles = new List<PuzzleData>();

            string[] puzzleTypes = ThemePuzzleTypes.TryGetValue(rule.CulturalTheme, out string[] types)
                ? types : ThemePuzzleTypes["lijiang"];

            int count = Mathf.RoundToInt(Mathf.Lerp(
                rule.MinPuzzleCount, rule.MaxPuzzleCount, rule.DifficultyFactor));
            count = Mathf.Clamp(count, rule.MinPuzzleCount, rule.MaxPuzzleCount);

            for (int i = 0; i < count; i++)
            {
                string puzzleType = puzzleTypes[i % puzzleTypes.Length];
                PuzzleData puzzle = GeneratePuzzleByType(puzzleType, rule, i);
                puzzles.Add(puzzle);
            }

            return puzzles;
        }

        private PuzzleData GeneratePuzzleByType(string type, LevelRule rule, int index)
        {
            // 根据文化主题和谜题类型生成具体谜题
            return type switch
            {
                "dongba_symbol_match" => GenerateDongbaSymbolPuzzle(index),
                "naxi_music_riddle" => GenerateNaxiMusicPuzzle(index),
                "waterway_navigation" => GenerateWaterwayPuzzle(index),
                "ancient_script_decode" => GenerateAncientScriptPuzzle(index),
                "bai_pattern_match" => GenerateBaiPatternPuzzle(index),
                _ => GenerateGenericCulturePuzzle(rule.CulturalTheme, index)
            };
        }

        private PuzzleData GenerateDongbaSymbolPuzzle(int index)
        {
            // 东巴文字谜题：根据东巴象形文字语义生成匹配题
            string[] symbols = { "日", "月", "山", "水", "火", "木", "金" };
            string target = symbols[UnityEngine.Random.Range(0, symbols.Length)];

            return new PuzzleData
            {
                Id = index + 1,
                Type = "symbol_match",
                Title = "东巴文字",
                Description = "东巴文是纳西族的象形文字，被称为世界上唯一活着的象形文字。",
                Question = $"请找到代表「{target}」含义的东巴文符号",
                CorrectAnswer = $"dongba_{target.ToLower()}",
                Options = GenerateSymbolOptions(target),
                CultureResourceId = 1,
                Hints = new List<string>
                {
                    "观察建筑墙壁上的东巴文字雕刻",
                    "询问附近的东巴祭司"
                }
            };
        }

        private PuzzleData GenerateNaxiMusicPuzzle(int index)
        {
            return new PuzzleData
            {
                Id = index + 1,
                Type = "riddle",
                Title = "纳西古乐",
                Description = "纳西古乐被誉为「音乐活化石」，保存了大量唐宋元时期的音乐。",
                Question = "纳西古乐中最核心的乐器是什么？",
                CorrectAnswer = "口弦",
                Options = new List<string> { "口弦", "古筝", "琵琶", "胡琴" },
                CultureResourceId = 2,
                Hints = new List<string> { "在市集上倾听当地艺人演奏" }
            };
        }

        private PuzzleData GenerateWaterwayPuzzle(int index)
        {
            return new PuzzleData
            {
                Id = index + 1,
                Type = "sequence",
                Title = "水系寻踪",
                Description = "丽江古城的水系被誉为「高原姑苏」，三股水系从玉龙雪山流入古城。",
                Question = "按照正确的顺序连接水系节点，找到古城水源",
                CorrectAnswer = "north-center-south",
                Options = new List<string> { "north-center-south", "south-center-north", "center-north-south", "east-west-center" },
                CultureResourceId = 3,
                Hints = new List<string> { "观察水流方向", "向当地纳西老人询问水系来源" }
            };
        }

        private PuzzleData GenerateAncientScriptPuzzle(int index)
        {
            return new PuzzleData
            {
                Id = index + 1,
                Type = "riddle",
                Title = "古滇文明",
                Description = "古滇国是云南最早的国家政权，以滇池为中心建立了独特的青铜文明。",
                Question = "古滇国最具代表性的青铜器是什么？",
                CorrectAnswer = "贮贝器",
                Options = new List<string> { "贮贝器", "编钟", "青铜鼎", "铜镜" },
                CultureResourceId = 4,
                Hints = new List<string> { "查看博物馆展示的文物图片" }
            };
        }

        private PuzzleData GenerateBaiPatternPuzzle(int index)
        {
            return new PuzzleData
            {
                Id = index + 1,
                Type = "symbol_match",
                Title = "白族扎染",
                Description = "大理白族扎染技艺是国家非物质文化遗产，有着千年历史。",
                Question = "请选出正确的白族扎染传统纹样",
                CorrectAnswer = "bai_flower_pattern",
                Options = new List<string> { "bai_flower_pattern", "han_cloud_pattern", "yi_fire_pattern", "tibetan_dragon_pattern" },
                CultureResourceId = 5,
                Hints = new List<string> { "观察当地白族妇女的服装" }
            };
        }

        private PuzzleData GenerateGenericCulturePuzzle(string theme, int index)
        {
            return new PuzzleData
            {
                Id = index + 1,
                Type = "riddle",
                Title = $"{theme}文化探索",
                Description = "探索云南丰富的区域文化",
                Question = "完成文化探索任务",
                CorrectAnswer = "explore",
                CultureResourceId = index + 1,
                Hints = new List<string> { "探索周围场景" }
            };
        }

        // ==================== 线索生成 ====================

        private List<ClueData> GenerateClues(LevelRule rule, List<PuzzleData> puzzles, SceneLayoutData layout)
        {
            List<ClueData> clues = new List<ClueData>();
            int clueId = 1;

            foreach (PuzzleData puzzle in puzzles)
            {
                // 每个谜题生成1-2个线索
                int clueCount = UnityEngine.Random.Range(1, 3);
                for (int i = 0; i < clueCount && clueId <= rule.MaxClueCount; i++)
                {
                    Vector3 spawnPos = layout.ClueSpawnPoints.Count > 0
                        ? layout.ClueSpawnPoints[(clueId - 1) % layout.ClueSpawnPoints.Count]
                        : Vector3.zero;

                    clues.Add(new ClueData
                    {
                        Id = clueId++,
                        Type = i == 0 ? "text" : "image",
                        Content = GenerateClueContent(puzzle, i),
                        RelatedPuzzleId = puzzle.Id,
                        CultureResourceId = puzzle.CultureResourceId,
                        WorldPosition = spawnPos + UnityEngine.Random.insideUnitSphere * 2f
                    });
                }
            }

            return clues;
        }

        private string GenerateClueContent(PuzzleData puzzle, int clueIndex)
        {
            return clueIndex == 0
                ? $"[文字线索] 关于{puzzle.Title}的提示：{puzzle.Description}"
                : $"[图像线索] culture_clue_{puzzle.CultureResourceId}_{clueIndex}";
        }

        // ==================== NPC生成 ====================

        private List<NpcData> GenerateNpcs(LevelRule rule, SceneLayoutData layout)
        {
            List<NpcData> npcs = new List<NpcData>();
            string[] npcTypes = rule.CulturalTheme == "lijiang"
                ? new[] { "naxi_elder", "dongba_priest", "market_vendor" }
                : new[] { "local_elder", "cultural_guide", "market_vendor" };

            for (int i = 0; i < Mathf.Min(npcTypes.Length, layout.NpcSpawnPoints.Count); i++)
            {
                npcs.Add(new NpcData
                {
                    Id = i + 1,
                    Name = GetNpcName(npcTypes[i], rule.CulturalTheme),
                    NpcType = npcTypes[i],
                    PrefabPath = $"Prefabs/NPCs/{npcTypes[i]}",
                    SpawnPosition = layout.NpcSpawnPoints[i],
                    InteractionRange = 2.5f,
                    RelatedCultureId = i + 1,
                    DialogueTree = GenerateDialogueTree(npcTypes[i], rule)
                });
            }

            return npcs;
        }

        private string GetNpcName(string npcType, string theme)
        {
            return npcType switch
            {
                "naxi_elder" => "纳西族长老 · 木和",
                "dongba_priest" => "东巴祭司 · 阿普",
                "market_vendor" => "集市商贩 · 阿丽",
                "local_elder" => "当地长者",
                "cultural_guide" => "文化向导",
                _ => "村民"
            };
        }

        private NpcDialogueTree GenerateDialogueTree(string npcType, LevelRule rule)
        {
            // 根据NPC类型和规则JSON生成对话树
            return new NpcDialogueTree
            {
                StartNodeId = 1,
                Nodes = new List<NpcDialogueNode>
                {
                    new NpcDialogueNode
                    {
                        Id = 1,
                        Text = GetNpcGreeting(npcType),
                        Options = new List<NpcDialogueOption>
                        {
                            new NpcDialogueOption { Text = "请问这里有什么特别之处？", NextNodeId = 2, Action = "none" },
                            new NpcDialogueOption { Text = "我在寻找线索，您能帮我吗？", NextNodeId = 3, Action = "give_clue" },
                            new NpcDialogueOption { Text = "告别", NextNodeId = -1, Action = "end" }
                        }
                    },
                    new NpcDialogueNode
                    {
                        Id = 2,
                        Text = GetCulturalKnowledge(npcType, rule.CulturalTheme),
                        CultureResourceId = 1,
                        Options = new List<NpcDialogueOption>
                        {
                            new NpcDialogueOption { Text = "非常感谢您的讲解", NextNodeId = 1, Action = "unlock_culture" },
                            new NpcDialogueOption { Text = "我还有问题", NextNodeId = 3, Action = "none" }
                        }
                    },
                    new NpcDialogueNode
                    {
                        Id = 3,
                        Text = "你需要找到散落在古城中的线索，每个线索都与我们的文化传统有关。",
                        Options = new List<NpcDialogueOption>
                        {
                            new NpcDialogueOption { Text = "我明白了，谢谢", NextNodeId = -1, Action = "reveal_puzzle" }
                        }
                    }
                }
            };
        }

        private string GetNpcGreeting(string npcType)
        {
            return npcType switch
            {
                "naxi_elder" => "你好，年轻的旅行者。欢迎来到丽江古城，这里有着千年纳西族的文化传承。",
                "dongba_priest" => "东巴文是我们纳西族的文字，我可以为你解读这些古老的符号。",
                "market_vendor" => "来来来，看看我们的手工艺品，都是正宗的纳西族传统工艺！",
                _ => "欢迎来到我们这里，探索这片土地的文化。"
            };
        }

        private string GetCulturalKnowledge(string npcType, string theme)
        {
            if (theme == "lijiang" && npcType == "dongba_priest")
                return "东巴文是世界上唯一仍在使用的象形文字，我们东巴祭司世代相传，用它记录纳西族的历史、神话和仪式。每一个符号都有其深刻的文化含义。";
            if (theme == "lijiang")
                return "丽江古城已有800年历史，城内的水系从玉龙雪山引来清泉，滋养了一代代纳西族人。";
            return "这里有着丰富的民族文化传统，值得深入探索。";
        }

        // ==================== 辅助工具方法 ====================

        private List<Vector3> GenerateMeanderingPath(Vector3 start, Vector3 end, int segments)
        {
            List<Vector3> points = new List<Vector3> { start };
            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector3 point = Vector3.Lerp(start, end, t);
                point.x += UnityEngine.Random.Range(-cellSize, cellSize);
                point.z += UnityEngine.Random.Range(-cellSize, cellSize);
                points.Add(point);
            }
            points.Add(end);
            return points;
        }

        private bool IsOnWaterway(List<WaterwayData> waterways, Vector3 position)
        {
            foreach (var waterway in waterways)
            {
                foreach (var point in waterway.Points)
                {
                    if (Vector3.Distance(position, point) < waterway.Width * 2)
                        return true;
                }
            }
            return false;
        }

        private List<Vector2> PoissonDiskSampling(float width, float height, float minDist, int maxSamples)
        {
            List<Vector2> samples = new List<Vector2>();
            List<Vector2> active = new List<Vector2>();

            Vector2 first = new Vector2(
                UnityEngine.Random.Range(0, width),
                UnityEngine.Random.Range(0, height));
            samples.Add(first);
            active.Add(first);

            while (active.Count > 0 && samples.Count < maxSamples)
            {
                int idx = UnityEngine.Random.Range(0, active.Count);
                Vector2 point = active[idx];
                bool found = false;

                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
                    float radius = UnityEngine.Random.Range(minDist, minDist * 2);
                    Vector2 candidate = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    if (candidate.x < 0 || candidate.x > width || candidate.y < 0 || candidate.y > height)
                        continue;

                    bool valid = true;
                    foreach (var sample in samples)
                    {
                        if (Vector2.Distance(candidate, sample) < minDist)
                        { valid = false; break; }
                    }

                    if (valid)
                    {
                        samples.Add(candidate);
                        active.Add(candidate);
                        found = true;
                        break;
                    }
                }

                if (!found) active.RemoveAt(idx);
            }

            return samples;
        }

        private string WeightedRandom(BuildingWeight[] weights)
        {
            float total = 0;
            foreach (var w in weights) total += w.Weight;
            float rand = UnityEngine.Random.Range(0, total);
            float cumulative = 0;
            foreach (var w in weights)
            {
                cumulative += w.Weight;
                if (rand <= cumulative) return w.Type;
            }
            return weights[0].Type;
        }

        private List<string> GenerateSymbolOptions(string target)
        {
            string[] allSymbols = { "日", "月", "山", "水", "火", "木", "金" };
            List<string> options = new List<string> { $"dongba_{target.ToLower()}" };
            foreach (var s in allSymbols)
            {
                if (s != target && options.Count < 4)
                    options.Add($"dongba_{s.ToLower()}");
            }
            // 打乱顺序
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }
            return options;
        }

        private SceneLayoutRule ParseSceneRule(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new SceneLayoutRule { HasWaterway = true, BuildingDensity = 0.6f };
            try { return JsonUtility.FromJson<SceneLayoutRule>(json); }
            catch { return new SceneLayoutRule { HasWaterway = true, BuildingDensity = 0.6f }; }
        }

        private LevelData GenerateFallbackLevel(LevelRule rule)
        {
            LevelData data = GenerateLevelInternal(rule);
            data.Id = -1; // 标记为降级关卡
            return data;
        }
    }

    [System.Serializable]
    internal class BuildingWeight
    {
        public string Type;
        public float Weight;
        public string PrefabName;

        public BuildingWeight(string type, float weight, string prefabName)
        { Type = type; Weight = weight; PrefabName = prefabName; }
    }

    [System.Serializable]
    internal class SceneLayoutRule
    {
        public bool HasWaterway;
        public float BuildingDensity;
    }
}

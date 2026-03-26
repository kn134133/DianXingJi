using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DianXingJi.Data;
using DianXingJi.Network;

namespace DianXingJi.Core
{
    /// <summary>
    /// 文化知识管理器 - 管理已解锁的云南文化内容
    /// 功能：解锁记录、知识库展示、文化内容查询
    /// </summary>
    public class CultureKnowledgeManager : MonoBehaviour
    {
        public static CultureKnowledgeManager Instance { get; private set; }

        private Dictionary<int, CultureResource> _allResources = new Dictionary<int, CultureResource>();
        private HashSet<int> _unlockedIds = new HashSet<int>();

        // 云南文化资源本地缓存（离线可用）
        private static readonly List<CultureResource> DefaultResources = new List<CultureResource>
        {
            new CultureResource
            {
                Id = 1, Name = "东巴文字", Type = "symbol", Category = "dongba",
                CulturalTheme = "lijiang",
                Description = "世界唯一活着的象形文字",
                Content = "东巴文是纳西族东巴教祭司使用的象形文字，已有近千年历史。" +
                           "2003年被联合国教科文组织列入《世界记忆遗产名录》。" +
                           "东巴文字约有1400个单字，是人类社会文字起源和发展的\"活化石\"，" +
                           "对研究人类文字的起源和发展有重要价值。",
                ImageUrl = "culture/dongba_script.jpg",
                AudioUrl = "audio/dongba_intro.mp3",
                SortOrder = 1
            },
            new CultureResource
            {
                Id = 2, Name = "纳西古乐", Type = "behavior", Category = "dongba",
                CulturalTheme = "lijiang",
                Description = "被誉为音乐活化石",
                Content = "纳西古乐是流传于云南丽江纳西族中的传统音乐，" +
                           "保留了大量唐宋时期的词牌曲牌，是我国少数民族中保存完好的古代音乐。" +
                           "2006年被列入国家级非物质文化遗产名录。" +
                           "演奏古乐的老艺人平均年龄超过70岁，是真正活着的音乐史。",
                ImageUrl = "culture/naxi_music.jpg",
                AudioUrl = "audio/naxi_music_sample.mp3",
                SortOrder = 2
            },
            new CultureResource
            {
                Id = 3, Name = "丽江水系", Type = "scene", Category = "gudian",
                CulturalTheme = "lijiang",
                Description = "高原姑苏的水系格局",
                Content = "丽江古城的水系被誉为\"高原姑苏\"，玉泉水从城北流入古城，" +
                           "在四方街分为三支，流遍全城。古城居民巧妙利用水势，" +
                           "形成\"清泉石上流\"的独特水文化。" +
                           "古城水系总长12公里，滋养了世代生活于此的纳西族人。",
                ImageUrl = "culture/lijiang_waterway.jpg",
                SortOrder = 3
            },
            new CultureResource
            {
                Id = 4, Name = "古滇青铜器", Type = "symbol", Category = "gudian",
                CulturalTheme = "dali",
                Description = "古滇国的青铜文明遗产",
                Content = "古滇国（公元前3世纪-公元1世纪）是云南历史上最早的国家政权，" +
                           "以滇池为中心建立了高度发达的青铜文明。" +
                           "贮贝器是古滇国最具代表性的青铜器，用于储存贝币，" +
                           "器盖上雕刻着生动的历史场景，是研究古滇国社会生活的珍贵实物。",
                ImageUrl = "culture/gudian_bronze.jpg",
                SortOrder = 4
            },
            new CultureResource
            {
                Id = 5, Name = "白族扎染", Type = "behavior", Category = "minsu",
                CulturalTheme = "dali",
                Description = "千年传承的扎染技艺",
                Content = "大理白族扎染技艺有着1500余年的历史，是国家级非物质文化遗产。" +
                           "扎染以纯天然植物染料（板蓝根）为原料，" +
                           "通过扎、缝、缚、缀、夹等多种方式防染，形成独特的蓝白花纹图案。" +
                           "周城村是扎染的重要产地，被誉为\"中国扎染艺术之乡\"。",
                ImageUrl = "culture/bai_tiedye.jpg",
                SortOrder = 5
            },
            new CultureResource
            {
                Id = 6, Name = "傣族泼水节", Type = "behavior", Category = "minsu",
                CulturalTheme = "xishuangbanna",
                Description = "傣历新年的盛大庆典",
                Content = "泼水节是傣族最重要的传统节日，在傣历新年（公历4月中旬）举行，" +
                           "历时3-4天。节日期间人们互相泼水，象征着洗去过去一年的污垢和不祥，" +
                           "祈求新年幸福安康。2006年被列入国家级非物质文化遗产名录。",
                ImageUrl = "culture/dai_water_festival.jpg",
                AudioUrl = "audio/water_festival.mp3",
                SortOrder = 6
            },
            new CultureResource
            {
                Id = 7, Name = "彝族火把节", Type = "behavior", Category = "minsu",
                CulturalTheme = "lijiang",
                Description = "彝族的\"东方情人节\"",
                Content = "火把节是彝族最重要的传统节日，在农历六月二十四日举行。" +
                           "节日期间，人们点燃火把，在村寨间游行，" +
                           "祈求驱邪避灾、五谷丰登。火把节也是青年男女交流的重要场合，" +
                           "因此被称为彝族的\"东方情人节\"。",
                ImageUrl = "culture/yi_torch_festival.jpg",
                SortOrder = 7
            }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 初始化本地资源缓存
            foreach (var resource in DefaultResources)
                _allResources[resource.Id] = resource;
        }

        public void Start()
        {
            StartCoroutine(LoadResourcesFromServer());
        }

        // ==================== 资源加载 ====================

        private IEnumerator LoadResourcesFromServer()
        {
            yield return StartCoroutine(NetworkManager.Instance.GetCultureResources((resources) =>
            {
                if (resources != null && resources.Count > 0)
                {
                    foreach (var r in resources)
                        _allResources[r.Id] = r;
                    Debug.Log($"[CultureKnowledgeManager] 从服务器加载 {resources.Count} 个文化资源");
                }
            }));
        }

        // ==================== 解锁管理 ====================

        public void UnlockResource(int resourceId)
        {
            if (_unlockedIds.Contains(resourceId)) return;
            _unlockedIds.Add(resourceId);

            if (_allResources.TryGetValue(resourceId, out CultureResource resource))
            {
                resource.IsUnlocked = true;
                Debug.Log($"[CultureKnowledgeManager] 解锁文化资源: {resource.Name}");
            }
        }

        public void RestoreUnlocked(List<int> ids)
        {
            foreach (int id in ids)
                UnlockResource(id);
        }

        // ==================== 内容展示 ====================

        public void ShowCultureDetail(int resourceId)
        {
            if (!_allResources.TryGetValue(resourceId, out CultureResource resource))
            {
                Debug.LogWarning($"[CultureKnowledgeManager] 文化资源 {resourceId} 不存在");
                return;
            }

            UI.UIManager.Instance?.ShowCultureKnowledge(resource);
        }

        public List<CultureResource> GetAllUnlocked()
        {
            List<CultureResource> result = new List<CultureResource>();
            foreach (int id in _unlockedIds)
            {
                if (_allResources.TryGetValue(id, out CultureResource r))
                    result.Add(r);
            }
            return result;
        }

        public List<CultureResource> GetByTheme(string theme)
        {
            List<CultureResource> result = new List<CultureResource>();
            foreach (var r in _allResources.Values)
            {
                if (r.CulturalTheme == theme && r.IsUnlocked)
                    result.Add(r);
            }
            return result;
        }

        public CultureResource GetResource(int id)
        {
            return _allResources.TryGetValue(id, out CultureResource r) ? r : null;
        }

        public int GetUnlockedCount() => _unlockedIds.Count;
        public int GetTotalCount() => _allResources.Count;
        public float GetUnlockProgress() => _allResources.Count > 0
            ? (float)_unlockedIds.Count / _allResources.Count : 0f;
    }
}

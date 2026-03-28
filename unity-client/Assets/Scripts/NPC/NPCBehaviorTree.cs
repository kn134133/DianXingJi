using System;
using System.Collections;
using UnityEngine;
using DianXingJi.Data;
using DianXingJi.Player;
using DianXingJi.Core;

namespace DianXingJi.NPC
{
    /// <summary>
    /// NPC行为树管理器 - 基于行为树模型的NPC智能交互算法
    /// 行为树结构：根节点→序列节点→条件节点→装饰节点→动作节点
    ///
    /// 执行流程：
    ///   每帧执行一次 → 判断玩家是否在交互范围内
    ///   → 若在范围内：判断场景状态与游戏进度 → 选择对应对话或动作
    ///   → 若不在范围内：执行巡逻或待机动作
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NPCBehaviorTree : MonoBehaviour, IInteractable
    {
        [Header("NPC配置")]
        [SerializeField] private NpcData _npcData;
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float idleTime = 3f;

        [Header("行为概率（由后端动态调整）")]
        [SerializeField] private float patrolProbability = 0.6f;
        [SerializeField] private float idleProbability = 0.4f;

        private Animator _animator;
        private PlayerController _nearbyPlayer;
        private NpcState _currentState = NpcState.Idle;
        private int _currentDialogueNodeId;
        private float _stateTimer;
        private int _currentPatrolIndex;
        private Vector3[] _patrolPoints;

        // 行为树节点执行结果
        private enum BTResult { Success, Failure, Running }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Initialize(NpcData data)
        {
            _npcData = data;
            transform.position = data.SpawnPosition;
            interactionRange = data.InteractionRange;

            // 生成巡逻路径（在出生点附近）
            _patrolPoints = GeneratePatrolPoints(data.SpawnPosition);
            _currentDialogueNodeId = data.DialogueTree?.StartNodeId ?? 1;
        }

        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;
            ExecuteBehaviorTree();
        }

        // ==================== 行为树核心执行 ====================

        private void ExecuteBehaviorTree()
        {
            // 根节点 → 选择节点（Selector）
            BTResult result = SelectorNode_Root();
            if (result == BTResult.Failure)
                TransitionToState(NpcState.Idle);
        }

        /// <summary>
        /// 根选择节点：优先处理玩家交互，否则执行自主行为
        /// </summary>
        private BTResult SelectorNode_Root()
        {
            // 序列1：玩家交互序列（优先级最高）
            if (SequenceNode_PlayerInteraction() == BTResult.Success)
                return BTResult.Success;

            // 序列2：自主巡逻序列
            if (SequenceNode_Patrol() == BTResult.Success)
                return BTResult.Success;

            // 序列3：待机序列（默认行为）
            return ActionNode_Idle();
        }

        /// <summary>
        /// 序列节点：玩家交互序列
        /// 条件：玩家在范围内 AND 场景状态满足 AND 游戏进度满足
        /// </summary>
        private BTResult SequenceNode_PlayerInteraction()
        {
            // 条件节点1：检测玩家距离
            if (ConditionNode_PlayerInRange() == BTResult.Failure)
                return BTResult.Failure;

            // 条件节点2：检测场景状态（是否有待解谜题）
            if (ConditionNode_SceneState() == BTResult.Failure)
                return BTResult.Failure;

            // 装饰节点：调整行为概率（由后端下发参数控制）
            if (!DecoratorNode_ProbabilityCheck(0.85f))
                return BTResult.Failure;

            // 动作节点：面向玩家并进入待交互状态
            return ActionNode_FacePlayer();
        }

        /// <summary>
        /// 序列节点：自主巡逻序列
        /// </summary>
        private BTResult SequenceNode_Patrol()
        {
            // 条件：没有玩家在范围内 AND 有巡逻路径
            if (ConditionNode_PlayerInRange() == BTResult.Success)
                return BTResult.Failure;
            if (_patrolPoints == null || _patrolPoints.Length == 0)
                return BTResult.Failure;

            // 装饰节点：概率决定是否巡逻还是待机
            if (!DecoratorNode_ProbabilityCheck(patrolProbability))
                return BTResult.Failure;

            return ActionNode_Patrol();
        }

        // ==================== 条件节点 ====================

        private BTResult ConditionNode_PlayerInRange()
        {
            if (_nearbyPlayer == null)
            {
                // 主动检测范围内玩家
                Collider[] cols = Physics.OverlapSphere(transform.position, interactionRange);
                foreach (var col in cols)
                {
                    var player = col.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        _nearbyPlayer = player;
                        return BTResult.Success;
                    }
                }
                return BTResult.Failure;
            }

            float dist = Vector3.Distance(transform.position, _nearbyPlayer.transform.position);
            if (dist > interactionRange * 1.2f)
            {
                _nearbyPlayer = null;
                return BTResult.Failure;
            }
            return BTResult.Success;
        }

        private BTResult ConditionNode_SceneState()
        {
            // 根据场景状态决定是否提供交互
            // 如：已解决所有谜题时NPC给出祝贺对话
            var pm = Puzzle.PuzzleManager.Instance;
            if (pm == null) return BTResult.Success;

            // 有未解决谜题时优先交互
            if (pm.GetActivePuzzles().Count > 0)
                return BTResult.Success;

            // 全部解决时也可交互（提供文化延伸内容）
            return BTResult.Success;
        }

        // ==================== 装饰节点 ====================

        private bool DecoratorNode_ProbabilityCheck(float probability)
        {
            return UnityEngine.Random.value <= probability;
        }

        // ==================== 动作节点 ====================

        private BTResult ActionNode_FacePlayer()
        {
            if (_nearbyPlayer == null) return BTResult.Failure;

            // 平滑转向玩家
            Vector3 direction = (_nearbyPlayer.transform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }

            TransitionToState(NpcState.Interacting);
            _animator?.SetBool("IsInteracting", true);
            return BTResult.Success;
        }

        private BTResult ActionNode_Patrol()
        {
            TransitionToState(NpcState.Patrol);
            _animator?.SetBool("IsWalking", true);

            Vector3 target = _patrolPoints[_currentPatrolIndex];
            Vector3 direction = (target - transform.position).normalized;
            transform.position += direction * patrolSpeed * Time.deltaTime;

            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            if (Vector3.Distance(transform.position, target) < 0.3f)
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;

            return BTResult.Running;
        }

        private BTResult ActionNode_Idle()
        {
            if (_currentState != NpcState.Idle)
                TransitionToState(NpcState.Idle);

            _animator?.SetBool("IsWalking", false);
            _animator?.SetBool("IsInteracting", false);

            _stateTimer += Time.deltaTime;
            if (_stateTimer >= idleTime)
            {
                _stateTimer = 0;
                // 随机播放环境动画（看远处、整理衣物等）
                _animator?.SetTrigger("IdleAction");
            }

            return BTResult.Running;
        }

        private void TransitionToState(NpcState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            _stateTimer = 0;
        }

        // ==================== IInteractable 实现 ====================

        public string GetInteractionPrompt()
        {
            return $"[E] 与 {_npcData?.Name ?? "NPC"} 交谈";
        }

        public bool CanInteract()
        {
            return _npcData != null && _currentState != NpcState.Talking;
        }

        public void Interact(PlayerController player)
        {
            if (_npcData?.DialogueTree == null) return;

            player.SetMovementEnabled(false);
            TransitionToState(NpcState.Talking);
            _animator?.SetTrigger("Talk");

            // 获取当前对话节点
            NpcDialogueNode node = GetCurrentDialogueNode();
            if (node != null)
                UI.UIManager.Instance?.ShowDialogue(_npcData, node, OnDialogueOptionSelected);
        }

        private void OnDialogueOptionSelected(NpcDialogueOption option)
        {
            if (option == null)
            {
                EndDialogue();
                return;
            }

            // 执行选项动作
            ExecuteDialogueAction(option.Action);

            if (option.NextNodeId == -1)
            {
                EndDialogue();
                return;
            }

            _currentDialogueNodeId = option.NextNodeId;
            NpcDialogueNode nextNode = GetCurrentDialogueNode();
            if (nextNode != null)
                UI.UIManager.Instance?.UpdateDialogue(nextNode, OnDialogueOptionSelected);
            else
                EndDialogue();
        }

        private void ExecuteDialogueAction(string action)
        {
            switch (action)
            {
                case "give_clue":
                    // 给玩家一个线索
                    Debug.Log($"[NPC {_npcData.Name}] 给出线索");
                    break;
                case "unlock_culture":
                    if (_npcData.RelatedCultureId > 0)
                        GameManager.Instance?.UnlockCulturalContent(_npcData.RelatedCultureId);
                    break;
                case "reveal_puzzle":
                    Debug.Log($"[NPC {_npcData.Name}] 揭示谜题");
                    break;
            }
        }

        private void EndDialogue()
        {
            TransitionToState(NpcState.Idle);
            UI.UIManager.Instance?.HideDialogue();
            GameObject.FindObjectOfType<PlayerController>()?.SetMovementEnabled(true);
        }

        private NpcDialogueNode GetCurrentDialogueNode()
        {
            return _npcData?.DialogueTree?.Nodes?.Find(n => n.Id == _currentDialogueNodeId);
        }

        // ==================== 辅助方法 ====================

        private Vector3[] GeneratePatrolPoints(Vector3 center)
        {
            float radius = 5f;
            int count = 4;
            Vector3[] points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                points[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            }
            return points;
        }

        public void UpdateBehaviorParameters(float patrolProb, float idleProb)
        {
            patrolProbability = Mathf.Clamp01(patrolProb);
            idleProbability = Mathf.Clamp01(idleProb);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            if (_patrolPoints != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < _patrolPoints.Length; i++)
                {
                    Gizmos.DrawSphere(_patrolPoints[i], 0.3f);
                    Gizmos.DrawLine(_patrolPoints[i], _patrolPoints[(i + 1) % _patrolPoints.Length]);
                }
            }
        }
    }

    public enum NpcState
    {
        Idle,
        Patrol,
        Interacting,
        Talking,
        Reacting
    }
}

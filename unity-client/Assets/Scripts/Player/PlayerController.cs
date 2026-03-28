using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DianXingJi.Data;
using DianXingJi.Core;

namespace DianXingJi.Player
{
    /// <summary>
    /// 玩家控制器 - 处理角色移动、视角控制、场景交互输入
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.5f;

        [Header("视角参数")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float verticalLookLimit = 80f;

        [Header("交互参数")]
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private LayerMask interactionLayerMask;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode saveKey = KeyCode.F5;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _verticalRotation;
        private bool _isGrounded;
        private bool _canMove = true;

        // 交互检测
        private IInteractable _currentInteractable;
        private Collider[] _nearbyColliders = new Collider[10];

        public event System.Action<IInteractable> OnInteractableEnter;
        public event System.Action OnInteractableExit;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null)
                cameraTransform = Camera.main?.transform;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            HandleMovement();
            HandleCameraRotation();
            HandleInteractionDetection();
            HandleInput();
        }

        // ==================== 移动控制 ====================

        private void HandleMovement()
        {
            if (!_canMove) return;

            _isGrounded = _controller.isGrounded;
            if (_isGrounded && _velocity.y < 0)
                _velocity.y = -2f;

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            bool isRunning = Input.GetKey(KeyCode.LeftShift);

            float speed = isRunning ? runSpeed : moveSpeed;
            Vector3 move = transform.right * horizontal + transform.forward * vertical;
            _controller.Move(move * speed * Time.deltaTime);

            // 重力
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        // ==================== 视角控制 ====================

        private void HandleCameraRotation()
        {
            if (!_canMove) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -verticalLookLimit, verticalLookLimit);

            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
        }

        // ==================== 交互检测 ====================

        private void HandleInteractionDetection()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, interactionRange, _nearbyColliders, interactionLayerMask);

            IInteractable nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var interactable = _nearbyColliders[i].GetComponent<IInteractable>();
                if (interactable == null || !interactable.CanInteract()) continue;

                float dist = Vector3.Distance(transform.position, _nearbyColliders[i].transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = interactable;
                }
            }

            if (nearest != _currentInteractable)
            {
                _currentInteractable = nearest;
                if (nearest != null)
                    OnInteractableEnter?.Invoke(nearest);
                else
                    OnInteractableExit?.Invoke();
            }
        }

        // ==================== 输入处理 ====================

        private void HandleInput()
        {
            // 交互
            if (Input.GetKeyDown(interactKey) && _currentInteractable != null)
            {
                _currentInteractable.Interact(this);
            }

            // 手动存档
            if (Input.GetKeyDown(saveKey))
            {
                GameManager.Instance?.ManualSave();
            }

            // 暂停
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        // ==================== 公开接口 ====================

        public void SetMovementEnabled(bool enabled)
        {
            _canMove = enabled;
            if (!enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void TeleportTo(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }

        private void TogglePause()
        {
            if (GameManager.Instance.CurrentState == GameState.Playing)
            {
                GameManager.Instance.SetGameState(GameState.Paused);
                SetMovementEnabled(false);
            }
            else if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
                SetMovementEnabled(true);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }

    /// <summary>
    /// 可交互接口 - 场景中所有可交互对象实现此接口
    /// </summary>
    public interface IInteractable
    {
        string GetInteractionPrompt();
        bool CanInteract();
        void Interact(PlayerController player);
    }
}

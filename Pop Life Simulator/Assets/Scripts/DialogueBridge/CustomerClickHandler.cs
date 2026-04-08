using UnityEngine;
using PixelCrushers.DialogueSystem;
using Sirenix.OdinInspector;
using PopLife.Services;
using PopLife.Customers.Runtime;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Handles player clicks on customers to trigger dialogue
    ///
    /// Attach this component to a manager GameObject (e.g., DialogueBridgeManager)
    /// It uses Physics2D.Raycast to detect clicks on customers
    ///
    /// Requirements for customers:
    /// - Must have a Collider2D component
    /// - Must have CustomerDialogueTrigger component
    /// - Recommended: Set layer to "Customer" for filtered raycasting
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CustomerClickHandler : MonoBehaviour
    {
        #region Serialized Fields

        [Title("Click Detection")]
        [Tooltip("Layer mask for customer detection")]
        [SerializeField] private LayerMask customerLayerMask = -1; // Default: all layers

        [Tooltip("Camera to use for raycasting (null = Camera.main)")]
        [SerializeField] private Camera targetCamera;

        [Title("Settings")]
        [Tooltip("If true, clicking is disabled while a conversation is active")]
        [SerializeField] private bool disableDuringConversation = true;

        [Tooltip("If true, clicking is disabled while game is paused")]
        [SerializeField] private bool disableWhenPaused = true;

        [Tooltip("Mouse button to use (0 = left, 1 = right, 2 = middle)")]
        [SerializeField] private int mouseButton = 0;

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        [ShowIf("debugMode")]
        [SerializeField] private bool drawDebugRay = true;

        #endregion

        #region Private Fields

        private Camera _camera;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _camera = targetCamera != null ? targetCamera : Camera.main;

            if (_camera == null)
            {
                Debug.LogError("[CustomerClickHandler] No camera found!");
            }
        }

        private void Update()
        {
            // Check if clicking is allowed
            if (!CanClick())
            {
                return;
            }

            // Check for click: 使用 InputGateService 判定纯点击（避免拖拽误触）
            bool clicked = (mouseButton == 0 && InputGateService.Instance != null)
                ? InputGateService.Instance.WasClickThisFrame
                : Input.GetMouseButtonDown(mouseButton);

            if (clicked)
            {
                HandleClick();
            }
        }

        #endregion

        #region Click Handling

        /// <summary>
        /// Check if clicking is currently allowed
        /// </summary>
        private bool CanClick()
        {
            // Check if conversation is active
            if (disableDuringConversation && DialogueManager.isConversationActive)
            {
                return false;
            }

            // Check if game is paused
            if (disableWhenPaused && Time.timeScale == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handle mouse click
        /// </summary>
        private void HandleClick()
        {
            if (_camera == null) return;

            // Convert mouse position to world position
            Vector2 worldPos = _camera.ScreenToWorldPoint(Input.mousePosition);

            // Raycast to find customer
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f, customerLayerMask);

            if (debugMode && drawDebugRay)
            {
                Debug.DrawRay(worldPos, Vector3.forward * 10f, hit ? Color.green : Color.red, 1f);
            }

            if (hit.collider != null)
            {
                // Check for CustomerDialogueTrigger
                var trigger = hit.collider.GetComponent<CustomerDialogueTrigger>();

                if (trigger != null)
                {
                    // 有交互气泡时消费点击，阻止 BuildingInteractionManager 同帧响应
                    var adapter = hit.collider.GetComponent<CustomerBlackboardAdapter>();
                    if (adapter != null && adapter.interactionBubbleActive)
                    {
                        InputGateService.Instance?.ConsumeClick();
                    }

                    if (debugMode)
                    {
                        Debug.Log($"[CustomerClickHandler] Clicked on: {hit.collider.gameObject.name}");
                    }

                    trigger.TryTriggerDialogue();
                }
                else if (debugMode)
                {
                    Debug.Log($"[CustomerClickHandler] Hit object without CustomerDialogueTrigger: {hit.collider.gameObject.name}");
                }
            }
            else if (debugMode)
            {
                Debug.Log("[CustomerClickHandler] Click missed all targets");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the camera to use for raycasting
        /// </summary>
        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>
        /// Set the layer mask for customer detection
        /// </summary>
        public void SetLayerMask(LayerMask mask)
        {
            customerLayerMask = mask;
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Setup Customer Layer")]
        private void SetupCustomerLayer()
        {
            // This just reminds the developer what to do
            Debug.Log(@"[CustomerClickHandler] To set up customer layer:
1. Go to Edit > Project Settings > Tags and Layers
2. Add a new layer called 'Customer'
3. Set all customer prefabs to use this layer
4. Update the customerLayerMask in this component to only include 'Customer' layer");
        }
#endif

        #endregion
    }
}

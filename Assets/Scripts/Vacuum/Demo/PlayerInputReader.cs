using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hedgewitch.Vacuum.Demo
{
    /// <summary>
    /// One place that turns raw input into the handful of values the player scripts need.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move { get; private set; }

        public Vector2 AimStick { get; private set; }

        public Vector2 PointerPosition { get; private set; }

        public bool SuckHeld { get; private set; }

        public bool JumpPressed { get; private set; }

        public float CameraRotate { get; private set; }

        public float CameraZoom { get; private set; }

        public bool UsingPointer { get; private set; } = true;

#if ENABLE_INPUT_SYSTEM
        [SerializeField]
        private InputAction moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");

        [SerializeField]
        private InputAction aimAction = new InputAction("Aim", InputActionType.Value, expectedControlType: "Vector2");

        [SerializeField]
        private InputAction pointerAction = new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");

        [SerializeField]
        private InputAction suckAction = new InputAction("Suck", InputActionType.Button);

        [SerializeField]
        private InputAction jumpAction = new InputAction("Jump", InputActionType.Button);

        [SerializeField]
        private InputAction cameraRotateAction = new InputAction("CameraRotate", InputActionType.Value, expectedControlType: "Axis");

        [SerializeField]
        private InputAction cameraZoomAction = new InputAction("CameraZoom", InputActionType.Value, expectedControlType: "Axis");

        [Tooltip("Stick deflection above which aiming switches from the pointer to the stick.")]
        [Range(0.05f, 0.9f)] [SerializeField] private float stickAimDeadzone = 0.25f;

        private void Awake()
        {
            EnsureDefaultBindings();
        }

        private void OnEnable()
        {
            moveAction.Enable();
            aimAction.Enable();
            pointerAction.Enable();
            suckAction.Enable();
            jumpAction.Enable();
            cameraRotateAction.Enable();
            cameraZoomAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            aimAction.Disable();
            pointerAction.Disable();
            suckAction.Disable();
            jumpAction.Disable();
            cameraRotateAction.Disable();
            cameraZoomAction.Disable();

            Move = Vector2.zero;
            SuckHeld = false;
            JumpPressed = false;
        }

        private void Update()
        {
            Move = moveAction.ReadValue<Vector2>();
            AimStick = aimAction.ReadValue<Vector2>();
            SuckHeld = suckAction.IsPressed();
            JumpPressed = jumpAction.WasPressedThisFrame();
            CameraRotate = cameraRotateAction.ReadValue<float>();
            CameraZoom = cameraZoomAction.ReadValue<float>();

            Vector2 pointer = pointerAction.ReadValue<Vector2>();
            if (pointer.sqrMagnitude > 0f)
            {
                PointerPosition = pointer;
            }

            if (AimStick.magnitude > stickAimDeadzone)
            {
                UsingPointer = false;
            }
            else if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f)
            {
                UsingPointer = true;
            }
        }

        private void EnsureDefaultBindings()
        {
            if (moveAction.bindings.Count == 0)
            {
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
                moveAction.AddBinding("<Gamepad>/leftStick");
            }

            if (aimAction.bindings.Count == 0)
            {
                aimAction.AddBinding("<Gamepad>/rightStick");
            }

            if (pointerAction.bindings.Count == 0)
            {
                pointerAction.AddBinding("<Mouse>/position");
                pointerAction.AddBinding("<Pen>/position");
            }

            if (suckAction.bindings.Count == 0)
            {
                suckAction.AddBinding("<Mouse>/leftButton");
                suckAction.AddBinding("<Gamepad>/rightTrigger");
                suckAction.AddBinding("<Keyboard>/f");
            }

            if (jumpAction.bindings.Count == 0)
            {
                jumpAction.AddBinding("<Keyboard>/space");
                jumpAction.AddBinding("<Gamepad>/buttonSouth");
            }

            if (cameraRotateAction.bindings.Count == 0)
            {
                cameraRotateAction.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/q")
                    .With("Positive", "<Keyboard>/e");
            }

            if (cameraZoomAction.bindings.Count == 0)
            {
                cameraZoomAction.AddBinding("<Mouse>/scroll/y");
            }
        }
#else
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private KeyCode suckKey = KeyCode.F;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;

        private void Update()
        {
            Move = new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis));
            AimStick = Vector2.zero;
            PointerPosition = Input.mousePosition;
            SuckHeld = Input.GetMouseButton(0) || Input.GetKey(suckKey);
            JumpPressed = Input.GetKeyDown(jumpKey);
            CameraRotate = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            CameraZoom = Input.mouseScrollDelta.y;
            UsingPointer = true;
        }
#endif
    }
}

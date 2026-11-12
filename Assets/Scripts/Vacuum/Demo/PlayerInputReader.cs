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

        public bool SuckHeld => PrimaryHeld;

        // Light attack fire or suck
        public bool PrimaryHeld { get; private set; }

        public bool PrimaryPressed { get; private set; }

        // Heavy attack
        public bool SecondaryHeld { get; private set; }

        public bool SecondaryPressed { get; private set; }

        // Slot picked this frame or -1
        public int SelectedSlot { get; private set; } = -1;

        // Weapon cycle direction this frame or 0
        public int CycleDirection { get; private set; }

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

        [Tooltip("Primary: light attack, fire, suck. Kept under its old name so existing scenes keep their bindings.")]
        [SerializeField]
        private InputAction suckAction = new InputAction("Suck", InputActionType.Button);

        [Tooltip("Secondary: heavy attack.")]
        [SerializeField]
        private InputAction secondaryAction = new InputAction("Secondary", InputActionType.Button);

        [SerializeField]
        private InputAction weaponSlot1Action = new InputAction("WeaponSlot1", InputActionType.Button);

        [SerializeField]
        private InputAction weaponSlot2Action = new InputAction("WeaponSlot2", InputActionType.Button);

        [SerializeField]
        private InputAction weaponSlot3Action = new InputAction("WeaponSlot3", InputActionType.Button);

        [SerializeField]
        private InputAction nextWeaponAction = new InputAction("NextWeapon", InputActionType.Button);

        [SerializeField]
        private InputAction previousWeaponAction = new InputAction("PreviousWeapon", InputActionType.Button);

        [SerializeField]
        private InputAction jumpAction = new InputAction("Jump", InputActionType.Button);

        [SerializeField]
        private InputAction cameraRotateAction = new InputAction("CameraRotate", InputActionType.Value, expectedControlType: "Axis");

        [SerializeField]
        private InputAction cameraZoomAction = new InputAction("CameraZoom", InputActionType.Value, expectedControlType: "Axis");

        [Tooltip("Stick deflection above which aiming switches from the pointer to the stick.")]
        [Range(0.05f, 0.9f)] [SerializeField] private float stickAimDeadzone = 0.25f;

        private InputAction[] allActions;

        private void Awake()
        {
            EnsureActions();
            EnsureDefaultBindings();

            allActions = new[]
            {
                moveAction, aimAction, pointerAction, suckAction, secondaryAction,
                weaponSlot1Action, weaponSlot2Action, weaponSlot3Action,
                nextWeaponAction, previousWeaponAction,
                jumpAction, cameraRotateAction, cameraZoomAction
            };
        }

        private void OnEnable()
        {
            for (int i = 0; i < allActions.Length; i++)
            {
                allActions[i].Enable();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < allActions.Length; i++)
            {
                allActions[i].Disable();
            }

            Move = Vector2.zero;
            PrimaryHeld = false;
            PrimaryPressed = false;
            SecondaryHeld = false;
            SecondaryPressed = false;
            SelectedSlot = -1;
            CycleDirection = 0;
            JumpPressed = false;
        }

        private void Update()
        {
            Move = moveAction.ReadValue<Vector2>();
            AimStick = aimAction.ReadValue<Vector2>();
            PrimaryHeld = suckAction.IsPressed();
            PrimaryPressed = suckAction.WasPressedThisFrame();
            SecondaryHeld = secondaryAction.IsPressed();
            SecondaryPressed = secondaryAction.WasPressedThisFrame();
            JumpPressed = jumpAction.WasPressedThisFrame();
            CameraRotate = cameraRotateAction.ReadValue<float>();
            CameraZoom = cameraZoomAction.ReadValue<float>();

            SelectedSlot = weaponSlot1Action.WasPressedThisFrame() ? 0
                : weaponSlot2Action.WasPressedThisFrame() ? 1
                : weaponSlot3Action.WasPressedThisFrame() ? 2
                : -1;

            CycleDirection = nextWeaponAction.WasPressedThisFrame() ? 1
                : previousWeaponAction.WasPressedThisFrame() ? -1
                : 0;

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

        // Recreate any action cleared from script or the inspector
        private void EnsureActions()
        {
            moveAction ??= new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            aimAction ??= new InputAction("Aim", InputActionType.Value, expectedControlType: "Vector2");
            pointerAction ??= new InputAction("Pointer", InputActionType.Value, expectedControlType: "Vector2");
            suckAction ??= new InputAction("Suck", InputActionType.Button);
            secondaryAction ??= new InputAction("Secondary", InputActionType.Button);
            weaponSlot1Action ??= new InputAction("WeaponSlot1", InputActionType.Button);
            weaponSlot2Action ??= new InputAction("WeaponSlot2", InputActionType.Button);
            weaponSlot3Action ??= new InputAction("WeaponSlot3", InputActionType.Button);
            nextWeaponAction ??= new InputAction("NextWeapon", InputActionType.Button);
            previousWeaponAction ??= new InputAction("PreviousWeapon", InputActionType.Button);
            jumpAction ??= new InputAction("Jump", InputActionType.Button);
            cameraRotateAction ??= new InputAction("CameraRotate", InputActionType.Value, expectedControlType: "Axis");
            cameraZoomAction ??= new InputAction("CameraZoom", InputActionType.Value, expectedControlType: "Axis");
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

            if (secondaryAction.bindings.Count == 0)
            {
                secondaryAction.AddBinding("<Mouse>/rightButton");
                secondaryAction.AddBinding("<Gamepad>/leftTrigger");
            }

            if (weaponSlot1Action.bindings.Count == 0)
            {
                weaponSlot1Action.AddBinding("<Keyboard>/1");
                weaponSlot1Action.AddBinding("<Keyboard>/numpad1");
            }

            if (weaponSlot2Action.bindings.Count == 0)
            {
                weaponSlot2Action.AddBinding("<Keyboard>/2");
                weaponSlot2Action.AddBinding("<Keyboard>/numpad2");
            }

            if (weaponSlot3Action.bindings.Count == 0)
            {
                weaponSlot3Action.AddBinding("<Keyboard>/3");
                weaponSlot3Action.AddBinding("<Keyboard>/numpad3");
            }

            if (nextWeaponAction.bindings.Count == 0)
            {
                nextWeaponAction.AddBinding("<Gamepad>/rightShoulder");
            }

            if (previousWeaponAction.bindings.Count == 0)
            {
                previousWeaponAction.AddBinding("<Gamepad>/leftShoulder");
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
            PrimaryHeld = Input.GetMouseButton(0) || Input.GetKey(suckKey);
            PrimaryPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(suckKey);
            SecondaryHeld = Input.GetMouseButton(1);
            SecondaryPressed = Input.GetMouseButtonDown(1);
            SelectedSlot = Input.GetKeyDown(KeyCode.Alpha1) ? 0
                : Input.GetKeyDown(KeyCode.Alpha2) ? 1
                : Input.GetKeyDown(KeyCode.Alpha3) ? 2
                : -1;
            CycleDirection = 0;
            JumpPressed = Input.GetKeyDown(jumpKey);
            CameraRotate = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            CameraZoom = Input.mouseScrollDelta.y;
            UsingPointer = true;
        }
#endif
    }
}

// ============================================================================
//  THE HOT WAR — HW_Player.cs
//  TWO components:
//    HW_Interactor — the raycast + E handling. If Shahar's player controller
//                    already exists, add ONLY this to its camera and delete
//                    HW_Player from the build.
//    HW_Player     — a complete fallback FPS controller (CharacterController).
//  Both respect HW_Game.InputLocked (phone open / puzzle focus / game over).
// ============================================================================
using UnityEngine;

namespace TheHotWar
{
    [AddComponentMenu("The Hot War/Interactor (raycast + E)")]
    public class HW_Interactor : MonoBehaviour
    {
        [Tooltip("Camera used for the interaction ray. Defaults to this transform.")]
        public Transform rayOrigin;
        public float interactRange = 3.2f;

        HW_Interactable _target;

        void Update()
        {
            HW_Game gm = HW_Game.I;
            if (gm != null && gm.InputLocked)
            {
                gm.SetPrompt("");
                _target = null;
                return;
            }

            Transform o = rayOrigin != null ? rayOrigin : transform;
            _target = null;
            RaycastHit hit;
            if (Physics.Raycast(new Ray(o.position, o.forward), out hit, interactRange))
            {
                if (hit.collider != null)
                    _target = hit.collider.GetComponentInParent<HW_Interactable>();
            }
            if (gm != null) gm.SetPrompt(_target != null ? _target.Prompt : "");

            if (_target != null)
            {
                if (HotInput.InteractDown) _target.OnPress();
                if (HotInput.InteractHeld) _target.OnHold(Time.deltaTime);
            }
        }
    }

    [AddComponentMenu("The Hot War/Player (fallback FPS controller)")]
    [RequireComponent(typeof(CharacterController))]
    public class HW_Player : MonoBehaviour
    {
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.0f;
        public float mouseSensitivity = 2.2f;
        [Tooltip("Child camera transform. Auto-found if left empty.")]
        public Transform cam;

        CharacterController _cc;
        float _pitch;
        float _yVel;

        void Start()
        {
            _cc = GetComponent<CharacterController>();
            if (cam == null)
            {
                Camera c = GetComponentInChildren<Camera>();
                if (c != null) cam = c.transform;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            HW_Game gm = HW_Game.I;
            if (gm != null && gm.InputLocked) return;

            if (HotInput.EscapeDown)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked && HotInput.ClickDown)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0f, HotInput.LookX * mouseSensitivity, 0f);
                _pitch = Mathf.Clamp(_pitch - HotInput.LookY * mouseSensitivity, -85f, 85f);
                if (cam != null) cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            float sp = HotInput.SprintHeld ? sprintSpeed : walkSpeed;
            Vector3 wish = transform.right * HotInput.MoveX + transform.forward * HotInput.MoveY;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            if (_cc.isGrounded) _yVel = -1.5f;
            else _yVel -= 22f * Time.deltaTime;
            Vector3 vel = wish * sp;
            vel.y = _yVel;
            _cc.Move(vel * Time.deltaTime);
        }
    }
}

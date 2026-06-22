using UnityEngine;
using UnityEngine.InputSystem;

namespace AISandbox.View
{
    /// <summary>
    /// Isometric camera controls (new Input System):
    ///   - Pan:    WASD / arrow keys, or hold middle mouse and drag.
    ///   - Zoom:   mouse scroll wheel (changes orthographic size).
    ///   - Rotate: Q / E snap the view 90 degrees around the focus point.
    ///
    /// The camera orbits a "focus point" that sits on the ground (y = 0). Pitch is
    /// fixed for the iso look; yaw is what rotation changes. Attach to Main Camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsoCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [Tooltip("Keyboard pan speed (scaled by zoom).")]
        public float panSpeed = 1.5f;
        [Tooltip("Middle-mouse drag pan speed (scaled by zoom).")]
        public float dragSpeed = 2.5f;

        [Header("Zoom")]
        public float zoomStep = 1.5f;
        public float minZoom = 2f;
        public float maxZoom = 20f;

        [Header("Rotate")]
        [Tooltip("Degrees per second while snapping to the next 90.")]
        public float rotateSpeed = 540f;

        [Header("Angle")]
        [Tooltip("Fixed downward tilt of the iso view.")]
        public float pitch = 30f;

        private Camera _cam;
        private Vector3 _focus;
        private float _yaw;
        private float _targetYaw;
        private float _distance;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            if (!_cam.orthographic) _cam.orthographic = true;

            _yaw = transform.eulerAngles.y;
            _targetYaw = _yaw;

            // Focus = where the camera's forward ray meets the ground plane.
            Vector3 fwd = transform.forward;
            if (Mathf.Abs(fwd.y) > 0.001f)
            {
                float t = -transform.position.y / fwd.y;
                _focus = transform.position + fwd * t;
                _distance = Mathf.Max(t, 1f);
            }
            else
            {
                _focus = transform.position + fwd * 10f;
                _distance = 10f;
            }
        }

        private void LateUpdate()
        {
            HandleZoom();
            HandleRotate();
            HandlePan();
            ApplyTransform();
        }

        private void HandlePan()
        {
            Vector2 input = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            }

            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

            _focus += (right * input.x + fwd * input.y) * panSpeed * _cam.orthographicSize * Time.deltaTime;

            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                _focus -= (right * d.x + fwd * d.y) * dragSpeed * 0.01f * _cam.orthographicSize * Time.deltaTime;
            }
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float notches = scroll / 120f; // one mouse-wheel notch ~= 120
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - notches * zoomStep, minZoom, maxZoom);
        }

        private void HandleRotate()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) _targetYaw -= 90f;
                if (kb.eKey.wasPressedThisFrame) _targetYaw += 90f;
            }
            _yaw = Mathf.MoveTowardsAngle(_yaw, _targetYaw, rotateSpeed * Time.deltaTime);
        }

        private void ApplyTransform()
        {
            Quaternion rot = Quaternion.Euler(pitch, _yaw, 0f);
            transform.rotation = rot;
            transform.position = _focus + rot * Vector3.back * _distance;
        }
    }
}

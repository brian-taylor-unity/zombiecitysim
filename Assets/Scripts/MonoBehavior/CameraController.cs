using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float stickMinZoom;
    public float stickMaxZoom;
    public float zoomSpeed;
    public float orbitSpeed;
    public float moveSpeedMinZoom;
    public float moveSpeedMaxZoom;
    public float fastMoveSpeedFactor;
    public float mouseLookSpeed;
    public float mouseLookMaxPitch;
    public float mouseLookMinPitch;

    private Transform _swivel;
    private Transform _stick;

    private float _zoom = 1f;
    private float _zoomTarget = 0.4f;
    private readonly float _zoomAnimLength = 1.5f;
    private float _zoomAnimTimer;
    private float _orbitAngle;
    private float _pitchAngle;
    private bool _moveSpeedModifier;

    private InputAction _moveCameraAction;
    private InputAction _moveCameraSpeedModifierAction;
    private InputAction _lookCameraAction;
    private InputAction _zoomAction;
    private InputAction _rotateAction;

    private InputAction _mouseMoveCameraAction;
    private InputAction _mouseLookCameraAction;
    private InputAction _mouseLeftClickAction;
    private InputAction _mouseRightClickAction;
    private bool _mouseInputBlockedByUI;

    private void Start()
    {
        _swivel = transform.GetChild(0);
        _stick = _swivel.GetChild(0);

        transform.localPosition = new Vector3(GameController.Instance.numTilesX * 0.5f, 0f, GameController.Instance.numTilesY * 0.2f);

        _orbitAngle = transform.eulerAngles.y;
        _pitchAngle = _swivel.eulerAngles.x;

        _moveCameraAction = InputSystem.actions.FindAction("Camera/Move");
        _moveCameraSpeedModifierAction = InputSystem.actions.FindAction("Camera/FastMove");
        _lookCameraAction = InputSystem.actions.FindAction("Camera/Look");
        _zoomAction = InputSystem.actions.FindAction("Camera/Zoom");
        _rotateAction = InputSystem.actions.FindAction("Camera/Rotate");

        _mouseMoveCameraAction = InputSystem.actions.FindAction("Camera/MouseMove");
        _mouseLookCameraAction = InputSystem.actions.FindAction("Camera/MouseLook");

        _mouseLeftClickAction = InputSystem.actions.FindAction("Camera/MouseLeftClick");
        _mouseLeftClickAction.started += _ => _mouseInputBlockedByUI = GameController.Instance.mouseBlockedByUI;
        _mouseLeftClickAction.canceled += _ => _mouseInputBlockedByUI = false;

        _mouseRightClickAction = InputSystem.actions.FindAction("Camera/MouseRightClick");
        _mouseRightClickAction.started += _ => _mouseInputBlockedByUI = GameController.Instance.mouseBlockedByUI;
        _mouseRightClickAction.canceled += _ => _mouseInputBlockedByUI = false;
    }

    private void Update()
    {
        if (_moveCameraSpeedModifierAction.IsPressed())
        {
            _moveSpeedModifier = _moveCameraSpeedModifierAction.ReadValue<float>() > 0;
        }

        if (_moveCameraAction.IsPressed())
        {
            AdjustPosition(_moveCameraAction.ReadValue<Vector2>(), _moveSpeedModifier);
        }

        if (_mouseMoveCameraAction.IsPressed() && !_mouseInputBlockedByUI)
        {
            AdjustPosition(_mouseMoveCameraAction.ReadValue<Vector2>(), _moveSpeedModifier);
        }

        if (_mouseLookCameraAction.IsPressed() && !_mouseInputBlockedByUI)
        {
            AdjustLook(_mouseLookCameraAction.ReadValue<Vector2>());
        }

        if (_lookCameraAction.IsPressed())
        {
            AdjustLook(_lookCameraAction.ReadValue<Vector2>());
        }

        if (_zoomAction.IsPressed())
        {
            AdjustZoom(_zoomAction.ReadValue<Vector2>().y);
        }

        if (_rotateAction.IsPressed())
        {
            AdjustOrbit(_rotateAction.ReadValue<float>());
        }

        if (_zoomAnimTimer < _zoomAnimLength)
        {
            _zoomAnimTimer += Time.deltaTime;
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, _zoomAnimTimer / _zoomAnimLength);

            var distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, _zoom);
            _stick.localPosition = new Vector3(0f, 0f, distance);
        }
    }

    private void AdjustZoom(float delta)
    {
        if (delta == 0f)
            return;

        var zoomAmount = delta * zoomSpeed * Mathf.Clamp(1f - _zoomTarget, 0.05f, 1f);
        _zoomTarget = Mathf.Clamp01(_zoomTarget + zoomAmount);
        _zoomAnimTimer = 0f;
    }

    private void AdjustOrbit(float delta)
    {
        if (delta == 0f)
            return;

        _orbitAngle += delta * orbitSpeed;

        if (_orbitAngle < 0f)
            _orbitAngle += 360f;
        if (_orbitAngle >= 360f)
            _orbitAngle -= 360f;

        transform.localRotation = Quaternion.Euler(0f, _orbitAngle, 0f);
    }

    private void AdjustPosition(Vector2 delta, bool fast)
    {
        if (delta is { x: 0f, y: 0f })
            return;

        if (fast)
        {
            delta.x *= fastMoveSpeedFactor;
            delta.y *= fastMoveSpeedFactor;
        }

        var direction = transform.localRotation * new Vector3(delta.x, 0f, delta.y).normalized;
        var damping = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        var distance = Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, _zoom) * damping * Time.deltaTime;

        var position = transform.localPosition;
        position += direction * distance;
        transform.localPosition = position;
    }

    private void AdjustLook(Vector2 delta)
    {
        if (delta is { x: 0f, y: 0f })
            return;

        _pitchAngle = Mathf.Clamp(_pitchAngle + delta.y * mouseLookSpeed, mouseLookMinPitch, mouseLookMaxPitch);
        _orbitAngle += delta.x * mouseLookSpeed;
        transform.localRotation = Quaternion.Euler(0f, _orbitAngle, 0f);
        _swivel.localRotation = Quaternion.Euler(_pitchAngle, 0f, 0f);
    }
}

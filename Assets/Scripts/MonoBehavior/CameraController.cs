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

    public MouseInputUIBlocker[] UIBlockingMouse;

    private Transform swivel;
    private Transform stick;
    
    private float zoom = 1f;
    private float zoomTarget = 0.4f;
    private readonly float zoomAnimLength = 1.5f;
    private float zoomAnimTimer = 0;
    private float orbitAngle = 0;
    private float pitchAngle = 0;

    private InputAction moveCameraAction;
    private InputAction moveCameraSpeedModifierAction;
    private InputAction lookCameraAction;
    private InputAction zoomAction;
    private InputAction rotateAction;

    private void Start()
    {
        swivel = transform.GetChild(0);
        stick = swivel.GetChild(0);

        transform.localPosition = new Vector3(GameController.Instance.numTilesX * 0.5f, 0f, GameController.Instance.numTilesY * 0.2f);

        orbitAngle = transform.eulerAngles.y;
        pitchAngle = swivel.eulerAngles.x;

        moveCameraAction = InputSystem.actions.FindAction("Camera/Move");
        moveCameraSpeedModifierAction = InputSystem.actions.FindAction("Camera/FastMove");
        lookCameraAction = InputSystem.actions.FindAction("Camera/Look");
        zoomAction = InputSystem.actions.FindAction("Camera/Zoom");
        rotateAction = InputSystem.actions.FindAction("Camera/Rotate");
    }

    private void Update()
    {
        var moveDelta = moveCameraAction.ReadValue<Vector2>();
        var moveCameraSpeedModifier = moveCameraSpeedModifierAction.ReadValue<float>();
        var lookCamera = lookCameraAction.ReadValue<Vector2>();
        var zoomDelta = zoomAction.ReadValue<Vector2>();
        var rotateDelta = rotateAction.ReadValue<float>();

        if (zoomAnimTimer < zoomAnimLength)
        {
            zoomAnimTimer += Time.deltaTime;
            zoom = Mathf.Lerp(zoom, zoomTarget, zoomAnimTimer / zoomAnimLength);

            var distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);
        }

        AdjustZoom(zoomDelta.y);
        AdjustOrbit(rotateDelta);
        AdjustPosition(moveDelta, moveCameraSpeedModifier > 0);
        AdjustMouseLook(lookCamera);
    }

    private void AdjustZoom(float delta)
    {
        if (delta == 0f)
            return;

        var zoomAmount = delta * zoomSpeed * Mathf.Clamp(1f - zoomTarget, 0.05f, 1f);
        zoomTarget = Mathf.Clamp01(zoomTarget + zoomAmount);
        zoomAnimTimer = 0f;
    }

    private void AdjustOrbit(float delta)
    {
        if (delta == 0f)
            return;

        orbitAngle += delta * orbitSpeed;

        if (orbitAngle < 0f)
            orbitAngle += 360f;
        if (orbitAngle >= 360f)
            orbitAngle -= 360f;

        transform.localRotation = Quaternion.Euler(0f, orbitAngle, 0f);
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
        var distance = Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) * damping * Time.deltaTime;

        var position = transform.localPosition;
        position += direction * distance;
        transform.localPosition = position;
    }

    private void AdjustMouseLook(Vector2 delta)
    {
        if (delta is { x: 0f, y: 0f })
            return;

        pitchAngle = Mathf.Clamp(pitchAngle + delta.y * mouseLookSpeed, mouseLookMinPitch, mouseLookMaxPitch);
        orbitAngle += delta.x * mouseLookSpeed;
        transform.localRotation = Quaternion.Euler(0f, orbitAngle, 0f);
        swivel.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
    }

    private bool IsMouseBlockedByUI()
    {
        foreach (var blocker in UIBlockingMouse)
        {
            if (blocker.blockedByUI)
                return true;
        }

        return false;
    }
}

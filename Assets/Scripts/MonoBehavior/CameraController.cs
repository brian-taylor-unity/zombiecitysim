using UnityEngine;

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

    // Start is called before the first frame update
    private void Start()
    {
        swivel = transform.GetChild(0);
        stick = swivel.GetChild(0);

        transform.localPosition = new Vector3(GameController.Instance.numTilesX * 0.5f, 0f, GameController.Instance.numTilesY * 0.2f);

        orbitAngle = transform.eulerAngles.y;
        pitchAngle = swivel.eulerAngles.x;
    }

    // Update is called once per frame
    private void Update()
    {
        var mouseWheelDelta = Input.GetAxis("Mouse ScrollWheel");
        var mouseHorizontalDelta = Input.GetAxis("Mouse X");
        var mouseVerticalDelta = Input.GetAxis("Mouse Y");
        var rotationDelta = Input.GetAxis("Rotation");
        var keyHorizontalDelta = Input.GetAxis("Horizontal");
        var keyVerticalDelta = Input.GetAxis("Vertical");
        var shift = Input.GetButton("Fire3");
        var leftClick = Input.GetMouseButton(0);
        var rightClick = Input.GetMouseButton(1);

        if (zoomAnimTimer < zoomAnimLength)
        {
            zoomAnimTimer += Time.deltaTime;
            zoom = Mathf.Lerp(zoom, zoomTarget, zoomAnimTimer / zoomAnimLength);

            var distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);
        }

        if (mouseWheelDelta != 0f)
            AdjustZoom(mouseWheelDelta);

        if (rotationDelta != 0f)
            AdjustOrbit(rotationDelta);

        if (leftClick && (mouseHorizontalDelta != 0f || mouseVerticalDelta != 0f) && !IsMouseBlockedByUI())
            AdjustPosition(mouseHorizontalDelta * -1.8f, mouseVerticalDelta * -1.8f, shift);
        else if (keyHorizontalDelta != 0f || keyVerticalDelta != 0f)
            AdjustPosition(keyHorizontalDelta, keyVerticalDelta, shift);

        if (rightClick)
            AdjustMouseLook(-mouseVerticalDelta, mouseHorizontalDelta);
    }

    private void AdjustZoom(float delta)
    {
        var zoomAmount = delta * zoomSpeed * Mathf.Clamp(1f - zoomTarget, 0.05f, 1f);
        zoomTarget = Mathf.Clamp01(zoomTarget + zoomAmount);
        zoomAnimTimer = 0f;
    }

    private void AdjustOrbit(float delta)
    {
        orbitAngle += delta * orbitSpeed;

        if (orbitAngle < 0f)
            orbitAngle += 360f;
        if (orbitAngle >= 360f)
            orbitAngle -= 360f;

        transform.localRotation = Quaternion.Euler(0f, orbitAngle, 0f);
    }

    private void AdjustPosition(float xDelta, float zDelta, bool fast)
    {
        if (fast)
        {
            xDelta *= fastMoveSpeedFactor;
            zDelta *= fastMoveSpeedFactor;
        }

        var direction = transform.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
        var damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
        var distance = Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) * damping * Time.deltaTime;

        var position = transform.localPosition;
        position += direction * distance;
        transform.localPosition = position;
    }

    private void AdjustMouseLook(float pitchDelta, float yawDelta)
    {
        pitchAngle = Mathf.Clamp(pitchAngle + pitchDelta * mouseLookSpeed, mouseLookMinPitch, mouseLookMaxPitch);
        orbitAngle += yawDelta * mouseLookSpeed;
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

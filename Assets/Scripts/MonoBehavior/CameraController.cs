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

    private Transform swivel;
    private Transform stick;
    
    private float zoom = 1f;
    private float zoomTarget = 0.4f;
    private readonly float zoomAnimLength = 1.5f;
    private float zoomAnimTimer = 0;
    private float orbitAngle = 0;
    private float pitchAngle = 0;

    // Start is called before the first frame update
    void Start()
    {
        swivel = transform.GetChild(0);
        stick = swivel.GetChild(0);

        transform.localPosition = new Vector3(GameController.instance.numTilesX * 0.5f, 0f, GameController.instance.numTilesY * 0.2f);

        orbitAngle = transform.eulerAngles.y;
        pitchAngle = swivel.eulerAngles.x;
    }

    // Update is called once per frame
    void Update()
    {
        float mouseWheelDelta = Input.GetAxis("Mouse ScrollWheel");
        float mouseHorizontalDelta = Input.GetAxis("Mouse X");
        float mouseVerticalDelta = Input.GetAxis("Mouse Y");
        float rotationDelta = Input.GetAxis("Rotation");
        float keyHorizontalDelta = Input.GetAxis("Horizontal");
        float keyVerticalDelta = Input.GetAxis("Vertical");
        bool shift = Input.GetButton("Fire3");
        bool middleClick = Input.GetMouseButton(2);

        if (zoomAnimTimer < zoomAnimLength)
        {
            zoomAnimTimer += Time.deltaTime;
            zoom = Mathf.Lerp(zoom, zoomTarget, zoomAnimTimer / zoomAnimLength);

            float distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);
        }

        if (mouseWheelDelta != 0f)
            AdjustZoom(mouseWheelDelta);

        if (rotationDelta != 0f)
            AdjustOrbit(rotationDelta);

        if (keyHorizontalDelta != 0f || keyVerticalDelta != 0f)
            AdjustPosition(keyHorizontalDelta, keyVerticalDelta, shift);

        if (middleClick)
            AdjustMouseLook(-mouseVerticalDelta, mouseHorizontalDelta);
    }

    private void AdjustZoom(float delta)
    {
        float zoomAmount = delta * zoomSpeed * Mathf.Clamp(1f - zoomTarget, 0.05f, 1f);
        zoomTarget = Mathf.Clamp01(zoomTarget + zoomAmount);
        if (zoomAnimTimer != 0f)
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

        Vector3 direction = transform.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
        float damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
        float distance = Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) * damping * Time.deltaTime;

        Vector3 position = transform.localPosition;
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
}

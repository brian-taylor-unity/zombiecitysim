using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public float stickMinZoom;
    public float stickMaxZoom;
    public float swivelMinZoom;
    public float swivelMaxZoom;
    public float rotationSpeed;
    public float moveSpeedMinZoom;
    public float moveSpeedMaxZoom;

    private Transform swivel;
    private Transform stick;
    
    private float zoom = 1f;
    private float zoomTarget = 1f;
    private float zoomAnimLength = 1.5f;
    private float zoomAnimTimer;
    private float rotationAngle;

    // Start is called before the first frame update
    void Start()
    {
        swivel = transform.GetChild(0);
        stick = swivel.GetChild(0);
    }

    // Update is called once per frame
    void Update()
    {
        float zoomDelta = Input.GetAxis("Mouse ScrollWheel");
        if (zoomDelta != 0f)
        {
            zoomTarget = Mathf.Clamp01(zoomTarget + zoomDelta);
            zoomAnimTimer = 0f;
        }

        if (zoomAnimTimer < zoomAnimLength)
        {
            zoomAnimTimer += Time.deltaTime;
            zoom = Mathf.Lerp(zoom, zoomTarget, zoomAnimTimer / zoomAnimLength);

            float distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);

            float angle = Mathf.Lerp(swivelMinZoom, swivelMaxZoom, zoom);
            swivel.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        float rotationDelta = Input.GetAxis("Rotation");
        if (rotationDelta != 0f)
            AdjustRotation(rotationDelta);

        float xDelta = Input.GetAxis("Horizontal");
        float zDelta = Input.GetAxis("Vertical");
        if (xDelta != 0f || zDelta != 0f)
            AdjustPosition(xDelta, zDelta);
    }

    private void AdjustRotation(float delta)
    {
        rotationAngle += delta * rotationSpeed * Time.deltaTime;
        if (rotationAngle < 0f)
            rotationAngle += 360f;
        if (rotationAngle >= 360f)
            rotationAngle -= 360f;

        transform.localRotation = Quaternion.Euler(0f, rotationAngle, 0f);
    }

    private void AdjustPosition(float xDelta, float zDelta)
    {
        Vector3 direction = transform.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
        float damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
        float distance = Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) * damping * Time.deltaTime;

        Vector3 position = transform.localPosition;
        position += direction * distance;
        transform.localPosition = position;
    }
}

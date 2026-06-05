// CameraController.cs - 相机平移和缩放控制
using UnityEngine;

/// <summary>正交相机：滚轮缩放、右键/中键拖拽与 WASD 平移，随窗口更新 aspect。</summary>
public class CameraController : MonoBehaviour
{
    [Header("缩放设置")]
    public float zoomSpeed = 5f;       // 滚轮缩放灵敏度
    public float minOrthoSize = 2f;    // 最近缩放（视野最小）
    public float maxOrthoSize = 600f;  // 最远缩放（可俯瞰全图）

    [Header("平移设置")]
    public float panSpeed = 10f;       // 拖拽/键盘平移速度

    private Camera cam;
    private Vector3 lastMousePos;
    private bool isDragging = false;
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        SyncCameraAspect();

        // 初始位置：世界中心
        float center = SimulationConfig.EnvirSize * SimulationConfig.PixelPerEnvir / 2f;
        transform.position = new Vector3(center, center, -10f);
        cam.orthographicSize = 20f;
    }

    void Update()
    {
        SyncCameraAspectIfNeeded();
        HandleZoom();
        HandlePan();
        HandleKeyboardPan();
    }

    void SyncCameraAspectIfNeeded()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            SyncCameraAspect();
    }

    void SyncCameraAspect()
    {
        if (cam == null) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        cam.aspect = (float)Screen.width / Screen.height;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float newSize = cam.orthographicSize - scroll * zoomSpeed * cam.orthographicSize * 0.1f;
            cam.orthographicSize = Mathf.Clamp(newSize, minOrthoSize, maxOrthoSize);
        }
    }

    void HandlePan()
    {
        // 鼠标中键或右键拖拽平移
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            float scaleFactor = 2f * cam.orthographicSize / Screen.height;
            Vector3 move = new Vector3(-delta.x * scaleFactor, -delta.y * scaleFactor, 0);
            transform.position += move;
            lastMousePos = Input.mousePosition;
        }
    }

    void HandleKeyboardPan()
    {
        float h = 0, v = 0;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v = 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v = -1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1;

        if (Mathf.Abs(h) > 0 || Mathf.Abs(v) > 0)
        {
            float speed = panSpeed * cam.orthographicSize * Time.deltaTime;
            transform.position += new Vector3(h * speed, v * speed, 0);
        }
    }

    /// <summary>
    /// 获取当前视野内的环境格范围
    /// </summary>
    public void GetVisibleGridRange(out int minX, out int maxX, out int minY, out int maxY)
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 pos = transform.position;
        float ppe = SimulationConfig.PixelPerEnvir;

        minX = Mathf.Max(1, Mathf.FloorToInt((pos.x - halfWidth) / ppe));
        maxX = Mathf.Min(SimulationConfig.EnvirSize, Mathf.CeilToInt((pos.x + halfWidth) / ppe));
        minY = Mathf.Max(1, Mathf.FloorToInt((pos.y - halfHeight) / ppe));
        maxY = Mathf.Min(SimulationConfig.EnvirSize, Mathf.CeilToInt((pos.y + halfHeight) / ppe));
    }

    /// <summary>
    /// 获取当前可见的环境格总数
    /// </summary>
    public int GetVisibleGridCount()
    {
        int minX, maxX, minY, maxY;
        GetVisibleGridRange(out minX, out maxX, out minY, out maxY);
        return Mathf.Max(0, (maxX - minX + 1) * (maxY - minY + 1));
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using UnityEngine.Device;

public class OffAxisProjection : MonoBehaviour
{
    private float width = 0.597f;
    private float offsetX = 0f; //optional adjustment
    private float offsetY = 0f; //optional adjustment

    private float near = 0.05f;
    private float far = 100f;

    private Camera cam;
    private UdpClient udp;
    private Vector3 rawPos = new Vector3(0f, 0f, 0f);
    private Vector3 vr, vu, vn;

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, 10, 500, 30), "RawPos: " + rawPos.ToString("F3"), style);
        GUI.Label(new Rect(10, 40, 500, 30), $"Width: {width:F4}  OffsetX: {offsetX:F4}  OffsetY: {offsetY:F4}", style);
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.nearClipPlane = near;
        cam.farClipPlane = far;

        vr = Vector3.right;
        vu = Vector3.up;
        vn = Vector3.forward;

        udp = new UdpClient(5005);
        udp.Client.Blocking = false;
    }

    void ReceiveUDP()
    {
        if (udp.Available > 0)
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 5005);
            byte[] data = udp.Receive(ref ep);
            string json = Encoding.UTF8.GetString(data);

            var parsed = JsonUtility.FromJson<HeadData>(json);

            rawPos = new Vector3(-parsed.x / 1000f, -parsed.y / 1000f, parsed.z / 1000f);
        }
    }

    void ApplyOffAxis()
    {
        Vector3 pe = rawPos;

        float height = width / cam.aspect;
        Vector3 pa = new Vector3((-width / 2) + offsetX, (-height / 2) + offsetY, 0);
        Vector3 pb = new Vector3((width / 2) + offsetX, (-height / 2) + offsetY, 0);
        Vector3 pc = new Vector3((-width / 2) + offsetX, (height / 2) + offsetY, 0);

        Vector3 va = pa - pe;
        float d = -Vector3.Dot(va, vn); // distance from eye to screen plane
        if (d <= 0.001f) {
            return;
        }

        float left = Vector3.Dot(vr, va) * (near / d);
        float right = Vector3.Dot(vr, pb - pe) * (near / d);
        float bottom = Vector3.Dot(vu, va) * (near / d);
        float top = Vector3.Dot(vu, pc - pe) * (near / d);

        // Projection matrix
        Matrix4x4 proj = Matrix4x4.zero;
        proj[0, 0] = (2.0f * near) / (right - left);
        proj[0, 2] = (right + left) / (right - left);
        proj[1, 1] = (2.0f * near) / (top - bottom);
        proj[1, 2] = (top + bottom) / (top - bottom);
        proj[2, 2] = -(far + near) / (far - near);
        proj[2, 3] = -(2.0f * far * near) / (far - near);
        proj[3, 2] = -1.0f;
        cam.projectionMatrix = proj;

        cam.transform.position = pe;
        Debug.Log($"Camera position: {cam.transform.position.ToString("F4")}");
        cam.transform.rotation = Quaternion.identity;

    }

    void Update()
    {
        ReceiveUDP();
        ApplyOffAxis();

        float step = 0.001f;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed) offsetX -= step;
            if (keyboard.rightArrowKey.isPressed) offsetX += step;
            if (keyboard.upArrowKey.isPressed) offsetY += step;
            if (keyboard.downArrowKey.isPressed) offsetY -= step;
            if (keyboard.leftBracketKey.isPressed) width -= step;
            if (keyboard.rightBracketKey.isPressed) width += step;
        }
    }

    [System.Serializable]
    class HeadData
    {
        public float x;
        public float y;
        public float z;
    }
}
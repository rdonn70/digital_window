using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using UnityEngine.Device;

// Heavily borrows from Robert Kooima's Generalized Perspective Projection (2009) Paper: https://discussions.unity.com/uploads/short-url/r7D1Sc8bPTsZhNnSXHTBDJM0XEw.pdf

public class OffAxisProjection : MonoBehaviour
{
    private float width = 1.2460f; // setting a magic number baseline value for the screen width
    private float offsetX = 0f; // optional x adjustment offset
    private float offsetY = 0f; // optional y adjustment offset
    private Vector3 smoothedPos = new Vector3(0f, 0f, 0.6f);
    private float smoothing = 0.1f;

    private float near = 0.05f;
    private float far = 100f;

    private Camera cam;
    private UdpClient udp;
    private Vector3 raw_pos = new Vector3(0f, 0f, 0f);
    private Vector3 raw_pos_prev = new Vector3(0f, 0f, 0f);
    private Vector3 vr, vu, vn;

    void OnGUI()
    {
        // this is all just debug stuff, feel free to ignore
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, 10, 500, 30), "RawPos: " + raw_pos.ToString("F3"), style);
        GUI.Label(new Rect(10, 40, 500, 30), $"Width: {width:F4}  OffsetX: {offsetX:F4}  OffsetY: {offsetY:F4}  Smoothing: {smoothing:F4}", style);
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.nearClipPlane = near;
        cam.farClipPlane = far;

        // Using the power of unity, I can skip the subtraction/cross product to determine the right, up, and forward vectors :)
        vr = Vector3.right;
        vu = Vector3.up;
        vn = Vector3.forward;

        udp = new UdpClient(5005); // start listening for the data from the python program
        udp.Client.Blocking = false;
    }

    void ReceiveUDP()
    {
        if (udp.Available > 0)
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, 5005);
            byte[] data = udp.Receive(ref ip);
            string json = Encoding.UTF8.GetString(data);

            var parsed = JsonUtility.FromJson<HeadData>(json);

            raw_pos = new Vector3(-parsed.x / 1000f, -parsed.y / 1000f, parsed.z / 1000f); // correcting incoming data from the python program
        }
    }

    void ApplyOffAxis()
    {
        smoothedPos = Vector3.Lerp(raw_pos_prev, raw_pos, smoothing);
        Vector3 pe = smoothedPos;

        float height = width / cam.aspect;
        Vector3 pa = new Vector3((-width / 2) + offsetX, (-height / 2) + offsetY, 0);
        Vector3 pb = new Vector3((width / 2) + offsetX, (-height / 2) + offsetY, 0);
        Vector3 pc = new Vector3((-width / 2) + offsetX, (height / 2) + offsetY, 0);

        Vector3 va = pa - pe;
        Vector3 vb = pb - pe;
        Vector3 vc = pc - pe;

        float d = -Vector3.Dot(va, vn); // distance from eye to screen plane
        if (d <= 0.001f) {
            return; // prevent the projection from breaking if the user goes past the camera
        }

        float left = Vector3.Dot(vr, va) * (near / d);
        float right = Vector3.Dot(vr, vb) * (near / d);
        float bottom = Vector3.Dot(vu, va) * (near / d);
        float top = Vector3.Dot(vu, vc) * (near / d);

        // "Off Center Projection Matrix", taken from https://docs.unity3d.com/560/Documentation/ScriptReference/Camera-projectionMatrix.html
        Matrix4x4 proj = Matrix4x4.zero;
        proj[0, 0] = (2.0f * near) / (right - left);
        proj[0, 2] = (right + left) / (right - left);
        proj[1, 1] = (2.0f * near) / (top - bottom);
        proj[1, 2] = (top + bottom) / (top - bottom);
        proj[2, 2] = -(far + near) / (far - near);
        proj[2, 3] = -(2.0f * far * near) / (far - near);
        proj[3, 2] = -1.0f;

        cam.projectionMatrix = proj; // sets the camera's projection matrix
        cam.transform.position = pe; // update's the camera's position in world space with the head tracked location from the python program

        cam.transform.rotation = Quaternion.identity; // we care not for camera rotation (at this point in time)

        raw_pos_prev = raw_pos; // for temporal linear interpolation - sounds fancy when you add the word "temporal" :^)
    }

    void Update()
    {
        ReceiveUDP();
        ApplyOffAxis();

        // after getting the UDP packet and applying the projection, we look for keyboard inputs in order to calibrate the screen and adjust head data smoothing
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
            if (keyboard.commaKey.isPressed) smoothing = Mathf.Max(0.0f, smoothing - 0.01f);
            if (keyboard.periodKey.isPressed) smoothing = Mathf.Min(1.0f, smoothing + 0.01f);
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Heavily borrows from Robert Kooima's Generalized Perspective Projection (2009) Paper: https://discussions.unity.com/uploads/short-url/r7D1Sc8bPTsZhNnSXHTBDJM0XEw.pdf

public class OffAxisProjection : MonoBehaviour
{
    private bool calibrated = false;
    private Vector3[] calibration_points = new Vector3[3];
    private int calibration_step = 0;
    private float width = 0f;
    private float height = 0f;
    private float near = 0.05f;
    private float far = 100f;

    private float offsetX = 0f; // optional x adjustment offset
    private float offsetY = 0f; // optional y adjustment offset

    private Camera cam;
    private UdpClient udp;

    private bool keyboard_mode_toggle = false;
    private Vector3 raw_pos = new Vector3(0f, 0f, 0f);
    private Vector3 raw_pos_prev = new Vector3(0f, 0f, 0f);
    private float timestamp = 0f;
    private float timestamp_prev = 0f;
    private Vector3 vr, vu, vn;
    private Vector3 pa, pb, pc;

    private Vector3 filtered_prev = new Vector3(0f, 0f, 0f); // y_t-1
    private float min_cutoff_freq = 5.0f; // fc_min, confirugable
    private float speed_coefficient = 0.02f; // beta, configurable
    private float sample_interval = 0f; // T_e

    void Calibration()
    {
        cam.transform.position = new Vector3(0, 0, -1);
        cam.transform.rotation = Quaternion.identity;
        cam.ResetProjectionMatrix();

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            float t = -ray.origin.z / ray.direction.z;
            Vector3 hit = ray.origin + ray.direction * t;

            calibration_points[calibration_step] = hit;
            calibration_step++;

            if (calibration_step >= 3) {
                pa = calibration_points[0]; // bottom left
                pb = calibration_points[1]; // bottom right
                pc = calibration_points[2]; // top left

                width = Vector3.Distance(pa, pb);
                height = Vector3.Distance(pa, pc);

                calibrated = true;
            }
        }

    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;
        if (calibrated != false) { // debug stuff
            GUI.Label(new Rect(10, 10, 500, 30), $"{timestamp:F4}", style);
            GUI.Label(new Rect(10, 40, 500, 30), "RawPos: " + raw_pos.ToString("F3"), style);
            GUI.Label(new Rect(10, 70, 500, 30), $"Width: {width:F4}  OffsetX: {offsetX:F4}  OffsetY: {offsetY:F4}", style);
            GUI.Label(new Rect(10, 100, 500, 30), $"Beta: {speed_coefficient:F4}  fc_min: {min_cutoff_freq:F4}", style);
        } else { // calibration stuff
            if (calibration_step == 0) {
                GUI.DrawTexture(new Rect(26, (Screen.height - 13), 26, 26), Texture2D.whiteTexture);
            } else if (calibration_step == 1) {
                GUI.DrawTexture(new Rect((Screen.width - 13), (Screen.height - 13), 26, 26), Texture2D.whiteTexture);
            } else if (calibration_step == 2) {
                GUI.DrawTexture(new Rect(13, 13, 26, 26), Texture2D.whiteTexture);
            }
        }
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
        if (udp.Available > 0) {
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, 5005);
            byte[] data = udp.Receive(ref ip);
            string json = Encoding.UTF8.GetString(data);

            var parsed = JsonUtility.FromJson<HeadData>(json);

            timestamp = parsed.time;

            if (timestamp_prev <= timestamp) { // skip the packet if the timestamp is out of order... granted, could do TCP but I want that juicy speed boost :)
                raw_pos = new Vector3(-parsed.x / 1000f, -parsed.y / 1000f, parsed.z / 1000f); // correcting incoming data from the python program
                sample_interval = (timestamp - timestamp_prev) / 1000f;
                timestamp_prev = timestamp;
                ApplyOffAxis();
            }
        }
    }

    void ApplyOffAxis()
    {
        if(sample_interval <= 0f) {
            return;
        }

        // Implementation of the 1 euro filter, taken from https://github.com/MKSharaf/OneEuroFilterExplained/
        Vector3 signal_speed = (raw_pos - raw_pos_prev) / sample_interval;
        float cutoff_freq = min_cutoff_freq + (speed_coefficient * signal_speed.magnitude);

        // let k = tau / sample interval
        float k = (1 / (2 * Mathf.PI * cutoff_freq)) / sample_interval;

        Vector3 pe = (raw_pos + (k * filtered_prev)) / (1 + k);

        raw_pos_prev = raw_pos;
        filtered_prev = pe;

        pe.x += offsetX;
        pe.y += offsetY;

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
    }

    void Update()
    {
        if (!calibrated) {
            Calibration();
            return;
        }

        ReceiveUDP();

        // after getting the UDP packet and applying the projection, we look for keyboard inputs in order to calibrate the screen and adjust head data smoothing
        float step = 0.001f;
        var keyboard = Keyboard.current;
        if (keyboard != null) {
            if (keyboard_mode_toggle == false) {
                if (keyboard.leftArrowKey.wasPressedThisFrame) offsetX -= step;
                if (keyboard.rightArrowKey.wasPressedThisFrame) offsetX += step;
                if (keyboard.upArrowKey.wasPressedThisFrame) offsetY += step;
                if (keyboard.downArrowKey.wasPressedThisFrame) offsetY -= step;
                if (keyboard.semicolonKey.wasPressedThisFrame) min_cutoff_freq = Mathf.Max(0.01f, min_cutoff_freq - step);
                if (keyboard.quoteKey.wasPressedThisFrame) min_cutoff_freq += step;
                if (keyboard.commaKey.wasPressedThisFrame) speed_coefficient = Mathf.Max(0.0f, speed_coefficient - step);
                if (keyboard.periodKey.wasPressedThisFrame) speed_coefficient = Mathf.Min(1.0f, speed_coefficient + step);
                if (keyboard.equalsKey.wasPressedThisFrame) keyboard_mode_toggle = true;
            } else {
                if (keyboard.leftArrowKey.isPressed) offsetX -= step;
                if (keyboard.rightArrowKey.isPressed) offsetX += step;
                if (keyboard.upArrowKey.isPressed) offsetY += step;
                if (keyboard.downArrowKey.isPressed) offsetY -= step;
                if (keyboard.semicolonKey.isPressed) min_cutoff_freq = Mathf.Max(0.01f, min_cutoff_freq - step);
                if (keyboard.quoteKey.isPressed) min_cutoff_freq += step;
                if (keyboard.commaKey.isPressed) speed_coefficient = Mathf.Max(0.0f, speed_coefficient - step);
                if (keyboard.periodKey.isPressed) speed_coefficient = Mathf.Min(1.0f, speed_coefficient + step);
                if (keyboard.equalsKey.wasPressedThisFrame) keyboard_mode_toggle = false;
            }
        }
    }

    [System.Serializable]
    class HeadData
    {
        public float x;
        public float y;
        public float z;
        public float time;
    }
}

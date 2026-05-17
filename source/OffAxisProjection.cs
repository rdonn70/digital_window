using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public class OffAxisProjection : MonoBehaviour
{
    private Camera cam;
    private UdpClient udp;

    private bool calibrated = false;
    private bool show_debug_gui = true;
    private float near = 0.01f; // was 0.05f
    private float far = 250f;

    private float offsetX = 0f; // optional x adjustment offset
    private float offsetY = 0f; // optional y adjustment offset
    private float offsetZ = 0f; // optional z adjustment offset
    private float sensitivityX = 1.0f; // optional x effect scaling (horizontal sensitivity) recommended = 1.8f
    private float sensitivityY = 1.0f; // optional y effect scaling (vertical sensitivity) recommended = 1.8f
    private float sensitivityZ = 1.0f; // optional z effect scaling (depth sensitivity) recommended = 0.8f?
    private float offsetZ_window = 0.7f;

    private bool keyboard_mode_toggle = false;
    private Vector3 raw_pos = new Vector3(0f, 0f, 0f);
    private float timestamp = 0f;
    private float timestamp_prev = 0f;

    private Vector3 Xs, Ys, Zs;
    private double screen_height, screen_width;
    private float aspect_ratio;
    private float screen_diagonal = 28.0f;
    private Vector3 LL = new Vector3(-0.3f, 0f, 0.7f);
    private Vector3 LR = new Vector3(0.3f, 0f, 0.7f);
    private Vector3 UL = new Vector3(-0.3f, 0.335f, 0.7f);
    private Vector3 UR = new Vector3(0.3f, 0.335f, 0.7f);
    private Matrix4x4 RotMat;

    private Vector3 filtered_prev = new Vector3(0f, 0f, 0f); // y_t-1
    private float min_cutoff_freq = 5.0f; // fc_min, confirugable
    private float speed_coefficient = 0.02f; // beta, configurable
    private float sample_interval = 0f; // T_e

    private LineRenderer calibration_line;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.nearClipPlane = near;
        cam.farClipPlane = far;

        cam.transform.position = new Vector3(0, 0, 0);
        cam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        cam.ResetProjectionMatrix();

        aspect_ratio = (float)Screen.width / Screen.height;

        calibration_line = gameObject.AddComponent<LineRenderer>();
        calibration_line.positionCount = 5;
        calibration_line.startWidth = 0.01f;
        calibration_line.endWidth = 0.01f;
        calibration_line.material = new Material(Shader.Find("Sprites/Default"));
        calibration_line.startColor = Color.red;
        calibration_line.endColor = Color.red;
        calibration_line.useWorldSpace = true;

        calibration_line.enabled = true;

        udp = new UdpClient(5005); // start listening for the data from the python program
        udp.Client.Blocking = false;
    }

    void Calibration()
    {
        screen_height = screen_diagonal / (Mathf.Sqrt((aspect_ratio * aspect_ratio) + 1));
        screen_width = aspect_ratio * screen_height;

        screen_height = screen_height / 39.37; // convert to meters
        screen_width = screen_width / 39.37; // convert to meters

        LL = new Vector3((-((float)screen_width) / 2), 0f, offsetZ_window);
        LR = new Vector3(((float)screen_width / 2), 0f, offsetZ_window);
        UL = new Vector3((-((float)screen_width) / 2), (float)screen_height, offsetZ_window);
        UR = new Vector3(((float)screen_width / 2), (float)screen_height, offsetZ_window);

        calibration_line.SetPosition(0, LL); // bottom left
        calibration_line.SetPosition(1, LR); // to bottom right
        calibration_line.SetPosition(2, UR); // to top right
        calibration_line.SetPosition(3, UL); // to top left
        calibration_line.SetPosition(4, LL); // back to bottom left

        cam.transform.position = new Vector3(offsetX, offsetY, offsetZ);
    }

    void OnGUI()
    {
        if (show_debug_gui == true)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(10, 10, 500, 30), $"{timestamp:F4}", style);
            GUI.Label(new Rect(10, 40, 500, 30), "RawPos: " + raw_pos.ToString("F3"), style);
            GUI.Label(new Rect(10, 70, 500, 30), $"OffsetX: {offsetX:F4}  OffsetY: {offsetY:F4}  OffsetZ: {offsetZ:F4}", style);
            GUI.Label(new Rect(10, 100, 500, 30), $"Beta: {speed_coefficient:F4}  fc_min: {min_cutoff_freq:F4}", style);
            GUI.Label(new Rect(10, 130, 500, 30), $"X Sensitivity: {sensitivityX:F4}  Y Sensitivity: {sensitivityY:F4}  Z Sensitivity: {sensitivityZ:F4}", style);
            GUI.Label(new Rect(10, 160, 500, 30), $"Screen Diagonal (in inches): {screen_diagonal:F4}  Screen Z Offset: {offsetZ_window:F4}", style);
        }
    }

    void ReceiveUDP()
    {
        if (udp.Available > 0)
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, 5005);
            byte[] data = udp.Receive(ref ip);
            string json = Encoding.UTF8.GetString(data);

            var parsed = JsonUtility.FromJson<HeadData>(json);

            timestamp = parsed.time;

            if (timestamp_prev <= timestamp)
            { // skip the packet if the timestamp is out of order... granted, could do TCP but I want that juicy speed boost :)
                raw_pos = new Vector3(-parsed.x / 1000f, -parsed.y / 1000f, -parsed.z / 1000f); // correcting incoming data from the python program
                sample_interval = (timestamp - timestamp_prev) / 1000f;
                timestamp_prev = timestamp;
                ApplyOffAxis();
            }
        }
    }

    void ApplyOffAxis()
    {
        if (sample_interval <= 0f)
        { //skips update if timestamps are out of order
            return;
        }

        // Implementation of the 1 euro filter, taken from https://github.com/MKSharaf/OneEuroFilterExplained/
        Vector3 signal_speed = (raw_pos - filtered_prev) / sample_interval;
        float cutoff_freq = min_cutoff_freq + (speed_coefficient * signal_speed.magnitude);

        // let k = tau / sample interval
        float k = (1 / (2 * Mathf.PI * cutoff_freq)) / sample_interval;

        Vector3 pe = (raw_pos + (k * filtered_prev)) / (1 + k);
        filtered_prev = pe;

        pe.x += offsetX;
        pe.y += offsetY;
        pe.z += offsetZ;

        pe.x *= sensitivityX;
        pe.y *= sensitivityY;
        pe.z *= sensitivityZ;

        Vector3 pes = pe - LL;
        float L = Vector3.Dot(pes, Xs);
        float R = (LR - LL).magnitude - L;
        float B = Vector3.Dot(pes, Ys);
        float T = (UL - LL).magnitude - B;
        float distance = Vector3.Dot(pes, Zs);

        float left = -L * near / distance;
        float right = R * near / distance;
        float bottom = -B * near / distance;
        float top = T * near / distance;

        Matrix4x4 frustum = Matrix4x4.Frustum(left, right, bottom, top, near, far);
        cam.projectionMatrix = frustum;

        Matrix4x4 ViewMat = Matrix4x4.Translate(-pe) * RotMat;
        cam.worldToCameraMatrix = ViewMat;
    }

    void Update()
    {
        // look for keyboard inputs in order to calibrate the screen and adjust head data smoothing
        float step = 0.001f;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard_mode_toggle == false)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) offsetX -= step;
                if (keyboard.rightArrowKey.wasPressedThisFrame) offsetX += step;
                if (keyboard.upArrowKey.wasPressedThisFrame) offsetY += step;
                if (keyboard.downArrowKey.wasPressedThisFrame) offsetY -= step;
                if (keyboard.leftBracketKey.wasPressedThisFrame) offsetZ -= step;
                if (keyboard.rightBracketKey.wasPressedThisFrame) offsetZ += step;
                if (keyboard.aKey.wasPressedThisFrame) offsetZ_window -= step;
                if (keyboard.dKey.wasPressedThisFrame) offsetZ_window += step;
                if (keyboard.sKey.wasPressedThisFrame) screen_diagonal -= 0.1f;
                if (keyboard.wKey.wasPressedThisFrame) screen_diagonal += 0.1f;
                if (keyboard.gKey.wasPressedThisFrame) sensitivityX -= 0.1f;
                if (keyboard.tKey.wasPressedThisFrame) sensitivityX += 0.1f;
                if (keyboard.hKey.wasPressedThisFrame) sensitivityY -= 0.1f;
                if (keyboard.yKey.wasPressedThisFrame) sensitivityY += 0.1f;
                if (keyboard.jKey.wasPressedThisFrame) sensitivityZ -= 0.1f;
                if (keyboard.uKey.wasPressedThisFrame) sensitivityZ += 0.1f;
                if (keyboard.semicolonKey.wasPressedThisFrame) min_cutoff_freq = Mathf.Max(0.01f, min_cutoff_freq - step);
                if (keyboard.quoteKey.wasPressedThisFrame) min_cutoff_freq += step;
                if (keyboard.commaKey.wasPressedThisFrame) speed_coefficient = Mathf.Max(0.0f, speed_coefficient - step);
                if (keyboard.periodKey.wasPressedThisFrame) speed_coefficient = Mathf.Min(1.0f, speed_coefficient + step);
                if (keyboard.equalsKey.wasPressedThisFrame) keyboard_mode_toggle = true;
            }
            else
            {
                if (keyboard.leftArrowKey.isPressed) offsetX -= step;
                if (keyboard.rightArrowKey.isPressed) offsetX += step;
                if (keyboard.upArrowKey.isPressed) offsetY += step;
                if (keyboard.downArrowKey.isPressed) offsetY -= step;
                if (keyboard.leftBracketKey.isPressed) offsetZ -= step;
                if (keyboard.rightBracketKey.isPressed) offsetZ += step;
                if (keyboard.aKey.isPressed) offsetZ_window -= step;
                if (keyboard.dKey.isPressed) offsetZ_window += step;
                if (keyboard.sKey.isPressed) screen_diagonal -= 0.1f;
                if (keyboard.wKey.isPressed) screen_diagonal += 0.1f;
                if (keyboard.gKey.isPressed) sensitivityX -= 0.1f;
                if (keyboard.tKey.isPressed) sensitivityX += 0.1f;
                if (keyboard.hKey.isPressed) sensitivityY -= 0.1f;
                if (keyboard.yKey.isPressed) sensitivityY += 0.1f;
                if (keyboard.jKey.isPressed) sensitivityZ -= 0.1f;
                if (keyboard.uKey.isPressed) sensitivityZ += 0.1f;
                if (keyboard.semicolonKey.isPressed) min_cutoff_freq = Mathf.Max(0.01f, min_cutoff_freq - step);
                if (keyboard.quoteKey.isPressed) min_cutoff_freq += step;
                if (keyboard.commaKey.isPressed) speed_coefficient = Mathf.Max(0.0f, speed_coefficient - step);
                if (keyboard.periodKey.isPressed) speed_coefficient = Mathf.Min(1.0f, speed_coefficient + step);
                if (keyboard.equalsKey.wasPressedThisFrame) keyboard_mode_toggle = false;
            }
            if (keyboard.backquoteKey.wasPressedThisFrame)
            { // turn on/off GUI debug text
                if (show_debug_gui == true)
                {
                    show_debug_gui = false;
                }
                else
                {
                    show_debug_gui = true;
                }
            }
            if (keyboard.enterKey.isPressed)
            { //finish calibration by hitting enter key
                if (calibrated == false)
                {
                    LL = new Vector3(LL.x - offsetX, LL.y - offsetY, LL.z - offsetZ);
                    LR = new Vector3(LR.x - offsetX, LR.y - offsetY, LR.z - offsetZ);
                    UL = new Vector3(UL.x - offsetX, UL.y - offsetY, UL.z - offsetZ);
                    UR = new Vector3(UR.x - offsetX, UR.y - offsetY, UR.z - offsetZ);
                    offsetX = 0;
                    offsetY = 0;
                    offsetZ = 0;
                    cam.transform.position = new Vector3(0, 0, 0);
                    cam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    cam.ResetProjectionMatrix();
                    calibration_line.SetPosition(0, LL); // bottom left
                    calibration_line.SetPosition(1, LR); // to bottom right
                    calibration_line.SetPosition(2, UR); // to top right
                    calibration_line.SetPosition(3, UL); // to top left
                    calibration_line.SetPosition(4, LL); // back to bottom left
                    calibration_line.enabled = false;

                    // Directly from Dave Pape (https://www.evl.uic.edu/pape/caveproj/):
                    Xs = LR - LL;
                    Xs = (LR - LL) / Xs.magnitude;

                    Ys = UL - LL;
                    Ys = (UL - LL) / Ys.magnitude;

                    Zs = -Vector3.Cross(Xs, Ys);

                    RotMat = Matrix4x4.zero;
                    RotMat[0, 0] = Xs.x;
                    RotMat[0, 1] = Ys.x;
                    RotMat[0, 2] = Zs.x;
                    RotMat[1, 0] = Xs.y;
                    RotMat[1, 1] = Ys.y;
                    RotMat[1, 2] = Zs.y;
                    RotMat[2, 0] = Xs.z;
                    RotMat[2, 1] = Ys.z;
                    RotMat[2, 2] = Zs.z;
                    RotMat[3, 3] = 1.0f;

                    RotMat = RotMat.inverse;

                    calibrated = true;
                }
            }
        }

        if (calibrated == false)
        {
            Calibration();
        }
        else
        {
            ReceiveUDP();
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

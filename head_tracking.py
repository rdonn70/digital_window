import time
import socket
import json
import cv2
import numpy as np
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

UDP_IP = "127.0.0.1"
UDP_PORT = 5005
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

latest_result = None
last_pos = np.array([0.0, 0.0, 0.0])
velocity = np.array([0.0, 0.0, 0.0])
frames_missing = 0

def print_result(result, output_image, timestamp_ms):
    global latest_result
    latest_result = result

BaseOptions = mp.tasks.BaseOptions
FaceLandmarker = mp.tasks.vision.FaceLandmarker
FaceLandmarkerOptions = mp.tasks.vision.FaceLandmarkerOptions
FaceLandmarkerResult = mp.tasks.vision.FaceLandmarkerResult
VisionRunningMode = mp.tasks.vision.RunningMode

options = FaceLandmarkerOptions(
    base_options=BaseOptions(model_asset_path='face_landmarker.task'),
    running_mode=VisionRunningMode.LIVE_STREAM,
    result_callback=print_result)

cap = cv2.VideoCapture(0)
start_time = time.time()

with FaceLandmarker.create_from_options(options) as landmarker:
    while True:
        retval, frame = cap.read()

        h, w = frame.shape[:2]

        frame_timestamp_ms = int((time.time() - start_time) * 1000)
        
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

        landmarker.detect_async(mp_image, frame_timestamp_ms)
        
        if(latest_result and latest_result.face_landmarks):

            landmarks = latest_result.face_landmarks[0]

            left_eye = landmarks[33]
            right_eye = landmarks[263]
            nose_bridge = landmarks[168]

            lx, ly = int(left_eye.x * w), int(left_eye.y * h)
            rx, ry = int(right_eye.x * w), int(right_eye.y * h)
            nx, ny = int(nose_bridge.x * w), int(nose_bridge.y * h)

            cv2.circle(frame, (nx, ny), 5, (0,255,0), -1)

            focal_length = w
            cx, cy = (w / 2), (h / 2)

            pixel_eye_distance = np.sqrt((lx - rx)**2 + (ly - ry)**2)

            REAL_EYE_DISTANCE_MM = 63.5

            if(pixel_eye_distance > 0):
                Z = (REAL_EYE_DISTANCE_MM * focal_length) / pixel_eye_distance
                X = ((nx - cx) * Z) / focal_length
                Y = ((ny - cy) * Z) / focal_length

                new_pos = np.array([X, Y, Z])
                velocity = new_pos - last_pos
                last_pos = new_pos
                frames_missing = 0

        else:
            frames_missing += 1

            if(frames_missing < 5):
                last_pos = last_pos + velocity
                velocity = velocity * 0.6
            else:
                velocity = velocity * 0

            X, Y, Z = last_pos

        cv2.putText(frame, f"X: {int(X)} mm", (20,40), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)
        cv2.putText(frame, f"Y: {int(Y)} mm", (20,70), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)
        cv2.putText(frame, f"Z: {int(Z)} mm", (20,100), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)     
        cv2.imshow("Head Position", frame)

        head_data = {'x': X, 'y': Y, 'z': Z}
        sock.sendto(json.dumps(head_data).encode(), (UDP_IP, UDP_PORT))
        
        if(cv2.waitKey(1) & 0xFF == 27):
            break

cap.release()
cv2.destroyAllWindows()
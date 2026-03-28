import sys
import os
import time
import socket
import json
import cv2
import numpy as np
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

def resource_path(relative_path):
    try:
        base_path = sys._MEIPASS
    except:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)

def call_result(result, output_image, timestamp_ms):
    global latest_result
    latest_result = result

######
#INIT#
######

UDP_IP = "127.0.0.1"
UDP_PORT = 5005
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

latest_result = None
last_pos = np.array([0.0, 0.0, 0.0])
velocity = np.array([0.0, 0.0, 0.0])
frames_missing = 0
x,y,z = 0.0, 0.0, 0.0

BaseOptions = mp.tasks.BaseOptions
FaceLandmarker = mp.tasks.vision.FaceLandmarker
FaceLandmarkerOptions = mp.tasks.vision.FaceLandmarkerOptions
FaceLandmarkerResult = mp.tasks.vision.FaceLandmarkerResult
VisionRunningMode = mp.tasks.vision.RunningMode
model_path = resource_path("face_landmarker.task")                                                                                          #checks to see if it's packed with .exe (sys._MEIPASS) or in root 

options = FaceLandmarkerOptions(base_options=BaseOptions(model_asset_path=model_path), running_mode=VisionRunningMode.LIVE_STREAM, result_callback=call_result)

######

cap = cv2.VideoCapture(0)                                                                                                                   #start webcam capture
start_time = time.time()                                                                                                                    #get initial time for timestamping frames

with FaceLandmarker.create_from_options(options) as landmarker:
    while True:
        retval, frame = cap.read()                                                                                                          #get a frame from the webcam

        h, w = frame.shape[:2]                                                                                                              #get frame height and width

        frame_timestamp_ms = int((time.time() - start_time) * 1000)
        
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

        landmarker.detect_async(mp_image, frame_timestamp_ms)                                                                               #do async detection to avoid blocking

        if(latest_result and latest_result.face_landmarks):

            landmarks = latest_result.face_landmarks[0]

            left_eye = landmarks[263]
            right_eye = landmarks[33]

            lx, ly = (left_eye.x * w), (left_eye.y * h)                                                                                     #get x,y pixel locations for the left eye corner
            rx, ry = (right_eye.x * w), (right_eye.y * h)                                                                                   #get x,y pixel locations for the right eye corner
            nx, ny = ((lx + rx) / 2), ((ly + ry) / 2)                                                                                       #get x,y pixel locations for the midpoint of the eyes

            cv2.circle(frame, (int(nx), int(ny)), 5, (0,255,0), -1)                                                                         #debug to draw a green circle on the nose eye midpoint (location for x,y,z calculation)

            focal_length = w                                                                                                                #approximation of focal length, will cause drift without proper calibration... will eventually replace with OpenCV calibration
            cx, cy = (w / 2), (h / 2)                                                                                                       #getting the x,y pixel locations of the center of the frame to use as the principal point... will eventually replace with OpenCV calibration

            pixel_eye_distance = np.sqrt((lx - rx)**2 + (ly - ry)**2)                                                                       #disance between iris centers using 2 dimension euclidean formula

            real_eye_distance_mm = 85.2                                                                                                     #takes the average Outer Canthal Distance of ~85.2 mm from this paper: https://pmc.ncbi.nlm.nih.gov/articles/PMC6408655/

            if(pixel_eye_distance > 0):
                z = (real_eye_distance_mm * focal_length) / pixel_eye_distance                                                              #depth estimation from pinhole model, using iris frame locations and estimated eye distance
                x = ((nx - cx) * z) / focal_length                                                                                          #x, calculated from same pinhole model equation, accounting for offset from principal point
                y = ((ny - cy) * z) / focal_length                                                                                          #y, calculated from same pinhole model equation, accounting for offset from principal point

                new_pos = np.array([x, y, z]) 
                velocity = new_pos - last_pos
                last_pos = new_pos
                frames_missing = 0                                                                                                          #reset missing frames count once we get a good frame back

        else:                                                                                                                               #since this is running in async, we will try and fill in frames by guessing where the person was moving to last.
            frames_missing += 1

            if(frames_missing < 5):
                last_pos = last_pos + velocity
                velocity = velocity * 0.6
            else:
                velocity = velocity * 0                                                                                                     #if there's too many consecutive missed frames, just give up

            x, y, z = last_pos

        cv2.putText(frame, f"{int(frame_timestamp_ms)}", (20,20), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)                          #more debugging code
        cv2.putText(frame, f"X: {int(x)} mm", (20,50), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)
        cv2.putText(frame, f"Y: {int(y)} mm", (20,80), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)
        cv2.putText(frame, f"Z: {int(z)} mm", (20,110), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255,255,255), 2)     
        cv2.imshow("Head Position", frame)

        head_data = {'x': x, 'y': y, 'z': z, 'time': frame_timestamp_ms}
        sock.sendto(json.dumps(head_data).encode(), (UDP_IP, UDP_PORT))                                                                     #packs this data and sends it over UDP to port 5005
        
        if(cv2.waitKey(1) & 0xFF == 27):
            break

cap.release()
cv2.destroyAllWindows()

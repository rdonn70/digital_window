# Digital Window (WIP)
Project to create a CAVE/AR type of display based on head tracking data + off-axis projection. Currently, only head tracking is present, currently working on fine-tuning the head tracking and Unity script.    
# Requirements
Face Landmark Model: https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task  
Unity Version 6000.3.9f1  
Webcam  
# Setup
1. Create a new folder on your desktop, this will hold the landmark detection Python script (head_tracking.py) and Face Landmark Model (https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task).  
2. Create a new Unity Project using Unity Version 6000.3.9f1.  
3. Put a default camera in the Unity Project at the world origin (0, 0, 0). Assign C# file "OffAxisProjection.cs" to the camera.  
4. Run Python Script (head_tracking.py), Execute the game in Unity.  
5. Calibrate the camera using arrow keys, and square bracket keys on keyboard.  

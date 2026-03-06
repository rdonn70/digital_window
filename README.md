# Digital Window (WIP)
Project to create a CAVE (Cave Automatic Virtual Environment)-type of display on a projector, based on head tracking data + off-axis projection. By tracking the viewer's head position with a webcam and dynamically adjusting the camera projection matrix, the display behaves like a window into a 3D world rather than a flat monitor.  
  
A prototype is currently available for download **(Current Version: 0.1)**.  
  
# How It Works  
1. A webcam captures the user's face.  
2. MediaPipe detects facial landmarks and estimates head position.  
3. The head position is streamed to Unity using UDP (on port 5005).  
4. Unity modifies the camera projection matrix using off-axis projection.  
5. The rendered scene updates perspective based on the viewer's head movement.  
  
# Running from Build (recommended)  
1. Download the latest release from GitHub.
2. Extract .zip to your computer.
3. Make sure you have a webcam connected (current ideal setup is webcam mounted in the center of the monitor and on top of the primary display while facing the user).  
4. Run "launcher.exe".
  
## Controls
### Adjusting the Screen
- Use Left/Right arrow keys for X offset.  
- Use Up/Down arrow keys for Y offset.  
- Use Left/Right bracket keys for monitor width adjustment.  
- Use Comma/Period keys for smoothing.
  
# Compiling from Source  
## Python Setup  
1. Download the Face Landmark Model from Google (https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task).  
2. Download Python (version 3.13.4).  
3. Install the library dependencies: "pip install opencv-python==4.13.0.92", "pip install numpy==2.4.2", "pip install mediapipe==0.10.32", "pip install pyinstaller==6.18.0".  
4. From the source folder on this GitHub, download "head_tracking.py" and "launcher.py" to a development folder of your choice. Feel free to modify this file.  
5. Move the "face_landmarker.task" file into the same development folder.
7. When you are done modifying "head_tracking.py", navigate to your development folder in cmd prompt and run the following commands:  
   **pyinstaller --onefile head_tracking.py --collect-all mediapipe --add-data "face_landmarker.task;."**  
   **pyinstaller --onefile launcher.py**  
## Unity Project Setup  
1. Download Unity (version 6000.3.9f1).  
2. Create a new project titled "Digital Window", and place a default Camera at position (0, 0, 0) with rotation (0, 0, 0) and scale (1, 1, 1).  
3. From the source folder on this GitHub, download "OffAxisProjection.cs" and move it to your project's asset folder.  
4. Assign the "OffAxisProjection.cs" file as a script component to your camera.  
5. Create whatever scene you want in front of the camera, when you are done, build the game to the same development folder where "launcher.exe", "face_landmarker.task", and "head_tracking.exe" are located.
6. Run "launcher.exe" to start the project.
  
# Current Issues  
1. **Perspective Jittering in Engine.** This is most likely due to the poor filtering I am doing in the current implementation. I attempted to do a linear interpolation without realizing that it just introduces lag to the system. The correct way of doing this will most likely involve implementing a one euro filter on the Unity side of things to solve the noisy data stream coming in from the current face capture setup.  
2. **Y-Axis Adjustments Not Correct.** The intended offset adjustment for the y-axis is a translation, not a camera tilt. This is most likely due to an incorrect sign upon importing the data, or I am putting the y-axis offset in the wrong place.  
3. **Keyboard Inputs.** Current keyboard inputs are extremely sensitive. This should be a simple fix of preventing values from changing when the key is held down.  
4. **Latency and Missed-Frame Prediction.** I will probably never be satisfied with this, but I believe there could be big improvements to the latency from Capture -> UDP -> Unity. I also believe the current missed-frame prediction (inside the "head_tracking.py" script) is not working perfectly, and could be improved through better mediapipe implementation and adding optional camera calibration.  
  
# REFERENCES  
- **Generalized Perspective Projection _(Robert Kooima, 2009)_.** (Source: https://discussions.unity.com/uploads/short-url/r7D1Sc8bPTsZhNnSXHTBDJM0XEw.pdf)  
- **Unity API Documentation.** (Source: https://docs.unity3d.com/560/Documentation/ScriptReference/Camera-projectionMatrix.html)  
- **Google MediaPipe Documentation.** (Source: https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker)  

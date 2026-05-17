# Unity/Python Digital Window
A project attempting to create a digital window on a monitor/projector, based on head tracking data and using off-axis projection. By tracking the viewer's head position with a webcam and adjusting the camera projection matrix, the display behaves like a window into a digital world.  
  
A prototype is currently available for download **(Current Version: 0.6)**.  
  
Youtube Preview: https://www.youtube.com/playlist?list=PLalW30gFUM7ttZJ-8__12-GfoM4lg7Qoj
  
![ezgif-6b751e48e957a0e7](https://github.com/user-attachments/assets/d244120e-34fd-406c-8d30-84d99c7be26a)
  
# How It Works  
1. A webcam captures the user's face.  
2. MediaPipe detects face landmarks and roughly estimates the viewer's head position.  
3. The head position data is streamed to Unity using UDP (on port 5005).  
4. Unity modifies the camera projection matrix using cleaned up head position data.  
5. The rendered scene updates perspective based on the viewer's head movement.  
  
# Running from Build (recommended)  
1. Download the latest release from GitHub.  
2. Extract .zip to your desktop.  
3. Make sure you have a webcam connected (current ideal setup is webcam mounted in the center of the monitor and on top of the primary display while facing the user).  
4. Run "launcher.exe".  
  
## Controls  
### Calibrating/Adjusting the Screen  
- Use Equal key to toggle between continuous and step adjustments.  
- Use Left/Right arrow keys for X offset.  
- Use Up/Down arrow keys for Y offset.  
- Use Left/Right bracket keys for Z offset.  
- Use W/S keys to adjust screen diagonal.  
- Use T/G keys to adjust X sensitivity.  
- Use Y/H keys to adjust Y sensitivity.  
- Use U/J keys to adjust Z sensitivity.  
- Use Semi-Colon/Quote keys for minimum cutoff frequency (One Euro Smoothing).  
- Use Comma/Period keys for speed coefficient (One Euro Smoothing).  
- Use Tilda/Grave key to enable and disable debug UI.  
  
**WHEN DONE WITH THE IN-GAME CALIBRATION, HIT THE ENTER KEY TO BEGIN THE PROJECTION**  
  
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
1. **Latency and Missed-Frame Prediction.** I will probably never be satisfied with this, but I believe there could be big improvements to the latency from Capture -> UDP -> Unity. I also believe the current missed-frame prediction (inside the "head_tracking.py" script) is not working perfectly, and could be improved through better mediapipe implementation and adding camera calibration for depth (current plan is to have an initial "align your face" graphic to make this system camera-agnostic and have a more reliable depth calculation).
2. **Small Jittering and Distortion.** There is still some jittering and issues with the projection stemming from how the python script calculates the cyclopean eye. To attempt to stabilize the projection, the current version uses the outer canthal distance (of which I am still trying to find papers that have an average from a very large and diverse sample of people). I added some modifiers for X,Y,Z on the estimated eye position which should help to minimize jitter by reducing how much head movements effect the projection.  
  
# REFERENCES  
- **Computing the CAVE Projection Transformation _(Dave Pape, 2005)_.** (Source: https://www.evl.uic.edu/pape/caveproj/)  
- **One Euro Filter Explained _(MKSharaf, 2026)_.** (Source: https://github.com/MKSharaf/OneEuroFilterExplained/)  
- **Google MediaPipe Documentation.** (Source: https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker)  

# Unity/Python Digital Window
Project to create a CAVE-type of display on a monitor/projector, based on head tracking data and using off-axis projection. By tracking the viewer's head position with a webcam and dynamically adjusting the camera projection matrix, the display behaves like a window into a digital world.  
  
A prototype is currently available for download **(Current Version: 0.4)**.  
  
Youtube Preview: https://www.youtube.com/watch?v=DHONF42AUAg  
  
![digital_window](https://github.com/user-attachments/assets/573b87e9-a49b-4eaa-96a5-44e5822e7ab6)  
  
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
- Use Equal button to toggle between continuous and step adjustments.  
- Use Left/Right arrow keys for X offset.  
- Use Up/Down arrow keys for Y offset.  
- Used Left/Right bracket keys for Z offset.  
- Use Semi-Colon/Quote keys for minimum cutoff frequency (One Euro Smoothing).  
- Use Comma/Period keys for speed coefficient (One Euro Smoothing).  
  
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
1. **Latency and Missed-Frame Prediction.** I will probably never be satisfied with this, but I believe there could be big improvements to the latency from Capture -> UDP -> Unity. I also believe the current missed-frame prediction (inside the "head_tracking.py" script) is not working perfectly, and could be improved through better mediapipe implementation and adding optional camera calibration.
2. **Small Jittering and Distortion.** There is still some small jittering in the system despite the one euro filter, and some weird behavior when moving your head closer to/further away from the monitor. This might just be some fine tuning or a bug in the projection matrix.  
3. **Initial Screen Calibration.** I have created a calibration system where, upon startup, you attempt to lineup the red box with the screen borders to the best of your ability. Currently, the values of this rectangle are hard coded. In the next update, I plan on implementing the ability to change the height/width of the display to accomodate different monitors.  
  
# REFERENCES  
- **Computing the CAVE Projection Transformation _(Dave Pape, 2005)_.** (Source: https://www.evl.uic.edu/pape/caveproj/)  
- **One Euro Filter Explained _(MKSharaf, 2026)_.** (Source: https://github.com/MKSharaf/OneEuroFilterExplained/)  
- **Google MediaPipe Documentation.** (Source: https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker)  

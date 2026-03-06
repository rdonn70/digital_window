import subprocess

tracker = subprocess.Popen(["head_tracking.exe"])
game = subprocess.Popen(["Digital Window.exe"])

game.wait()
tracker.terminate()
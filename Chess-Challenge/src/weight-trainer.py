import subprocess
import os

os.chdir("Chess-Challenge/")
result = subprocess.run(["dotnet", "run"], capture_output=True, text=True, check=False)
print(float(result.stdout))
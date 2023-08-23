import subprocess
import os
import pygad

os.chdir("Chess-Challenge/")
weight_path = "src/My Bot/weights.txt"

def fitness_func(ga_instance, solution, solution_idx):
    # set up weights in file
    with open(weight_path, "w") as file:
        for weight in solution:
            file.write(str(weight) + '\n')

    # run chess code and read results
    result = subprocess.run(["dotnet", "run"], capture_output=True, text=True, check=False)
    return float(result.stdout)
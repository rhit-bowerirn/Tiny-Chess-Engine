import subprocess
import os
import pygad

num_generations = 5
pop_size = 4
num_parents_mating = 4
parent_selection_type = "rank" # sss, rws, sus, rank, random, tournament
keep_elitism = 0

crossover_type = "uniform" #single_point, two_points, uniform, scattered, None
crossover_probability = 0.6 

mutation_type = "random" #random, swap, inversion, scramble, adaptive, None
mutation_probability = 0.5 
mutation_low_change = -1
mutation_high_change = 1

num_weights = 16 
weight_type = int #int or float
init_range_low = 0
init_range_high = 5

os.chdir("Chess-Challenge/")
weight_path = "src/My Bot/weights.txt"  

def fitness_func(ga_instance, solution, solution_idx):
    print("called ", ga_instance.generations_completed, " ", solution_idx)
    # set up weights in file
    with open(weight_path, "w") as file:
        for weight in solution:
            file.write(str(weight) + '\n')

    # run chess code and read results
    result = subprocess.run(["dotnet", "run"], capture_output=True, text=True, check=False)
    return 0.01 * float(result.stdout)

def on_generation(ga_instance):
    print("Finished generation ", ga_instance.generations_completed)
    print("Fitnesses: ", ga_instance.last_generation_fitness)


ga_instance = pygad.GA(num_generations=num_generations,
                       num_parents_mating=num_parents_mating,
                       fitness_func=fitness_func,
                       sol_per_pop=pop_size,
                       num_genes=num_weights,
                       gene_type=weight_type,
                       init_range_low=init_range_low,
                       init_range_high=init_range_high,
                       parent_selection_type=parent_selection_type,
                       keep_elitism=keep_elitism,
                       crossover_type=crossover_type,
                       crossover_probability=crossover_probability,
                       mutation_type=mutation_type,
                       mutation_percent_genes=mutation_probability,
                       random_mutation_min_val=mutation_low_change,
                       random_mutation_max_val=mutation_high_change,
                       allow_duplicate_genes=True,
                       save_best_solutions=True,
                       save_solutions=True,
                       on_generation=on_generation)

ga_instance.run()

solution, solution_fitness, solution_idx = ga_instance.best_solution()
print("Parameters of the best solution : {solution}".format(solution=solution))
print("Fitness value of the best solution = {solution_fitness}".format(solution_fitness=solution_fitness))

os.chdir("..")
if not os.path.exists("plots"): 
    os.mkdir("plots")

# ga_instance.plot_fitness(save_dir="plots/fitness")
ga_instance.plot_genes(save_dir="plots/genes", solutions="best")
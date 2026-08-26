using System;
using System.IO;

namespace LPR_381_Project
{
    internal class Program
    {
        private static LinearModel currentModel;

        static void Main(string[] args)
        {
            Console.Title = "LPR381 Linear and Integer Programming";

            while (true)
            {
                Console.Clear();
                Console.WriteLine("==============================================");
                Console.WriteLine("LPR381 PROGRAMMING PROJECT");
                Console.WriteLine("==============================================");
                Console.WriteLine("1. Load input model");
                Console.WriteLine("2. Branch & Bound Simplex");
                Console.WriteLine("3. Branch & Bound Knapsack");
                Console.WriteLine("4. Display current model");
                Console.WriteLine("5. Exit");
                Console.WriteLine();
                Console.Write("Select option: ");

                string choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            LoadModel();
                            break;

                        case "2":
                            RunBranchAndBoundSimplex();
                            break;

                        case "3":
                            RunBranchAndBoundKnapsack();
                            break;

                        case "4":
                            DisplayModel();
                            break;

                        case "5":
                            return;

                        default:
                            Console.WriteLine("Invalid menu option.");
                            Pause();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("ERROR: " + ex.Message);
                    Pause();
                }
            }
        }

        private static void LoadModel()
        {
            Console.Write("Enter input file path: ");
            string path = Console.ReadLine();

            currentModel = LinearModel.LoadFromFile(path);

            Console.WriteLine();
            Console.WriteLine("Input model loaded successfully.");
            Console.WriteLine();
            Console.WriteLine(currentModel.ToCanonicalText());
            Pause();
        }

        private static void RunBranchAndBoundSimplex()
        {
            RequireModel();

            var solver = new BranchAndBoundSimplexSolver(currentModel);
            string output = solver.Solve();

            Console.Clear();
            Console.WriteLine(output);

            string outputPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "branch_and_bound_simplex_output.txt");

            OutputWriter.Write(
                outputPath,
                currentModel.ToCanonicalText(),
                output);

            Console.WriteLine();
            Console.WriteLine("Output saved to:");
            Console.WriteLine(outputPath);
            Pause();
        }

        private static void RunBranchAndBoundKnapsack()
        {
            RequireModel();

            var solver = new BranchAndBoundKnapsackSolver(currentModel);
            string output = solver.Solve();

            Console.Clear();
            Console.WriteLine(output);

            string outputPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "branch_and_bound_knapsack_output.txt");

            OutputWriter.Write(
                outputPath,
                currentModel.ToCanonicalText(),
                output);

            Console.WriteLine();
            Console.WriteLine("Output saved to:");
            Console.WriteLine(outputPath);
            Pause();
        }

        private static void DisplayModel()
        {
            RequireModel();

            Console.WriteLine();
            Console.WriteLine(currentModel.ToCanonicalText());
            Pause();
        }

        private static void RequireModel()
        {
            if (currentModel == null)
                throw new InvalidOperationException(
                    "Load an input model before selecting an algorithm.");
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Press ENTER to continue...");
            Console.ReadLine();
        }
    }
}

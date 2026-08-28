using System;
using System.Linq;

namespace LPR_381_Project
{
    internal static class Program
    {
        private static LPModel _model;

        private static void Main()
        {
            Console.WriteLine("=========================================");
            Console.WriteLine(" LPR381 - LP / IP Solver (Full Reference Build)");
            Console.WriteLine("=========================================");

            bool running = true;
            while (running)
            {
                PrintMenu();
                string choice = Console.ReadLine();
                if (choice != null) choice = choice.Trim();
                try
                {
                    switch (choice)
                    {
                        case "1": LoadFile(); break;
                        case "2": RunPrimalSimplex(); break;
                        case "3": RunRevisedPrimalSimplex(); break;
                        case "4": RunBranchAndBoundSimplex(); break;
                        case "5": RunBranchAndBoundKnapsack(); break;
                        case "6": RunCuttingPlane(); break;
                        case "7": RunSensitivityAnalysis(); break;
                        case "8": RunNonLinearBonus(); break;
                        case "9":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("Unrecognised option. Please choose a number from the menu.");
                            break;
                    }
                }
                catch (InputValidationException ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("[Input Error] " + ex.Message);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("[Unexpected Error] " + ex.Message);
                    Console.WriteLine();
                }
            }
        }

        private static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("1. Load Input File");
            Console.WriteLine("2. Solve with Primal Simplex");
            Console.WriteLine("3. Solve with Revised Primal Simplex");
            Console.WriteLine("4. Solve with Branch and Bound Simplex");
            Console.WriteLine("5. Solve with Branch and Bound Knapsack");
            Console.WriteLine("6. Solve with Cutting Plane (Gomory)");
            Console.WriteLine("7. Sensitivity Analysis / Duality");
            Console.WriteLine("8. Non-Linear Bonus (f(x) = x^2)");
            Console.WriteLine("9. Exit");
            Console.Write("> ");
        }

        private static void LoadFile()
        {
            Console.Write("Enter path to input file: ");
            string path = Console.ReadLine();
            _model = FileParser.ParseFile(path);
            Console.WriteLine("Loaded model: " + _model.VariableCount + " variable(s), " + _model.ConstraintCount + " constraint(s).");
        }

        private static void RunPrimalSimplex()
        {
            string error = AlgorithmCompatibilityChecker.CheckCompatibility(_model, AlgorithmChoice.PrimalSimplex);
            if (error != null) { Console.WriteLine(error); return; }

            ConvertResult convResult = CanonicalFormConverter.Convert(_model);
            Tableau tableau = convResult.Tableau;
            var artNames = tableau.ColumnNames.Where(n => n.StartsWith("a")).ToList();
            SimplexResult result = PrimalSimplexSolver.Solve(tableau, artNames);

            PrintResult(result.Status, result.Message, result.ObjectiveValue);
            OutputWriter.WriteIterations("output.txt", "Primal Simplex", result.Iterations, result.Message);
            Console.WriteLine("Full tableau iterations written to output.txt");
        }

        private static void RunRevisedPrimalSimplex()
        {
            string error = AlgorithmCompatibilityChecker.CheckCompatibility(_model, AlgorithmChoice.RevisedPrimalSimplex);
            if (error != null) { Console.WriteLine(error); return; }

            ConvertResult convResult = CanonicalFormConverter.Convert(_model);
            var artNames = convResult.Tableau.ColumnNames.Where(n => n.StartsWith("a")).ToList();
            RevisedSimplexResult result = RevisedPrimalSimplexSolver.Solve(convResult.Tableau, artNames);

            foreach (string line in result.Iterations)
                Console.WriteLine(line);

            if (result.Status == SolverStatus.Optimal)
            {
                Console.WriteLine("Optimal objective value: " + Math.Round(result.ObjectiveValue, 3));
                for (int i = 0; i < convResult.Mappings.Count; i++)
                {
                    VariableMapping m = convResult.Mappings[i];
                    double pos = result.Solution[m.PositiveColumn];
                    double neg = m.NegativeColumn >= 0 ? result.Solution[m.NegativeColumn] : 0;
                    Console.WriteLine("x" + (i + 1) + " = " + Math.Round(m.Recover(pos, neg), 3));
                }
            }
            else
            {
                Console.WriteLine("Status: " + result.Status);
                Console.WriteLine(result.Message);
            }
        }

        private static void RunBranchAndBoundSimplex()
        {
            string error = AlgorithmCompatibilityChecker.CheckCompatibility(_model, AlgorithmChoice.BranchAndBoundSimplex);
            if (error != null) { Console.WriteLine(error); return; }

            BranchAndBoundResult result = BranchAndBoundSimplexSolver.Solve(_model);

            foreach (string line in result.NodeLog)
                Console.WriteLine(line);

            if (result.Status == SolverStatus.Optimal)
            {
                Console.WriteLine("Best candidate objective: " + Math.Round(result.ObjectiveValue, 3));
                for (int i = 0; i < result.VariableValues.Count; i++)
                    Console.WriteLine("x" + (i + 1) + " = " + result.VariableValues[i]);
            }
            else
            {
                Console.WriteLine("Status: " + result.Status);
                Console.WriteLine(result.Message);
            }
        }

        private static void RunBranchAndBoundKnapsack()
        {
            string error = AlgorithmCompatibilityChecker.CheckCompatibility(_model, AlgorithmChoice.BranchAndBoundKnapsack);
            if (error != null) { Console.WriteLine(error); return; }

            KnapsackResult result = BranchAndBoundKnapsackSolver.Solve(_model);

            foreach (string line in result.NodeLog)
                Console.WriteLine(line);

            Console.WriteLine("Optimal value: " + Math.Round(result.ObjectiveValue, 3));
            Console.WriteLine("Selected items: " + string.Join(", ", result.Selection.Select(i => "x" + (i + 1))));
        }

        private static void RunCuttingPlane()
        {
            string error = AlgorithmCompatibilityChecker.CheckCompatibility(_model, AlgorithmChoice.CuttingPlane);
            if (error != null) { Console.WriteLine(error); return; }

            CuttingPlaneResult result = CuttingPlaneSolver.Solve(_model);

            if (result.Status == SolverStatus.Optimal)
            {
                Console.WriteLine("Optimal integer solution found after " + result.CutsAdded + " cut(s).");
                Console.WriteLine("Objective value: " + Math.Round(result.ObjectiveValue, 3));
                for (int i = 0; i < result.VariableValues.Count; i++)
                    Console.WriteLine("x" + (i + 1) + " = " + result.VariableValues[i]);
            }
            else
            {
                Console.WriteLine("Status: " + result.Status);
                Console.WriteLine(result.Message);
            }

            OutputWriter.WriteIterations("output.txt", "Cutting Plane", result.Iterations, result.Message);
            Console.WriteLine("Full tableau iterations written to output.txt");
        }

        private static void RunSensitivityAnalysis()
        {
            string error = AlgorithmCompatibilityChecker.CheckCompatibility(_model, AlgorithmChoice.PrimalSimplex);
            if (error != null) { Console.WriteLine(error); return; }

            ConvertResult convResult = CanonicalFormConverter.Convert(_model);
            var artNames = convResult.Tableau.ColumnNames.Where(n => n.StartsWith("a")).ToList();
            SimplexResult lp = PrimalSimplexSolver.Solve(convResult.Tableau, artNames);

            if (lp.Status != SolverStatus.Optimal)
            {
                Console.WriteLine("Cannot run sensitivity analysis: model is " + lp.Status);
                Console.WriteLine(lp.Message);
                return;
            }

            SensitivityEngine engine;
            try
            {
                engine = SensitivityAdapter.Build(_model, lp.FinalTableau);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine("1. Shadow Prices");
            Console.WriteLine("2. Non-Basic Variable Range");
            Console.WriteLine("3. Basic Variable Range");
            Console.WriteLine("4. RHS Range");
            Console.WriteLine("5. Duality");
            Console.Write("> ");
            string sub = Console.ReadLine();

            switch (sub)
            {
                case "1":
                    Console.WriteLine(engine.PrintShadowPrices());
                    break;
                case "2":
                    Console.Write("Non-basic variable index (0-based): ");
                    int nb = int.Parse(Console.ReadLine());
                    Console.WriteLine(engine.GetNonBasicVariableRange(nb));
                    break;
                case "3":
                    Console.Write("Basic variable index (0-based): ");
                    int bIdx = int.Parse(Console.ReadLine());
                    Console.WriteLine(engine.GetBasicVariableRange(bIdx));
                    break;
                case "4":
                    Console.Write("Constraint index (0-based): ");
                    int rIdx = int.Parse(Console.ReadLine());
                    Console.WriteLine(engine.GetRHSRange(rIdx));
                    break;
                case "5":
                    Console.WriteLine(engine.ApplyDuality());
                    break;
                default:
                    Console.WriteLine("Unrecognised option.");
                    break;
            }
        }

        private static void RunNonLinearBonus()
        {
            NonLinearResult result = NonLinearSolver.MinimizeGoldenSection(x => x * x, -10, 10, 100);
            Console.WriteLine("Minimising f(x) = x^2 on [-10, 10] using Golden Section Search:");
            Console.WriteLine("x* = " + Math.Round(result.BestX, 6));
            Console.WriteLine("f(x*) = " + Math.Round(result.BestF, 6));
            Console.WriteLine("Iterations: " + result.Iterations);
        }

        private static void PrintResult(SolverStatus status, string message, double objective)
        {
            switch (status)
            {
                case SolverStatus.Optimal:
                    Console.WriteLine("Optimal objective value: " + Math.Round(objective, 3));
                    break;
                case SolverStatus.Infeasible:
                    Console.WriteLine("Result: INFEASIBLE");
                    Console.WriteLine(message);
                    break;
                case SolverStatus.Unbounded:
                    Console.WriteLine("Result: UNBOUNDED");
                    Console.WriteLine(message);
                    break;
            }
        }
    }
}

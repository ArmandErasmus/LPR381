namespace LPR_381_Project
{
    public enum AlgorithmChoice
    {
        PrimalSimplex,
        RevisedPrimalSimplex,
        BranchAndBoundSimplex,
        CuttingPlane,
        BranchAndBoundKnapsack
    }

    public static class AlgorithmCompatibilityChecker
    {
        public static string CheckCompatibility(LPModel model, AlgorithmChoice algorithm)
        {
            if (model == null)
                return "No model is loaded. Load an input file first (Main Menu -> Load Input File).";

            if (!model.IsStructurallyConsistent())
                return "The loaded model is inconsistent (mismatched variable counts between the " +
                       "objective, constraints, and restrictions). Reload a valid input file.";

            if (algorithm == AlgorithmChoice.PrimalSimplex || algorithm == AlgorithmChoice.RevisedPrimalSimplex)
            {
                return null;
            }
            else if (algorithm == AlgorithmChoice.CuttingPlane || algorithm == AlgorithmChoice.BranchAndBoundSimplex)
            {
                if (!model.IsIntegerProgram)
                    return DescribeAlgorithm(algorithm) + " requires at least one \"int\" or \"bin\" " +
                           "decision variable. This model is a pure LP - use Primal Simplex or " +
                           "Revised Primal Simplex instead.";
                return null;
            }
            else if (algorithm == AlgorithmChoice.BranchAndBoundKnapsack)
            {
                if (!model.IsPureBinaryKnapsack)
                    return "Branch and Bound Knapsack requires a pure Knapsack model: every " +
                           "decision variable must be \"bin\" and there must be exactly one \"<=\" " +
                           "constraint. Use Branch and Bound Simplex or Cutting Plane for other " +
                           "integer models instead.";
                return null;
            }
            else
            {
                return "Unrecognised algorithm selection.";
            }
        }

        private static string DescribeAlgorithm(AlgorithmChoice a)
        {
            switch (a)
            {
                case AlgorithmChoice.PrimalSimplex: return "Primal Simplex";
                case AlgorithmChoice.RevisedPrimalSimplex: return "Revised Primal Simplex";
                case AlgorithmChoice.BranchAndBoundSimplex: return "Branch and Bound Simplex";
                case AlgorithmChoice.CuttingPlane: return "Cutting Plane";
                case AlgorithmChoice.BranchAndBoundKnapsack: return "Branch and Bound Knapsack";
                default: return a.ToString();
            }
        }
    }
}

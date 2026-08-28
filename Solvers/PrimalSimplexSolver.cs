using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class SimplexResult
    {
        public SolverStatus Status { get; set; }
        public List<Tableau> Iterations { get; set; } = new List<Tableau>();
        public Tableau FinalTableau { get { return Iterations.Count > 0 ? Iterations[Iterations.Count - 1] : null; } }
        public double ObjectiveValue { get; set; }
        public string Message { get; set; } = "";
    }

    public static class PrimalSimplexSolver
    {
        private const double BigM = 1000000;
        private const double Epsilon = 1e-9;
        private const int MaxIterations = 500;

        public static SimplexResult Solve(Tableau initial, List<string> artificialColumnNames)
        {
            Tableau tableau = initial.Clone();
            tableau.Label = "Initial Tableau (Canonical Form)";
            SimplexResult result = new SimplexResult();
            result.Iterations.Add(tableau.Clone());

            for (int col = 0; col < tableau.ColCount - 1; col++)
            {
                if (artificialColumnNames.Contains(tableau.ColumnNames[col]))
                    tableau.Data[0, col] = BigM;
            }

            for (int col = 0; col < tableau.ColCount - 1; col++)
            {
                if (artificialColumnNames.Contains(tableau.ColumnNames[col]))
                {
                    int basicRowIdx = tableau.BasicVariables.IndexOf(col);
                    if (basicRowIdx >= 0)
                    {
                        int row = basicRowIdx + 1;
                        for (int c = 0; c < tableau.ColCount; c++)
                            tableau.Data[0, c] -= BigM * tableau.Data[row, c];
                    }
                }
            }
            tableau.Label = "Initial Tableau (Big-M applied)";
            result.Iterations[0] = tableau.Clone();

            int iteration = 0;
            while (iteration++ < MaxIterations)
            {
                int pivotCol = -1;
                double mostNegative = -Epsilon;
                for (int c = 0; c < tableau.ColCount - 1; c++)
                {
                    if (tableau.Data[0, c] < mostNegative)
                    {
                        mostNegative = tableau.Data[0, c];
                        pivotCol = c;
                    }
                }

                if (pivotCol == -1) break;

                int pivotRow = -1;
                double bestRatio = double.PositiveInfinity;
                for (int r = 1; r < tableau.RowCount; r++)
                {
                    double coeff = tableau.Data[r, pivotCol];
                    if (coeff > Epsilon)
                    {
                        double ratio = tableau.Data[r, tableau.ColCount - 1] / coeff;
                        if (ratio < bestRatio - Epsilon)
                        {
                            bestRatio = ratio;
                            pivotRow = r;
                        }
                    }
                }

                if (pivotRow == -1)
                {
                    result.Status = SolverStatus.Unbounded;
                    result.Message = "Model is unbounded: entering column \"" + tableau.ColumnNames[pivotCol] +
                                      "\" has no positive coefficient in any constraint row, so it can increase indefinitely.";
                    return result;
                }

                tableau.Pivot(pivotRow, pivotCol);
                tableau.BasicVariables[pivotRow - 1] = pivotCol;
                tableau.Label = "Iteration " + iteration + ": pivot on column \"" + tableau.ColumnNames[pivotCol] + "\", row " + pivotRow;
                result.Iterations.Add(tableau.Clone());
            }

            for (int r = 0; r < tableau.BasicVariables.Count; r++)
            {
                int basicCol = tableau.BasicVariables[r];
                if (artificialColumnNames.Contains(tableau.ColumnNames[basicCol]) &&
                    tableau.Data[r + 1, tableau.ColCount - 1] > Epsilon)
                {
                    result.Status = SolverStatus.Infeasible;
                    result.Message = "Model is infeasible: artificial variable \"" + tableau.ColumnNames[basicCol] +
                                      "\" remains in the basis with a non-zero value at optimality, so no feasible " +
                                      "point satisfies all constraints simultaneously.";
                    return result;
                }
            }

            result.Status = SolverStatus.Optimal;
            double zInternal = tableau.Data[0, tableau.ColCount - 1];
            result.ObjectiveValue = tableau.IsMax ? zInternal : -zInternal;
            tableau.Label = "Optimal Tableau";
            result.Iterations[result.Iterations.Count - 1] = tableau.Clone();
            return result;
        }
    }
}

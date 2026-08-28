using System;

namespace LPR_381_Project
{
    public static class DualSimplexSolver
    {
        private const double Epsilon = 1e-9;
        private const int MaxIterations = 200;

        public static SimplexResult Resolve(Tableau tableau)
        {
            SimplexResult result = new SimplexResult();
            Tableau current = tableau.Clone();
            current.Label = "Tableau after adding cut";
            result.Iterations.Add(current.Clone());

            int iteration = 0;
            while (iteration++ < MaxIterations)
            {
                int pivotRow = -1;
                double mostNegative = -Epsilon;
                for (int r = 1; r < current.RowCount; r++)
                {
                    double rhs = current.Data[r, current.ColCount - 1];
                    if (rhs < mostNegative)
                    {
                        mostNegative = rhs;
                        pivotRow = r;
                    }
                }

                if (pivotRow == -1) break;

                int pivotCol = -1;
                double bestRatio = double.PositiveInfinity;
                for (int c = 0; c < current.ColCount - 1; c++)
                {
                    double coeff = current.Data[pivotRow, c];
                    if (coeff < -Epsilon)
                    {
                        double ratio = Math.Abs(current.Data[0, c] / coeff);
                        if (ratio < bestRatio - Epsilon)
                        {
                            bestRatio = ratio;
                            pivotCol = c;
                        }
                    }
                }

                if (pivotCol == -1)
                {
                    result.Status = SolverStatus.Infeasible;
                    result.Message = "Model is infeasible: after adding the Gomory cut, row \"" +
                                      current.ColumnNames[current.BasicVariables[pivotRow - 1]] +
                                      "\" has a negative RHS with no negative coefficient to pivot on, " +
                                      "so no feasible integer point exists.";
                    return result;
                }

                current.Pivot(pivotRow, pivotCol);
                current.BasicVariables[pivotRow - 1] = pivotCol;
                current.Label = "Dual Simplex Iteration " + iteration + ": pivot on column \"" +
                                 current.ColumnNames[pivotCol] + "\", row " + pivotRow;
                result.Iterations.Add(current.Clone());
            }

            result.Status = SolverStatus.Optimal;
            double zInternal = current.Data[0, current.ColCount - 1];
            result.ObjectiveValue = current.IsMax ? zInternal : -zInternal;
            current.Label = "Optimal Tableau (after cut)";
            result.Iterations[result.Iterations.Count - 1] = current.Clone();
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class RevisedSimplexResult
    {
        public SolverStatus Status { get; set; }
        public List<string> Iterations { get; set; } = new List<string>();
        public double ObjectiveValue { get; set; }
        public double[] Solution { get; set; }
        public string Message { get; set; } = "";
    }

    public static class RevisedPrimalSimplexSolver
    {
        private const double Epsilon = 1e-9;
        private const double BigM = 1000000;
        private const int MaxIterations = 500;

        public static RevisedSimplexResult Solve(Tableau initial, List<string> artificialColumnNames)
        {
            RevisedSimplexResult result = new RevisedSimplexResult();
            int m = initial.RowCount - 1;
            int totalCols = initial.ColCount - 1;

            double[] c = new double[totalCols];
            for (int j = 0; j < totalCols; j++)
                c[j] = -initial.Data[0, j];

            double[,] A = new double[m, totalCols];
            double[] b = new double[m];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < totalCols; j++)
                    A[i, j] = initial.Data[i + 1, j];
                b[i] = initial.Data[i + 1, totalCols];
            }

            List<int> basis = new List<int>(initial.BasicVariables);
            double[,] BInv = IdentityLike(m);

            for (int col = 0; col < totalCols; col++)
            {
                if (artificialColumnNames.Contains(initial.ColumnNames[col]))
                {
                    int idx = basis.IndexOf(col);
                    if (idx >= 0)
                        c[col] -= BigM;
                }
            }

            int iteration = 0;
            while (iteration++ < MaxIterations)
            {
                double[] cB = basis.Select(j => c[j]).ToArray();
                double[] y = VecMatMul(cB, BInv);

                int enter = -1;
                double bestReduced = Epsilon;
                for (int j = 0; j < totalCols; j++)
                {
                    if (basis.Contains(j)) continue;
                    double[] Aj = GetColumn(A, j);
                    double zj = Dot(y, Aj);
                    double reduced = c[j] - zj;
                    if (reduced > bestReduced)
                    {
                        bestReduced = reduced;
                        enter = j;
                    }
                }

                if (enter == -1)
                {
                    result.Iterations.Add("Iteration " + iteration + ": no improving column found. Optimal reached.");
                    break;
                }

                double[] Aenter = GetColumn(A, enter);
                double[] d = MatVecMul(BInv, Aenter);
                double[] xB = MatVecMul(BInv, b);

                int leaveRow = -1;
                double bestRatio = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > Epsilon)
                    {
                        double ratio = xB[i] / d[i];
                        if (ratio < bestRatio - Epsilon)
                        {
                            bestRatio = ratio;
                            leaveRow = i;
                        }
                    }
                }

                if (leaveRow == -1)
                {
                    result.Status = SolverStatus.Unbounded;
                    result.Message = "Model is unbounded: entering column \"" + initial.ColumnNames[enter] +
                                      "\" has no positive entry in the direction vector, so it can increase indefinitely.";
                    return result;
                }

                result.Iterations.Add("Iteration " + iteration + ": entering \"" + initial.ColumnNames[enter] +
                                       "\", leaving \"" + initial.ColumnNames[basis[leaveRow]] + "\" (row " + leaveRow + "), Product Form eta update on B_inv.");

                double[,] eta = IdentityLike(m);
                for (int i = 0; i < m; i++)
                    eta[i, leaveRow] = i == leaveRow ? 1.0 / d[i] : -d[i] / d[leaveRow];

                BInv = MatMatMul(eta, BInv);
                basis[leaveRow] = enter;
            }

            double[] cBFinal = basis.Select(j => c[j]).ToArray();
            double[] xBFinal = MatVecMul(BInv, b);

            for (int i = 0; i < m; i++)
            {
                int bcol = basis[i];
                if (artificialColumnNames.Contains(initial.ColumnNames[bcol]) && xBFinal[i] > Epsilon)
                {
                    result.Status = SolverStatus.Infeasible;
                    result.Message = "Model is infeasible: artificial variable \"" + initial.ColumnNames[bcol] +
                                      "\" remains basic with a non-zero value at optimality.";
                    return result;
                }
            }

            double[] solution = new double[totalCols];
            for (int i = 0; i < m; i++)
                solution[basis[i]] = xBFinal[i];

            double z = 0;
            for (int i = 0; i < m; i++)
                z += cBFinal[i] * xBFinal[i];

            result.Status = SolverStatus.Optimal;
            result.ObjectiveValue = initial.IsMax ? z : -z;
            result.Solution = solution;
            return result;
        }

        private static double[,] IdentityLike(int n)
        {
            double[,] r = new double[n, n];
            for (int i = 0; i < n; i++) r[i, i] = 1;
            return r;
        }

        private static double[] GetColumn(double[,] A, int col)
        {
            int m = A.GetLength(0);
            double[] r = new double[m];
            for (int i = 0; i < m; i++) r[i] = A[i, col];
            return r;
        }

        private static double[] MatVecMul(double[,] M, double[] v)
        {
            int rows = M.GetLength(0);
            int cols = M.GetLength(1);
            double[] r = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < cols; j++) sum += M[i, j] * v[j];
                r[i] = sum;
            }
            return r;
        }

        private static double[] VecMatMul(double[] v, double[,] M)
        {
            int rows = M.GetLength(0);
            int cols = M.GetLength(1);
            double[] r = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                double sum = 0;
                for (int i = 0; i < rows; i++) sum += v[i] * M[i, j];
                r[j] = sum;
            }
            return r;
        }

        private static double[,] MatMatMul(double[,] A, double[,] B)
        {
            int n = A.GetLength(0);
            double[,] r = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < n; k++) sum += A[i, k] * B[k, j];
                    r[i, j] = sum;
                }
            return r;
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }
    }
}

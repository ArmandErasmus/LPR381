using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR_381_Project
{
    public class SimplexResult
    {
        public bool IsOptimal { get; set; }
        public bool IsInfeasible { get; set; }
        public bool IsUnbounded { get; set; }
        public double ObjectiveValue { get; set; }
        public double[] X { get; set; }
        public List<string> Iterations { get; set; }
        public string Message { get; set; }

        public SimplexResult()
        {
            Iterations = new List<string>();
            X = new double[0];
        }
    }

    internal class StandardVariable
    {
        public int OriginalIndex;
        public double Factor;
        public bool Artificial;
        public string Name;

        public StandardVariable(int originalIndex, double factor,
            bool artificial, string name)
        {
            OriginalIndex = originalIndex;
            Factor = factor;
            Artificial = artificial;
            Name = name;
        }
    }

    internal class StandardForm
    {
        public List<double[]> Rows = new List<double[]>();
        public List<double> RHS = new List<double>();
        public List<int> Basis = new List<int>();
        public List<StandardVariable> Variables = new List<StandardVariable>();
        public HashSet<int> ArtificialColumns = new HashSet<int>();
        public int OriginalVariableCount;
    }

    public class SimplexSolver
    {
        private const double EPS = 1e-8;
        private readonly int maxIterations;

        public SimplexSolver(int maxIterations = 10000)
        {
            this.maxIterations = maxIterations;
        }

        public SimplexResult Solve(LinearModel model,
            IEnumerable<LinearConstraint> extraConstraints = null)
        {
            var result = new SimplexResult();

            try
            {
                StandardForm sf = BuildStandardForm(model, extraConstraints);
                int m = sf.Rows.Count;
                int n = sf.Variables.Count;

                if (m == 0)
                {
                    result.IsOptimal = true;
                    result.X = new double[model.VariableCount];
                    result.ObjectiveValue = 0.0;
                    result.Message = "No constraints.";
                    return result;
                }

                double[,] tableau = new double[m + 1, n + 1];

                for (int i = 0; i < m; i++)
                {
                    for (int j = 0; j < n; j++)
                        tableau[i, j] = sf.Rows[i][j];

                    tableau[i, n] = sf.RHS[i];
                }

                // Phase 1: maximise negative artificial variables.
                if (sf.ArtificialColumns.Count > 0)
                {
                    double[] phase1Costs = new double[n];

                    foreach (int col in sf.ArtificialColumns)
                        phase1Costs[col] = -1.0;

                    SetObjectiveRow(tableau, sf.Basis, phase1Costs);
                    result.Iterations.Add(CreateTableauText(
                        "Phase 1 Initial Tableau", tableau, sf.Variables));

                    string phase1Status = RunSimplex(
                        tableau, sf.Basis, phase1Costs, sf.ArtificialColumns,
                        result.Iterations, sf.Variables, "Phase 1");

                    if (phase1Status == "UNBOUNDED")
                    {
                        result.IsInfeasible = true;
                        result.Message = "Phase 1 became unbounded. The model is infeasible.";
                        return result;
                    }

                    if (tableau[m, n] < -1e-6)
                    {
                        result.IsInfeasible = true;
                        result.Message = "No feasible solution exists.";
                        return result;
                    }

                    // Remove artificial basic variables where possible.
                    for (int row = 0; row < m; row++)
                    {
                        int basic = sf.Basis[row];
                        if (!sf.ArtificialColumns.Contains(basic))
                            continue;

                        int pivotCol = -1;
                        for (int col = 0; col < n; col++)
                        {
                            if (sf.ArtificialColumns.Contains(col))
                                continue;

                            if (Math.Abs(tableau[row, col]) > EPS)
                            {
                                pivotCol = col;
                                break;
                            }
                        }

                        if (pivotCol >= 0)
                            Pivot(tableau, sf.Basis, row, pivotCol);
                    }
                }

                double[] phase2Costs = new double[n];

                for (int j = 0; j < n; j++)
                {
                    StandardVariable v = sf.Variables[j];

                    if (sf.ArtificialColumns.Contains(j) || v.OriginalIndex < 0)
                    {
                        phase2Costs[j] = 0.0;
                    }
                    else
                    {
                        phase2Costs[j] = model.Objective[v.OriginalIndex] * v.Factor;

                        // The simplex engine always maximises.
                        if (model.ProblemType == "MIN")
                            phase2Costs[j] *= -1.0;
                    }
                }

                SetObjectiveRow(tableau, sf.Basis, phase2Costs);
                result.Iterations.Add(CreateTableauText(
                    "Phase 2 Initial Tableau", tableau, sf.Variables));

                string phase2Status = RunSimplex(
                    tableau, sf.Basis, phase2Costs, sf.ArtificialColumns,
                    result.Iterations, sf.Variables, "Phase 2");

                if (phase2Status == "UNBOUNDED")
                {
                    result.IsUnbounded = true;
                    result.Message = "The LP relaxation is unbounded.";
                    return result;
                }

                if (phase2Status == "ITERATION_LIMIT")
                {
                    result.Message = "Simplex iteration limit reached.";
                    return result;
                }

                double[] transformedX = new double[n];

                for (int row = 0; row < m; row++)
                {
                    int basic = sf.Basis[row];
                    if (!sf.ArtificialColumns.Contains(basic))
                        transformedX[basic] = Clean(tableau[row, n]);
                }

                double[] x = new double[model.VariableCount];

                for (int j = 0; j < n; j++)
                {
                    StandardVariable v = sf.Variables[j];

                    if (v.OriginalIndex >= 0)
                        x[v.OriginalIndex] += v.Factor * transformedX[j];
                }

                for (int i = 0; i < x.Length; i++)
                    if (Math.Abs(x[i]) < EPS)
                        x[i] = 0.0;

                double objective = 0.0;
                for (int i = 0; i < model.VariableCount; i++)
                    objective += model.Objective[i] * x[i];

                result.IsOptimal = true;
                result.X = x;
                result.ObjectiveValue = objective;
                result.Message = "Optimal LP relaxation found.";
                return result;
            }
            catch (Exception ex)
            {
                result.Message = "Simplex error: " + ex.Message;
                return result;
            }
        }

        private StandardForm BuildStandardForm(LinearModel model,
            IEnumerable<LinearConstraint> extraConstraints)
        {
            var sf = new StandardForm();
            sf.OriginalVariableCount = model.VariableCount;

            // Map original variables to non-negative standard variables.
            for (int i = 0; i < model.VariableCount; i++)
            {
                string restriction = model.SignRestrictions[i].ToLowerInvariant();

                if (restriction == "urs")
                {
                    sf.Variables.Add(new StandardVariable(i, 1.0, false, "x" + (i + 1) + "+"));
                    sf.Variables.Add(new StandardVariable(i, -1.0, false, "x" + (i + 1) + "-"));
                }
                else if (restriction == "-")
                {
                    sf.Variables.Add(new StandardVariable(i, -1.0, false, "x" + (i + 1) + "-"));
                }
                else
                {
                    sf.Variables.Add(new StandardVariable(i, 1.0, false, "x" + (i + 1)));
                }
            }

            var allConstraints = model.Constraints.Select(c => c.Clone()).ToList();

            if (extraConstraints != null)
                allConstraints.AddRange(extraConstraints.Select(c => c.Clone()));

            // Binary upper bounds.
            for (int i = 0; i < model.VariableCount; i++)
            {
                if (model.SignRestrictions[i].ToLowerInvariant() == "bin")
                {
                    double[] bound = new double[model.VariableCount];
                    bound[i] = 1.0;
                    allConstraints.Add(new LinearConstraint(bound, "<=", 1.0));
                }
            }

            foreach (LinearConstraint c in allConstraints)
            {
                double[] transformed = new double[sf.Variables.Count];

                for (int j = 0; j < sf.Variables.Count; j++)
                    transformed[j] = c.Coefficients[sf.Variables[j].OriginalIndex]
                        * sf.Variables[j].Factor;

                string relation = c.Relation;
                double rhs = c.RHS;

                if (rhs < -EPS)
                {
                    rhs = -rhs;
                    for (int j = 0; j < transformed.Length; j++)
                        transformed[j] = -transformed[j];

                    if (relation == "<=") relation = ">=";
                    else if (relation == ">=") relation = "<=";
                }

                int slackColumn;

                if (relation == "<=")
                {
                    slackColumn = AddVariable(sf, -1, 0.0, false, "s" + (sf.Rows.Count + 1));
                    transformed = ExtendRow(transformed, sf.Variables.Count);
                    transformed[slackColumn] = 1.0;
                    sf.Basis.Add(slackColumn);
                }
                else if (relation == ">=")
                {
                    slackColumn = AddVariable(sf, -1, 0.0, false, "s" + (sf.Rows.Count + 1));
                    transformed = ExtendRow(transformed, sf.Variables.Count);
                    transformed[slackColumn] = -1.0;

                    int artificialColumn = AddVariable(
                        sf, -1, 0.0, true, "a" + (sf.Rows.Count + 1));

                    transformed = ExtendRow(transformed, sf.Variables.Count);
                    transformed[artificialColumn] = 1.0;

                    sf.ArtificialColumns.Add(artificialColumn);
                    sf.Basis.Add(artificialColumn);
                }
                else if (relation == "=")
                {
                    int artificialColumn = AddVariable(
                        sf, -1, 0.0, true, "a" + (sf.Rows.Count + 1));

                    transformed = ExtendRow(transformed, sf.Variables.Count);
                    transformed[artificialColumn] = 1.0;

                    sf.ArtificialColumns.Add(artificialColumn);
                    sf.Basis.Add(artificialColumn);
                }
                else
                {
                    throw new FormatException("Unknown relation: " + relation);
                }

                while (transformed.Length < sf.Variables.Count)
                    transformed = ExtendRow(transformed, sf.Variables.Count);

                sf.Rows.Add(transformed);
                sf.RHS.Add(rhs);
            }

            // All rows must have the final number of columns.
            for (int i = 0; i < sf.Rows.Count; i++)
                sf.Rows[i] = ExtendRow(sf.Rows[i], sf.Variables.Count);

            return sf;
        }

        private int AddVariable(StandardForm sf, int originalIndex,
            double factor, bool artificial, string name)
        {
            sf.Variables.Add(new StandardVariable(
                originalIndex, factor, artificial, name));
            return sf.Variables.Count - 1;
        }

        private static double[] ExtendRow(double[] row, int length)
        {
            double[] x = new double[length];
            Array.Copy(row, x, Math.Min(row.Length, length));
            return x;
        }

        private void SetObjectiveRow(double[,] tableau, List<int> basis,
            double[] costs)
        {
            int rows = tableau.GetLength(0) - 1;
            int cols = tableau.GetLength(1) - 1;

            for (int j = 0; j < cols; j++)
                tableau[rows, j] = costs[j];

            tableau[rows, cols] = 0.0;

            for (int i = 0; i < rows; i++)
            {
                int basic = basis[i];
                double cb = costs[basic];

                if (Math.Abs(cb) < EPS)
                    continue;

                for (int j = 0; j <= cols; j++)
                    tableau[rows, j] -= cb * tableau[i, j];
            }
        }

        private string RunSimplex(double[,] tableau, List<int> basis,
            double[] costs, HashSet<int> artificialColumns,
            List<string> log, List<StandardVariable> variables,
            string phaseName)
        {
            int rows = tableau.GetLength(0) - 1;
            int cols = tableau.GetLength(1) - 1;

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                int entering = -1;
                double best = EPS;

                for (int j = 0; j < cols; j++)
                {
                    if (artificialColumns.Contains(j) && phaseName == "Phase 2")
                        continue;

                    if (tableau[rows, j] > best)
                    {
                        best = tableau[rows, j];
                        entering = j;
                    }
                }

                if (entering == -1)
                    return "OPTIMAL";

                int leaving = -1;
                double bestRatio = double.PositiveInfinity;

                for (int i = 0; i < rows; i++)
                {
                    double coefficient = tableau[i, entering];

                    if (coefficient > EPS)
                    {
                        double ratio = tableau[i, cols] / coefficient;

                        if (ratio >= -EPS && ratio < bestRatio)
                        {
                            bestRatio = ratio;
                            leaving = i;
                        }
                    }
                }

                if (leaving == -1)
                    return "UNBOUNDED";

                Pivot(tableau, basis, leaving, entering);

                log.Add(CreateTableauText(
                    phaseName + " Iteration " + iteration +
                    " | Enter x=" + entering +
                    " | Leave row=" + (leaving + 1),
                    tableau, variables));
            }

            return "ITERATION_LIMIT";
        }

        private static void Pivot(double[,] tableau, List<int> basis,
            int pivotRow, int pivotColumn)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            double pivot = tableau[pivotRow, pivotColumn];

            for (int j = 0; j < cols; j++)
                tableau[pivotRow, j] /= pivot;

            for (int i = 0; i < rows; i++)
            {
                if (i == pivotRow)
                    continue;

                double factor = tableau[i, pivotColumn];

                if (Math.Abs(factor) < EPS)
                    continue;

                for (int j = 0; j < cols; j++)
                    tableau[i, j] -= factor * tableau[pivotRow, j];
            }

            basis[pivotRow] = pivotColumn;
        }

        private static string CreateTableauText(string title,
            double[,] tableau, List<StandardVariable> variables)
        {
            var sb = new StringBuilder();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            sb.AppendLine();
            sb.AppendLine("=== " + title + " ===");

            sb.Append("Basis\t");
            for (int j = 0; j < variables.Count; j++)
                sb.Append(variables[j].Name + "\t");

            sb.AppendLine("RHS");

            for (int i = 0; i < rows; i++)
            {
                sb.Append(i == rows - 1 ? "OBJ\t" : "R" + (i + 1) + "\t");

                for (int j = 0; j < cols; j++)
                    sb.Append(tableau[i, j].ToString("0.000") + "\t");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static double Clean(double x)
        {
            return Math.Abs(x) < EPS ? 0.0 : x;
        }
    }
}

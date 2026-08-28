using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class CuttingPlaneResult
    {
        public SolverStatus Status { get; set; }
        public List<Tableau> Iterations { get; set; } = new List<Tableau>();
        public double ObjectiveValue { get; set; }
        public List<double> VariableValues { get; set; } = new List<double>();
        public string Message { get; set; } = "";
        public int CutsAdded { get; set; }
    }

    public static class CuttingPlaneSolver
    {
        private const double IntegerTolerance = 1e-6;
        private const int MaxCuts = 50;

        public static CuttingPlaneResult Solve(LPModel model)
        {
            CuttingPlaneResult result = new CuttingPlaneResult();

            if (!model.IsIntegerProgram)
                throw new InvalidOperationException(
                    "Cutting Plane was invoked on a model with no int/bin variables. " +
                    "Use Primal Simplex or Revised Primal Simplex for pure LP models instead.");

            ConvertResult convResult = CanonicalFormConverter.Convert(model);
            Tableau tableau = convResult.Tableau;
            List<VariableMapping> mappings = convResult.Mappings;
            List<string> artificialNames = tableau.ColumnNames.Where(n => n.StartsWith("a")).ToList();

            SimplexResult lpResult = PrimalSimplexSolver.Solve(tableau, artificialNames);
            result.Iterations.AddRange(lpResult.Iterations);

            if (lpResult.Status != SolverStatus.Optimal)
            {
                result.Status = lpResult.Status;
                result.Message = lpResult.Message;
                return result;
            }

            Tableau current = lpResult.FinalTableau.Clone();

            HashSet<int> integerColumns = new HashSet<int>();
            for (int i = 0; i < model.VariableCount; i++)
            {
                if (model.Restrictions[i] == VariableRestriction.Int || model.Restrictions[i] == VariableRestriction.Bin)
                    integerColumns.Add(mappings[i].PositiveColumn);
            }

            int cutsAdded = 0;
            while (cutsAdded < MaxCuts)
            {
                int cutRow = FindMostFractionalRow(current, integerColumns);
                if (cutRow == -1) break;

                cutsAdded++;
                current = AddGomoryCut(current, cutRow, cutsAdded);
                current.Label = "Gomory Cut " + cutsAdded + " added";
                result.Iterations.Add(current.Clone());

                SimplexResult dualResult = DualSimplexSolver.Resolve(current);
                result.Iterations.AddRange(dualResult.Iterations.Skip(1));

                if (dualResult.Status != SolverStatus.Optimal)
                {
                    result.Status = dualResult.Status;
                    result.Message = dualResult.Message;
                    return result;
                }

                current = dualResult.FinalTableau.Clone();
            }

            if (cutsAdded >= MaxCuts)
            {
                result.Status = SolverStatus.IterationLimitReached;
                result.Message = "Stopped after " + MaxCuts + " cuts without reaching an all-integer solution. " +
                                  "This usually indicates cycling; check the model.";
                return result;
            }

            result.Status = SolverStatus.Optimal;
            double zInternal = current.Data[0, current.ColCount - 1];
            result.ObjectiveValue = current.IsMax ? zInternal : -zInternal;
            result.CutsAdded = cutsAdded;
            result.VariableValues = ExtractSolution(current, mappings);
            current.Label = "Final Integer-Optimal Tableau";
            result.Iterations[result.Iterations.Count - 1] = current.Clone();

            return result;
        }

        private static int FindMostFractionalRow(Tableau t, HashSet<int> integerColumns)
        {
            int bestRow = -1;
            double bestFrac = IntegerTolerance;

            for (int r = 0; r < t.BasicVariables.Count; r++)
            {
                int basicCol = t.BasicVariables[r];
                if (!integerColumns.Contains(basicCol)) continue;

                double rhs = t.Data[r + 1, t.ColCount - 1];
                double frac = rhs - Math.Floor(rhs);
                double fractionality = Math.Min(frac, 1 - frac);

                if (fractionality > bestFrac)
                {
                    bestFrac = fractionality;
                    bestRow = r + 1;
                }
            }
            return bestRow;
        }

        private static Tableau AddGomoryCut(Tableau t, int sourceRow, int cutNumber)
        {
            int newRows = t.RowCount + 1;
            int newCols = t.ColCount + 1;
            Tableau result = new Tableau(newRows, newCols);
            result.IsMax = t.IsMax;
            result.ColumnNames = new List<string>(t.ColumnNames);
            result.ColumnNames.Add("g" + cutNumber);
            result.BasicVariables = new List<int>(t.BasicVariables);

            for (int r = 0; r < t.RowCount; r++)
            {
                for (int c = 0; c < t.ColCount - 1; c++)
                    result.Data[r, c] = t.Data[r, c];
                result.Data[r, newCols - 1] = t.Data[r, t.ColCount - 1];
            }

            for (int c = 0; c < t.ColCount - 1; c++)
            {
                double a = t.Data[sourceRow, c];
                double frac = a - Math.Floor(a);
                result.Data[newRows - 1, c] = -frac;
            }
            result.Data[newRows - 1, t.ColCount - 1] = 1;
            double bRhs = t.Data[sourceRow, t.ColCount - 1];
            double bFrac = bRhs - Math.Floor(bRhs);
            result.Data[newRows - 1, newCols - 1] = -bFrac;

            result.BasicVariables.Add(t.ColCount - 1);

            return result;
        }

        private static List<double> ExtractSolution(Tableau t, List<VariableMapping> mappings)
        {
            double[] values = new double[t.ColCount - 1];
            for (int r = 0; r < t.BasicVariables.Count; r++)
                values[t.BasicVariables[r]] = t.Data[r + 1, t.ColCount - 1];

            List<double> solution = new List<double>();
            foreach (VariableMapping m in mappings)
            {
                double pos = values[m.PositiveColumn];
                double neg = m.NegativeColumn >= 0 ? values[m.NegativeColumn] : 0;
                solution.Add(Math.Round(m.Recover(pos, neg), 3));
            }
            return solution;
        }
    }
}

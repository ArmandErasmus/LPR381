using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public static class SensitivityAdapter
    {
        public static SensitivityEngine Build(LPModel model, Tableau finalTableau)
        {
            if (model.Constraints.Any(con => con.Relation != RelationType.LessOrEqual))
                throw new InvalidOperationException(
                    "This reference build's Sensitivity Analysis adapter only supports models where every " +
                    "constraint is \"<=\" (no artificial variables). Extend SensitivityAdapter to support " +
                    ">= and = if needed.");

            int n = model.VariableCount;
            int m = model.Constraints.Count;

            double[] c = new double[n + m];
            for (int j = 0; j < n; j++)
                c[j] = model.Objective.Coefficients[j];

            double[,] a = new double[m, n + m];
            double[] b = new double[m];
            for (int r = 0; r < m; r++)
            {
                for (int j = 0; j < n; j++)
                    a[r, j] = model.Constraints[r].Coefficients[j];
                a[r, n + r] = 1;
                b[r] = model.Constraints[r].Rhs;
            }
            int totalVars = n + m;
            List<int> basicIdx = new List<int>(finalTableau.BasicVariables);
            List<int> nonBasicIdx = Enumerable.Range(0, totalVars).Except(basicIdx).ToList();

            double[,] bInv = new double[m, m];
            for (int r = 0; r < m; r++)
                for (int col = 0; col < m; col++)
                    bInv[r, col] = finalTableau.Data[r + 1, n + col];

            double[] optX = new double[totalVars];
            for (int r = 0; r < finalTableau.BasicVariables.Count; r++)
                optX[finalTableau.BasicVariables[r]] = finalTableau.Data[r + 1, finalTableau.ColCount - 1];

            double zInternal = finalTableau.Data[0, finalTableau.ColCount - 1];
            double optZ = finalTableau.IsMax ? zInternal : -zInternal;

            string problemType = model.Objective.Type == ObjectiveType.Max ? "MAX" : "MIN";

            SensitivityEngine engine = new SensitivityEngine(problemType, c, a, b, basicIdx, nonBasicIdx, bInv, optX, optZ);
            engine.NumDecisionVars = n;
            engine.NumConstraints = m;
            return engine;
        }
    }
}

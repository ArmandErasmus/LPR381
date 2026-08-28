using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class VariableMapping
    {
        public int OriginalIndex { get; set; }
        public string OriginalName { get; set; }
        public int PositiveColumn { get; set; }
        public int NegativeColumn { get; set; } = -1;
        public bool WasNegated { get; set; }

        public double Recover(double positiveValue, double negativeValue)
        {
            double v = positiveValue - (NegativeColumn >= 0 ? negativeValue : 0);
            return WasNegated ? -v : v;
        }
    }

    public class ConvertResult
    {
        public Tableau Tableau;
        public List<VariableMapping> Mappings;
    }

    public static class CanonicalFormConverter
    {
        public static ConvertResult Convert(LPModel model)
        {
            int n = model.VariableCount;
            List<VariableMapping> mappings = new List<VariableMapping>();
            List<string> columnNames = new List<string>();

            int colIndex = 0;
            List<int> perVarPos = new List<int>();
            List<int> perVarNeg = new List<int>();

            for (int i = 0; i < n; i++)
            {
                VariableRestriction restriction = model.Restrictions[i];
                VariableMapping map = new VariableMapping();
                map.OriginalIndex = i;
                map.OriginalName = "x" + (i + 1);

                if (restriction == VariableRestriction.Urs)
                {
                    map.PositiveColumn = colIndex++;
                    columnNames.Add("x" + (i + 1) + "+");
                    map.NegativeColumn = colIndex++;
                    columnNames.Add("x" + (i + 1) + "-");
                    perVarPos.Add(map.PositiveColumn);
                    perVarNeg.Add(map.NegativeColumn);
                }
                else if (restriction == VariableRestriction.Negative)
                {
                    map.PositiveColumn = colIndex++;
                    map.WasNegated = true;
                    columnNames.Add("x" + (i + 1) + "'");
                    perVarPos.Add(map.PositiveColumn);
                    perVarNeg.Add(-1);
                }
                else
                {
                    map.PositiveColumn = colIndex++;
                    columnNames.Add("x" + (i + 1));
                    perVarPos.Add(map.PositiveColumn);
                    perVarNeg.Add(-1);
                }
                mappings.Add(map);
            }

            List<Constraint> expandedConstraints = new List<Constraint>(model.Constraints);
            for (int i = 0; i < n; i++)
            {
                if (model.Restrictions[i] == VariableRestriction.Bin)
                {
                    List<double> coeffs = new List<double>(new double[n]);
                    coeffs[i] = 1;
                    expandedConstraints.Add(new Constraint(coeffs, RelationType.LessOrEqual, 1));
                }
            }

            int m = expandedConstraints.Count;
            int slackSurplusCount = m;
            List<int> artificialRows = new List<int>();

            for (int r = 0; r < m; r++)
            {
                RelationType rel = expandedConstraints[r].Relation;
                if (rel == RelationType.GreaterOrEqual || rel == RelationType.Equal)
                    artificialRows.Add(r);
            }

            int decisionCols = colIndex;
            int totalCols = decisionCols + slackSurplusCount + artificialRows.Count + 1;

            Tableau tableau = new Tableau(m + 1, totalCols);
            tableau.IsMax = model.Objective.Type == ObjectiveType.Max;
            tableau.Label = "Initial Tableau (Canonical Form)";

            bool isMax = tableau.IsMax;
            for (int i = 0; i < n; i++)
            {
                double c = model.Objective.Coefficients[i];
                int pos = perVarPos[i];
                int neg = perVarNeg[i];
                double effectiveC = isMax ? c : -c;
                double signedC = mappings[i].WasNegated ? -effectiveC : effectiveC;
                tableau.Data[0, pos] = -signedC;
                if (neg >= 0) tableau.Data[0, neg] = signedC;
            }

            int slackCol = decisionCols;
            int artCol = decisionCols + slackSurplusCount;
            List<int> basicVars = new List<int>();
            int artIdx = 0;

            for (int r = 0; r < m; r++)
            {
                Constraint c = expandedConstraints[r];
                for (int i = 0; i < n; i++)
                {
                    double coeff = c.Coefficients[i];
                    int pos = perVarPos[i];
                    int neg = perVarNeg[i];
                    double signedCoeff = mappings[i].WasNegated ? -coeff : coeff;
                    tableau.Data[r + 1, pos] = signedCoeff;
                    if (neg >= 0) tableau.Data[r + 1, neg] = -signedCoeff;
                }

                double rhs = c.Rhs;
                int thisSlackCol = slackCol + r;

                if (c.Relation == RelationType.LessOrEqual)
                {
                    columnNames.Add("s" + (r + 1));
                    tableau.Data[r + 1, thisSlackCol] = 1;
                    basicVars.Add(thisSlackCol);
                }
                else if (c.Relation == RelationType.GreaterOrEqual)
                {
                    columnNames.Add("e" + (r + 1));
                    tableau.Data[r + 1, thisSlackCol] = -1;
                    int thisArtCol = artCol + artIdx++;
                    tableau.Data[r + 1, thisArtCol] = 1;
                    basicVars.Add(thisArtCol);
                }
                else
                {
                    columnNames.Add("s" + (r + 1));
                    int thisArtCol = artCol + artIdx++;
                    tableau.Data[r + 1, thisArtCol] = 1;
                    basicVars.Add(thisArtCol);
                }

                if (rhs < 0)
                    throw new InvalidOperationException(
                        "Constraint " + (r + 1) + " has a negative RHS after conversion; this build does not " +
                        "auto-flip such rows. Multiply the constraint by -1 in the input file.");

                tableau.Data[r + 1, totalCols - 1] = rhs;
            }

            for (int a = 0; a < artificialRows.Count; a++)
                columnNames.Add("a" + (a + 1));

            tableau.ColumnNames = columnNames;
            tableau.BasicVariables = basicVars;

            ConvertResult result = new ConvertResult();
            result.Tableau = tableau;
            result.Mappings = mappings;
            return result;
        }
    }
}

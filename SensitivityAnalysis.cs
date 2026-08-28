using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR_381_Project
{
    public class SensitivityEngine
    {

        public string ProblemType { get; set; } = "MAX";
        public int NumDecisionVars { get; set; }
        public int NumConstraints { get; set; }

        public double[] OriginalC { get; set; }
        public double[] OriginalB { get; set; }
        public double[,] OriginalA { get; set; }

        public List<int> BasicIndices { get; set; } = new List<int>();
        public List<int> NonBasicIndices { get; set; } = new List<int>();

        public double[,] BInverse { get; set; }
        public double[] OptimalX { get; set; }
        public double OptimalZ { get; set; }

        public SensitivityEngine(string problemType, double[] c, double[,] a, double[] b,
        List<int> basicIdx, List<int> nonBasicIdx, double[,] bInv, double[] optX, double optZ)
        {
            ProblemType = problemType.ToUpper();
            OriginalC = c;
            OriginalA = a;
            OriginalB = b;
            NumDecisionVars = c.Length;
            NumConstraints = b.Length;
            BasicIndices = basicIdx;
            NonBasicIndices = nonBasicIdx;
            BInverse = bInv;
            OptimalX = optX;
            OptimalZ = optZ;
        }



        public string GetNonBasicVariableRange(int varIdx)
        {
            if (!NonBasicIndices.Contains(varIdx))
                return $"Variable x{varIdx + 1} is currently a BASIC variable.";

            double reducedCost = CalculateReducedCost(varIdx);
            double currentCj = OriginalC[varIdx];

            if (ProblemType == "MAX")
            {

                double upperLimit = currentCj - reducedCost;
                return $"Range for Non-Basic x{varIdx + 1} coefficient (c_{varIdx + 1}): (-Infinity, {Math.Round(upperLimit, 3)}]";
            }
            else
            {
                double lowerLimit = currentCj - reducedCost;
                return $"Range for Non-Basic x{varIdx + 1} coefficient (c_{varIdx + 1}): [{Math.Round(lowerLimit, 3)}, +Infinity)";
            }
        }


        public string ApplyNonBasicVariableChange(int varIdx, double newCj)
        {
            double oldCj = OriginalC[varIdx];
            OriginalC[varIdx] = newCj;
            double newReducedCost = CalculateReducedCost(varIdx);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Changed c_{varIdx + 1} from {oldCj} to {newCj}.");
            sb.AppendLine($"New Reduced Cost c_bar_{varIdx + 1} = {Math.Round(newReducedCost, 3)}.");

            bool stillOptimal = (ProblemType == "MAX") ? (newReducedCost <= 0) : (newReducedCost >= 0);
            if (stillOptimal)
            {
                sb.AppendLine("Basis remains OPTIMAL. Solution values and Z remain unchanged.");
            }
            else
            {
                sb.AppendLine("Basis is NO LONGER OPTIMAL. Variable x" + (varIdx + 1) + " can enter the basis (Run Primal Simplex).");
            }
            return sb.ToString();
        }




        public string GetBasicVariableRange(int varIdx)
        {
            int basicPos = BasicIndices.IndexOf(varIdx);
            if (basicPos == -1)
                return $"Variable x{varIdx + 1} is not a BASIC variable.";

            double currentC = OriginalC[varIdx];
            double maxDecrease = double.PositiveInfinity;
            double maxIncrease = double.PositiveInfinity;

            foreach (int j in NonBasicIndices)
            {
                double d_kj = GetUpdatedColumn(j)[basicPos];
                double rc = CalculateReducedCost(j);

                if (ProblemType == "MAX")
                {
                    if (d_kj > 0) maxIncrease = Math.Min(maxIncrease, -rc / d_kj);
                    else if (d_kj < 0) maxDecrease = Math.Min(maxDecrease, rc / d_kj);
                }
                else
                {
                    if (d_kj > 0) maxDecrease = Math.Min(maxDecrease, rc / d_kj);
                    else if (d_kj < 0) maxIncrease = Math.Min(maxIncrease, -rc / d_kj);
                }
            }

            double minVal = double.IsInfinity(maxDecrease) ? double.NegativeInfinity : currentC - maxDecrease;
            double maxVal = double.IsInfinity(maxIncrease) ? double.PositiveInfinity : currentC + maxIncrease;

            return $"Range for Basic x{varIdx + 1} coefficient (c_{varIdx + 1}): [{Math.Round(minVal, 3)}, {Math.Round(maxVal, 3)}]";
        }


        public string ApplyBasicVariableChange(int varIdx, double newC)
        {
            OriginalC[varIdx] = newC;


            double newZ = 0;
            for (int i = 0; i < BasicIndices.Count; i++)
            {
                newZ += OriginalC[BasicIndices[i]] * OptimalX[BasicIndices[i]];
            }
            OptimalZ = newZ;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Updated c_{varIdx + 1} to {newC}.");
            sb.AppendLine($"New Optimal Objective Z = {Math.Round(OptimalZ, 3)}.");
            sb.AppendLine("Note: Recalculate reduced costs across non-basic variables to verify if re-optimization is needed.");
            return sb.ToString();
        }


        public string GetRHSRange(int constraintIdx)
        {
            double currentBi = OriginalB[constraintIdx];
            double maxDecrease = double.PositiveInfinity;
            double maxIncrease = double.PositiveInfinity;

            for (int r = 0; r < NumConstraints; r++)
            {
                double beta = BInverse[r, constraintIdx];
                double currentX_B = OptimalX[BasicIndices[r]];

                if (beta > 0)
                    maxDecrease = Math.Min(maxDecrease, currentX_B / beta);
                else if (beta < 0)
                    maxIncrease = Math.Min(maxIncrease, -currentX_B / beta);
            }

            double minVal = double.IsInfinity(maxDecrease) ? double.NegativeInfinity : currentBi - maxDecrease;
            double maxVal = double.IsInfinity(maxIncrease) ? double.PositiveInfinity : currentBi + maxIncrease;

            return $"Range for Constraint {constraintIdx + 1} RHS (b_{constraintIdx + 1}): [{Math.Round(minVal, 3)}, {Math.Round(maxVal, 3)}]";
        }


        public string ApplyRHSChange(int constraintIdx, double newBi)
        {
            double delta = newBi - OriginalB[constraintIdx];
            OriginalB[constraintIdx] = newBi;

            double[] newBasicValues = new double[NumConstraints];
            bool holdsFeasibility = true;

            for (int r = 0; r < NumConstraints; r++)
            {
                newBasicValues[r] = OptimalX[BasicIndices[r]] + (BInverse[r, constraintIdx] * delta);
                if (newBasicValues[r] < 0) holdsFeasibility = false;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Applied RHS Change for Constraint {constraintIdx + 1} to {newBi}.");

            if (holdsFeasibility)
            {
                sb.AppendLine("Current basis remains FEASIBLE. Updated variable values:");
                for (int r = 0; r < NumConstraints; r++)
                {
                    OptimalX[BasicIndices[r]] = newBasicValues[r];
                    sb.AppendLine($"  x{BasicIndices[r] + 1} = {Math.Round(newBasicValues[r], 3)}");
                }
            }
            else
            {
                sb.AppendLine("Basis becomes PRIMAL INFEASIBLE. Re-optimization via Dual Simplex is required.");
            }
            return sb.ToString();
        }


        public string GetNonBasicColumnVariableRange(int nonBasicVarIdx, int constraintIdx)
        {
            double currentA = OriginalA[constraintIdx, nonBasicVarIdx];
            double shadowPrice = GetShadowPrices()[constraintIdx];
            double currentRC = CalculateReducedCost(nonBasicVarIdx);

            if (shadowPrice == 0)
            {
                return $"Range for a_{constraintIdx + 1},{nonBasicVarIdx + 1}: (-Infinity, +Infinity) [Shadow price is 0]";
            }


            double limitDelta = currentRC / shadowPrice;
            return $"Changing a_{constraintIdx + 1},{nonBasicVarIdx + 1} by delta must satisfy: -y_{constraintIdx + 1} * delta <= {-Math.Round(currentRC, 3)}";
        }


        public string ApplyNonBasicColumnVariableChange(int nonBasicVarIdx, int constraintIdx, double newA)
        {
            OriginalA[constraintIdx, nonBasicVarIdx] = newA;
            double updatedRC = CalculateReducedCost(nonBasicVarIdx);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Updated A_{constraintIdx + 1},{nonBasicVarIdx + 1} to {newA}.");
            sb.AppendLine($"New Reduced Cost = {Math.Round(updatedRC, 3)}.");
            return sb.ToString();
        }


        public string AddNewActivity(double newC, double[] newColumnA)
        {
            double shadowPriceProduct = 0;
            double[] y = GetShadowPrices();
            for (int i = 0; i < NumConstraints; i++)
            {
                shadowPriceProduct += y[i] * newColumnA[i];
            }

            double newRC = (ProblemType == "MAX") ? (newC - shadowPriceProduct) : (shadowPriceProduct - newC);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Added New Activity x_{NumDecisionVars + 1} with c = {newC}.");
            sb.AppendLine($"Calculated Reduced Cost: {Math.Round(newRC, 3)}.");

            bool optimal = (ProblemType == "MAX") ? (newRC <= 0) : (newRC >= 0);
            if (optimal)
            {
                sb.AppendLine("New activity is NOT attractive. Current solution remains optimal (x_new = 0).");
            }
            else
            {
                sb.AppendLine("New activity IS attractive! Add column B^-1 * A_new and run Primal Simplex.");
            }
            return sb.ToString();
        }


        public string AddNewConstraint(double[] newRowA, string relation, double newRHS)
        {

            double lhsValue = 0;
            for (int j = 0; j < NumDecisionVars; j++)
            {
                lhsValue += newRowA[j] * OptimalX[j];
            }

            bool satisfied = false;
            if (relation == "<=" && lhsValue <= newRHS) satisfied = true;
            else if (relation == ">=" && lhsValue >= newRHS) satisfied = true;
            else if (relation == "=" && Math.Abs(lhsValue - newRHS) < 0.0001) satisfied = true;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"New Constraint LHS evaluation: {Math.Round(lhsValue, 3)} {relation} {newRHS}");

            if (satisfied)
            {
                sb.AppendLine("Current optimal solution SATISFIES the new constraint. Solution remains OPTIMAL.");
            }
            else
            {
                sb.AppendLine("New constraint is VIOLATED. Add slack row to tableau and run Dual Simplex.");
            }
            return sb.ToString();
        }


        public double[] GetShadowPrices()
        {
            double[] y = new double[NumConstraints];
            for (int j = 0; j < NumConstraints; j++)
            {
                double sum = 0;
                for (int i = 0; i < BasicIndices.Count; i++)
                {
                    sum += OriginalC[BasicIndices[i]] * BInverse[i, j];
                }
                y[j] = sum;
            }
            return y;
        }

        public string PrintShadowPrices()
        {
            double[] y = GetShadowPrices();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Shadow Prices (Dual Variables) ---");
            for (int i = 0; i < y.Length; i++)
            {
                sb.AppendLine($"Constraint {i + 1} (y_{i + 1}): {Math.Round(y[i], 3)}");
            }
            return sb.ToString();
        }


        public string ApplyDuality()
        {
            string dualSense = (ProblemType == "MAX") ? "MIN" : "MAX";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==========================================");
            sb.AppendLine($"DUAL PROGRAMMING MODEL ({dualSense})");
            sb.AppendLine("==========================================");


            sb.Append($"{dualSense} W = ");
            for (int i = 0; i < NumConstraints; i++)
            {
                sb.Append($"({OriginalB[i]})y_{i + 1} ");
                if (i < NumConstraints - 1) sb.Append("+ ");
            }
            sb.AppendLine();


            sb.AppendLine("Subject to:");
            for (int j = 0; j < NumDecisionVars; j++)
            {
                sb.Append("  ");
                for (int i = 0; i < NumConstraints; i++)
                {
                    sb.Append($"({OriginalA[i, j]})y_{i + 1} ");
                    if (i < NumConstraints - 1) sb.Append("+ ");
                }
                string rel = (ProblemType == "MAX") ? ">=" : "<=";
                sb.AppendLine($"{rel} {OriginalC[j]}");
            }

            sb.AppendLine("Sign Restrictions: All y_i >= 0 (assuming standard <= primal constraints)");
            return sb.ToString();
        }


        public string SolveAndVerifyDuality(double dualObjectiveValue, bool isDualFeasible)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- Duality Verification ---");
            sb.AppendLine($"Primal Objective (Z*): {Math.Round(OptimalZ, 3)}");
            sb.AppendLine($"Dual Objective   (W*): {Math.Round(dualObjectiveValue, 3)}");

            if (isDualFeasible && Math.Abs(OptimalZ - dualObjectiveValue) < 0.001)
            {
                sb.AppendLine("VERIFICATION SUCCESSFUL: Strong Duality Holds (Z* == W*).");
            }
            else if (OptimalZ <= dualObjectiveValue)
            {
                sb.AppendLine("VERIFICATION SUCCESSFUL: Weak Duality Holds (Z <= W).");
            }
            else
            {
                sb.AppendLine("Duality Gap detected or Model is Infeasible/Unbounded.");
            }
            return sb.ToString();
        }



        private double CalculateReducedCost(int varIdx)
        {
            double[] y = GetShadowPrices();
            double yA = 0;
            for (int i = 0; i < NumConstraints; i++)
            {
                yA += y[i] * OriginalA[i, varIdx];
            }
            return (ProblemType == "MAX") ? (OriginalC[varIdx] - yA) : (yA - OriginalC[varIdx]);
        }

        private double[] GetUpdatedColumn(int nonBasicVarIdx)
        {
            double[] alpha = new double[NumConstraints];
            for (int i = 0; i < NumConstraints; i++)
            {
                double sum = 0;
                for (int j = 0; j < NumConstraints; j++)
                {
                    sum += BInverse[i, j] * OriginalA[j, nonBasicVarIdx];
                }
                alpha[i] = sum;
            }
            return alpha;
        }
    }
}


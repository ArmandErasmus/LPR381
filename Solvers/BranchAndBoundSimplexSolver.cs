using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class BranchAndBoundNode
    {
        public LPModel Model;
        public List<string> Path = new List<string>();
    }

    public class BranchAndBoundResult
    {
        public SolverStatus Status { get; set; }
        public double ObjectiveValue { get; set; }
        public List<double> VariableValues { get; set; } = new List<double>();
        public List<string> NodeLog { get; set; } = new List<string>();
        public string Message { get; set; } = "";
    }

    public static class BranchAndBoundSimplexSolver
    {
        private const double IntegerTolerance = 1e-6;
        private const int MaxNodes = 500;

        public static BranchAndBoundResult Solve(LPModel model)
        {
            BranchAndBoundResult result = new BranchAndBoundResult();

            bool isMax = model.Objective.Type == ObjectiveType.Max;
            double bestObjective = isMax ? double.NegativeInfinity : double.PositiveInfinity;
            List<double> bestSolution = null;

            Queue<BranchAndBoundNode> queue = new Queue<BranchAndBoundNode>();
            queue.Enqueue(new BranchAndBoundNode { Model = model, Path = new List<string> { "Root" } });

            int nodesProcessed = 0;

            while (queue.Count > 0 && nodesProcessed < MaxNodes)
            {
                nodesProcessed++;
                BranchAndBoundNode node = queue.Dequeue();
                string label = "Node " + nodesProcessed + " (" + string.Join(" -> ", node.Path) + ")";

                ConvertResult conv;
                try
                {
                    conv = CanonicalFormConverter.Convert(node.Model);
                }
                catch (Exception ex)
                {
                    result.NodeLog.Add(label + ": could not convert to canonical form (" + ex.Message + "). Fathomed.");
                    continue;
                }

                List<string> artNames = conv.Tableau.ColumnNames.Where(n => n.StartsWith("a")).ToList();
                SimplexResult lp = PrimalSimplexSolver.Solve(conv.Tableau, artNames);

                if (lp.Status == SolverStatus.Infeasible)
                {
                    result.NodeLog.Add(label + ": INFEASIBLE. Fathomed by infeasibility.");
                    continue;
                }
                if (lp.Status == SolverStatus.Unbounded)
                {
                    result.NodeLog.Add(label + ": UNBOUNDED.");
                    result.Status = SolverStatus.Unbounded;
                    result.Message = lp.Message;
                    return result;
                }

                bool boundIsWorse = isMax ? lp.ObjectiveValue <= bestObjective + IntegerTolerance
                                           : lp.ObjectiveValue >= bestObjective - IntegerTolerance;
                if (bestSolution != null && boundIsWorse)
                {
                    result.NodeLog.Add(label + ": LP relaxation objective " + Math.Round(lp.ObjectiveValue, 3) +
                                        " no better than current best " + Math.Round(bestObjective, 3) + ". Fathomed by bound.");
                    continue;
                }

                List<double> values = ExtractRelaxationValues(node.Model, conv, lp.FinalTableau);

                int fractionalVar = -1;
                double fractionalValue = 0;
                for (int i = 0; i < node.Model.VariableCount; i++)
                {
                    if (node.Model.Restrictions[i] != VariableRestriction.Int && node.Model.Restrictions[i] != VariableRestriction.Bin)
                        continue;
                    double v = values[i];
                    double frac = v - Math.Floor(v);
                    if (frac > IntegerTolerance && frac < 1 - IntegerTolerance)
                    {
                        fractionalVar = i;
                        fractionalValue = v;
                        break;
                    }
                }

                if (fractionalVar == -1)
                {
                    result.NodeLog.Add(label + ": integer-feasible, objective = " + Math.Round(lp.ObjectiveValue, 3) + ". Candidate solution.");
                    bool better = bestSolution == null ||
                                  (isMax ? lp.ObjectiveValue > bestObjective + IntegerTolerance
                                         : lp.ObjectiveValue < bestObjective - IntegerTolerance);
                    if (better)
                    {
                        bestObjective = lp.ObjectiveValue;
                        bestSolution = values;
                        result.NodeLog.Add(label + ": new best candidate.");
                    }
                    continue;
                }

                result.NodeLog.Add(label + ": LP objective " + Math.Round(lp.ObjectiveValue, 3) +
                                    ", x" + (fractionalVar + 1) + " = " + Math.Round(fractionalValue, 3) +
                                    " is fractional. Branching.");

                LPModel floorChild = CloneWithBound(node.Model, fractionalVar, Math.Floor(fractionalValue), true);
                LPModel ceilChild = CloneWithBound(node.Model, fractionalVar, Math.Ceiling(fractionalValue), false);

                List<string> floorPath = new List<string>(node.Path) { "x" + (fractionalVar + 1) + "<=" + Math.Floor(fractionalValue) };
                List<string> ceilPath = new List<string>(node.Path) { "x" + (fractionalVar + 1) + ">=" + Math.Ceiling(fractionalValue) };

                queue.Enqueue(new BranchAndBoundNode { Model = floorChild, Path = floorPath });
                queue.Enqueue(new BranchAndBoundNode { Model = ceilChild, Path = ceilPath });
            }

            if (bestSolution == null)
            {
                result.Status = SolverStatus.Infeasible;
                result.Message = "No integer-feasible solution was found (all nodes fathomed by infeasibility or bound).";
                return result;
            }

            result.Status = SolverStatus.Optimal;
            result.ObjectiveValue = bestObjective;
            result.VariableValues = bestSolution.Select(v => Math.Round(v, 3)).ToList();
            return result;
        }

        private static List<double> ExtractRelaxationValues(LPModel model, ConvertResult conv, Tableau finalTableau)
        {
            double[] colValues = new double[finalTableau.ColCount - 1];
            for (int r = 0; r < finalTableau.BasicVariables.Count; r++)
                colValues[finalTableau.BasicVariables[r]] = finalTableau.Data[r + 1, finalTableau.ColCount - 1];

            List<double> result = new List<double>();
            foreach (VariableMapping m in conv.Mappings)
            {
                double pos = colValues[m.PositiveColumn];
                double neg = m.NegativeColumn >= 0 ? colValues[m.NegativeColumn] : 0;
                result.Add(m.Recover(pos, neg));
            }
            return result;
        }

        private static LPModel CloneWithBound(LPModel model, int varIndex, double bound, bool isUpperBound)
        {
            LPModel clone = new LPModel();
            clone.Objective = new ObjectiveFunction(model.Objective.Type, new List<double>(model.Objective.Coefficients));
            clone.Restrictions = new List<VariableRestriction>(model.Restrictions);
            clone.Constraints = model.Constraints.Select(c => new Constraint(new List<double>(c.Coefficients), c.Relation, c.Rhs)).ToList();

            List<double> coeffs = new List<double>(new double[model.VariableCount]);
            coeffs[varIndex] = 1;
            RelationType rel = isUpperBound ? RelationType.LessOrEqual : RelationType.GreaterOrEqual;
            clone.Constraints.Add(new Constraint(coeffs, rel, bound));

            return clone;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR_381_Project
{
    public class BranchAndBoundNode
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int Depth { get; set; }
        public List<LinearConstraint> Constraints { get; set; }
        public SimplexResult Relaxation { get; set; }
        public string Status { get; set; }
        public string FathomReason { get; set; }
        public int BranchVariable { get; set; }
        public double BranchValue { get; set; }

        public BranchAndBoundNode()
        {
            Constraints = new List<LinearConstraint>();
            BranchVariable = -1;
        }
    }

    public class BranchAndBoundSimplexSolver
    {
        private const double EPS = 1e-7;

        private readonly LinearModel model;
        private readonly SimplexSolver simplex;
        private readonly List<BranchAndBoundNode> nodes;
        private readonly StringBuilder log;
        private int nextNodeId;

        private double bestObjective;
        private double[] bestX;
        private bool hasCandidate;

        public BranchAndBoundSimplexSolver(LinearModel model)
        {
            this.model = model;
            simplex = new SimplexSolver();
            nodes = new List<BranchAndBoundNode>();
            log = new StringBuilder();
            nextNodeId = 1;
        }

        public string Solve()
        {
            if (model.ProblemType != "MAX" && model.ProblemType != "MIN")
                throw new InvalidOperationException("Only MAX and MIN models are supported.");

            hasCandidate = false;
            bestX = null;
            bestObjective = model.ProblemType == "MAX"
                ? double.NegativeInfinity
                : double.PositiveInfinity;

            log.AppendLine("==============================================");
            log.AppendLine("BRANCH AND BOUND SIMPLEX ALGORITHM");
            log.AppendLine("==============================================");
            log.AppendLine(model.ToCanonicalText());

            var root = new BranchAndBoundNode
            {
                Id = nextNodeId++,
                ParentId = 0,
                Depth = 0
            };

            Explore(root);

            log.AppendLine();
            log.AppendLine("==============================================");
            log.AppendLine("FINAL BEST CANDIDATE");
            log.AppendLine("==============================================");

            if (!hasCandidate)
            {
                log.AppendLine("No integer feasible solution was found.");
            }
            else
            {
                log.AppendLine("Best objective = " + bestObjective.ToString("0.000"));
                for (int i = 0; i < bestX.Length; i++)
                    log.AppendLine("x" + (i + 1) + " = " + bestX[i].ToString("0.000"));
            }

            log.AppendLine();
            log.AppendLine("NODE SUMMARY");

            foreach (BranchAndBoundNode n in nodes)
            {
                log.AppendLine(
                    "Node " + n.Id +
                    " | Parent " + n.ParentId +
                    " | Depth " + n.Depth +
                    " | Status: " + n.Status +
                    (string.IsNullOrWhiteSpace(n.FathomReason)
                        ? ""
                        : " | " + n.FathomReason));
            }

            return log.ToString();
        }

        private void Explore(BranchAndBoundNode node)
        {
            nodes.Add(node);

            log.AppendLine();
            log.AppendLine("----------------------------------------------");
            log.AppendLine("NODE " + node.Id +
                " | Parent: " + node.ParentId +
                " | Depth: " + node.Depth);

            if (node.Constraints.Count > 0)
            {
                log.AppendLine("Additional branch constraints:");
                foreach (LinearConstraint c in node.Constraints)
                    log.AppendLine("  " + ConstraintText(c));
            }
            else
            {
                log.AppendLine("Root node: no additional branch constraints.");
            }

            node.Relaxation = simplex.Solve(model, node.Constraints);

            foreach (string iteration in node.Relaxation.Iterations)
                log.AppendLine(iteration);

            if (node.Relaxation.IsInfeasible)
            {
                Fathom(node, "Fathomed by infeasibility.");
                return;
            }

            if (node.Relaxation.IsUnbounded)
            {
                node.Status = "UNBOUNDED RELAXATION";
                node.FathomReason =
                    "LP relaxation is unbounded. This model requires the special case handling module.";
                log.AppendLine(node.FathomReason);
                return;
            }

            if (!node.Relaxation.IsOptimal)
            {
                Fathom(node, "Fathomed because the LP relaxation could not be solved.");
                return;
            }

            double bound = node.Relaxation.ObjectiveValue;

            log.AppendLine("LP relaxation bound = " + bound.ToString("0.000"));

            if (hasCandidate && IsWorseOrEqual(bound, bestObjective))
            {
                Fathom(node,
                    "Fathomed by bound. The relaxation cannot improve the current best candidate.");
                return;
            }

            int fractionalVariable = FindFractionalIntegerVariable(
                node.Relaxation.X);

            if (fractionalVariable == -1)
            {
                UpdateCandidate(node.Relaxation.X, node.Relaxation.ObjectiveValue);
                node.Status = "INTEGER FEASIBLE";
                node.FathomReason = "Fathomed by integer feasibility.";
                log.AppendLine("Integer feasible solution found.");
                return;
            }

            double value = node.Relaxation.X[fractionalVariable];
            double floor = Math.Floor(value);
            double ceil = Math.Ceiling(value);

            node.BranchVariable = fractionalVariable;
            node.BranchValue = value;
            node.Status = "BRANCHED";

            log.AppendLine(
                "Fractional variable selected: x" +
                (fractionalVariable + 1) +
                " = " + value.ToString("0.000"));

            log.AppendLine(
                "Creating branches: x" +
                (fractionalVariable + 1) +
                " <= " + floor.ToString("0.000") +
                " and x" +
                (fractionalVariable + 1) +
                " >= " + ceil.ToString("0.000"));

            var left = new BranchAndBoundNode
            {
                Id = nextNodeId++,
                ParentId = node.Id,
                Depth = node.Depth + 1,
                Constraints = new List<LinearConstraint>(
                    node.Constraints.Select(c => c.Clone()))
            };

            double[] leftA = new double[model.VariableCount];
            leftA[fractionalVariable] = 1.0;
            left.Constraints.Add(new LinearConstraint(
                leftA, "<=", floor));

            var right = new BranchAndBoundNode
            {
                Id = nextNodeId++,
                ParentId = node.Id,
                Depth = node.Depth + 1,
                Constraints = new List<LinearConstraint>(
                    node.Constraints.Select(c => c.Clone()))
            };

            double[] rightA = new double[model.VariableCount];
            rightA[fractionalVariable] = 1.0;
            right.Constraints.Add(new LinearConstraint(
                rightA, ">=", ceil));

            // Depth-first backtracking.
            Explore(left);
            Explore(right);
        }

        private int FindFractionalIntegerVariable(double[] x)
        {
            int selected = -1;
            double largestFractionality = 0.0;

            for (int i = 0; i < model.VariableCount; i++)
            {
                string restriction = model.SignRestrictions[i].ToLowerInvariant();

                if (restriction != "int" && restriction != "bin")
                    continue;

                double value = x[i];

                if (restriction == "bin" &&
                    (value < -EPS || value > 1.0 + EPS))
                    return i;

                double fractionality = Math.Abs(value - Math.Round(value));

                if (fractionality > EPS && fractionality > largestFractionality)
                {
                    largestFractionality = fractionality;
                    selected = i;
                }
            }

            return selected;
        }

        private void UpdateCandidate(double[] x, double objective)
        {
            if (!hasCandidate ||
                (model.ProblemType == "MAX"
                    ? objective > bestObjective + EPS
                    : objective < bestObjective - EPS))
            {
                hasCandidate = true;
                bestObjective = objective;
                bestX = (double[])x.Clone();

                log.AppendLine(
                    "NEW BEST CANDIDATE: Z = " +
                    objective.ToString("0.000"));
            }
        }

        private bool IsWorseOrEqual(double bound, double incumbent)
        {
            if (model.ProblemType == "MAX")
                return bound <= incumbent + EPS;

            return bound >= incumbent - EPS;
        }

        private void Fathom(BranchAndBoundNode node, string reason)
        {
            node.Status = "FATHOMED";
            node.FathomReason = reason;
            log.AppendLine(reason);
        }

        private static string ConstraintText(LinearConstraint c)
        {
            return string.Join(" ",
                c.Coefficients.Select((v, i) =>
                    (v >= 0 ? "+" : "") +
                    v.ToString("0.###") +
                    "x" + (i + 1)))
                + " " + c.Relation + " " + c.RHS.ToString("0.###");
        }
    }
}

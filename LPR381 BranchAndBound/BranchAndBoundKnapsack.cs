using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR_381_Project
{
    public class KnapsackItem
    {
        public int OriginalIndex { get; set; }
        public double Weight { get; set; }
        public double Value { get; set; }
        public double Ratio
        {
            get { return Weight <= 0 ? double.PositiveInfinity : Value / Weight; }
        }
    }

    public class KnapsackNode
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int Level { get; set; }
        public double Weight { get; set; }
        public double Value { get; set; }
        public double UpperBound { get; set; }
        public string Decision { get; set; }
        public string Status { get; set; }
        public int[] Decisions { get; set; }
    }

    public class BranchAndBoundKnapsackSolver
    {
        private const double EPS = 1e-7;

        private readonly LinearModel model;
        private readonly List<KnapsackItem> items;
        private readonly List<KnapsackNode> nodes;
        private readonly StringBuilder log;
        private readonly double capacity;

        private int nextNodeId;
        private double bestValue;
        private double bestWeight;
        private int[] bestSolution;

        public BranchAndBoundKnapsackSolver(LinearModel model)
        {
            this.model = model;
            items = new List<KnapsackItem>();
            nodes = new List<KnapsackNode>();
            log = new StringBuilder();
            nextNodeId = 1;

            ValidateModel();

            capacity = model.Constraints[0].RHS;

            for (int i = 0; i < model.VariableCount; i++)
            {
                items.Add(new KnapsackItem
                {
                    OriginalIndex = i,
                    Weight = model.Constraints[0].Coefficients[i],
                    Value = model.Objective[i]
                });
            }

            items.Sort((a, b) => b.Ratio.CompareTo(a.Ratio));
        }

        public string Solve()
        {
            bestValue = double.NegativeInfinity;
            bestWeight = 0.0;
            bestSolution = new int[model.VariableCount];

            log.AppendLine("==============================================");
            log.AppendLine("BRANCH AND BOUND KNAPSACK ALGORITHM");
            log.AppendLine("==============================================");
            log.AppendLine(model.ToCanonicalText());
            log.AppendLine("Capacity = " + capacity.ToString("0.000"));

            log.AppendLine();
            log.AppendLine("ITEM ORDER USING VALUE / WEIGHT RATIO");

            foreach (KnapsackItem item in items)
            {
                log.AppendLine(
                    "x" + (item.OriginalIndex + 1) +
                    " | value = " + item.Value.ToString("0.000") +
                    " | weight = " + item.Weight.ToString("0.000") +
                    " | ratio = " + item.Ratio.ToString("0.000"));
            }

            var root = new KnapsackNode
            {
                Id = nextNodeId++,
                ParentId = 0,
                Level = 0,
                Weight = 0.0,
                Value = 0.0,
                Decisions = Enumerable.Repeat(-1, model.VariableCount).ToArray(),
                Decision = "Root"
            };

            root.UpperBound = CalculateUpperBound(root);

            Explore(root);

            log.AppendLine();
            log.AppendLine("==============================================");
            log.AppendLine("FINAL BEST CANDIDATE");
            log.AppendLine("==============================================");
            log.AppendLine("Best value = " + bestValue.ToString("0.000"));
            log.AppendLine("Total weight = " + bestWeight.ToString("0.000"));

            for (int i = 0; i < bestSolution.Length; i++)
                log.AppendLine("x" + (i + 1) + " = " + bestSolution[i]);

            log.AppendLine();
            log.AppendLine("NODE SUMMARY");

            foreach (KnapsackNode node in nodes)
            {
                log.AppendLine(
                    "Node " + node.Id +
                    " | Parent " + node.ParentId +
                    " | Level " + node.Level +
                    " | Weight " + node.Weight.ToString("0.000") +
                    " | Value " + node.Value.ToString("0.000") +
                    " | Bound " + node.UpperBound.ToString("0.000") +
                    " | " + node.Status +
                    " | " + node.Decision);
            }

            return log.ToString();
        }

        private void Explore(KnapsackNode node)
        {
            nodes.Add(node);

            log.AppendLine();
            log.AppendLine("----------------------------------------------");
            log.AppendLine("NODE " + node.Id);
            log.AppendLine("Parent = " + node.ParentId);
            log.AppendLine("Level = " + node.Level);
            log.AppendLine("Weight = " + node.Weight.ToString("0.000"));
            log.AppendLine("Value = " + node.Value.ToString("0.000"));
            log.AppendLine("Upper bound = " + node.UpperBound.ToString("0.000"));

            if (node.Weight > capacity + EPS)
            {
                node.Status = "FATHOMED";
                node.Decision = "Infeasible: capacity exceeded.";
                log.AppendLine("Fathomed by infeasibility.");
                return;
            }

            if (node.UpperBound <= bestValue + EPS)
            {
                node.Status = "FATHOMED";
                node.Decision = "Bound cannot improve incumbent.";
                log.AppendLine("Fathomed by bound.");
                return;
            }

            if (node.Level == items.Count)
            {
                UpdateBest(node);
                node.Status = "INTEGER FEASIBLE";
                node.Decision = "All binary decisions assigned.";
                log.AppendLine("Fathomed by integer feasibility.");
                return;
            }

            KnapsackItem item = items[node.Level];

            // Include branch x_i = 1.
            var include = CreateChild(node, item, 1);
            include.UpperBound = CalculateUpperBound(include);
            Explore(include);

            // Exclude branch x_i = 0.
            var exclude = CreateChild(node, item, 0);
            exclude.UpperBound = CalculateUpperBound(exclude);
            Explore(exclude);
        }

        private KnapsackNode CreateChild(KnapsackNode parent,
            KnapsackItem item, int decision)
        {
            int[] decisions = (int[])parent.Decisions.Clone();
            decisions[item.OriginalIndex] = decision;

            return new KnapsackNode
            {
                Id = nextNodeId++,
                ParentId = parent.Id,
                Level = parent.Level + 1,
                Weight = parent.Weight + decision * item.Weight,
                Value = parent.Value + decision * item.Value,
                Decisions = decisions,
                Decision = "x" + (item.OriginalIndex + 1) +
                           " = " + decision
            };
        }

        private double CalculateUpperBound(KnapsackNode node)
        {
            if (node.Weight > capacity + EPS)
                return double.NegativeInfinity;

            double weight = node.Weight;
            double value = node.Value;

            for (int level = node.Level; level < items.Count; level++)
            {
                KnapsackItem item = items[level];

                if (weight + item.Weight <= capacity + EPS)
                {
                    weight += item.Weight;
                    value += item.Value;
                }
                else
                {
                    double remaining = capacity - weight;

                    if (remaining > EPS && item.Weight > EPS)
                        value += remaining * item.Ratio;

                    break;
                }
            }

            return value;
        }

        private void UpdateBest(KnapsackNode node)
        {
            if (node.Value > bestValue + EPS)
            {
                bestValue = node.Value;
                bestWeight = node.Weight;
                bestSolution = (int[])node.Decisions.Clone();

                log.AppendLine(
                    "NEW BEST CANDIDATE: value = " +
                    bestValue.ToString("0.000"));
            }
        }

        private void ValidateModel()
        {
            if (model.ProblemType != "MAX")
                throw new InvalidOperationException(
                    "Branch and Bound Knapsack requires a maximisation model.");

            if (model.Constraints.Count != 1)
                throw new InvalidOperationException(
                    "Knapsack requires exactly one capacity constraint.");

            if (model.Constraints[0].Relation != "<=")
                throw new InvalidOperationException(
                    "Knapsack requires a <= capacity constraint.");

            for (int i = 0; i < model.VariableCount; i++)
            {
                if (model.SignRestrictions[i].ToLowerInvariant() != "bin")
                    throw new InvalidOperationException(
                        "Knapsack requires every decision variable to be binary.");

                if (model.Constraints[0].Coefficients[i] < 0)
                    throw new InvalidOperationException(
                        "Knapsack item weights must be non-negative.");
            }
        }
    }
}

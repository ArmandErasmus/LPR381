using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class KnapsackResult
    {
        public SolverStatus Status { get; set; }
        public double ObjectiveValue { get; set; }
        public List<int> Selection { get; set; } = new List<int>();
        public List<string> NodeLog { get; set; } = new List<string>();
        public string Message { get; set; } = "";
    }

    public static class BranchAndBoundKnapsackSolver
    {
        private class Item
        {
            public int OriginalIndex;
            public double Value;
            public double Weight;
            public double Ratio;
        }

        public static KnapsackResult Solve(LPModel model)
        {
            KnapsackResult result = new KnapsackResult();

            if (!model.IsPureBinaryKnapsack)
                throw new InvalidOperationException(
                    "Branch and Bound Knapsack requires a pure 0-1 Knapsack model: all variables binary, one <= constraint.");

            int n = model.VariableCount;
            double capacity = model.Constraints[0].Rhs;

            List<Item> items = new List<Item>();
            for (int i = 0; i < n; i++)
            {
                double value = model.Objective.Coefficients[i];
                double weight = model.Constraints[0].Coefficients[i];
                items.Add(new Item { OriginalIndex = i, Value = value, Weight = weight, Ratio = weight == 0 ? double.PositiveInfinity : value / weight });
            }

            List<Item> sorted = items.OrderByDescending(it => it.Ratio).ToList();

            double bestValue = double.NegativeInfinity;
            bool[] bestTake = new bool[n];

            int nodesProcessed = 0;
            Branch(sorted, 0, 0, 0, new bool[n], capacity, ref bestValue, bestTake, result, ref nodesProcessed);

            result.Status = SolverStatus.Optimal;
            result.ObjectiveValue = bestValue;
            for (int i = 0; i < n; i++)
                if (bestTake[i]) result.Selection.Add(i);

            return result;
        }

        private static void Branch(List<Item> sorted, int level, double currentValue, double currentWeight,
            bool[] taken, double capacity, ref double bestValue, bool[] bestTake, KnapsackResult result, ref int nodesProcessed)
        {
            nodesProcessed++;
            string label = "Node " + nodesProcessed + " (level " + level + ")";

            if (currentWeight > capacity)
            {
                result.NodeLog.Add(label + ": weight " + Math.Round(currentWeight, 3) + " exceeds capacity. Fathomed (infeasible).");
                return;
            }

            double bound = FractionalBound(sorted, level, currentValue, currentWeight, capacity);
            if (bound <= bestValue)
            {
                result.NodeLog.Add(label + ": bound " + Math.Round(bound, 3) + " <= best " + Math.Round(bestValue, 3) + ". Fathomed by bound.");
                return;
            }

            if (level == sorted.Count)
            {
                result.NodeLog.Add(label + ": leaf reached, value = " + Math.Round(currentValue, 3) + ".");
                if (currentValue > bestValue)
                {
                    bestValue = currentValue;
                    Array.Copy(taken, bestTake, taken.Length);
                    result.NodeLog.Add(label + ": new best candidate.");
                }
                return;
            }

            Item item = sorted[level];

            bool[] takeTrue = (bool[])taken.Clone();
            takeTrue[item.OriginalIndex] = true;
            result.NodeLog.Add(label + ": branch x" + (item.OriginalIndex + 1) + " = 1.");
            Branch(sorted, level + 1, currentValue + item.Value, currentWeight + item.Weight, takeTrue, capacity, ref bestValue, bestTake, result, ref nodesProcessed);

            bool[] takeFalse = (bool[])taken.Clone();
            takeFalse[item.OriginalIndex] = false;
            result.NodeLog.Add(label + ": branch x" + (item.OriginalIndex + 1) + " = 0.");
            Branch(sorted, level + 1, currentValue, currentWeight, takeFalse, capacity, ref bestValue, bestTake, result, ref nodesProcessed);
        }

        private static double FractionalBound(List<Item> sorted, int level, double currentValue, double currentWeight, double capacity)
        {
            double bound = currentValue;
            double remaining = capacity - currentWeight;
            for (int i = level; i < sorted.Count && remaining > 0; i++)
            {
                Item item = sorted[i];
                if (item.Weight <= remaining)
                {
                    bound += item.Value;
                    remaining -= item.Weight;
                }
                else
                {
                    bound += item.Ratio * remaining;
                    remaining = 0;
                }
            }
            return bound;
        }
    }
}

using System;

namespace LPR_381_Project
{
    public class NonLinearResult
    {
        public double BestX { get; set; }
        public double BestF { get; set; }
        public int Iterations { get; set; }
    }

    public static class NonLinearSolver
    {
        private const double GoldenRatio = 0.6180339887498949;
        private const double Tolerance = 1e-6;

        public static NonLinearResult MinimizeGoldenSection(Func<double, double> f, double lowerBound, double upperBound, int maxIterations)
        {
            double a = lowerBound;
            double b = upperBound;
            double c = b - GoldenRatio * (b - a);
            double d = a + GoldenRatio * (b - a);
            int iterations = 0;

            while (Math.Abs(b - a) > Tolerance && iterations < maxIterations)
            {
                iterations++;
                if (f(c) < f(d))
                    b = d;
                else
                    a = c;

                c = b - GoldenRatio * (b - a);
                d = a + GoldenRatio * (b - a);
            }

            double bestX = (a + b) / 2;
            return new NonLinearResult { BestX = bestX, BestF = f(bestX), Iterations = iterations };
        }
    }
}

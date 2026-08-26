using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LPR_381_Project
{
    public class LinearConstraint
    {
        public double[] Coefficients { get; set; }
        public string Relation { get; set; }
        public double RHS { get; set; }

        public LinearConstraint(double[] coefficients, string relation, double rhs)
        {
            Coefficients = (double[])coefficients.Clone();
            Relation = relation;
            RHS = rhs;
        }

        public LinearConstraint Clone()
        {
            return new LinearConstraint(Coefficients, Relation, RHS);
        }
    }

    public class LinearModel
    {
        public string ProblemType { get; set; }
        public double[] Objective { get; set; }
        public List<LinearConstraint> Constraints { get; set; }
        public string[] SignRestrictions { get; set; }

        public int VariableCount
        {
            get { return Objective.Length; }
        }

        public LinearModel(string problemType, double[] objective,
            List<LinearConstraint> constraints, string[] signRestrictions)
        {
            ProblemType = problemType.ToUpperInvariant();
            Objective = (double[])objective.Clone();
            Constraints = constraints.Select(c => c.Clone()).ToList();
            SignRestrictions = signRestrictions == null
                ? Enumerable.Repeat("+", objective.Length).ToArray()
                : (string[])signRestrictions.Clone();
        }

        public LinearModel Clone()
        {
            return new LinearModel(ProblemType, Objective, Constraints, SignRestrictions);
        }

        public void AddConstraint(double[] coefficients, string relation, double rhs)
        {
            if (coefficients.Length != VariableCount)
                throw new ArgumentException("Constraint has the wrong number of coefficients.");

            Constraints.Add(new LinearConstraint(coefficients, relation, rhs));
        }

        public string ToCanonicalText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("MODEL");
            sb.AppendLine(ProblemType + " " + string.Join(" ",
                Objective.Select((v, i) => FormatSigned(v) + "x" + (i + 1))));
            sb.AppendLine("Subject to:");

            foreach (var c in Constraints)
            {
                sb.AppendLine("  " + string.Join(" ",
                    c.Coefficients.Select((v, i) => FormatSigned(v) + "x" + (i + 1)))
                    + " " + c.Relation + " " + c.RHS.ToString("0.###"));
            }

            sb.AppendLine("Sign restrictions: " +
                string.Join(" ", SignRestrictions));
            return sb.ToString();
        }

        private static string FormatSigned(double value)
        {
            return (value >= 0 ? "+" : "") + value.ToString("0.###");
        }

        public static LinearModel LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Input file was not found.", path);

            string[] raw = File.ReadAllLines(path)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            if (raw.Length < 2)
                throw new FormatException("The input file must contain an objective, at least one constraint, and sign restrictions.");

            string[] first = raw[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (first.Length < 3)
                throw new FormatException("The objective line is invalid.");

            string type = first[0].ToUpperInvariant();
            if (type != "MAX" && type != "MIN")
                throw new FormatException("The first word must be max or min.");

            if ((first.Length - 1) % 2 != 0)
                throw new FormatException("Objective coefficients must be supplied as sign and value pairs.");

            int n = (first.Length - 1) / 2;
            double[] objective = new double[n];

            for (int i = 0; i < n; i++)
            {
                string sign = first[1 + 2 * i];
                string value = first[2 + 2 * i];

                objective[i] = ParseSignedNumber(sign, value);
            }

            var constraints = new List<LinearConstraint>();
            string[] signs = null;

            for (int lineIndex = 1; lineIndex < raw.Length; lineIndex++)
            {
                string[] tokens = raw[lineIndex].Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length == n &&
                    tokens.All(t => IsSignRestriction(t)))
                {
                    signs = tokens;
                    break;
                }

                if (tokens.Length != 2 * n + 2)
                    throw new FormatException(
                        "Constraint line " + (lineIndex + 1) +
                        " does not contain the expected number of values.");

                double[] a = new double[n];

                for (int i = 0; i < n; i++)
                    a[i] = ParseSignedNumber(tokens[2 * i], tokens[2 * i + 1]);

                string relation = tokens[2 * n];
                if (relation != "<=" && relation != ">=" && relation != "=")
                    throw new FormatException("Invalid constraint relation: " + relation);

                double rhs;
                if (!double.TryParse(tokens[2 * n + 1],
                    NumberStyles.Float, CultureInfo.InvariantCulture, out rhs))
                    throw new FormatException("Invalid RHS value: " + tokens[2 * n + 1]);

                constraints.Add(new LinearConstraint(a, relation, rhs));
            }

            if (signs == null)
                throw new FormatException("Sign restrictions were not found.");

            return new LinearModel(type, objective, constraints, signs);
        }

        private static bool IsSignRestriction(string value)
        {
            string x = value.ToLowerInvariant();
            return x == "+" || x == "-" || x == "urs" || x == "int" || x == "bin";
        }

        private static double ParseSignedNumber(string sign, string value)
        {
            double number;
            if (!double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out number))
                throw new FormatException("Invalid coefficient: " + sign + " " + value);

            if (sign == "-")
                return -Math.Abs(number);

            if (sign == "+")
                return Math.Abs(number);

            throw new FormatException("Invalid coefficient sign: " + sign);
        }
    }
}

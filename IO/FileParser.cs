using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPR_381_Project
{
    public static class FileParser
    {
        private static readonly Regex SignedNumber = new Regex(@"^[+-]\d+(\.\d+)?$", RegexOptions.Compiled);
        private static readonly Regex RelationSplit = new Regex(@"^(<=|>=|=)\s*([+-]?\d+(\.\d+)?)$", RegexOptions.Compiled);

        public static LPModel ParseFile(string path)
        {
            if (!File.Exists(path))
                throw new InputValidationException("Input file not found: \"" + path + "\". Check the path and try again.");

            string[] rawLines;
            try
            {
                rawLines = File.ReadAllLines(path);
            }
            catch (Exception ex)
            {
                throw new InputValidationException("Could not read the input file: " + ex.Message);
            }

            List<string> lines = rawLines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

            if (lines.Count < 3)
                throw new InputValidationException(
                    "Input file is incomplete. It must contain an objective line, at least one " +
                    "constraint line, and a sign-restriction line.");

            LPModel model = new LPModel();
            model.Objective = ParseObjective(lines[0]);
            int n = model.Objective.VariableCount;
            model.Restrictions = ParseRestrictions(lines[lines.Count - 1], n);

            for (int i = 1; i < lines.Count - 1; i++)
                model.Constraints.Add(ParseConstraint(lines[i], n, i + 1));

            if (model.Constraints.Count == 0)
                throw new InputValidationException("No constraints were found between the objective line and the sign-restriction line.");

            return model;
        }

        private static ObjectiveFunction ParseObjective(string line)
        {
            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
                throw new InputValidationException(
                    "Line 1 (objective function) is malformed. Expected: \"max\"/\"min\" followed by " +
                    "one signed coefficient per decision variable, e.g. \"max +2 +3 +5\".");

            string keyword = tokens[0].ToLowerInvariant();
            ObjectiveType type;
            if (keyword == "max") type = ObjectiveType.Max;
            else if (keyword == "min") type = ObjectiveType.Min;
            else throw new InputValidationException("Line 1 must start with \"max\" or \"min\" (found \"" + tokens[0] + "\").");

            List<double> coeffs = new List<double>();
            for (int i = 1; i < tokens.Length; i++)
                coeffs.Add(ParseSignedNumber(tokens[i], "objective coefficient #" + i));

            if (coeffs.Count == 0)
                throw new InputValidationException("Line 1 has no decision-variable coefficients.");

            return new ObjectiveFunction(type, coeffs);
        }

        private static Constraint ParseConstraint(string line, int expectedVarCount, int lineNumber)
        {
            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < expectedVarCount + 1)
                throw new InputValidationException(
                    "Line " + lineNumber + " (constraint) has too few tokens. Expected " + expectedVarCount +
                    " coefficients plus a relation and RHS, e.g. \"+1 +2 <=10\".");

            List<double> coeffs = new List<double>();
            for (int i = 0; i < expectedVarCount; i++)
                coeffs.Add(ParseSignedNumber(tokens[i], "line " + lineNumber + ", coefficient #" + (i + 1)));

            string tail = string.Join("", tokens.Skip(expectedVarCount)).Replace(" ", "");
            Match match = RelationSplit.Match(tail);
            if (!match.Success)
                throw new InputValidationException(
                    "Line " + lineNumber + " has an invalid relation/RHS (\"" + tail + "\"). Expected one of " +
                    "<=, >=, = followed by a number, e.g. \"<=40\".");

            string relOp = match.Groups[1].Value;
            RelationType relation;
            if (relOp == "<=") relation = RelationType.LessOrEqual;
            else if (relOp == ">=") relation = RelationType.GreaterOrEqual;
            else if (relOp == "=") relation = RelationType.Equal;
            else throw new InputValidationException("Line " + lineNumber + " has an unrecognised relation operator.");

            double rhs = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

            return new Constraint(coeffs, relation, rhs);
        }

        private static List<VariableRestriction> ParseRestrictions(string line, int expectedVarCount)
        {
            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != expectedVarCount)
                throw new InputValidationException(
                    "The sign-restriction line has " + tokens.Length + " entries but the objective function " +
                    "defines " + expectedVarCount + " decision variables. There must be exactly one restriction per variable.");

            List<VariableRestriction> result = new List<VariableRestriction>();
            foreach (string t in tokens)
            {
                string lt = t.ToLowerInvariant();
                if (lt == "+") result.Add(VariableRestriction.Positive);
                else if (lt == "-") result.Add(VariableRestriction.Negative);
                else if (lt == "urs") result.Add(VariableRestriction.Urs);
                else if (lt == "int") result.Add(VariableRestriction.Int);
                else if (lt == "bin") result.Add(VariableRestriction.Bin);
                else throw new InputValidationException("Unrecognised sign restriction \"" + t + "\". Must be one of: +, -, urs, int, bin.");
            }
            return result;
        }

        private static double ParseSignedNumber(string token, string context)
        {
            if (!SignedNumber.IsMatch(token))
                throw new InputValidationException(
                    "Invalid number at " + context + ": \"" + token + "\". Every coefficient must include an explicit " +
                    "+ or - sign, e.g. \"+2\" or \"-3.5\".");

            return double.Parse(token, CultureInfo.InvariantCulture);
        }
    }
}

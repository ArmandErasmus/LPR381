using System.Collections.Generic;

namespace LPR_381_Project
{
    public class ObjectiveFunction
    {
        public ObjectiveType Type { get; set; }
        public List<double> Coefficients { get; set; } = new List<double>();
        public int VariableCount { get { return Coefficients.Count; } }

        public ObjectiveFunction() { }

        public ObjectiveFunction(ObjectiveType type, List<double> coefficients)
        {
            Type = type;
            Coefficients = coefficients;
        }
    }
}

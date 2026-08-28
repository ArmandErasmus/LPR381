using System.Collections.Generic;

namespace LPR_381_Project
{
    public class Constraint
    {
        public List<double> Coefficients { get; set; } = new List<double>();
        public RelationType Relation { get; set; }
        public double Rhs { get; set; }

        public Constraint() { }

        public Constraint(List<double> coefficients, RelationType relation, double rhs)
        {
            Coefficients = coefficients;
            Relation = relation;
            Rhs = rhs;
        }
    }
}

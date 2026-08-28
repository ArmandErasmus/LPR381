using System.Collections.Generic;
using System.Linq;

namespace LPR_381_Project
{
    public class LPModel
    {
        public ObjectiveFunction Objective { get; set; }
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();
        public List<VariableRestriction> Restrictions { get; set; } = new List<VariableRestriction>();

        public int VariableCount { get { return Objective == null ? 0 : Objective.Coefficients.Count; } }
        public int ConstraintCount { get { return Constraints.Count; } }

        public bool IsIntegerProgram
        {
            get { return Restrictions.Any(r => r == VariableRestriction.Int || r == VariableRestriction.Bin); }
        }

        public bool IsPureBinaryKnapsack
        {
            get
            {
                return Restrictions.Count > 0 &&
                       Restrictions.All(r => r == VariableRestriction.Bin) &&
                       Constraints.Count == 1 &&
                       Constraints[0].Relation == RelationType.LessOrEqual;
            }
        }

        public bool IsStructurallyConsistent()
        {
            if (Objective == null) return false;
            int n = VariableCount;
            if (n == 0) return false;
            if (Restrictions.Count != n) return false;
            return Constraints.All(c => c.Coefficients.Count == n);
        }
    }
}

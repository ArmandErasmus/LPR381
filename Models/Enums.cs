namespace LPR_381_Project
{
    public enum ObjectiveType
    {
        Max,
        Min
    }

    public enum RelationType
    {
        LessOrEqual,
        GreaterOrEqual,
        Equal
    }

    public enum VariableRestriction
    {
        Positive,
        Negative,
        Urs,
        Int,
        Bin
    }

    public enum SolverStatus
    {
        Optimal,
        Infeasible,
        Unbounded,
        IterationLimitReached
    }
}

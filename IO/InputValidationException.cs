using System;

namespace LPR_381_Project
{
    public class InputValidationException : Exception
    {
        public InputValidationException(string message) : base(message) { }
    }
}

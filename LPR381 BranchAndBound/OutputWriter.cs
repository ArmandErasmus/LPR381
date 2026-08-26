using System;
using System.IO;
using System.Text;

namespace LPR_381_Project
{
    public static class OutputWriter
    {
        public static void Write(string path, string canonicalForm, string algorithmOutput)
        {
            var sb = new StringBuilder();

            sb.AppendLine("LPR381 PROGRAMMING PROJECT");
            sb.AppendLine("==============================================");
            sb.AppendLine("CANONICAL / INPUT MODEL");
            sb.AppendLine("==============================================");
            sb.AppendLine(canonicalForm);
            sb.AppendLine();
            sb.AppendLine(algorithmOutput);

            File.WriteAllText(path, sb.ToString());
        }
    }
}

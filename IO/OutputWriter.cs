using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace LPR_381_Project
{
    public static class OutputWriter
    {
        public static void WriteIterations(string path, string algorithmName, IEnumerable<Tableau> iterations, string finalMessage)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Algorithm: " + algorithmName);
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();

            foreach (Tableau t in iterations)
            {
                sb.AppendLine(t.ToString());
                sb.AppendLine(new string('-', 60));
            }

            if (!string.IsNullOrEmpty(finalMessage))
            {
                sb.AppendLine();
                sb.AppendLine(finalMessage);
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}

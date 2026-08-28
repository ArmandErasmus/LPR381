using System;
using System.Collections.Generic;
using System.Text;

namespace LPR_381_Project
{
    public class Tableau
    {
        public double[,] Data { get; set; }
        public List<string> ColumnNames { get; set; } = new List<string>();
        public List<int> BasicVariables { get; set; } = new List<int>();
        public bool IsMax { get; set; }
        public string Label { get; set; } = "";

        public int RowCount { get { return Data.GetLength(0); } }
        public int ColCount { get { return Data.GetLength(1); } }

        public Tableau(int rows, int cols)
        {
            Data = new double[rows, cols];
        }

        public Tableau Clone()
        {
            Tableau copy = new Tableau(RowCount, ColCount);
            copy.ColumnNames = new List<string>(ColumnNames);
            copy.BasicVariables = new List<int>(BasicVariables);
            copy.IsMax = IsMax;
            copy.Label = Label;
            Array.Copy(Data, copy.Data, Data.Length);
            return copy;
        }

        public double Rhs(int row)
        {
            return Data[row, ColCount - 1];
        }

        public void Pivot(int pivotRow, int pivotCol)
        {
            double pivotVal = Data[pivotRow, pivotCol];
            if (Math.Abs(pivotVal) < 1e-9)
                throw new InvalidOperationException("Attempted to pivot on a zero element.");

            for (int c = 0; c < ColCount; c++)
                Data[pivotRow, c] /= pivotVal;

            for (int r = 0; r < RowCount; r++)
            {
                if (r == pivotRow) continue;
                double factor = Data[r, pivotCol];
                if (Math.Abs(factor) < 1e-12) continue;
                for (int c = 0; c < ColCount; c++)
                    Data[r, c] -= factor * Data[pivotRow, c];
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Label))
                sb.AppendLine(Label);

            sb.Append("Basis\t");
            foreach (string name in ColumnNames) sb.Append(name + "\t");
            sb.AppendLine("RHS");

            for (int r = 0; r < RowCount; r++)
            {
                string rowLabel = r == 0 ? "z" : ColumnNames[BasicVariables[r - 1]];
                sb.Append(rowLabel + "\t");
                for (int c = 0; c < ColCount; c++)
                    sb.Append(Math.Round(Data[r, c], 3).ToString("0.000") + "\t");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gurobi;
using System.IO;

namespace Crossword
{
    public class mae_sdo
    {
        /// <summary>
        /// I used this Gurobi model to calculate the fixed point baseline.
        /// </summary>
        public mae_sdo()
        {
            string path = @"C:\Users\Roman Bolzern\Desktop\D4\neu\meta_data_training.csv";

            var lines = File.ReadLines(path).ToArray();

            GRBEnv env = new GRBEnv();
            GRBModel m = new GRBModel(env);

            var scale = 1e9;

            var y = m.AddVar(1e-9*scale, 1e-2 * scale, 0d, GRB.CONTINUOUS, "y");

            var obj = new GRBLinExpr();

            for (int i = 1; i < lines.Length; i++)
            {
                var diff = double.Parse(lines[i].Split(',')[3]) * scale - y;
                var diffinput = m.AddVar(-1e-2 * scale, 1e-2 * scale, 0d, GRB.CONTINUOUS, "diffinput");
                m.AddConstr(diffinput == diff);
                var diffres = m.AddVar(0, 1e-2 * scale, 0d, GRB.CONTINUOUS, "diffres");
                m.AddGenConstrAbs(diffres, diffinput, "diffAbs");
                obj += diffres;
            }

            m.SetObjective(obj, GRB.MINIMIZE);
            m.Optimize();

            Console.WriteLine($"Objective: {((GRBLinExpr)m.GetObjective()).Value / scale}");
            Console.WriteLine($"fixed point: {y.X / scale}");
        }
    }
}

using Crossword.Fields;
using Gurobi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    public class WordsSolver2
    {
        public WordsSolver2(Crossword cwd)
        {
            var wordsArray = File.ReadLines(@"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\useful\wordlist.txt").ToArray();
            var wordsList = wordsArray.GroupBy(f => f.Length).ToDictionary(f => f.Key, f => f.ToList());

            int sizeY = cwd.Grid.GetLength(0);
            int sizeX = cwd.Grid.GetLength(1);

            GRBEnv env = new GRBEnv();
            GRBModel m = new GRBModel(env);

            // letters - (0), A-Z (1-27)
            

            for (int y = 0; y < cwd.Grid.GetLength(0); y++)
            {
                for (int x = 0; x < cwd.Grid.GetLength(1); x++)
                {
                    if (cwd.Grid[y, x] is Question)
                    {
                        
                    }
                }
            }

            //m.SetObjective(deadFieldPenalty + clusterPenalty, GRB.MINIMIZE);

            m.Optimize();
            //m.ComputeIIS();
            //m.Write("model.ilp");

            m.Dispose();
            env.Dispose();
        }
    }
}

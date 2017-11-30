using Crossword.Fields;
using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    public class GurobiSolver
    {
        public GurobiSolver(Crossword crossword)
        {
            GRBEnv env = new GRBEnv();
            GRBModel model = new GRBModel(env);

            int sizeY = crossword.Grid.GetLength(0);
            int sizeX = crossword.Grid.GetLength(1);

            GRBVar[,] fields = new GRBVar[sizeY, sizeX];
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (!(crossword.Grid[y, x] is Blocked))
                    {
                        fields[y, x] = new 
                            }
                }
            }
        }
    }
}

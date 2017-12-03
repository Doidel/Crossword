using Crossword.Fields;
using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    class GRBMipSolCallback : GRBCallback
    {
        private GRBVar[,] fields;
        private GRBVar[,] questionType;

        public GRBMipSolCallback(GRBVar[,] fields, GRBVar[,] questionType)
        {
            this.fields = fields;
            this.questionType = questionType;
        }

        protected override void Callback()
        {
            if (where == GRB.Callback.MIPSOL) // new mip incumbent
            {
                var height = fields.GetLength(0);
                var width = fields.GetLength(1);

                Console.WriteLine("-----------MIPSOL------------");
                Field[,] res = new Field[height, width];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var letterOrQuestion = GetSolution(fields[y, x]) > 0.5 ? 1 : 0;
                        if (letterOrQuestion == 1)
                        {
                            var qType = GetSolution(questionType[y, x]) > 0.5 ? 1 : 0;
                            res[y, x] = new Question(qType == 0 ? Question.ArrowType.Right : Question.ArrowType.Down);
                        }
                        else
                        {
                            res[y, x] = new Letter('x');
                        }
                    }
                }

                Crossword cw = new Crossword(res);
                cw.Draw();
            }
        }
    }
}

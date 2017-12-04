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
        private bool saveBest;

        public static Crossword Best;
        public static double BestScore;

        public GRBMipSolCallback(GRBVar[,] fields, GRBVar[,] questionType, bool saveBest = true)
        {
            this.fields = fields;
            this.questionType = questionType;
            this.saveBest = saveBest;
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
                            res[y, x] = new Letter('.');
                        }
                    }
                }

                Crossword cw = new Crossword(res);
                cw.Draw();

                if (saveBest)
                {
                    var newScore = cw.Score();
                    double newScoreTotal = 1d;
                    foreach (var k in newScore.Keys)
                        newScoreTotal += newScore[k];
                    newScoreTotal /= newScore.Count;
                    if (Best == null || BestScore < newScoreTotal)
                    {
                        Best = cw;
                        BestScore = newScoreTotal;
                        cw.Save("best15x15");
                    }
                }
            }
        }
    }
}

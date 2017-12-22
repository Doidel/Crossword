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
        private Crossword inputCw;
        private GRBVar[,] fields;
        private GRBVar[,] questionType;
        private bool saveBest;

        public static Crossword[] Best = new Crossword[3];
        public static double[] BestScores = new double[3];

        public GRBMipSolCallback(Crossword inputCrossword, GRBVar[,] fields, GRBVar[,] questionType, bool saveBest = true)
        {
            this.inputCw = inputCrossword;
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
                        if (!inputCw.HasBlock(y,x))
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
                        } else
                        {
                            res[y, x] = new Blocked();
                        }
                    }
                }

                Crossword cw = new Crossword(res);
                cw.Draw();

                // Add lazy constraint to cut off current solution
                //AddLazy()

                if (saveBest)
                {
                    var newScore = cw.Score();
                    double newScoreTotal = 0d;
                    foreach (var k in newScore.Keys)
                        newScoreTotal += Math.Max(0, newScore[k]);
                    newScoreTotal /= newScore.Count;
                    for (int i = 0; i < 3; i++)
                    {
                        if (BestScores[i] < newScoreTotal)
                        {
                            var cw_temp = Best[i];
                            var score_temp = BestScores[i];
                            Best[i] = cw;
                            BestScores[i] = newScoreTotal;
                            cw.Save(cw.Grid.GetLength(0) + "x" + cw.Grid.GetLength(1) + "_" + (i+1));

                            cw = cw_temp;
                            newScoreTotal = score_temp;
                        }
                    }
                }
                Console.WriteLine("-----------------------------");
            }
        }
    }
}

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
        private GRBVar[,,] specialQuestionType;
        private GRBVar[] wordCounts;

        private bool saveBest;
        public static Crossword[] Best = new Crossword[3];
        public static double[] BestScores = new double[3];

        public GRBMipSolCallback(Crossword inputCrossword, GRBVar[,] fields, GRBVar[,] questionType, GRBVar[,,] specialQuestionType, bool saveBest = true, GRBVar[] wordCounts = null)
        {
            this.inputCw = inputCrossword;
            this.fields = fields;
            this.questionType = questionType;
            this.saveBest = saveBest;
            this.specialQuestionType = specialQuestionType;
            this.wordCounts = wordCounts;
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
                        if (!inputCw.HasBlock(y, x))
                        {
                            var letterOrQuestion = GetSolution(fields[y, x]) > 0.5 ? 1 : 0;
                            if (letterOrQuestion == 1)
                            {
                                if (specialQuestionType != null)
                                {
                                    for (int type = 0; type < 4; type++)
                                    {
                                        if ((object)specialQuestionType[y, x, type] != null)
                                        {
                                            if (GetSolution(specialQuestionType[y, x, type]) > 0.5)
                                            {
                                                // 0 = Down, then right
                                                // 1 = Left, then down
                                                // 2 = Right, then down
                                                // 3 = Up, then right
                                                switch (type)
                                                {
                                                    case 0:
                                                        res[y, x] = new Question(Question.ArrowType.DownRight);
                                                        break;
                                                    case 1:
                                                        res[y, x] = new Question(Question.ArrowType.LeftDown);
                                                        break;
                                                    case 2:
                                                        res[y, x] = new Question(Question.ArrowType.RightDown);
                                                        break;
                                                    case 3:
                                                        res[y, x] = new Question(Question.ArrowType.UpRight);
                                                        break;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (res[y, x] == null)
                                {
                                    var qType = GetSolution(questionType[y, x]) > 0.5 ? 1 : 0;
                                    res[y, x] = new Question(qType == 0 ? Question.ArrowType.Right : Question.ArrowType.Down);
                                }
                            }
                            else
                            {
                                res[y, x] = new Empty();
                            }
                        }
                        else
                        {
                            res[y, x] = new Blocked();
                        }
                    }
                }

                Crossword cw = new Crossword(res);
                cw.Draw();

                if (wordCounts != null)
                {
                    var counts = wordCounts.Select(wc => GetSolution(wc)).ToArray();
                    var total = counts.Sum();
                    var hist = new Dictionary<int, int>() {
                        { 2, 0 },
                        { 3, 18 },
                        { 4, 24 },
                        { 5, 20 },
                        { 6, 18 },
                        { 7, 12 },
                        { 8, 4 },
                        { 9, 4 }
                    };
                    var diffs = counts.Select((c, i) => Math.Round(Math.Abs(c / total * 100 - hist[i + 2]))).ToArray();
                    Console.WriteLine("WordCounts Model: " + string.Join(",", counts));
                    Console.WriteLine("In %: " + string.Join(",", counts.Select(c => Math.Round(c / total * 100)).ToArray()));
                    Console.WriteLine("Diff: " + string.Join(",", diffs));
                    Console.WriteLine("Diff Total: " + diffs.Average());
                }

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
                            cw.Save("_" + (i + 1));

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

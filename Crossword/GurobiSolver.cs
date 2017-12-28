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
            var wordLengthHistogram = new Dictionary<int, int>() {
                { 3, 18 },
                { 4, 24 },
                { 5, 20 },
                { 6, 18 },
                { 7, 12 },
                { 8, 4 },
                { 9, 4 },
            };

            const int maxWordLength = 9;

            int sizeY = crossword.Grid.GetLength(0);
            int sizeX = crossword.Grid.GetLength(1);

            int amountQuestions = (int)Math.Round(0.22 * sizeX * sizeY);


            GRBEnv env = new GRBEnv();
            GRBModel m = new GRBModel(env);

            // 0 = letter, 1 = question
            GRBVar[,] fields = new GRBVar[sizeY, sizeX];
            // 0 = right, 1 = down
            GRBVar[,] questionType = new GRBVar[sizeY, sizeX];


            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    // create a var for every non-blocked field
                    if (!(crossword.Grid[y, x] is Blocked))
                    {
                        fields[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "Field" + x + "_" + y);
                        questionType[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "QType" + x + "_" + y);
                    }
                }
            }

            GRBLinExpr allFieldsSum = new GRBLinExpr();

            // count word lengths (to compare with histogram)
            var lengths = new Dictionary<int, GRBLinExpr>();
            foreach (var l in wordLengthHistogram.Keys)
                lengths.Add(l, new GRBLinExpr());

            var partOfAWord = new GRBLinExpr[sizeY, sizeX];
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++)
                    partOfAWord[y, x] = new GRBLinExpr();

            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    allFieldsSum += fields[y, x];

                    foreach (var l in wordLengthHistogram.Keys)
                    {
                        if (x + l < sizeX)
                        {
                            // + 1 if the following fields are all words and the last one is not a word (question or outside grid)
                            var wordCount = new GRBLinExpr();
                            for (int i = 1; i <= l; i++)
                                wordCount += fields[y, x + i];

                            // last field is question or outside?
                            var lastFieldIsNoWord = m.AddVar(0, 1, 0, GRB.BINARY, "lastFieldIsNoWord" + y + "_" + x + "_" + l);
                            if (x + l + 1 < sizeX)
                                m.AddConstr(lastFieldIsNoWord == fields[y, x + l + 1], "lastFieldIsInGridAndIsQuestion" + y + "_" + x + "_" + l);
                            else
                                m.AddConstr(lastFieldIsNoWord == 1, "lastFieldOutsideGrid" + y + "_" + x + "_" + l);

                            // https://cs.stackexchange.com/questions/51025/cast-to-boolean-for-integer-linear-programming
                            var hasCorrectWordCount = m.AddVar(0, 1, 0, GRB.BINARY, "hasCorrect" + y + "_" + x + "_" + l);
                            var hcTemp = m.AddVar(0, 1, 0, GRB.BINARY, "hasCorrectTemp" + y + "_" + x + "_" + l);
                            var worddiff = wordCount - l;
                            m.AddConstr(-sizeX * hasCorrectWordCount <= worddiff, "ToBoolean" + y + "_" + x + "_" + l + "_1");
                            m.AddConstr(worddiff <= sizeX * hasCorrectWordCount, "ToBoolean" + y + "_" + x + "_" + l + "_2");
                            m.AddConstr(0.001 * hasCorrectWordCount - (sizeX + 0.001) * hcTemp <= worddiff, "ToBoolean" + y + "_" + x + "_" + l + "_3");
                            m.AddConstr(worddiff <= -0.001 * hasCorrectWordCount + (sizeX + 0.001) * (1 - hcTemp), "ToBoolean" + y + "_" + x + "_" + l + "_4");

                            // firstFieldIsQuestion AND questionIsHorizontal AND hasCorrectWordCount AND lastFieldIsNoWord
                            var allConditionsMet = m.AddVar(0, 1, 0, GRB.BINARY, "allCondsMet" + y + "_" + x + "_" + l);
                            var condVal = fields[y, x] + (1 - questionType[y, x]) + hasCorrectWordCount + lastFieldIsNoWord - 3;
                            m.AddConstr(allConditionsMet >= condVal, "AllConditionsAND" + y + "_" + x + "_" + l + "_1");
                            m.AddConstr(allConditionsMet <= fields[y, x], "AllConditionsAND" + y + "_" + x + "_" + l + "_2");
                            m.AddConstr(allConditionsMet <= (1 - questionType[y, x]), "AllConditionsAND" + y + "_" + x + "_" + l + "_3");
                            m.AddConstr(allConditionsMet <= hasCorrectWordCount, "AllConditionsAND" + y + "_" + x + "_" + l + "_4");
                            m.AddConstr(allConditionsMet <= lastFieldIsNoWord, "AllConditionsAND" + y + "_" + x + "_" + l + "_5");
                            lengths[l] += allConditionsMet;

                            // If all conditions are met, all letters are part of a word
                            for (int i = 1; i <= l; i++)
                                partOfAWord[y, x + i] += allConditionsMet;
                        }

                        if (y + l < sizeY)
                        {
                            // + 1 if the following fields are all words and the last one is not a word (question or outside grid)
                            var wordCount = new GRBLinExpr();
                            for (int i = 1; i <= l; i++)
                                wordCount += fields[y + i, x];

                            // last field is question or outside?
                            var lastFieldIsNoWord = m.AddVar(0, 1, 0, GRB.BINARY, "lastFieldIsNoWord" + y + "_" + x + "_" + l);
                            if (y + l + 1 < sizeY)
                                m.AddConstr(lastFieldIsNoWord == fields[y + l + 1, x], "lastFieldIsInGridAndIsQuestion" + y + "_" + x + "_" + l);
                            else
                                m.AddConstr(lastFieldIsNoWord == 1, "lastFieldOutsideGrid" + y + "_" + x + "_" + l);

                            // https://cs.stackexchange.com/questions/51025/cast-to-boolean-for-integer-linear-programming
                            var hasCorrectWordCount = m.AddVar(0, 1, 0, GRB.BINARY, "hasCorrect" + y + "_" + x + "_" + l);
                            var hcTemp = m.AddVar(0, 1, 0, GRB.BINARY, "hasCorrectTemp" + y + "_" + x + "_" + l);
                            var worddiff = wordCount - l;
                            m.AddConstr(-sizeY * hasCorrectWordCount <= worddiff, "ToBoolean" + y + "_" + x + "_" + l + "_1");
                            m.AddConstr(worddiff <= sizeY * hasCorrectWordCount, "ToBoolean" + y + "_" + x + "_" + l + "_2");
                            m.AddConstr(0.001 * hasCorrectWordCount - (sizeY + 0.001) * hcTemp <= worddiff, "ToBoolean" + y + "_" + x + "_" + l + "_3");
                            m.AddConstr(worddiff <= -0.001 * hasCorrectWordCount + (sizeY + 0.001) * (1 - hcTemp), "ToBoolean" + y + "_" + x + "_" + l + "_4");

                            // firstFieldIsQuestion AND questionIsHorizontal AND hasCorrectWordCount AND lastFieldIsNoWord
                            var allConditionsMet = m.AddVar(0, 1, 0, GRB.BINARY, "allCondsMet" + y + "_" + x + "_" + l);
                            var condVal = fields[y, x] + (questionType[y, x]) + hasCorrectWordCount + lastFieldIsNoWord - 3;
                            m.AddConstr(allConditionsMet >= condVal, "AllConditionsAND" + y + "_" + x + "_" + l + "_1");
                            m.AddConstr(allConditionsMet <= fields[y, x], "AllConditionsAND" + y + "_" + x + "_" + l + "_2");
                            m.AddConstr(allConditionsMet <= (questionType[y, x]), "AllConditionsAND" + y + "_" + x + "_" + l + "_3");
                            m.AddConstr(allConditionsMet <= hasCorrectWordCount, "AllConditionsAND" + y + "_" + x + "_" + l + "_4");
                            m.AddConstr(allConditionsMet <= lastFieldIsNoWord, "AllConditionsAND" + y + "_" + x + "_" + l + "_5");
                            lengths[l] += allConditionsMet;

                            // If all conditions are met, all letters are part of a word
                            for (int i = 1; i <= l; i++)
                                partOfAWord[y + i, x] += allConditionsMet;
                        }
                    }

                    // max word count
                    if (x + maxWordLength < sizeX)
                    {
                        var maxWordCount = new GRBLinExpr();
                        var range = maxWordLength + 1;
                        if (x + range == sizeX) range--;
                        for (int i = 0; i < maxWordLength + 1; i++)
                            maxWordCount += 1 - fields[y, x + i];
                        m.AddConstr(maxWordCount <= maxWordLength, "maxWordsX" + y + "_" + x);
                    }
                    if (y + maxWordLength < sizeY)
                    {
                        var maxWordCount = new GRBLinExpr();
                        var range = maxWordLength + 1;
                        if (y + range == sizeY) range--;
                        for (int i = 0; i < maxWordLength + 1; i++)
                            maxWordCount += 1 - fields[y + i, x];
                        m.AddConstr(maxWordCount <= maxWordLength, "maxWordsY" + y + "_" + x);
                    }
                }
            }


            // All non-question fields have to belong to a word
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    m.AddConstr(1 - fields[y, x] <= partOfAWord[y, x], "NonQuestionsToWords" + y + "_" + x + "_1");
                    m.AddConstr(1 - fields[y, x] >= partOfAWord[y, x] * 0.5, "NonQuestionsToWords" + y + "_" + x + "_2");
                }
            }

            // after a question, no word of length 1 or 2 is allowed
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (x < sizeX - 3)
                    {
                        // if there's a question AND it's pointing right THEN sum(fields[y, x+1/2/3]) = 0
                        var minLetters = 3 - (fields[y, x + 1] + fields[y, x + 2] + fields[y, x + 3]);
                        m.AddConstr(fields[y, x] + (1 - questionType[y, x]) - 1 <= minLetters * (1d / 3d), "AfterRightQuestionMin2Letters" + y + "_" + x);
                    }
                    else
                    {
                        m.AddConstr(questionType[y, x] == 1, "NoRightQuestionAtRightBorder" + y + "_" + x);
                    }

                    if (y < sizeY - 3)
                    {
                        // if there's a question AND it's pointing down THEN sum(fields[y+1/2/3, x]) = 0
                        var minLetters = 3 - (fields[y + 1, x] + fields[y + 2, x] + fields[y + 3, x]);
                        m.AddConstr(fields[y, x] + questionType[y, x] - 1 <= minLetters * (1d / 3d), "AfterDownQuestionMin2Letters" + y + "_" + x);
                    }
                    else if (x < sizeX - 3)
                    {
                        m.AddConstr(questionType[y, x] == 0, "NoDownQuestionAtBottomBorder" + y + "_" + x);
                    }
                    else
                    {
                        m.AddConstr(fields[y, x] == 0, "OnlyWordsInBottomrightCorner" + y + "_" + x);
                    }
                }
            }

            // Objective:
            // questions should be around ~22% (allFieldsSum ~= amountQuestions)
            var amountOfQuestionsRating = m.AddVar(0, sizeX * sizeY - amountQuestions, 0, GRB.INTEGER, "amountOfQuestionsRating");
            var amountOfQuestionsAbsInput = m.AddVar(-amountQuestions, sizeX * sizeY - amountQuestions, 0, GRB.INTEGER, "amountOfQuestionsAbsInput");
            m.AddConstr(amountOfQuestionsAbsInput == allFieldsSum - amountQuestions);
            m.AddGenConstrAbs(amountOfQuestionsRating, amountOfQuestionsAbsInput, "amountOfQuestionsAbs");
            /*int tolerance = (int)(amountQuestions * 0.1);
            m.AddConstr(allFieldsSum - amountQuestions >= -tolerance);
            m.AddConstr(allFieldsSum - amountQuestions <= tolerance);*/

            // as many partOfAWord == 2 as possible
            var manyCrossedWords = new GRBLinExpr();
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++)
                    manyCrossedWords += partOfAWord[y, x];

            // ideal histogram comparison
            var wordHistogramDifferences = new GRBQuadExpr();
            foreach (var wl in wordLengthHistogram.Keys)
            {
                var diff = (wordLengthHistogram[wl] - lengths[wl]) * 2;
                wordHistogramDifferences += diff * diff;
            }

            // question field clusters
            var clusterPenalty = new GRBLinExpr();
            for (int y = 0; y < sizeY - 2; y++)
            {
                for (int x = 0; x < sizeX - 2; x++)
                {
                    var clusterTotal = new GRBLinExpr();
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            clusterTotal += fields[y + i, x + j];
                        }
                    }
                    var varClusterTotalPenalty = m.AddVar(0, 7, 0, GRB.INTEGER, "varClusterTotalPenalty" + y + "_" + x);
                    m.AddConstr(varClusterTotalPenalty >= clusterTotal - 2);
                    clusterPenalty += varClusterTotalPenalty;
                }
            }

            //amountOfQuestionsRating * (100d / sizeX / sizeY) + manyCrossedWords +  + wordHistogramDifferences
            // clusterPenalty * 100
            m.SetObjective(amountOfQuestionsRating * (100d / sizeX / sizeY) + clusterPenalty * 100 + wordHistogramDifferences + manyCrossedWords, GRB.MINIMIZE);

            m.SetCallback(new GRBMipSolCallback(crossword, fields, questionType, null));

            m.Optimize();
            m.ComputeIIS();
            m.Write("model.ilp");

            m.Dispose();
            env.Dispose();
        }
    }
}

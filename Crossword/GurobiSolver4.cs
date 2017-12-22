using Crossword.Fields;
using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    /// <summary>
    /// Introduce special question types and blocks
    /// </summary>
    public class GurobiSolver4
    {
        public GurobiSolver4(Crossword crossword)
        {
            var wordLengthHistogram = new Dictionary<int, double>() {
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


            var requiredAmountOfLetters = wordLengthHistogram.Sum(wl => wl.Key * wl.Value) / 1.8d;
            int totalFields = sizeX * sizeY;
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++)
                    totalFields -= crossword.Grid[y, x] is Blocked ? 1 : 0;

            int amountQuestions = (int)Math.Round(0.22 * totalFields);

            var wordLengthRatio = requiredAmountOfLetters / (totalFields - amountQuestions);

            GRBEnv env = new GRBEnv();
            GRBModel m = new GRBModel(env);

            // 0 = letter, 1 = question
            GRBVar[,] fields = new GRBVar[sizeY, sizeX];

            // 0 = right, 1 = down
            GRBVar[,] questionType = new GRBVar[sizeY, sizeX];

            // 0 = Down, then right
            // 1 = Left, then down
            // 2 = Right, then down
            // 3 = Up, then right
            // Mostly null, except for places down and right of a blocked field or y==0 or x==0
            GRBVar[,,] specialQuestionType = new GRBVar[sizeY, sizeX, 4];


            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    // create a var for every non-blocked field
                    if (!(crossword.Grid[y, x] is Blocked))
                    {
                        fields[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "Field" + x + "_" + y);
                        questionType[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "QType" + x + "_" + y);

                        // Is null except for places down and right of a blocked field or y==0 or x==0
                        if (y == 0 || x == 0 || crossword.Grid[y - 1, x] is Blocked || crossword.Grid[y, x - 1] is Blocked)
                        {
                            for (int t = 0; t < 4; t++)
                            {
                                specialQuestionType[y, x, t] = m.AddVar(0, 1, 0, GRB.BINARY, "SpecialQType" + t + "_" + x + "_" + y);
                            }
                            // Max 1 special type, can also be no special question
                            m.AddConstr(specialQuestionType[y, x, 0] + specialQuestionType[y, x, 1] + specialQuestionType[y, x, 2] + specialQuestionType[y, x, 3] <= 1);
                        }
                    }
                }
            }
            // [0,0] HAS to be a special question
            m.AddConstr(specialQuestionType[0, 0, 0] + specialQuestionType[0, 0, 1] + specialQuestionType[0, 0, 2] + specialQuestionType[0, 0, 3] == 1);


            // TODO specialQuestions logic from here

            GRBLinExpr allFieldsSum = new GRBLinExpr();

            // All non-question fields have to belong to a word
            // E.g. if a question points right, only lengths 3 to 9 are allowed
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    allFieldsSum += fields[y, x];

                    bool noQuestionToTheRightAllowed = false;
                    bool noQuestionTowardsDownAllowed = false;
                    bool specialQuestionsAllowed = y == 0 || x == 0 || crossword.Grid[y - 1, x] is Blocked || crossword.Grid[y, x - 1] is Blocked;
                    
                    if (!specialQuestionsAllowed)
                    {
                        if (x + 3 < sizeX)
                        {
                            // for right: if [0,0] is question, [0,1..3] must not be question or end
                            var totalQuestionsHorizontal = fields[y, x + 1] + fields[y, x + 2] + fields[y, x + 3];
                            m.AddConstr(fields[y, x] + (1 - questionType[y, x]) - 1 <= 1 - fields[y, x + 1], "MinWordLength3" + y + "_" + x + "_right1");
                            m.AddConstr(fields[y, x] + (1 - questionType[y, x]) - 1 <= 1 - fields[y, x + 2], "MinWordLength3" + y + "_" + x + "_right2");
                            m.AddConstr(fields[y, x] + (1 - questionType[y, x]) - 1 <= 1 - fields[y, x + 3], "MinWordLength3" + y + "_" + x + "_right3");
                        }
                        else
                        {
                            noQuestionToTheRightAllowed = true;
                        }

                        // for down:
                        if (y + 3 < sizeY)
                        {
                            var totalQuestionsVertical = fields[y + 1, x] + fields[y + 2, x] + fields[y + 3, x];
                            m.AddConstr(fields[y, x] + questionType[y, x] - 1 <= 1 - fields[y + 1, x], "MinWordLength3" + y + "_" + x + "_down1");
                            m.AddConstr(fields[y, x] + questionType[y, x] - 1 <= 1 - fields[y + 2, x], "MinWordLength3" + y + "_" + x + "_down2");
                            m.AddConstr(fields[y, x] + questionType[y, x] - 1 <= 1 - fields[y + 3, x], "MinWordLength3" + y + "_" + x + "_down3");
                        }
                        else
                        {
                            noQuestionTowardsDownAllowed = true;
                        }

                        if (noQuestionToTheRightAllowed && noQuestionTowardsDownAllowed)
                        {
                            m.AddConstr(fields[y, x] == 0, "NoQuestionAllowed" + y + "_" + x);
                        }
                        else
                        {
                            if (noQuestionToTheRightAllowed) m.AddConstr(questionType[y, x] == 1, "QuestionCantPointRight" + y + "_" + x);
                            if (noQuestionTowardsDownAllowed) m.AddConstr(questionType[y, x] == 0, "QuestionCantPointDown" + y + "_" + x);
                        }

                        // max word length constraints
                        if (x + maxWordLength + 1 < sizeX)
                        {
                            // for right: if [0,0] is question, [0,1..maxLength+1] must have at least another question field
                            var allHorizontalFields = new GRBLinExpr();
                            for (int xi = 1; xi <= maxWordLength + 1; xi++)
                                allHorizontalFields += fields[y, x + xi];
                            m.AddConstr(fields[y, x] + (1 - questionType[y, x]) - 1 <= allHorizontalFields, "MaxLengthHorizontal" + y + "_" + x);
                        }
                        if (y + maxWordLength + 1 < sizeY)
                        {
                            // for down:
                            var allVerticalFields = new GRBLinExpr();
                            for (int yi = 1; yi <= maxWordLength + 1; yi++)
                                allVerticalFields += fields[y + yi, x];
                            m.AddConstr(fields[y, x] + questionType[y, x] - 1 <= allVerticalFields, "MaxLengthVertical" + y + "_" + x);
                        }
                    }
                    else
                    {

                    }
                }
            }

            // Does a field belong to zero, one or two questions?
            var partOfAWord = new GRBLinExpr[sizeY, sizeX, 2];
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    // this constraint doesn't work for [0,0]
                    if (x == 0 && y == 0) continue;

                    // does this field have a question to the left?
                    var attachedToHorizontalQuestion = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToHorizontalQuestion" + y + "_" + x);
                    for (int len = 1; len <= maxWordLength; len++)
                    {
                        if (x - len < 0) continue;
                        var isQuestionAndPointsRight = fields[y, x - len] + (1 - questionType[y, x - len]);
                        var questionsInbetween = new GRBLinExpr();
                        for (int xi = 0; xi < len; xi++)
                            questionsInbetween += fields[y, x - xi];
                        m.AddConstr(attachedToHorizontalQuestion >= isQuestionAndPointsRight - 1 - questionsInbetween);

                        // 0 IF first question is not pointing right OR there is no question to the left
                        // firstQuestion ==> total fields < 2
                        m.AddConstr(attachedToHorizontalQuestion <= questionsInbetween + (1 - fields[y, x - len]) + 1 - questionType[y, x - len]); // the first question but DOESNT look right
                    }
                    var questionsToTheLeft = new GRBLinExpr();
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (x - len < 0 || crossword.Grid[y, x - len] is Blocked) continue;
                        questionsToTheLeft += fields[y, x - len];
                    }
                    m.AddConstr(attachedToHorizontalQuestion <= questionsToTheLeft);

                    // does this field have a question towards down?
                    var attachedToVerticalQuestion = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToVerticalQuestion" + y + "_" + x);
                    for (int len = 1; len <= maxWordLength; len++)
                    {
                        if (y - len < 0) continue;
                        var isQuestionAndPointsDown = fields[y - len, x] + questionType[y - len, x];
                        var questionsInbetween = new GRBLinExpr();
                        for (int yi = 0; yi < len; yi++)
                            questionsInbetween += fields[y - yi, x];
                        m.AddConstr(attachedToVerticalQuestion >= isQuestionAndPointsDown - 1 - questionsInbetween);

                        m.AddConstr(attachedToVerticalQuestion <= questionsInbetween + (1 - fields[y - len, x]) + 1 - (1 - questionType[y - len, x])); // the first question but DOESNT look down
                    }
                    var questionsTowardsDown = new GRBLinExpr();
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (y - len < 0 || crossword.Grid[y - len, x] is Blocked) continue;
                        questionsTowardsDown += fields[y - len, x];
                    }
                    m.AddConstr(attachedToVerticalQuestion <= questionsTowardsDown);

                    var c = m.AddConstr(attachedToHorizontalQuestion + attachedToVerticalQuestion >= 1 - fields[y, x], "AttachedToQuestionConstraint_" + y + "_" + x);
                    partOfAWord[y, x, 0] = attachedToHorizontalQuestion;
                    partOfAWord[y, x, 1] = attachedToVerticalQuestion;
                }
            }

            // right now, [0,0] can only be a question
            m.AddConstr(fields[0, 0] == 1);
            // and similarly the bottom 3x3 can only be letters
            for (int y = sizeY - 3; y < sizeY; y++)
                for (int x = sizeX - 3; x < sizeX; x++)
                    m.AddConstr(fields[y, x] == 0);

            // Objective:
            // questions should be around ~22% (allFieldsSum ~= amountQuestions)
            int tolerance = (int)(amountQuestions * 0.1);
            m.AddConstr(allFieldsSum - amountQuestions >= -tolerance, "amountOfQuestionsTolerance_1");
            m.AddConstr(allFieldsSum - amountQuestions <= tolerance, "amountOfQuestionsTolerance_2");

            // dead fields
            var uncrossedLetters = new GRBVar[sizeY, sizeX];
            var uncrossedLettersPenalty = new GRBLinExpr();
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (x >= 1 || y >= 1)
                    {
                        uncrossedLetters[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "isUncrossedLetter" + y + "_" + x);
                        m.AddConstr(uncrossedLetters[y, x] <= partOfAWord[y, x, 0] + partOfAWord[y, x, 1]); // if 0 ==> 0 NECESSARY?
                        m.AddConstr(uncrossedLetters[y, x] <= 2 - partOfAWord[y, x, 0] - partOfAWord[y, x, 1]); // if 2 ==> 0
                        m.AddConstr(uncrossedLetters[y, x] <= 1 - fields[y, x]); // if it's a question it can't be a dead field

                        m.AddConstr(uncrossedLetters[y, x] >= partOfAWord[y, x, 0] - partOfAWord[y, x, 1] - fields[y, x]); // horizontal XOR vertical
                        m.AddConstr(uncrossedLetters[y, x] >= partOfAWord[y, x, 1] - partOfAWord[y, x, 0] - fields[y, x]);
                        uncrossedLettersPenalty += uncrossedLetters[y, x];
                    }
                }
            }
            // penalty for nearby uncrossed letters
            var deadFieldPenalty = new GRBLinExpr();
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (x >= 1 && y >= 1)
                    {
                        var isDeadArea = m.AddVar(0, 1, 0, GRB.BINARY, "isDeadArea" + y + "_" + x);
                        m.AddConstr(isDeadArea >= uncrossedLetters[y, x] + uncrossedLetters[y - 1, x] - 1);
                        m.AddConstr(isDeadArea >= uncrossedLetters[y, x] + uncrossedLetters[y, x - 1] - 1);
                        m.AddConstr(isDeadArea <= uncrossedLetters[y, x]);
                        m.AddConstr(isDeadArea <= uncrossedLetters[y - 1, x] + uncrossedLetters[y, x - 1]);
                        deadFieldPenalty += isDeadArea;
                    }
                }
            }


            // as many partOfAWord == 2 as possible
            /*var manyCrossedWords = new GRBLinExpr();
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++)
                    manyCrossedWords += partOfAWord[y, x];*/

            // ideal histogram comparison
            //var wordHistogramDifferences = new GRBLinExpr();
            foreach (var wl in wordLengthHistogram.Keys)
            {
                /*var varDiffInput = m.AddVar(-sizeX * sizeY / 3, sizeX * sizeY / 3, 0, GRB.INTEGER, "varDiffInput" + wl);
                m.AddConstr(varDiffInput == (wordLengthHistogram[wl] - lengths[wl]));
                var varDiffRes = m.AddVar(0, sizeX * sizeY / 3, 0, GRB.INTEGER, "varDiff" + wl);
                m.AddGenConstrAbs(varDiffRes, varDiffInput, "diffGenConstr" + wl);
                wordHistogramDifferences += varDiffRes;*/

                int histogramTolerance = Math.Max(1, (int)(wordLengthHistogram[wl] * 0.2 * wordLengthRatio));
                //m.AddConstr(wordLengthHistogram[wl] - lengths[wl] >= -histogramTolerance);
                //m.AddConstr(wordLengthHistogram[wl] - lengths[wl] <= histogramTolerance);
            }

            // question field clusters
            // in a field of 2x2, minimize the nr of fields where there are 2-4 questions resp. maximize 0-1 questions
            var clusterPenalty = new GRBLinExpr();
            int area = 2;
            for (int y = 0; y < sizeY - (area - 1); y++)
            {
                for (int x = 0; x < sizeX - (area - 1); x++)
                {
                    var clusterTotal = new GRBLinExpr();
                    for (int i = 0; i < area; i++)
                    {
                        for (int j = 0; j < area; j++)
                        {
                            clusterTotal += fields[y + i, x + j];
                        }
                    }
                    var varClusterTotalPenalty = m.AddVar(0, 1, 0, GRB.BINARY, "varClusterTotalPenalty" + y + "_" + x);
                    // 0-1 = good, 2-4 = bad
                    m.AddConstr(varClusterTotalPenalty <= clusterTotal * 0.5, "clusterPenaltyConstr1_" + y + "_" + x);
                    m.AddConstr(varClusterTotalPenalty >= (clusterTotal - 1) * (1d / 3), "clusterPenaltyConstr2_" + y + "_" + x);
                    clusterPenalty += varClusterTotalPenalty;
                }
            }

            //m.AddConstr(deadFieldPenalty <= 30);

            //amountOfQuestionsRating * (100d / sizeX / sizeY) + manyCrossedWords +  + wordHistogramDifferences
            // clusterPenalty * 100
            m.SetObjective(deadFieldPenalty, GRB.MINIMIZE);

            m.SetCallback(new GRBMipSolCallback(crossword, fields, questionType));

            m.Optimize();
            m.ComputeIIS();
            m.Write("model.ilp");

            m.Dispose();
            env.Dispose();
        }
    }
}

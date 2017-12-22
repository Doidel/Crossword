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
    /// Rewrite word lengths
    /// </summary>
    public class GurobiSolver3
    {
        public GurobiSolver3(Crossword crossword)
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

            // All non-question fields have to belong to a word
            // E.g. if a question points right, only lengths 3 to 9 are allowed
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    allFieldsSum += fields[y, x];

                    bool noQuestionToTheRightAllowed = false;
                    bool noQuestionTowardsDownAllowed = false;
                    if (x + 3 < sizeX)
                    {
                        // for right: if [0,0] is question, [0,1..3] must not be question
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
                    /*var attachedToHorizontalQuestion = new GRBLinExpr();
                    for (int l = 0; l < maxWordLength; l++)
                    {
                        if (x - l - 1 < 0) continue;
                        var attachedToHorizontalQuestionSpecificLength = m.AddVar(0, 1, 0, GRB.BINARY, "varAttachedToHorizontalQuestionLength" + l + "_" + y + "_" + x);
                        // If the first field is a question and points to the right, and no letters inbetween are questions
                        var isQuestionAndPointsRight = fields[y, x - l - 1] + (1 - questionType[y, x - l - 1]);
                        var allHorizontalFields = new GRBLinExpr();
                        for (int xi = 0; xi <= l; xi++)
                            allHorizontalFields += fields[y, x - xi];
                        m.AddConstr(attachedToHorizontalQuestionSpecificLength >= isQuestionAndPointsRight - 1 - allHorizontalFields);
                        for (int xi = 0; xi <= l; xi++) m.AddConstr(attachedToHorizontalQuestionSpecificLength <= 1 - fields[y, x - xi]);
                        m.AddConstr(attachedToHorizontalQuestionSpecificLength <= fields[y, x - l - 1]);
                        m.AddConstr(attachedToHorizontalQuestionSpecificLength <= 1 - questionType[y, x - l - 1]);
                        //m.AddConstr(attachedToHorizontalQuestionSpecificLength <= isQuestionAndPointsRight * 0.5);
                        //m.AddConstr(attachedToHorizontalQuestionSpecificLength <= 1 - allHorizontalFields * (1d / (l + 1)));
                        attachedToHorizontalQuestion += attachedToHorizontalQuestionSpecificLength;
                    }*/
                    // RETRY
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
                    /*var attachedToVerticalQuestion = new GRBLinExpr();
                    for (int l = 0; l < maxWordLength; l++)
                    {
                        if (y - l - 1 < 0) continue;
                        var attachedToVerticalQuestionSpecificLength = m.AddVar(0, 1, 0, GRB.BINARY, "varAttachedToVerticalQuestionLength" + l + "_" + y + "_" + x);
                        // If the first field is a question and points to the right, and no letters inbetween are questions
                        var isQuestionAndPointsDown = fields[y - l - 1, x] + questionType[y - l - 1, x];
                        var allVerticalFields = new GRBLinExpr();
                        for (int yi = 0; yi <= l; yi++)
                            allVerticalFields += fields[y - yi, x];
                        m.AddConstr(attachedToVerticalQuestionSpecificLength >= isQuestionAndPointsDown - 1 - allVerticalFields);
                        for (int yi = 0; yi <= l; yi++) m.AddConstr(attachedToVerticalQuestionSpecificLength <= 1 - fields[y - yi, x]);
                        m.AddConstr(attachedToVerticalQuestionSpecificLength <= fields[y - l - 1, x]);
                        m.AddConstr(attachedToVerticalQuestionSpecificLength <= questionType[y - l - 1, x]);
                        //m.AddConstr(attachedToVerticalQuestionSpecificLength <= isQuestionAndPointsDown * 0.5);
                        //m.AddConstr(attachedToVerticalQuestionSpecificLength <= 1 - allVerticalFields * (1d / (l + 1)));
                        attachedToVerticalQuestion += attachedToVerticalQuestionSpecificLength;
                    }*/
                    var attachedToVerticalQuestion = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToVerticalQuestion" + y + "_" + x);
                    for (int len = 1; len <= maxWordLength; len++)
                    {
                        if (y - len < 0) continue;
                        var isQuestionAndPointsDown = fields[y - len, x] + questionType[y - len, x];
                        var questionsInbetween = new GRBLinExpr();
                        for (int yi = 0; yi < len; yi++)
                            questionsInbetween += fields[y - yi, x];
                        m.AddConstr(attachedToVerticalQuestion >= isQuestionAndPointsDown - 1 - questionsInbetween);

                        m.AddConstr(attachedToVerticalQuestion <= questionsInbetween + (1-fields[y - len, x]) + 1 - (1 - questionType[y - len, x])); // the first question but DOESNT look down
                    }
                    var questionsTowardsDown = new GRBLinExpr();
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (y - len < 0 || crossword.Grid[y - len, x] is Blocked) continue;
                        questionsTowardsDown += fields[y - len, x];
                    }
                    m.AddConstr(attachedToVerticalQuestion <= questionsTowardsDown);

                    var c = m.AddConstr(attachedToHorizontalQuestion + attachedToVerticalQuestion >= 1 - fields[y, x], "AttachedToQuestionConstraint_" + y + "_" + x);
                    //c.Lazy = 1;
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
            /*var uncrossedLetters = new GRBVar[sizeY, sizeX];
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
            }*/


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
            m.SetObjective(clusterPenalty, GRB.MINIMIZE);

            m.SetCallback(new GRBMipSolCallback(fields, questionType));

            m.Optimize();
            m.ComputeIIS();
            m.Write("model.ilp");

            m.Dispose();
            env.Dispose();
        }
    }
}

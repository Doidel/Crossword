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


            var specialQuestionUsed = new GRBLinExpr[sizeY, sizeX];
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
                            specialQuestionUsed[y, x] = specialQuestionType[y, x, 0] + specialQuestionType[y, x, 1] + specialQuestionType[y, x, 2] + specialQuestionType[y, x, 3];
                            // Max 1 special type, can also be no special question
                            m.AddConstr(specialQuestionUsed[y, x] <= 1);
                        }
                    }
                }
            }




            // TODO specialQuestions logic from here

            GRBLinExpr allFieldsSum = new GRBLinExpr();

            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (crossword.Grid[y, x] is Blocked)
                        continue;


                    allFieldsSum += fields[y, x];

                    bool noQuestionToTheRightAllowed = false;
                    bool noQuestionTowardsDownAllowed = false;
                    if (x + 3 < sizeX && !crossword.HasBlock(y, x, y, x + 3))
                    {
                        // for right: if [0,0] is question, [0,1..3] must not be question
                        var totalQuestionsHorizontal = fields[y, x + 1] + fields[y, x + 2] + fields[y, x + 3];
                        var isQuestionAndPointsRight = fields[y, x] + (1 - questionType[y, x]) - 1;
                        if ((object)specialQuestionUsed[y, x] != null)
                            isQuestionAndPointsRight += (1 - specialQuestionUsed[y, x]) - 1;
                        m.AddConstr(isQuestionAndPointsRight <= 1 - fields[y, x + 1], "MinWordLength3" + y + "_" + x + "_right1");
                        m.AddConstr(isQuestionAndPointsRight <= 1 - fields[y, x + 2], "MinWordLength3" + y + "_" + x + "_right2");
                        m.AddConstr(isQuestionAndPointsRight <= 1 - fields[y, x + 3], "MinWordLength3" + y + "_" + x + "_right3");
                    }
                    else
                    {
                        noQuestionToTheRightAllowed = true;
                    }

                    // for down:
                    if (y + 3 < sizeY && !crossword.HasBlock(y, x, y + 3, x))
                    {
                        var totalQuestionsVertical = fields[y + 1, x] + fields[y + 2, x] + fields[y + 3, x];
                        var isQuestionAndPointsDown = fields[y, x] + questionType[y, x] - 1;
                        if ((object)specialQuestionUsed[y, x] != null)
                            isQuestionAndPointsDown += (1 - specialQuestionUsed[y, x]) - 1;
                        m.AddConstr(isQuestionAndPointsDown <= 1 - fields[y + 1, x], "MinWordLength3" + y + "_" + x + "_down1");
                        m.AddConstr(isQuestionAndPointsDown <= 1 - fields[y + 2, x], "MinWordLength3" + y + "_" + x + "_down2");
                        m.AddConstr(isQuestionAndPointsDown <= 1 - fields[y + 3, x], "MinWordLength3" + y + "_" + x + "_down3");
                    }
                    else
                    {
                        noQuestionTowardsDownAllowed = true;
                    }


                    bool atLeastOneSpecialAllowed = false;
                    if ((object)specialQuestionUsed[y, x] != null)
                    {
                        // down, then right
                        if (y + 1 < sizeY && x + 2 < sizeX && !crossword.HasBlock(y + 1, x, y + 1, x + 2))
                        {
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= 1 - fields[y + 1, x]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= 1 - fields[y + 1, x + 1]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= 1 - fields[y + 1, x + 2]);
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 0] == 0);
                        }
                        // left, then down
                        if (y + 2 < sizeY && x - 1 >= 0 && !crossword.HasBlock(y, x - 1, y + 2, x - 1))
                        {
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= 1 - fields[y, x - 1]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= 1 - fields[y + 1, x - 1]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= 1 - fields[y + 2, x - 1]);
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 1] == 0);
                        }
                        // right, then down
                        if (y + 2 < sizeY && x + 1 < sizeX && !crossword.HasBlock(y, x + 1, y + 2, x + 1))
                        {
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= 1 - fields[y, x + 1]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= 1 - fields[y + 1, x + 1]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= 1 - fields[y + 2, x + 1]);
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 2] == 0);
                        }
                        // up, then right
                        if (y - 1 >= 0 && x + 2 < sizeX && !crossword.HasBlock(y - 1, x, y - 1, x + 2))
                        {
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= 1 - fields[y - 1, x]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= 1 - fields[y - 1, x + 1]);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= 1 - fields[y - 1, x + 2]);
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 3] == 0);
                        }

                        if (!atLeastOneSpecialAllowed)
                        {
                            m.AddConstr(specialQuestionUsed[y, x] == 0);
                        }
                    }

                    if (noQuestionToTheRightAllowed && noQuestionTowardsDownAllowed)
                    {
                        if (!atLeastOneSpecialAllowed)
                        {
                            m.AddConstr(fields[y, x] == 0, "NoQuestionAllowed" + y + "_" + x);
                        }
                        else
                        {
                            m.AddConstr(specialQuestionUsed[y, x] == 1);
                        }
                    }
                    else
                    {
                        if (noQuestionToTheRightAllowed) m.AddConstr(questionType[y, x] == 1, "QuestionCantPointRight" + y + "_" + x);
                        if (noQuestionTowardsDownAllowed) m.AddConstr(questionType[y, x] == 0, "QuestionCantPointDown" + y + "_" + x);
                    }

                    // max word length constraints
                    // for right: if [0,0] is question, [0,1..maxLength+1] must have at least another question field
                    if (x + maxWordLength + 1 < sizeX && !crossword.HasBlock(y, x, y, x + maxWordLength + 1))
                    {
                        var allHorizontalFields = new GRBLinExpr();
                        for (int xi = 1; xi <= maxWordLength + 1; xi++)
                            allHorizontalFields += fields[y, x + xi];
                        if ((object)specialQuestionUsed[y, x] != null)
                            m.AddConstr(fields[y, x] + (1 - questionType[y, x]) + (1 - specialQuestionUsed[y, x]) - 2 <= allHorizontalFields, "MaxLengthHorizontal" + y + "_" + x);
                        else
                            m.AddConstr(fields[y, x] + (1 - questionType[y, x]) - 1 <= allHorizontalFields, "MaxLengthHorizontal" + y + "_" + x);
                    }
                    // for down:
                    if (y + maxWordLength + 1 < sizeY && !crossword.HasBlock(y, x, y + maxWordLength + 1, x))
                    {
                        var fieldsSum = fields.SumRange(y + 1, x, y + maxWordLength + 1, x);
                        if ((object)specialQuestionUsed[y, x] != null)
                            m.AddConstr(fields[y, x] + questionType[y, x] + (1 - specialQuestionUsed[y, x]) - 2 <= fieldsSum, "MaxLengthVertical" + y + "_" + x);
                        else
                            m.AddConstr(fields[y, x] + questionType[y, x] - 1 <= fieldsSum, "MaxLengthVertical" + y + "_" + x);
                    }
                    if ((object)specialQuestionUsed[y, x] != null)
                    {
                        // down, then right
                        if (y + 1 < sizeY && x + maxWordLength < sizeX && !crossword.HasBlock(y + 1, x, y + 1, x + maxWordLength))
                        {
                            var fieldsSum = fields.SumRange(y + 1, x, y + 1, x + maxWordLength);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fieldsSum, "MaxLengthSpecialQuestion0_" + y + "_" + x);
                            // if there is a normal field to the left of the word, it has to be a question
                            if (x - 1 >= 0 && !crossword.HasBlock(y + 1, x - 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y + 1, x - 1]);
                        }
                        // left, then down
                        if (y + maxWordLength < sizeY && x - 1 >= 0 && !crossword.HasBlock(y, x - 1, y + maxWordLength, x - 1))
                        {
                            var fieldsSum = fields.SumRange(y, x - 1, y + maxWordLength, x - 1);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= fieldsSum, "MaxLengthSpecialQuestion1_" + y + "_" + x);
                            if (y - 1 >= 0 && !crossword.HasBlock(y - 1, x - 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y - 1, x - 1]);
                        }
                        // right, then down
                        if (y + maxWordLength < sizeY && x + 1 < sizeX && !crossword.HasBlock(y, x + 1, y + maxWordLength, x + 1))
                        {
                            var fieldsSum = fields.SumRange(y, x + 1, y + maxWordLength, x + 1);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= fieldsSum, "MaxLengthSpecialQuestion2_" + y + "_" + x);
                            if (y - 1 >= 0 && !crossword.HasBlock(y - 1, x + 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y - 1, x + 1]);
                        }
                        // up, then right
                        if (y - 1 >= 0 && x + maxWordLength < sizeX && !crossword.HasBlock(y - 1, x, y - 1, x + maxWordLength))
                        {
                            var fieldsSum = fields.SumRange(y - 1, x, y - 1, x + maxWordLength);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= fieldsSum, "MaxLengthSpecialQuestion3_" + y + "_" + x);
                            if (x - 1 >= 0 && !crossword.HasBlock(y - 1, x - 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y - 1, x - 1]);
                        }
                    }
                }
            }

            // Does a field belong to zero, one or two questions?
            var partOfAWord = new GRBLinExpr[sizeY, sizeX, 6];
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    // this constraint doesn't work for [0,0]
                    if (crossword.HasBlock(y, x)) continue;

                    // Is this field attached to a question on the left, pointing right?
                    var attachedToHorizontalQuestion = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToHorizontalQuestion" + y + "_" + x);
                    for (int len = 1; len <= maxWordLength; len++)
                    {
                        if (x - len < 0 || crossword.HasBlock(y, x - len, y, x)) continue;
                        var isQuestionAndPointsRight = fields[y, x - len] + (1 - questionType[y, x - len]);
                        if ((object)specialQuestionUsed[y, x - len] != null)
                            isQuestionAndPointsRight += (1 - specialQuestionUsed[y, x - len]) - 1;
                        var questionsInbetween = fields.SumRange(y, x - len + 1, y, x);
                        m.AddConstr(attachedToHorizontalQuestion >= isQuestionAndPointsRight - 1 - questionsInbetween);

                        // 0 IF first question is not pointing right OR there is no question to the left
                        // firstQuestion ==> total fields < 2
                        if ((object)specialQuestionUsed[y, x - len] != null)
                            m.AddConstr(attachedToHorizontalQuestion <= questionsInbetween + (1 - fields[y, x - len]) + 1 - (questionType[y, x - len] + specialQuestionUsed[y, x - len]) * 0.5); // the first question but DOESNT look right
                        else
                            m.AddConstr(attachedToHorizontalQuestion <= questionsInbetween + (1 - fields[y, x - len]) + 1 - questionType[y, x - len]); // the first question but DOESNT look right
                    }
                    var questionsToTheLeft = new GRBLinExpr();
                    int qlCount = 0;
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (x - len < 0 || crossword.HasBlock(y, x - len, y, x)) continue;
                        questionsToTheLeft += fields[y, x - len];
                        qlCount++;
                    }
                    if (qlCount > 0) m.AddConstr(attachedToHorizontalQuestion <= questionsToTheLeft);

                    // Is this field attached to a question pointing towards down?
                    var attachedToVerticalQuestion = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToVerticalQuestion" + y + "_" + x);
                    for (int len = 1; len <= maxWordLength; len++)
                    {
                        if (y - len < 0 || crossword.HasBlock(y - len, x, y, x)) continue;
                        var isQuestionAndPointsDown = fields[y - len, x] + questionType[y - len, x];
                        if ((object)specialQuestionUsed[y - len, x] != null)
                            isQuestionAndPointsDown += (1 - specialQuestionUsed[y - len, x]) - 1;
                        var questionsInbetween = fields.SumRange(y - len + 1, x, y, x);
                        m.AddConstr(attachedToVerticalQuestion >= isQuestionAndPointsDown - 1 - questionsInbetween);
                        if ((object)specialQuestionUsed[y - len, x] != null)
                            m.AddConstr(attachedToVerticalQuestion <= questionsInbetween + (1 - fields[y - len, x]) + 1 - (1 - questionType[y - len, x] + specialQuestionUsed[y - len, x]) * 0.5); // the first question but DOESNT look down OR IS specialquestion
                        else
                            m.AddConstr(attachedToVerticalQuestion <= questionsInbetween + (1 - fields[y - len, x]) + 1 - (1 - questionType[y - len, x])); // the first question but DOESNT look down OR IS specialquestion
                    }
                    var questionsTowardsDown = new GRBLinExpr();
                    int qdCount = 0;
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (y - len < 0 || crossword.HasBlock(y - len, x, y, x)) continue;
                        questionsTowardsDown += fields[y - len, x];
                        qdCount++;
                    }
                    if (qdCount > 0) m.AddConstr(attachedToVerticalQuestion <= questionsTowardsDown);


                    var attachedToSpecialQuestions = new GRBLinExpr[4];
                    var spAll = new GRBLinExpr();
                    for (int type = 0; type < 4; type++)
                    {
                        attachedToSpecialQuestions[type] = AttachedToSpecialQuestion(y, x, type, crossword, m, sizeX, sizeY, maxWordLength, fields, specialQuestionUsed, specialQuestionType);
                        if ((object)attachedToSpecialQuestions[type] != null) spAll += attachedToSpecialQuestions[type];
                    }

                    // if attached to horizontal question, can't be attached to horizontal sq (0, 3)
                    if ((object)attachedToSpecialQuestions[0] != null)
                        m.AddConstr((1 - attachedToHorizontalQuestion) >= attachedToSpecialQuestions[0]);
                    if ((object)attachedToSpecialQuestions[3] != null)
                        m.AddConstr((1 - attachedToHorizontalQuestion) >= attachedToSpecialQuestions[3]);
                    // give preference to one horizontal kind of sq
                    if ((object)attachedToSpecialQuestions[0] != null && (object)attachedToSpecialQuestions[3] != null)
                        m.AddConstr((1 - attachedToSpecialQuestions[0]) >= attachedToSpecialQuestions[3]);

                    // if attached to vertical question, can't be attached to vertical sq (1, 2)
                    if ((object)attachedToSpecialQuestions[1] != null)
                        m.AddConstr((1 - attachedToVerticalQuestion) >= attachedToSpecialQuestions[1]);
                    if ((object)attachedToSpecialQuestions[2] != null)
                        m.AddConstr((1 - attachedToVerticalQuestion) >= attachedToSpecialQuestions[2]);
                    // give preference to one horizontal kind of sq
                    if ((object)attachedToSpecialQuestions[1] != null && (object)attachedToSpecialQuestions[2] != null)
                        m.AddConstr((1 - attachedToSpecialQuestions[1]) >= attachedToSpecialQuestions[2]);

                    var c = m.AddConstr(attachedToHorizontalQuestion + attachedToVerticalQuestion + spAll >= 1 - fields[y, x], "AttachedToQuestionConstraint_" + y + "_" + x);
                    //c.Lazy = 1;
                    partOfAWord[y, x, 0] = attachedToHorizontalQuestion;
                    partOfAWord[y, x, 1] = attachedToVerticalQuestion;
                    partOfAWord[y, x, 2] = attachedToSpecialQuestions[0];
                    partOfAWord[y, x, 3] = attachedToSpecialQuestions[1];
                    partOfAWord[y, x, 4] = attachedToSpecialQuestions[2];
                    partOfAWord[y, x, 5] = attachedToSpecialQuestions[3];
                }
            }

            // special questions can't be overlaying
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if ((object)specialQuestionUsed[y, x] != null)
                    {
                        // if horizontal sq, 
                    }
                }
            }

            // right now, [0,0] can only be a question
            //if (!crossword.HasBlock(0, 0)) m.AddConstr(fields[0, 0] == 1);
            // and similarly the bottom 3x3 can only be letters
            for (int y = sizeY - 3; y < sizeY; y++)
                for (int x = sizeX - 3; x < sizeX; x++)
                    if (!crossword.HasBlock(y, x)) m.AddConstr(fields[y, x] == 0);

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
                    if (!crossword.HasBlock(y, x)) //(x >= 1 || y >= 1) && 
                    {
                        uncrossedLetters[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "isUncrossedLetter" + y + "_" + x);
                        var partOfWordTotal = new GRBLinExpr();
                        for (int t = 0; t < 6; t++)
                            if ((object)partOfAWord[y, x, t] != null) partOfWordTotal += partOfAWord[y, x, t];
                        // if total < 2 && is a letter ==> uncrossed
                        m.AddConstr(uncrossedLetters[y, x] <= 1 - fields[y, x]); // if it's a question it can't be an uncrossed letter
                        m.AddConstr(uncrossedLetters[y, x] >= (partOfWordTotal - 1) * (1d / 5) - fields[y, x]);
                        m.AddConstr(uncrossedLetters[y, x] <= partOfWordTotal * 0.5);

                        /*m.AddConstr(uncrossedLetters[y, x] <= partOfWordTotal); // if 0 ==> 0 NECESSARY?
                        m.AddConstr(uncrossedLetters[y, x] <= 2 - partOfAWord[y, x, 0] - partOfAWord[y, x, 1]); // if 2 ==> 0
                        m.AddConstr(uncrossedLetters[y, x] <= 1 - fields[y, x]); // if it's a question it can't be a dead field

                        m.AddConstr(uncrossedLetters[y, x] >= partOfAWord[y, x, 0] - partOfAWord[y, x, 1] - fields[y, x]); // horizontal XOR vertical
                        m.AddConstr(uncrossedLetters[y, x] >= partOfAWord[y, x, 1] - partOfAWord[y, x, 0] - fields[y, x]);*/

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
                    var hby = y - 1 < 0 || !crossword.HasBlock(y - 1, x);
                    var hbx = x - 1 < 0 || !crossword.HasBlock(y, x - 1);
                    if (x >= 1 && y >= 1 && !crossword.HasBlock(y, x) && (hby || hbx))
                    {
                        var isDeadArea = m.AddVar(0, 1, 0, GRB.BINARY, "isDeadArea" + y + "_" + x);
                        if (hby) m.AddConstr(isDeadArea >= uncrossedLetters[y, x] + uncrossedLetters[y - 1, x] - 1);
                        if (hbx) m.AddConstr(isDeadArea >= uncrossedLetters[y, x] + uncrossedLetters[y, x - 1] - 1);
                        m.AddConstr(isDeadArea <= uncrossedLetters[y, x]);
                        if (hby && hbx)
                            m.AddConstr(isDeadArea <= uncrossedLetters[y - 1, x] + uncrossedLetters[y, x - 1]);
                        else if (hby)
                            m.AddConstr(isDeadArea <= uncrossedLetters[y - 1, x]);
                        else if (hbx)
                            m.AddConstr(isDeadArea <= uncrossedLetters[y, x - 1]);
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
                    int ct = 0;
                    for (int i = 0; i < area; i++)
                    {
                        for (int j = 0; j < area; j++)
                        {
                            if (crossword.HasBlock(y + i, x + j)) continue;
                            clusterTotal += fields[y + i, x + j];
                            ct++;
                        }
                    }
                    if (ct >= 3)
                    {
                        var varClusterTotalPenalty = m.AddVar(0, 1, 0, GRB.BINARY, "varClusterTotalPenalty" + y + "_" + x);
                        // 0-1 = good, 2-4 = bad
                        m.AddConstr(varClusterTotalPenalty <= clusterTotal * 0.5, "clusterPenaltyConstr1_" + y + "_" + x);
                        m.AddConstr(varClusterTotalPenalty >= (clusterTotal - 1) * (1d / 3), "clusterPenaltyConstr2_" + y + "_" + x);
                        clusterPenalty += varClusterTotalPenalty;
                    }
                }
            }

            //m.AddConstr(deadFieldPenalty <= 30);

            //amountOfQuestionsRating * (100d / sizeX / sizeY) + manyCrossedWords +  + wordHistogramDifferences
            // clusterPenalty * 100
            m.SetObjective(deadFieldPenalty, GRB.MINIMIZE);

            m.SetCallback(new GRBMipSolCallback(crossword, fields, questionType, specialQuestionType));

            m.Optimize();
            m.ComputeIIS();
            m.Write("model.ilp");

            m.Dispose();
            env.Dispose();
        }

        private GRBLinExpr AttachedToSpecialQuestion(int y, int x, int type, Crossword crossword, GRBModel m, int sizeX, int sizeY, int maxWordLength, GRBVar[,] fields, GRBLinExpr[,] specialQuestionUsed, GRBVar[,,] specialQuestionType)
        {
            // 0 = Down, then right
            // 1 = Left, then down
            // 2 = Right, then down
            // 3 = Up, then right
            if (type == 0 && y - 1 < 0) return null;
            if (type == 1 && x - 1 < 0) return null;
            if (type == 2 && x + 1 >= sizeX) return null;
            if (type == 3 && y + 1 >= sizeY) return null;

            // Is this field attached to a special question?
            var attachedToSpecialQuestion = new GRBLinExpr();
            for (int len = 1; len < maxWordLength; len++)
            {
                var qpos = new { y = y + (type == 0 ? -1 : 1), x = x - len };
                if (type == 1 || type == 2) qpos = new { y = y - len, x = x + (type == 2 ? 1 : -1) };

                if ((type == 0 || type == 3) && (x - len < 0 || crossword.HasBlock(y, x - len, y, x) || crossword.HasBlock(qpos.y, qpos.x))) continue;
                if ((type == 1 || type == 2) && (y - len < 0 || crossword.HasBlock(y - len, x, y, x) || crossword.HasBlock(qpos.y, qpos.x))) continue;

                if ((object)specialQuestionUsed[qpos.y, qpos.x] == null) continue;

                var atsp = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToSpecialQuestion" + type + "len" + len + "_" + y + "_" + x);
                var isSpecialQuestion = fields[qpos.y, qpos.x] + specialQuestionType[qpos.y, qpos.x, type];
                var questionsInbetween = (type == 0 || type == 3) ? fields.SumRange(y, x - len, y, x) : fields.SumRange(y - len, x, y, x);
                m.AddConstr(atsp >= isSpecialQuestion - 1 - questionsInbetween);
                if (type == 0 || type == 3)
                    for (int xi = x - len; xi <= x; xi++) m.AddConstr(atsp <= 1 - fields[y, xi]);
                else
                    for (int yi = y - len; yi <= y; yi++) m.AddConstr(atsp <= 1 - fields[yi, x]);
                m.AddConstr(atsp <= fields[qpos.y, qpos.x]);
                m.AddConstr(atsp <= specialQuestionType[qpos.y, qpos.x, type]);
                attachedToSpecialQuestion += atsp;
            }
            return attachedToSpecialQuestion;
        }
    }
}
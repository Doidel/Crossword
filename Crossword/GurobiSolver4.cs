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
                { 2, 0 },
                { 3, 18 },
                { 4, 24 },
                { 5, 20 },
                { 6, 18 },
                { 7, 12 },
                { 8, 4 },
                { 9, 1 },
                { 10, 1 },
                { 11, 1 },
                { 12, 1 },
                { 13, 0 },
                { 14, 0 },
                { 15, 0 },
            };

            const int maxWordLength = 15;
            const int minWordLength = 2;

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

            m.GetEnv().Method = 0;

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
                        fields[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "Field" + y + "_" + x);
                        questionType[y, x] = m.AddVar(0, 1, 0, GRB.BINARY, "QType" + y + "_" + x);

                        // Is null except for places down and right of a blocked field or y==0 or x==0
                        if (y == 0 || x == 0 || crossword.Grid[y - 1, x] is Blocked || crossword.Grid[y, x - 1] is Blocked)
                        {
                            for (int t = 0; t < 4; t++)
                            {
                                specialQuestionType[y, x, t] = m.AddVar(0, 1, 0, GRB.BINARY, "SpecialQType" + t + "_" + y + "_" + x);
                            }
                            specialQuestionUsed[y, x] = specialQuestionType[y, x, 0] + specialQuestionType[y, x, 1] + specialQuestionType[y, x, 2] + specialQuestionType[y, x, 3];
                            // Max 1 special type, can also be no special question
                            m.AddConstr(specialQuestionUsed[y, x] <= 1, "MaxOneSpecialQuestion" + y + "_" + x);
                        }
                    }
                }
            }

            // TEST
            //m.AddConstr(specialQuestionType[0, 0, 0] == 1);




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
                    if (x + minWordLength < sizeX && !crossword.HasBlock(y, x, y, x + minWordLength))
                    {
                        // for right: if [0,0] is question, [0,1..3] must not be question
                        var totalQuestionsHorizontal = fields.SumRange(y, x + 1, y, x + minWordLength);
                        var isQuestionAndPointsRight = fields[y, x] + (1 - questionType[y, x]) - 1;
                        if ((object)specialQuestionUsed[y, x] != null)
                            isQuestionAndPointsRight += (1 - specialQuestionUsed[y, x]) - 1;
                        for (int len = 1; len <= minWordLength; len++)
                            m.AddConstr(isQuestionAndPointsRight <= 1 - fields[y, x + len], "MinWordLength3" + y + "_" + x + "_right" + len);
                    }
                    else
                    {
                        noQuestionToTheRightAllowed = true;
                    }

                    // for down:
                    if (y + minWordLength < sizeY && !crossword.HasBlock(y, x, y + minWordLength, x))
                    {
                        var totalQuestionsVertical = fields.SumRange(y + 1, x, y + minWordLength, x);
                        var isQuestionAndPointsDown = fields[y, x] + questionType[y, x] - 1;
                        if ((object)specialQuestionUsed[y, x] != null)
                            isQuestionAndPointsDown += (1 - specialQuestionUsed[y, x]) - 1;
                        for (int len = 1; len <= minWordLength; len++)
                            m.AddConstr(isQuestionAndPointsDown <= 1 - fields[y + len, x], "MinWordLength3" + y + "_" + x + "_down" + len);
                    }
                    else
                    {
                        noQuestionTowardsDownAllowed = true;
                    }


                    bool atLeastOneSpecialAllowed = false;
                    if ((object)specialQuestionUsed[y, x] != null)
                    {
                        // down, then right
                        if (y + 1 < sizeY && x + minWordLength - 1 < sizeX && !crossword.HasBlock(y + 1, x, y + 1, x + minWordLength - 1))
                        {
                            for (int len = 1; len <= minWordLength; len++)
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= 1 - fields[y + 1, x + len - 1], "MinWordLength3" + y + "_" + x + "_downRight" + len);
                            if (x > 0) m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y + 1, x - 1], "QuestionBeforeSQ" + y + "_" + x + "_downRight");
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 0] == 0, "NoSpecialQuestionAllowed" + y + "_" + x + "_downRight");
                        }
                        // left, then down
                        if (y + minWordLength - 1 < sizeY && x - 1 >= 0 && !crossword.HasBlock(y, x - 1, y + minWordLength - 1, x - 1))
                        {
                            for (int len = 1; len <= minWordLength; len++)
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= 1 - fields[y + len - 1, x - 1], "MinWordLength3" + y + "_" + x + "_leftDown" + len);
                            if (y > 0) m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= fields[y - 1, x - 1], "QuestionBeforeSQ" + y + "_" + x + "_leftDown");
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 1] == 0, "NoSpecialQuestionAllowed" + y + "_" + x + "_leftDown");
                        }
                        // right, then down
                        if (y + minWordLength - 1 < sizeY && x + 1 < sizeX && !crossword.HasBlock(y, x + 1, y + minWordLength - 1, x + 1))
                        {
                            for (int len = 1; len <= minWordLength; len++)
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= 1 - fields[y + len - 1, x + 1], "MinWordLength3" + y + "_" + x + "_rightDown" + len);
                            if (y > 0) m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= fields[y - 1, x + 1], "QuestionBeforeSQ" + y + "_" + x + "_rightDown");
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 2] == 0, "NoSpecialQuestionAllowed" + y + "_" + x + "_rightDown");
                        }
                        // up, then right
                        if (y - 1 >= 0 && x + minWordLength - 1 < sizeX && !crossword.HasBlock(y - 1, x, y - 1, x + minWordLength - 1))
                        {
                            for (int len = 1; len <= minWordLength; len++)
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= 1 - fields[y - 1, x + len - 1], "MinWordLength3" + y + "_" + x + "_upRight" + len);
                            if (x > 0) m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= fields[y - 1, x - 1], "QuestionBeforeSQ" + y + "_" + x + "_upRight");
                            atLeastOneSpecialAllowed = true;
                        }
                        else
                        {
                            m.AddConstr(specialQuestionType[y, x, 3] == 0, "NoSpecialQuestionAllowed" + y + "_" + x + "_upRight");
                        }

                        if (!atLeastOneSpecialAllowed)
                        {
                            m.AddConstr(specialQuestionUsed[y, x] == 0, "NoSpecialQuestionAllowedAtALl" + y + "_" + x);
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
                            m.AddConstr(specialQuestionUsed[y, x] == 1, "OnlySpecialQuestionAllowed" + y + "_" + x);
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
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y + 1, x - 1], "QuestionRequiredBeforeSpecialQuestion0Word_" + y + "_" + x);
                        }
                        // left, then down
                        if (y + maxWordLength < sizeY && x - 1 >= 0 && !crossword.HasBlock(y, x - 1, y + maxWordLength, x - 1))
                        {
                            var fieldsSum = fields.SumRange(y, x - 1, y + maxWordLength, x - 1);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 1] - 1 <= fieldsSum, "MaxLengthSpecialQuestion1_" + y + "_" + x);
                            if (y - 1 >= 0 && !crossword.HasBlock(y - 1, x - 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y - 1, x - 1], "QuestionRequiredBeforeSpecialQuestion1Word_" + y + "_" + x);
                        }
                        // right, then down
                        if (y + maxWordLength < sizeY && x + 1 < sizeX && !crossword.HasBlock(y, x + 1, y + maxWordLength, x + 1))
                        {
                            var fieldsSum = fields.SumRange(y, x + 1, y + maxWordLength, x + 1);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 2] - 1 <= fieldsSum, "MaxLengthSpecialQuestion2_" + y + "_" + x);
                            if (y - 1 >= 0 && !crossword.HasBlock(y - 1, x + 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y - 1, x + 1], "QuestionRequiredBeforeSpecialQuestion2Word_" + y + "_" + x);
                        }
                        // up, then right
                        if (y - 1 >= 0 && x + maxWordLength < sizeX && !crossword.HasBlock(y - 1, x, y - 1, x + maxWordLength))
                        {
                            var fieldsSum = fields.SumRange(y - 1, x, y - 1, x + maxWordLength);
                            m.AddConstr(fields[y, x] + specialQuestionType[y, x, 3] - 1 <= fieldsSum, "MaxLengthSpecialQuestion3_" + y + "_" + x);
                            if (x - 1 >= 0 && !crossword.HasBlock(y - 1, x - 1))
                                m.AddConstr(fields[y, x] + specialQuestionType[y, x, 0] - 1 <= fields[y - 1, x - 1], "QuestionRequiredBeforeSpecialQuestion3Word_" + y + "_" + x);
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
                        m.AddConstr(attachedToHorizontalQuestion >= isQuestionAndPointsRight - 1 - questionsInbetween, "attachedToHorizontalQuestionConstraint0_" + y + "_" + x);

                        // 0 IF first question is not pointing right OR there is no question to the left
                        // firstQuestion ==> total fields < 2
                        if ((object)specialQuestionUsed[y, x - len] != null)
                            m.AddConstr(attachedToHorizontalQuestion <= questionsInbetween + (1 - fields[y, x - len]) + 1 - (questionType[y, x - len] + specialQuestionUsed[y, x - len]) * 0.5, "attachedToHorizontalQuestionConstraint1_" + y + "_" + x); // the first question but DOESNT look right
                        else
                            m.AddConstr(attachedToHorizontalQuestion <= questionsInbetween + (1 - fields[y, x - len]) + 1 - questionType[y, x - len], "attachedToHorizontalQuestionConstraint2_" + y + "_" + x); // the first question but DOESNT look right
                    }
                    var questionsToTheLeft = new GRBLinExpr();
                    int qlCount = 0;
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (x - len < 0 || crossword.HasBlock(y, x - len, y, x)) continue;
                        questionsToTheLeft += fields[y, x - len];
                        qlCount++;
                    }
                    if (qlCount > 0) m.AddConstr(attachedToHorizontalQuestion <= questionsToTheLeft, "attachedToHorizontalQuestionConstraint4_" + y + "_" + x);

                    // Is this field attached to a question pointing towards down?
                    var attachedToVerticalQuestion = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToVerticalQuestion" + y + "_" + x);
                    for (int len = 1; len <= maxWordLength; len++)
                    {
                        if (y - len < 0 || crossword.HasBlock(y - len, x, y, x)) continue;
                        var isQuestionAndPointsDown = fields[y - len, x] + questionType[y - len, x];
                        if ((object)specialQuestionUsed[y - len, x] != null)
                            isQuestionAndPointsDown += (1 - specialQuestionUsed[y - len, x]) - 1;
                        var questionsInbetween = fields.SumRange(y - len + 1, x, y, x);
                        m.AddConstr(attachedToVerticalQuestion >= isQuestionAndPointsDown - 1 - questionsInbetween, "attachedToVerticalQuestionConstraint0_" + y + "_" + x);
                        if ((object)specialQuestionUsed[y - len, x] != null)
                            m.AddConstr(attachedToVerticalQuestion <= questionsInbetween + (1 - fields[y - len, x]) + 1 - (1 - questionType[y - len, x] + specialQuestionUsed[y - len, x]) * 0.5, "attachedToVerticalQuestionConstraint1_" + y + "_" + x); // the first question but DOESNT look down OR IS specialquestion
                        else
                            m.AddConstr(attachedToVerticalQuestion <= questionsInbetween + (1 - fields[y - len, x]) + 1 - (1 - questionType[y - len, x]), "attachedToVerticalQuestionConstraint2_" + y + "_" + x); // the first question but DOESNT look down OR IS specialquestion
                    }
                    var questionsTowardsDown = new GRBLinExpr();
                    int qdCount = 0;
                    for (int len = 0; len <= maxWordLength; len++)
                    {
                        if (y - len < 0 || crossword.HasBlock(y - len, x, y, x)) continue;
                        questionsTowardsDown += fields[y - len, x];
                        qdCount++;
                    }
                    if (qdCount > 0) m.AddConstr(attachedToVerticalQuestion <= questionsTowardsDown, "attachedToVerticalQuestionConstraint3_" + y + "_" + x);


                    var attachedToSpecialQuestions = new GRBLinExpr[4];
                    var spAll = new GRBLinExpr();
                    for (int type = 0; type < 4; type++)
                    {
                        attachedToSpecialQuestions[type] = AttachedToSpecialQuestion(y, x, type, crossword, m, sizeX, sizeY, maxWordLength, fields, specialQuestionUsed, specialQuestionType);
                        if ((object)attachedToSpecialQuestions[type] != null) spAll += attachedToSpecialQuestions[type];
                    }

                    // if attached to horizontal question, can't be attached to horizontal sq (0, 3)
                    if ((object)attachedToSpecialQuestions[0] != null)
                        m.AddConstr((1 - attachedToHorizontalQuestion) >= attachedToSpecialQuestions[0], "noHorizontalOverlap1_" + y + "_" + x);
                    if ((object)attachedToSpecialQuestions[3] != null)
                        m.AddConstr((1 - attachedToHorizontalQuestion) >= attachedToSpecialQuestions[3], "noHorizontalOverlap2_" + y + "_" + x);
                    // give preference to one horizontal kind of sq
                    if ((object)attachedToSpecialQuestions[0] != null && (object)attachedToSpecialQuestions[3] != null)
                        m.AddConstr((1 - attachedToSpecialQuestions[0]) >= attachedToSpecialQuestions[3], "noHorizontalOverlap3_" + y + "_" + x);

                    // if attached to vertical question, can't be attached to vertical sq (1, 2)
                    if ((object)attachedToSpecialQuestions[1] != null)
                        m.AddConstr((1 - attachedToVerticalQuestion) >= attachedToSpecialQuestions[1], "noVerticalOverlap1_" + y + "_" + x);
                    if ((object)attachedToSpecialQuestions[2] != null)
                        m.AddConstr((1 - attachedToVerticalQuestion) >= attachedToSpecialQuestions[2], "noVerticalOverlap2_" + y + "_" + x);
                    // give preference to one horizontal kind of sq
                    if ((object)attachedToSpecialQuestions[1] != null && (object)attachedToSpecialQuestions[2] != null)
                        m.AddConstr((1 - attachedToSpecialQuestions[1]) >= attachedToSpecialQuestions[2], "noVerticalOverlap3_" + y + "_" + x);

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

            // right now, [0,0] can only be a question
            //if (!crossword.HasBlock(0, 0)) m.AddConstr(fields[0, 0] == 1);
            // and similarly the bottom 3x3 can only be letters
            for (int y = sizeY - minWordLength; y < sizeY; y++)
                for (int x = sizeX - minWordLength; x < sizeX; x++)
                    if (!crossword.HasBlock(y, x)) m.AddConstr(fields[y, x] == 0, "BottomOnlyLetters_" + y + "_" + x);

            // Objective:
            // questions should be around ~22% (allFieldsSum ~= amountQuestions)
            int tolerance = (int)(amountQuestions * 0.1);
            m.AddConstr(allFieldsSum >= amountQuestions - tolerance, "amountOfQuestionsTolerance_1");
            m.AddConstr(allFieldsSum <= amountQuestions + tolerance, "amountOfQuestionsTolerance_2");

            // uncrossed
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
                        m.AddConstr(uncrossedLetters[y, x] <= 1 - fields[y, x], "uncrossedConstr1" + y + "_" + x); // if it's a question it can't be an uncrossed letter
                        m.AddConstr(uncrossedLetters[y, x] >= 1 - partOfWordTotal * 0.5 - fields[y, x], "uncrossedConstr2" + y + "_" + x);
                        m.AddConstr(uncrossedLetters[y, x] <= 1 - (partOfWordTotal - 1) * (1d / 5), "uncrossedConstr3" + y + "_" + x);

                        /*m.AddConstr(uncrossedLetters[y, x] <= partOfWordTotal); // if 0 ==> 0 NECESSARY?
                        m.AddConstr(uncrossedLetters[y, x] <= 2 - partOfAWord[y, x, 0] - partOfAWord[y, x, 1]); // if 2 ==> 0
                        m.AddConstr(uncrossedLetters[y, x] <= 1 - fields[y, x]); // if it's a question it can't be a dead field

                        m.AddConstr(uncrossedLetters[y, x] >= partOfAWord[y, x, 0] - partOfAWord[y, x, 1] - fields[y, x]); // horizontal XOR vertical
                        m.AddConstr(uncrossedLetters[y, x] >= partOfAWord[y, x, 1] - partOfAWord[y, x, 0] - fields[y, x]);*/

                        uncrossedLettersPenalty += uncrossedLetters[y, x];
                    }
                }
            }

            // penalty for nearby uncrossed letters (dead fields)
            var deadFieldPenalty = new GRBLinExpr();
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    var hby = y - 1 >= 0 && !crossword.HasBlock(y - 1, x);
                    var hbx = x - 1 >= 0 && !crossword.HasBlock(y, x - 1);
                    if (!crossword.HasBlock(y, x) && (hby || hbx))
                    {
                        var isDeadArea = m.AddVar(0, 1, 0, GRB.BINARY, "isDeadArea" + y + "_" + x);
                        if (hby) m.AddConstr(isDeadArea >= uncrossedLetters[y, x] + uncrossedLetters[y - 1, x] - 1, "deadAreaConstr1" + y + "_" + x);
                        if (hbx) m.AddConstr(isDeadArea >= uncrossedLetters[y, x] + uncrossedLetters[y, x - 1] - 1, "deadAreaConstr2" + y + "_" + x);
                        m.AddConstr(isDeadArea <= uncrossedLetters[y, x]);
                        if (hby && hbx)
                            m.AddConstr(isDeadArea <= uncrossedLetters[y - 1, x] + uncrossedLetters[y, x - 1], "deadAreaConstr3" + y + "_" + x);
                        else if (hby)
                            m.AddConstr(isDeadArea <= uncrossedLetters[y - 1, x], "deadAreaConstr4" + y + "_" + x);
                        else if (hbx)
                            m.AddConstr(isDeadArea <= uncrossedLetters[y, x - 1], "deadAreaConstr5" + y + "_" + x);
                        deadFieldPenalty += isDeadArea;
                    }
                }
            }



            // ideal histogram comparison
            //var wordHistogramDifferences = new GRBLinExpr();
            var wlTotals = new Dictionary<int, GRBLinExpr>();
            foreach (var wl in wordLengthHistogram.Keys)
            {
                var total = new GRBLinExpr();
                for (int y = 0; y + wl - 1 < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        if (crossword.HasBlock(y, x, y + wl - 1, x))
                            continue;
                        // true if field-1 is question or start AND field + wl (after word) is question or end
                        var hasLength = m.AddVar(0, 1, 0, GRB.BINARY, "hasLenVert" + wl + "__" + y + "_" + x);
                        var sum = fields.SumRange(y, x, y + wl - 1, x);
                        // no questions inbetween
                        for (int i = 0; i < wl; i++)
                            m.AddConstr(hasLength <= 1 - fields[y + i, x]);
                        // question at end
                        if (y + wl < sizeY && !crossword.HasBlock(y + wl, x))
                        {
                            sum += (1 - fields[y + wl, x]);
                            m.AddConstr(hasLength <= fields[y + wl, x]);
                        }
                        // question at start
                        if (y - 1 >= 0 && !crossword.HasBlock(y - 1, x))
                        {
                            sum += (1 - fields[y - 1, x]);
                            m.AddConstr(hasLength <= fields[y - 1, x]);
                        }

                        // counts if a letter is attached to a horizontal question
                        var qsum = new GRBLinExpr();
                        if ((object)partOfAWord[y, x, 1] != null) qsum += partOfAWord[y, x, 1];
                        if ((object)partOfAWord[y, x, 3] != null) qsum += partOfAWord[y, x, 3];
                        if ((object)partOfAWord[y, x, 4] != null) qsum += partOfAWord[y, x, 4];
                        sum += 1 - qsum;
                        m.AddConstr(hasLength <= qsum);

                        m.AddConstr(hasLength >= 1 - sum);
                        total += hasLength;
                    }
                }
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x + wl - 1 < sizeX; x++)
                    {
                        if (crossword.HasBlock(y, x, y, x + wl - 1))
                            continue;
                        var hasLength = m.AddVar(0, 1, 0, GRB.BINARY, "hasLenHoriz" + wl + "__" + y + "_" + x);
                        var sum = fields.SumRange(y, x, y, x + wl - 1);
                        // no questions inbetween
                        for (int i = 0; i < wl; i++)
                            m.AddConstr(hasLength <= 1 - fields[y, x + i]);
                        // question at end
                        if (x + wl < sizeX && !crossword.HasBlock(y, x + wl))
                        {
                            sum += (1 - fields[y, x + wl]);
                            m.AddConstr(hasLength <= fields[y, x + wl]);
                        }
                        // question at start
                        if (x - 1 >= 0 && !crossword.HasBlock(y, x - 1))
                        {
                            sum += (1 - fields[y, x - 1]);
                            m.AddConstr(hasLength <= fields[y, x - 1]);
                        }

                        // counts if a letter is attached to a horizontal question
                        var qsum = new GRBLinExpr();
                        if ((object)partOfAWord[y, x, 0] != null) qsum += partOfAWord[y, x, 0];
                        if ((object)partOfAWord[y, x, 2] != null) qsum += partOfAWord[y, x, 2];
                        if ((object)partOfAWord[y, x, 5] != null) qsum += partOfAWord[y, x, 5];
                        sum += 1 - qsum;
                        m.AddConstr(hasLength <= qsum);

                        m.AddConstr(hasLength >= 1 - sum);
                        total += hasLength;
                    }
                }
                if (wl <= 9)
                    wlTotals.Add(wl, total);
                else
                    wlTotals[9] += total;
            }
            var wlPenalty = new GRBLinExpr();
            var wordCounts = m.AddVars(8, 0, amountQuestions * 2, GRB.INTEGER, "amount");
            foreach (var wl in wlTotals.Keys)
            {
                var input = wordCounts[wl - 2];
                m.AddConstr(input == wlTotals[wl]);
                var absRes = m.AddVar(0, 100, 0, GRB.CONTINUOUS, "absRes");
                Console.WriteLine(wl == 9 ? 4 : wordLengthHistogram[wl]);
                var percentageDiff = input * (100d / amountQuestions) - (wl == 9 ? 4 : wordLengthHistogram[wl]);
                m.AddConstr(percentageDiff <= absRes, "absPos");
                m.AddConstr(-percentageDiff <= absRes, "absNeg");
                wlPenalty += absRes;
            }
            wlPenalty *= (1d / 8);

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
            m.SetObjective(wlPenalty + clusterPenalty + deadFieldPenalty , GRB.MINIMIZE);

            m.SetCallback(new GRBMipSolCallback(crossword, fields, questionType, specialQuestionType, true, wordCounts));

            m.Optimize();
            m.ComputeIIS();
            m.Write("model.ilp");

            m.Dispose();
            env.Dispose();

            // Insert previous solution
            /*var cwdCheck = new Crossword(@"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\15x15_2.cwg");
            cwdCheck.Draw();
            for (int y = 0; y < cwdCheck.Grid.GetLength(0); y++)
            {
                for (int x = 0; x < cwdCheck.Grid.GetLength(1); x++)
                {
                    if (cwdCheck.Grid[y, x] is Question)
                    {
                        m.AddConstr(fields[y, x] == 1);
                        var q = (Question)cwdCheck.Grid[y, x];
                        if (q.Arrow == Question.ArrowType.Right)
                        {
                            m.AddConstr(questionType[y, x] == 0);
                            if ((object)specialQuestionType[y, x, 0] != null)
                                m.AddConstr(specialQuestionType[y, x, 0]+ specialQuestionType[y, x, 1] + specialQuestionType[y, x, 2] + specialQuestionType[y, x, 3] == 0);
                        }
                        else if (q.Arrow == Question.ArrowType.Down)
                        {
                            m.AddConstr(questionType[y, x] == 1);
                            if ((object)specialQuestionType[y, x, 0] != null)
                                m.AddConstr(specialQuestionType[y, x, 0] + specialQuestionType[y, x, 1] + specialQuestionType[y, x, 2] + specialQuestionType[y, x, 3] == 0);
                        }
                        else if (q.Arrow == Question.ArrowType.DownRight)
                        {
                            m.AddConstr(specialQuestionType[y, x, 0] == 1);
                        }
                        else if (q.Arrow == Question.ArrowType.LeftDown)
                        {
                            m.AddConstr(specialQuestionType[y, x, 1] == 1);
                        }
                        else if (q.Arrow == Question.ArrowType.RightDown)
                        {
                            m.AddConstr(specialQuestionType[y, x, 2] == 1);
                        }
                        else if (q.Arrow == Question.ArrowType.UpRight)
                        {
                            m.AddConstr(specialQuestionType[y, x, 3] == 1);
                        }
                    }
                    else if (cwdCheck.Grid[y, x] is Letter)
                    {
                        m.AddConstr(fields[y, x] == 0);
                    }
                }
            }*/
        }

        private GRBLinExpr AttachedToSpecialQuestion(int y, int x, int type, Crossword crossword, GRBModel m, int sizeX, int sizeY, int maxWordLength, GRBVar[,] fields, GRBLinExpr[,] specialQuestionUsed, GRBVar[,,] specialQuestionType)
        {
            // 0 = Down, then right
            // 1 = Left, then down
            // 2 = Right, then down
            // 3 = Up, then right
            if (type == 0 && y - 1 < 0) return null;
            if (type == 1 && x + 1 >= sizeX) return null;
            if (type == 2 && x - 1 < 0) return null;
            if (type == 3 && y + 1 >= sizeY) return null;

            // Is this field attached to a special question?
            var attachedToSpecialQuestion = new GRBLinExpr();
            for (int len = 0; len < maxWordLength; len++)
            {
                var qpos = new { y = y + (type == 0 ? -1 : 1), x = x - len };
                if (type == 1 || type == 2) qpos = new { y = y - len, x = x + (type == 1 ? 1 : -1) };

                if ((type == 0 || type == 3) && (x - len < 0 || crossword.HasBlock(y, x - len, y, x) || crossword.HasBlock(qpos.y, qpos.x))) continue;
                if ((type == 1 || type == 2) && (y - len < 0 || crossword.HasBlock(y - len, x, y, x) || crossword.HasBlock(qpos.y, qpos.x))) continue;

                if ((object)specialQuestionUsed[qpos.y, qpos.x] == null) continue;

                var atsp = m.AddVar(0, 1, 0, GRB.BINARY, "attachedToSpecialQuestion" + type + "len" + len + "_" + y + "_" + x);
                var questionsInbetween = (type == 0 || type == 3) ? fields.SumRange(y, x - len, y, x) : fields.SumRange(y - len, x, y, x);
                m.AddConstr(atsp >= fields[qpos.y, qpos.x] + specialQuestionType[qpos.y, qpos.x, type] - 1 - questionsInbetween, "attachedToSpecialQuestion_len" + len + "_" + y + "_" + x);
                if (type == 0 || type == 3)
                    for (int xi = x - len; xi <= x; xi++) m.AddConstr(atsp <= 1 - fields[y, xi], "notAttachedToSpecialQuestion1_len" + len + "_" + y + "_" + x);
                else
                    for (int yi = y - len; yi <= y; yi++) m.AddConstr(atsp <= 1 - fields[yi, x], "notAttachedToSpecialQuestion1_len" + len + "_" + y + "_" + x);
                m.AddConstr(atsp <= fields[qpos.y, qpos.x], "notAttachedToSpecialQuestion2_len" + len + "_" + y + "_" + x);
                m.AddConstr(atsp <= specialQuestionType[qpos.y, qpos.x, type], "notAttachedToSpecialQuestion3_len" + len + "_" + y + "_" + x);
                attachedToSpecialQuestion += atsp;
            }
            return attachedToSpecialQuestion;
        }
    }
}
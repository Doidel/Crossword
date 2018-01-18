using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Crossword
{
    class SchnittpunktProgram
    {
        public SchnittpunktProgram()
        {

            // x, y
            var p1 = new Vector2(3, 5);
            var p2 = new Vector2(6, 5);


            // test solve

            GRBEnv env = new GRBEnv();
            GRBModel m = new GRBModel(env);

            var p3_X = m.AddVar(0, 10000, 0, GRB.CONTINUOUS, "p3_X");
            var p3_Y = m.AddVar(0, 10000, 0, GRB.CONTINUOUS, "p3_Y");
            var p4_X = m.AddVar(0, 10000, 0, GRB.CONTINUOUS, "p4_X"); // direction
            var p4_Y = m.AddVar(0, 10000, 0, GRB.CONTINUOUS, "p4_Y");

            // Are there a lambda1 and lambda2 so that line1 crosses line2?
            var lambda1 = m.AddVar(-100000, 100000, 0, GRB.CONTINUOUS, "lambda1");
            var lambda2 = m.AddVar(-100000, 100000, 0, GRB.CONTINUOUS, "lambda2");

            // Constraint: There must not exist two lambdas that cross
            // p1 + lambda1 * p2 = p3 + lambda2 * p4 // if there are such two lambdas, they intersect
            // p1 + lambda1 * p2 - p3 - lambda2 * p4 = 0
            // intersectionPoint =  p1 + lambda1 * p2 = p3 + lambda2 * p4

            // constr: lambda1 >= min <= max
            // constr: lambda2 >= min <= max
            // --constr: intersectionPoint =  p1 + lambda1 * p2
            // --constr: intersectionPoint =  p3 + lambda2 * p4
            // constr: p1 + lambda1 * p2 - p3 - lambda2 * p4 = slackVar
            // var hasIntersection = if slackVar > 0 ==> m.AddConstr(



            

            // var isParallel
            // constr: if not isParallel:  p1 + lambda1 * p2 == p3 + lambda2 * p4
            // var hasIntersection = lambda1 out of bounds OR lambda2 out of bounds


            var isParallel = m.AddVar(0, 1, 0, GRB.BINARY, "isParallel");
            // parallel, if the two slopes are equal
            var deltaLine1 = p2 - p1;
            var deltaLine2_X = p4_X - p3_X;
            var deltaLine2_Y = p4_Y - p3_Y;
            // ???
            // for now, and for simplicity: No two lines can be perfectly parallel
            m.AddConstr(isParallel == 0);

            var intersectX = p1.X + lambda1 * p2.X - (p3_X + lambda2 * (p4_X - p3_X));
            m.AddConstr(intersectX >= -isParallel * 10000);
            m.AddConstr(intersectX <= isParallel * 10000);
            var intersectY = p1.Y + lambda1 * p2.Y - (p3_Y + lambda2 * (p4_Y - p3_Y));
            m.AddConstr(intersectY >= -isParallel * 10000);
            m.AddConstr(intersectY <= isParallel * 10000);

            var hasIntersection = m.AddVar(0, 1, 0, GRB.BINARY, "hasIntersection");
            // if both lambdas between 0 and 1 AND !isParallel, lines cross
            var absLambda1 = m.AddVar(0, 10000, 0, GRB.CONTINUOUS, "absLambda1");
            m.AddConstr(lambda1 <= absLambda1, "absLambda1Pos");
            m.AddConstr(-lambda1 <= absLambda1, "absLambda1Neg");
            var absLambda2 = m.AddVar(0, 10000, 0, GRB.CONTINUOUS, "absLambda2");
            m.AddConstr(lambda2 <= absLambda2, "absLambda2Pos");
            m.AddConstr(-lambda2 <= absLambda2, "absLambda2Neg");
            var lambda1Between0and1 = m.AddVar(0, 1, 0, GRB.BINARY, "lambda1Between0and1");
            m.AddConstr(lambda1Between0and1 <= 1 - (absLambda1 - 1) * (1d / 10000));
            m.AddConstr(lambda1Between0and1 >= 1 - absLambda1 * 0.5);
            var lambda2Between0and1 = m.AddVar(0, 1, 0, GRB.BINARY, "lambda2Between0and1");
            m.AddConstr(lambda2Between0and1 <= 1 - (absLambda2 - 1) * (1d / 10000));
            m.AddConstr(lambda2Between0and1 >= 1 - absLambda2 * 0.5);

            m.AddConstr(hasIntersection <= (1 - isParallel));
            m.AddConstr(hasIntersection <= lambda1Between0and1);
            m.AddConstr(hasIntersection <= lambda2Between0and1);
            m.AddConstr(hasIntersection >= 1 - isParallel - (1 - lambda1Between0and1) - (1 - lambda2Between0and1));

            // no intersectino allowed
            m.SetObjective(hasIntersection * 1, GRB.MINIMIZE);
            m.Optimize();
        }
    }
}

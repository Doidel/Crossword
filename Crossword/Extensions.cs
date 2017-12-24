using Gurobi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    public static class Extensions
    {
        public static IEnumerable<float> AsFloat(this GRBVar[] _var)
        {
            for (var x = 0; x < _var.Length; x++)
                yield return (float)_var[x].Get(GRB.DoubleAttr.X);
        }

        public static float[,] AsFloat(this GRBVar[,] _var)
        {
            var res = new float[_var.GetLength(0), _var.GetLength(1)];
            try
            {
                for (var y = 0; y < res.GetLength(1); y++)
                    for (var x = 0; x < res.GetLength(0); x++)
                        res[x, y] = (float)_var[x, y].Get(GRB.DoubleAttr.X);
            }
            catch
            {
            }

            return res;
        }

        public static GRBLinExpr SumL1(this GRBVar[] _vars)
        {
            var res = new GRBLinExpr();
            foreach (var v in _vars)
                res.Add(v);
            return res;
        }

        public static GRBQuadExpr SumL2(this GRBVar[] _vars)
        {
            var res = new GRBQuadExpr();
            foreach (var v in _vars)
                res.Add(v * v);
            return res;
        }

        public static void AddConstr(this GRBModel _m, GRBTempConstr _constr)
        {
            try
            {
                _m.AddConstr(_constr, null);
            }
            catch (GRBException _e)
            {
                switch (_e.ErrorCode)
                {
                    case 10003:
                        _m.AddQConstr(_constr, null);
                        break;
                    case 20001:
                        _m.Update();
                        _m.AddConstr(_constr);
                        break;
                }
            }
        }

        public static GRBVar[,] AddVars(this GRBModel _m, int _width, int _height, double lb, double ub, char type)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height).ToArray(), Enumerable.Repeat(ub, _width * _height).ToArray(), null, Enumerable.Repeat(type, _width * _height).ToArray(), null);

            var i = 0;
            var res = new GRBVar[_width, _height];
            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                    res[x, y] = vars[i++];
            return res;
        }

        public static GRBVar[] AddVars(this GRBModel _m, int _count, double lb, double ub, char type)
        {
            return _m.AddVars(Enumerable.Repeat(lb, _count).ToArray(), Enumerable.Repeat(ub, _count).ToArray(), null, Enumerable.Repeat(type, _count).ToArray(), null);
        }

        public static GRBVar[,] AddVars(this GRBModel _m, int _width, int _height, double lb, double ub, char type, string _prefix = null)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height).ToArray(), Enumerable.Repeat(ub, _width * _height).ToArray(), null, Enumerable.Repeat(type, _width * _height).ToArray(),
                    _prefix == null ? null : Enumerable.Range(0, _width * _height).Select(j => $"{_prefix}[{j % _width},{j / _width}]").ToArray()
                );

            var i = 0;
            var res = new GRBVar[_width, _height];
            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                    res[x, y] = vars[i++];

            return res;
        }

        public static GRBVar[,,] AddVars(this GRBModel _m, int _width, int _height, int _depth, double lb, double ub, char type, string _prefix = null)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height * _depth).ToArray(), Enumerable.Repeat(ub, _width * _height * _depth).ToArray(), null, Enumerable.Repeat(type, _width * _height * _depth).ToArray(),
                    _prefix == null ? null : Enumerable.Range(0, _width * _height * _depth).Select(j => $"{_prefix}[{j % _width},{(j / _width) % _height},{j / _width / _height}]").ToArray()
                );

            var i = 0;
            var res = new GRBVar[_width, _height, _depth];
            for (var z = 0; z < _depth; z++)
                for (var y = 0; y < _height; y++)
                    for (var x = 0; x < _width; x++)
                        res[x, y, z] = vars[i++];

            return res;
        }

        public static GRBVar[,,,] AddVars(this GRBModel _m, int _width, int _height, int _depth, int _d4, double lb, double ub, char type, string _prefix = null)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height * _depth * _d4).ToArray(), Enumerable.Repeat(ub, _width * _height * _depth * _d4).ToArray(), null, Enumerable.Repeat(type, _width * _height * _depth * _d4).ToArray(),
                    _prefix == null ? null : Enumerable.Range(0, _width * _height * _depth * _d4).Select(j => $"{_prefix}[{j % _width},{(j / _width) % _height},{(j / _width / _height) % _depth},{j / _width / _height / _depth}]").ToArray()
                );

            var i = 0;
            var res = new GRBVar[_width, _height, _depth, _d4];
            for (var a = 0; a < _d4; a++)
                for (var z = 0; z < _depth; z++)
                    for (var y = 0; y < _height; y++)
                        for (var x = 0; x < _width; x++)
                            res[x, y, z, a] = vars[i++];
            return res;
        }

        public static GRBVar[] AddVars(this GRBModel _m, int _count, double lb, double ub, char type, string _prefix = null)
        {
            return _m.AddVars(Enumerable.Repeat(lb, _count).ToArray(), Enumerable.Repeat(ub, _count).ToArray(), null, Enumerable.Repeat(type, _count).ToArray(),
                    _prefix == null ? null : Enumerable.Range(0, _count).Select(i => $"{_prefix}[{i}]").ToArray()
                );
        }

        public static GRBLinExpr SumRange(this GRBVar[,] _vars, int x, int y, int x2, int y2)
        {
            if (y2 == -1) y2 = y;
            if (x2 == -1) x2 = x;

            if (y < 0 || y >= _vars.GetLength(0)) throw new ArgumentException("y out of bounds");
            if (x < 0 || x >= _vars.GetLength(1)) throw new ArgumentException("x out of bounds");
            if (y2 < 0 || y2 >= _vars.GetLength(0)) throw new ArgumentException("y2 out of bounds");
            if (x2 < 0 || x2 >= _vars.GetLength(1)) throw new ArgumentException("x2 out of bounds");
            if (x2 < x) throw new ArgumentException("x2 < x");
            if (y2 < y) throw new ArgumentException("y2 < y");

            var sum = new GRBLinExpr();
            for (int i = y; i <= y2; i++)
            {
                for (int j = x; j <= x2; j++)
                {
                    sum += _vars[i, j];
                }
            }
            return sum;
        }
    }
}

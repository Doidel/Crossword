using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword.Fields
{
    public class Question : Field
    {
        public ArrowType Arrow;

        public Question(ArrowType arrow)
        {
            Arrow = arrow;
        }

        public enum ArrowType
        {
            Down,
            DownRight,
            LeftDown,
            Right,
            RightDown,
            UpRight
        }

        public override string ToString()
        {
            return Arrow == ArrowType.Down ? "D" : "R";
        }
    }
}

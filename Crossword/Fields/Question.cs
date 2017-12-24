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
            switch (Arrow)
            {
                case ArrowType.Down:
                    return "v";
                case ArrowType.DownRight:
                    return "└";
                case ArrowType.LeftDown:
                    return "┌";
                case ArrowType.Right:
                    return ">";
                case ArrowType.RightDown:
                    return "¬";
                case ArrowType.UpRight:
                    return "┌";
            }
            return "?";
        }
    }
}

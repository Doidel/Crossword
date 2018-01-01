using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword.Fields
{
    public class Empty : Field
    {
        public override Field DeepClone()
        {
            return new Empty();
        }

        public override string ToString()
        {
            return ".";
        }
    }
}

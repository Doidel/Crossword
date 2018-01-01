using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword.Fields
{
    class Letter : Field
    {
        public Char L;
        public Letter(Char letter)
        {
            L = letter;
        }

        public override Field DeepClone()
        {
            return new Letter(L);
        }

        public override string ToString()
        {
            return L.ToString();
        }
    }
}

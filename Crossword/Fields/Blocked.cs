﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword.Fields
{
    public class Blocked : Field
    {
        public override Field DeepClone()
        {
            return new Blocked();
        }

        public override string ToString()
        {
            return " ";
        }
    }
}

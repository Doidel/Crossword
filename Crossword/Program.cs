using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    class Program
    {
        static void Main(string[] args)
        {
            //var cwd = new Crossword(@"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\test_cases\20x30_1.cwg");
            var cwd = new Crossword(@"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\test_cases\island_1.cwg");
            new GurobiSolver3(cwd);
        }
    }
}

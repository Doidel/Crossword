using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    class Program
    {
        const string _base = @"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\";

        static void Main(string[] args)
        {
            var cwd = new Crossword(@"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\test_cases\15x15_1.cwg");
            //var cwd = new Crossword(_base + @"test_cases\island_1.cwg");
            new GurobiSolver4(cwd);

            /*var cwd = new Crossword(_base + @"results\island_1.cwg");
            new WordsSolver(cwd);*/

            var x = Console.ReadKey();
        }
    }
}

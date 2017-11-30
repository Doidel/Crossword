using Crossword.Fields;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    public class Crossword
    {
        public Field[,] Grid;

        public Crossword(string path)
        {
            ReadFromFile(path);
        }

        private void ReadFromFile(string path)
        {
            var lines = File.ReadLines(path).ToArray();
            // read the two header rows to determine the grid size
            Grid = new Field[int.Parse(lines[0]), int.Parse(lines[1])];

            // read the grid fields and fill in the grid
            for (int i = 0; i < Grid.GetLength(0); i++)
            {
                var line = lines[i + 2];
                for (int j = 0; j < Grid.GetLength(1); j++)
                {
                    if (line[j] == '.')
                    {
                        Grid[i, j] = new Empty();
                    }
                    else
                    {
                        Grid[i, j] = new Blocked();
                    }
                }
            }
        }
    }
}

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
        public int[,] Clusters { get; private set; }

        private string Name;

        public Crossword(string path)
        {
            ReadFromFile(path);
            Name = Path.GetFileNameWithoutExtension(path);
        }

        public Crossword(Field[,] grid, string name = null)
        {
            Grid = grid;
            Name = name == null ? Grid.GetLength(0) + "x" + Grid.GetLength(1) : Name;
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
                    else if (line[j] == ' ')
                    {
                        Grid[i, j] = new Blocked();
                    }
                    else if (line[j] == '?')
                    {
                        Grid[i, j] = new Question(Question.ArrowType.Down);
                    }
                    else
                    {
                        Grid[i, j] = new Letter(line[j]);
                    }
                }
            }

            // if available, read question information
            for (int ql = Grid.GetLength(0) + 2; ql < lines.Length; ql++)
            {
                var qInfo = lines[ql].Split(' ');
                int y = int.Parse(qInfo[0]);
                int x = int.Parse(qInfo[1]);
                if (!(Grid[y, x] is Question)) throw new ArgumentException("Grid question inconsistency");
                Grid[y, x] = new Question((Question.ArrowType)(int.Parse(qInfo[2])));
            }
        }

        public bool HasBlock(int y, int x, int y2 = -1, int x2 = -1)
        {
            if (y2 == -1) y2 = y;
            if (x2 == -1) x2 = x;

            if (y < 0 || y >= Grid.GetLength(0)) throw new ArgumentException("y out of bounds");
            if (x < 0 || x >= Grid.GetLength(1)) throw new ArgumentException("x out of bounds");
            if (y2 < 0 || y2 >= Grid.GetLength(0)) throw new ArgumentException("y2 out of bounds");
            if (x2 < 0 || x2 >= Grid.GetLength(1)) throw new ArgumentException("x2 out of bounds");
            if (x2 < x) throw new ArgumentException("x2 < x");
            if (y2 < y) throw new ArgumentException("y2 < y");
            
            for (int i = y; i <= y2; i++)
            {
                for (int j = x; j <= x2; j++)
                {
                    if (Grid[i, j] is Blocked) return true;
                }
            }
            return false;
        }

        public Dictionary<string, double> Score()
        {
            var wordLengthHistogram = new Dictionary<int, int>() {
                { 2, 0 },
                { 3, 18 },
                { 4, 24 },
                { 5, 20 },
                { 6, 18 },
                { 7, 12 },
                { 8, 4 },
                { 9, 1 },
                { 10, 1 },
                { 11, 1 },
                { 12, 1 },
                { 13, 0 },
                { 14, 0 },
                { 15, 0 },
            };

            const int maxWordLength = 15;

            int sizeY = Grid.GetLength(0);
            int sizeX = Grid.GetLength(1);

            int amountQuestions = (int)Math.Round(0.22 * sizeX * sizeY);

            var actualWordlengths = new Dictionary<int, int>();
            foreach (var k in wordLengthHistogram.Keys) actualWordlengths.Add(k, 0);

            int totalQuestions = 0;
            int totalNonblocked = sizeX * sizeY;
            var crossings = new int[sizeY, sizeX];
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (Grid[y, x] is Question)
                    {
                        totalQuestions++;
                        var q = (Question)Grid[y, x];
                        
                        if (q.Arrow == Question.ArrowType.Down)
                        {
                            int offset = 1;
                            while (y + offset < sizeY && isEmptyOrLetter(Grid[y + offset, x]))
                            {
                                if (offset > maxWordLength + 1) throw new ArgumentException("Word too long");
                                crossings[y + offset, x]++;
                                offset++;
                            }
                            actualWordlengths[offset - 1]++;
                        }
                        else if (q.Arrow == Question.ArrowType.Right)
                        {
                            int offset = 1;
                            while (x + offset < sizeX && isEmptyOrLetter(Grid[y, x + offset]))
                            {
                                if (offset > maxWordLength + 1) throw new ArgumentException("Word too long");
                                crossings[y, x + offset]++;
                                offset++;
                            }
                            actualWordlengths[offset - 1]++;
                        }
                        else if (q.Arrow == Question.ArrowType.DownRight)
                        {
                            int offset = 0;
                            while (x + offset < sizeX && isEmptyOrLetter(Grid[y+1, x + offset]))
                            {
                                if (offset > maxWordLength) throw new ArgumentException("Word too long");
                                crossings[y+1, x + offset]++;
                                offset++;
                            }
                            actualWordlengths[offset]++;
                        }
                        else if (q.Arrow == Question.ArrowType.LeftDown)
                        {
                            int offset = 0;
                            while (y + offset < sizeY && isEmptyOrLetter(Grid[y + offset, x - 1]))
                            {
                                if (offset > maxWordLength) throw new ArgumentException("Word too long");
                                crossings[y + offset, x - 1]++;
                                offset++;
                            }
                            actualWordlengths[offset]++;
                        }
                        else if (q.Arrow == Question.ArrowType.RightDown)
                        {
                            int offset = 0;
                            while (y + offset < sizeY && isEmptyOrLetter(Grid[y + offset, x + 1]))
                            {
                                if (offset > maxWordLength) throw new ArgumentException("Word too long");
                                crossings[y + offset, x + 1]++;
                                offset++;
                            }
                            actualWordlengths[offset]++;
                        }
                        else if (q.Arrow == Question.ArrowType.UpRight)
                        {
                            int offset = 0;
                            while (x + offset < sizeX && isEmptyOrLetter(Grid[y - 1, x + offset]))
                            {
                                if (offset > maxWordLength) throw new ArgumentException("Word too long");
                                crossings[y - 1, x + offset]++;
                                offset++;
                            }
                            actualWordlengths[offset]++;
                        }
                    }
                    else if (Grid[y, x] is Blocked)
                    {
                        totalNonblocked--;
                    }
                }
            }

            int totalCrossings = 0;
            int totalLetters = 0;
            int totalDeadFields = 0;
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (isEmptyOrLetter(Grid[y, x]))
                    {
                        totalLetters++;
                        totalCrossings += Math.Max(crossings[y, x] - 1, 0);

                        if (crossings[y, x] == 1)
                        {
                            if ((x + 1 < sizeX && isEmptyOrLetter(Grid[y, x + 1]) && crossings[y, x + 1] == 1) ||
                                (x - 1 >= 0 && isEmptyOrLetter(Grid[y, x - 1]) && crossings[y, x - 1] == 1) ||
                                (y + 1 < sizeY && isEmptyOrLetter(Grid[y + 1, x]) && crossings[y + 1, x] == 1) ||
                                (y - 1 >= 0 && isEmptyOrLetter(Grid[y - 1, x]) && crossings[y - 1, x] == 1))
                            {
                                totalDeadFields++;
                            }
                        }
                    }
                }
            }

            // figure out clusters
            Clusters = new int[sizeY, sizeX];
            int clusterID = 1;
            Action<int, int> explore = null;
            explore = (int y, int x) =>
            {
                //questionsInCluster[y, x]
                for (int i = -1; i <= 1; i++)
                {
                    if (y + i >= 0 && y + i < sizeY)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (x + j >= 0 && x + j < sizeX)
                            {
                                if (i == 0 && j == 0) continue;
                                if (Grid[y + i, x + j] is Question && Clusters[y + i, x + j] == 0)
                                {
                                    Clusters[y + i, x + j] = clusterID;
                                    explore(y + i, x + j);
                                }
                            }
                        }
                    }
                }
            };
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (Grid[y, x] is Question && Clusters[y, x] == 0)
                    {
                        // start exploring
                        Clusters[y, x] = clusterID;
                        explore(y, x);
                        clusterID++;
                    }
                }
            }
            // cluster penalty
            double cTotal = 0;
            for (int cID = 1; cID < clusterID; cID++)
            {
                int total = 0;
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        if (Clusters[y, x] == cID) total++;
                    }
                }
                if (total >= 3)
                {
                    cTotal += total * total;
                }
            }
            cTotal /= clusterID - 1;

            double histogramTotal = 0;
            var totalWords = actualWordlengths.Values.Sum();
            double totalTest = 0;
            for (int wl = 2; wl <= 9; wl++)
            {
                var amount = wl < 9 ? actualWordlengths[wl] : actualWordlengths.Where(k => k.Key >= 9).Sum(k => k.Value);
                histogramTotal += Math.Pow(amount * (100d / totalWords) - (wl < 9 ? wordLengthHistogram[wl] : 4), 2) / 8d;
                totalTest += Math.Abs(amount * (100d / totalWords) - (wl < 9 ? wordLengthHistogram[wl] : 4)) / 8d;
            }
            Console.WriteLine("Test wl: " + totalTest);

            return new Dictionary<string, double>()
            {
                { "uncrossed fields", 100 - Math.Pow((Math.Max(0.2, (1d - totalCrossings/(double)totalLetters)) - 0.2) * 100d / 2, 2) },
                { "question fields", 100 - Math.Pow((totalQuestions / (double)totalNonblocked * 100 - 22) * 2, 2) },
                { "histogram", 100 - histogramTotal },
                { "dead fields", 100 - (totalDeadFields/(double)totalLetters) * 400d },
                { "question clusters", 100 - cTotal * 10d },
                { "nrDoubleQuestions", 100 }
            };
        }

        private bool isEmptyOrLetter(Field field)
        {
            return field is Letter || field is Empty;
        }

        public void Draw()
        {
            for (int y = 0; y < Grid.GetLength(0); y++)
            {
                for (int x = 0; x < Grid.GetLength(1); x++)
                {
                    Console.Write(Grid[y, x]);
                }
                Console.WriteLine();
            }

            var s = Score();

            int clusterMax = 0;
            for (int y = 0; y < Clusters.GetLength(0); y++)
            {
                for (int x = 0; x < Clusters.GetLength(1); x++)
                {
                    if (false && Clusters.GetLength(1) <= 15)
                    {
                        if (Clusters[y, x] != 0)
                            Console.Write(GetClusterCode(Clusters[y, x]));
                        else
                            Console.Write(" ");
                    }
                    clusterMax = Math.Max(clusterMax, Clusters[y, x]);
                }
                if (false && Clusters.GetLength(1) <= 15)  Console.WriteLine();
            }
            Console.WriteLine("# of clusters: " + clusterMax);

            double totalScore = 0;
            foreach (var k in s.Keys)
            {
                Console.WriteLine(k.PadRight(25) + Math.Round(s[k], 1) + "%");
                totalScore += Math.Max(s[k], 0);
            }
            totalScore /= s.Count;
            Console.WriteLine("TOTAL".PadRight(25) + Math.Round(totalScore, 1) + "%");
        }

        public void Save(string v)
        {
            v = Name + v;
            if (!v.EndsWith(".cwg")) v = v + ".cwg";

            string fileContent = "";

            var sizeY = Grid.GetLength(0);
            var sizeX = Grid.GetLength(1);
            fileContent += sizeY + Environment.NewLine;
            fileContent += sizeX + Environment.NewLine;

            string questionDefs = "";

            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (Grid[y, x] is Question)
                    {
                        fileContent += "?";
                        questionDefs += y + " " + x + " ";
                        questionDefs += ((int)((Question)Grid[y, x]).Arrow) + Environment.NewLine;
                    }
                    else
                    {
                        fileContent += Grid[y, x].ToString();
                    }
                }
                fileContent += Environment.NewLine;
            }

            fileContent += questionDefs;

            File.WriteAllText(v, fileContent);
            Console.WriteLine("Saved as " + v);
        }


        private const string clusterEncoding = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        private string GetClusterCode(int i)
        {
            if (i < clusterEncoding.Length)
                return clusterEncoding[i].ToString();
            else
            {
                int mult = i / clusterEncoding.Length;
                int rest = i - mult * clusterEncoding.Length;
                return "?";
            }
        }

        public Crossword DeepClone()
        {
            var fieldClone = new Field[Grid.GetLength(0), Grid.GetLength(1)];
            for (int y = 0; y < Grid.GetLength(0); y++)
            {
                for (int x = 0; x < Grid.GetLength(1); x++)
                {
                    fieldClone[y, x] = Grid[y, x].DeepClone();
                }
            }
            return new Crossword(Grid, Name);
        }
    }
}

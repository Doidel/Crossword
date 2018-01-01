using Crossword.Fields;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossword
{
    public class WordsSolver
    {
        public WordsSolver(Crossword cwd)
        {
            var wordsArray = File.ReadLines(@"C:\Users\Roman Bolzern\Documents\GitHub\Crossword\docs\useful\wordlist.txt").ToArray();
            var wordsList = wordsArray.GroupBy(f => f.Length).ToDictionary(f => f.Key, f => f.ToList());

            int sizeY = cwd.Grid.GetLength(0);
            int sizeX = cwd.Grid.GetLength(1);

            Random rnd = new Random();
            for (int it = 0; it < 10000; it++)
            {
                var fullList = Enumerable.Range(0, sizeY * sizeX).ToList();
                fullList.Shuffle(rnd);
                Crossword cwdCopy = cwd.DeepClone();
                try
                {
                    foreach (var i in fullList)
                    {
                        int y = i / sizeX;
                        int x = i - y * sizeX;
                        if (cwd.Grid[y, x] is Question)
                        {
                            switch (((Question)cwd.Grid[y, x]).Arrow)
                            {
                                case Question.ArrowType.Down:
                                    PutWordY(cwdCopy, wordsList, sizeY, rnd, y + 1, x);
                                    break;
                                case Question.ArrowType.DownRight:
                                    PutWordX(cwdCopy, wordsList, sizeX, rnd, y + 1, x);
                                    break;
                                case Question.ArrowType.LeftDown:
                                    PutWordY(cwdCopy, wordsList, sizeY, rnd, y, x - 1);
                                    break;
                                case Question.ArrowType.Right:
                                    PutWordX(cwdCopy, wordsList, sizeX, rnd, y, x + 1);
                                    break;
                                case Question.ArrowType.RightDown:
                                    PutWordY(cwdCopy, wordsList, sizeY, rnd, y, x + 1);
                                    break;
                                case Question.ArrowType.UpRight:
                                    PutWordX(cwdCopy, wordsList, sizeX, rnd, y - 1, x);
                                    break;
                            }
                        }
                    }
                }
                catch (ArgumentException e)
                {
                    //cwdCopy.Draw();
                    Console.WriteLine(it + " " + e.Message);
                    continue;
                }

                //worked
                cwd = cwdCopy;
                cwd.Draw();
            }
        }

        private static void PutWordY(Crossword cwd, Dictionary<int, List<string>> wordsList, int sizeY, Random rnd, int y, int x)
        {
            int l = y;
            var filledLetters = new List<char>();
            for (; l < sizeY; l++)
            {
                if (cwd.Grid[l, x] is Question || cwd.Grid[l, x] is Blocked)
                {
                    break;
                }
                if (cwd.Grid[l, x] is Letter)
                {
                    var letter = (Letter)cwd.Grid[l, x];
                    filledLetters.Add(letter.L);
                }
                else if (cwd.Grid[l, x] is Empty)
                {
                    filledLetters.Add('.');
                }
            }
            var w = _PickWord(wordsList, rnd, l - y, filledLetters);
            for (int i = 0; i < w.Length; i++)
                cwd.Grid[y + i, x] = new Letter(w[i]);
        }

        private static void PutWordX(Crossword cwd, Dictionary<int, List<string>> wordsList, int sizeX, Random rnd, int y, int x)
        {
            int l = x;
            var filledLetters = new List<char>();
            for (; l < sizeX; l++)
            {
                if (cwd.Grid[y, l] is Question || cwd.Grid[y, l] is Blocked)
                {
                    break;
                }
                if (cwd.Grid[y, l] is Letter)
                {
                    var letter = (Letter)cwd.Grid[y, l];
                    filledLetters.Add(letter.L);
                }
                else if (cwd.Grid[y, l] is Empty)
                {
                    filledLetters.Add('.');
                }
            }
            var w = _PickWord(wordsList, rnd, l - x, filledLetters);
            for (int i = 0; i < w.Length; i++)
                cwd.Grid[y, x + i] = new Letter(w[i]);
        }

        private static string _PickWord(Dictionary<int, List<string>> wordsList, Random rnd, int length, List<char> filledLetters)
        {
            var wl = wordsList[length];
            var wl_filtered = wl.Where(w =>
            {
                for (int i = 0; i < filledLetters.Count; i++)
                {
                    if (filledLetters[i] != '.' && w[i] != filledLetters[i]) return false;
                }
                return true;
            }).ToArray();
            if (wl_filtered.Length == 0) throw new ArgumentException("No matching word found");
            var wordIndex = rnd.Next(wl_filtered.Length);
            var word = wl_filtered[wordIndex];
            wl.Remove(word);
            return word;
        }
    }
}

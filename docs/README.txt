       ________              
   _jgN########Ngg_          
 _N##N@@""  ""9NN##Np_       
d###P            N####p      
"^^"              T####      
                  d###P      
               _g###@F       
            _gN##@P          
          gN###F"            
         d###F               
        0###F     READY TO  
        0###F      RIDDLE?
        0###F                
        "NN@'                
                             
         ___                 
        q###r                
         ""                  


INTRODUCTION
=============
Welcome, riddler...to the i4Ds crossword challenge!

There are crossword puzzles to be generated! More specifically, "clues-in-squares" crosswords (DE: Schwedenrätsel).
The goal is to fill the 'empty' grids with question fields and words from the provided dictionary (/useful/wordlist.txt). Make sure that your crossword generator is familiar with our constraints. Everything is explained below.

TEST CASES: You will find all the challenges in the "test_cases" directory.
BEWARE:
  * For island_1 and island_2, pay attention to the blocked fields. You must fill in everything else!
  * The file names and specifications (rows, columns, islands) of the filled grids must be *exactly* the same.	

Once you have managed to generate valid crossword puzzles for each test case, simply ZIP the generated .cwg files and upload them to receive your rating!

UPLOAD YOUR RESULTS: http://86.119.32.142:8090/#/challenge
DEADLINE: Sunday, 7th January 2018 23:59

Good luck!

Stephen & Lucas
24th Nov 2017

Questions?
> stephen.randles@fhnw.ch
> lucas.broennimann@fhnw.ch


Terminology
============
* Island: A group of 1 or more blocked fields (may contain neither a question nor a letter)
* Uncrossed field: a letter belonging to only one question
* Dead fields: 2 or more uncrossed fields next to each other (only vertically or horizontally)
* Cluster of question fields: Question fields next to each other (also diagonally)
see also: /useful/CrosswordDescription.png

Constraints
============
* Every field must be assigned a question or a letter (unless it's a blocked field)
* Every letter must belong to a question
* A question field may contain either 1 or 2 questions (fields with 2 questions are optional)

- Arrow Types
see also: /useful/arrow types.png

0	Down
1	Down, then right
2	Left, then down
3	Right
4	Right, then down
5	Up, then right

The arrow types 1,2,4 and 5 are only allowed on: row 0, column 0, to the right of an island, below an island

For question fields with 2 questions, only the following arrow combinations are allowed:
0+3
0+2
0+4
3+1
3+5


Rating
=======
Each crossword is rated individually based on these factors:

* Percentage of question fields (best: 22%)
* Percentage of uncrossed solution fields (best: < 20%)
* Histogram of word lengths (best: see "Ideal Histogram" below)
* Number of "dead" fields (best: none)
* Average size of question field clusters (best: < 3)
* Number of 'double questions' (best: either none, or 22% of the question fields)
* Bonus: 10% on each grid that is completely filled with valid words

- Ideal Histogram

length	%
2	0
3	18
4	24
5	20
6	18
7	12
8	4
9+	4


File specification
====================
See also: /example_grids

{header}
{grid fields}
{list of question fields}

- Header
# rows
# cols

- Grid fields
?	Question field (type must be defined below grid)
.	Unfilled solution field
A-Z	Solution field (part of a word)
-	Blocked field (not part of the puzzle, don't overwrite)

- Question fields
In the grid: Marked with a '?'. One such field may contain 2 questions with different arrow types (defined in the list).

In the list: Each question must be defined as follows, whereby row and column indices are 0-based:
	{row} {col} {typeId}

Example A: A question pointing right, then down, in the first field of the grid
	0	0	4
Example B: Two questions in the field (3, 2) with one question downwards, and one to the right
	3	2	0
	3	2	3


Rating Example
===============
Input:
3
3
...
...
...

Solved:
3
3
?L?
?EI
?AD
0 0 4
0 2 0
1 0 3
2 0 3

=> Result:
Score: 59.2%

Question fields: 0.0 (44% questions, way too much)
Uncrossed fields: 100.0 (only 1 uncrossed field, very good)
Histogram: 0.0 (only 2s and 3s, bad!)
Dead fields: 100.0 (no dead fields, yay!)
Clusters: 55.0 (3 question fields in a row, although it could be worse)
Double questions: 100.0 (no double questions, so that's ok)


Rating function details
=======================
For those who want to know the rating functions a bit more in detail, you can read it here:

1) Percentage of question fields
	score = 100 - (abs(x-22)*2)²
2) Percentage of uncrossed solution fields
	score = 100 – ((max(20,x)-20)/2)²
3) Histogram of word lengths
	score = 100 – sum((xi-yi)²)/8
4) Number of "dead" fields
	This counts all uncrossed fields with a neighbour which is also uncrossed
	score = 100 – (deadFields/TotalSolutionFields)*400
5) Average size of question field clusters
	Looks at every group of connected question fields, so a cluster may have size 1 to n.
	However, only clusters with size 3 or bigger are penalized. 
	score = 100 – (sum((clustersize|>3)²)/clusters)*10
	In the example above there are two clusters, one with size 3 and one with size 1. Score is therefore 100 - (3²/2)*2 = 55
6) Number of double questions
	double questions are optional, but if you want to use them, they should be around 22% of the questions or 4.84% of all unblocked fields.
	score = X=0: 100 | x>0: 100 – (abs(x-22))²

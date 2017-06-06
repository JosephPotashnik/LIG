using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using LIGParser;

namespace LIGLearner
{
    public class Learner
    {
        private readonly Dictionary<string, string> nonTerminalTypeDic;
        public readonly Grammar originalGrammar;
        private readonly HashSet<string> posTypes;
        private readonly Dictionary<string, int> sentencesWithCounts;
        private readonly Vocabulary voc;
        // ReSharper disable once UnusedMember.Local
        private GrammarPermutations gp = new GrammarPermutations();

        public Learner(Vocabulary v, Dictionary<string, string> n, HashSet<string> p,
            string[] s, Grammar o)
        {
            voc = v;
            nonTerminalTypeDic = n;
            posTypes = p;
            originalGrammar = o;

            sentencesWithCounts = s.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        }

        internal Grammar GetInitialGrammar()
        {
            var initialGrammar = new Grammar(voc);
            initialGrammar.NonTerminalsTypeDictionary = nonTerminalTypeDic;
            initialGrammar.POSTypes = posTypes;
            var posInText = voc.POSWithPossibleWords.Keys;

            foreach (var pos in posInText)
            {
                initialGrammar.AddRule(new Rule(1, initialGrammar.StartSymbol, new[] {pos, initialGrammar.StartSymbol},
                    0, 1));
                initialGrammar.AddRule(new Rule(1, initialGrammar.StartSymbol, new[] {pos}, 0, 0));
            }

            return initialGrammar;
        }

        internal Grammar GetInitialGrammar_old()
        {
            var initialGrammar = new Grammar(voc);
            initialGrammar.NonTerminalsTypeDictionary = nonTerminalTypeDic;
            initialGrammar.POSTypes = posTypes;

            foreach (var sentence in sentencesWithCounts.Keys)
            {
                var words = sentence.Split();

                //assume unamibigous interpretation of words. take the first POS for each word.
                var posList = words.Select(x => voc[x].First()).ToArray();

                if (posList.Length < 2) continue;

                var lastRuleName =
                    initialGrammar.AddRule(new Rule(0, null, new[] {posList[0], posList[1]}, 1, 0));

                for (var i = 0; i < posList.Length - 2; i++)
                {
                    lastRuleName =
                        initialGrammar.AddRule(new Rule(0, null, new[] {lastRuleName, posList[i + 2]}, 1, 0));
                }

                initialGrammar.AddRule(new Rule(0, initialGrammar.StartSymbol, new[] {lastRuleName}, 0, 0));
            }

            return initialGrammar;
        }

        private Tuple<Node, int>[] ParseAllSentences(Grammar currentHypothesis)
        {
            var allParses = new Tuple<Node, int>[sentencesWithCounts.Count];
            
            try
            {
                Parallel.ForEach(sentencesWithCounts, (sentenceItem, state, i) =>
                {
                    var parser = new Parser(currentHypothesis);
                    var n = parser.ParseSentence(sentenceItem.Key);
                    allParses[i] = Tuple.Create(n, sentenceItem.Value);
                });
                return allParses;
            }
            catch (Exception)
            {
                return null; //parsing failed.
            }
        }

        public EnergyData Energy(Grammar currentHypothesis)
        {
            var allParses = ParseAllSentences(currentHypothesis);
            if (allParses != null)
                return Encoder.TotalLength(currentHypothesis, allParses);

            return null;
        }

        public Dictionary<int, int> CollectUsages(Grammar currentHypothesis)
        {
            var allParses = ParseAllSentences(currentHypothesis);
            var usagesDic = new Dictionary<int, int>();

            if (allParses != null)
            {
                foreach (var item in allParses)
                    Encoder.CollectRuleUsages(item.Item1, usagesDic, item.Item2);

                return usagesDic;
            }

            return null;
        }

        internal Grammar GetNeighbor(Grammar currentHypothesis)
        {
            while (true)
            {
                var m = GrammarPermutations.GetWeightedRandomMutation();
                var newGrammar = new Grammar(currentHypothesis);

                var res = m(newGrammar);
                if (res)
                {
                    //Console.WriteLine("mutation accepted: {0}", m);
                    //Console.WriteLine("New grammar {0}",newGrammar.ToString());
                }
                else
                {
                    //if there is no mutation to accept, don't bother re-parsing the current grammar.
                    //Console.WriteLine("mutation rejected");
                    return null;
                }
                var infinite = newGrammar.DoesGrammarAllowInfiniteMovement();
                if (!infinite) return newGrammar;
            }
        }
    }
}
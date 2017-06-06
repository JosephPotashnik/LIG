using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LIGParser
{
    public class EnergyData
    {
        public int GrammarEnergy { get; set; }
        public int DataEnergy { get; set; }

        public int TotalEnergy
        {
            get { return GrammarEnergy + DataEnergy; }
        }

        public static bool operator >(EnergyData c1, EnergyData c2)
        {
            return c1.TotalEnergy > c2.TotalEnergy;
        }

        public static bool operator <(EnergyData c1, EnergyData c2)
        {
            return c1.TotalEnergy < c2.TotalEnergy;
        }

        public static int operator -(EnergyData c1, EnergyData c2)
        {
            return c1.TotalEnergy - c2.TotalEnergy;
        }

        public override string ToString()
        {
            return string.Format("Grammar Energy:{0} Data Energy: {1}, Total:{2}", GrammarEnergy, DataEnergy,
                TotalEnergy);
        }
    }

    public class Encoder
    {
        private const int BitsForProbabilityEncoding = 8; // 2 decimal places.
        public static bool Debug = false;


        public static int RequiredBitsGivenProbability(double probability)
        {
            return (int)Math.Ceiling(-Math.Log(probability, 2)) + 1;
        }

        public static int RequiredBitsGivenLogProbability(double logprobability)
        {
            return (int)Math.Ceiling(logprobability) + 1;
        }

        public static EnergyData TotalLength(Grammar grammar, Tuple<Node, int>[] parseTreesWithCounts)
        {
            var e = new EnergyData();
            //var rules = grammar.ruleNumberDictionary.Values;

            e.GrammarEnergy = GetGrammarLength(grammar);
            e.DataEnergy = GetDataLength(grammar, parseTreesWithCounts);

            return e;
        }

        public static int GetGrammarLength(Grammar grammar)
        {
            var allRules = grammar.ruleNumberDictionary.Values;
            var rules = allRules.Where(x => x.IsInitialRule()).ToArray();

            var productions = rules.SelectMany(r => r.Production);
            var names = rules.Select(r => r.Name);

            var overallTerms = productions.Concat(names);
            var overallNonTerminals = overallTerms.Select(r => r.NonTerminal).ToArray();
            var termDic = overallNonTerminals.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            var counts = termDic.Select(x => x.Value).ToArray();
            var totalCount = counts.Sum();

            //length of symbol table = length of symbol codewords + length of probability hashtable:
            //length of symbol codewords is:
            var symbolCodewordsLength =
                counts.Select(x => RequiredBitsGivenProbability(x / (float)totalCount)).Sum();
            //length of probability hashtable is:
            var symbolHashTableBits = counts.Length * BitsForProbabilityEncoding;

            //length of codewords contributed by symbols in rules 
            var overallSymbolRepresentationLength =
                counts.Select(x => RequiredBitsGivenProbability(x / (float)totalCount) * x).Sum();

            //add set of moveables to overall length 
            foreach (var moveable in grammar.Moveables)
            {
                if (termDic.ContainsKey(moveable))
                    overallSymbolRepresentationLength +=
                        RequiredBitsGivenProbability(termDic[moveable] / (float)totalCount);
            }

            //add set of landing sites to overall length 
            foreach (var site in grammar.LandingSites)
            {
                if (termDic.ContainsKey(site))
                    overallSymbolRepresentationLength += RequiredBitsGivenProbability(termDic[site] / (float)totalCount);
            }

            //for each rule: + 3 bits = control bits: 1) number of production terms (1/2 terms) 2) head/complement 3)..
            var ruleHashTableBits = rules.Length * (3 + BitsForProbabilityEncoding);


            //if (Debug)
            //    Console.WriteLine(
            //        $"{symbolCodewordsLength}, {symbolHashTableBits}, {overallSymbolRepresentationLength}, {ruleHashTableBits}");
            //see documentation. 3 bits for encoding head position, complement position, number of sons.

            return symbolCodewordsLength + symbolHashTableBits + overallSymbolRepresentationLength + ruleHashTableBits;
        }

        //public static int GetDerivationTreeLength(Node n)
        //{
        //    //rule number 0 is a scan operation of some POS / word.
        //    if (n.RuleNumber == Grammar.SCAN_RULE_NUMBER)
        //        return 0;

        //    var selfLength = RequiredBitsGivenLogProbability(n.LogProbability);
        //    if (selfLength < 0)
        //    {
        //        Console.WriteLine("rule number {0}, prob {1}", n.RuleNumber, n.LogProbability);
        //    }

        //    if (n.Children == null)
        //        return selfLength;

        //    var sumOfChildrenLengths = n.Children.Sum(child => GetDerivationTreeLength(child));

        //    return selfLength + sumOfChildrenLengths;
        //}

        public static void CollectRuleUsages(Node n, Dictionary<int, int> ruleCounts, int sentenceCount)
        {
            if (n.Children != null)
            {
                foreach (var child in n.Children)
                    CollectRuleUsages(child, ruleCounts, sentenceCount);
            }

            if (n.RuleNumber != 0) //SCAN_RULE_NUMBER = 0.
            {
                if (!ruleCounts.ContainsKey(n.RuleNumber)) ruleCounts[n.RuleNumber] = 0;
                ruleCounts[n.RuleNumber] += sentenceCount;
                //add +1 to the count of the rule, multiplied by the number of times the sentence appears in the text (sentenceCount).
            }
        }

        public static string RuleUsagesDicToString(Dictionary<int, int> d)
        {
            // Build up each line one-by-one and then trim the end
            var builder = new StringBuilder();
            foreach (var pair in d)
            {
                builder.Append(pair.Key).Append(" : ").Append(pair.Value).AppendLine();
            }
            var result = builder.ToString();
            // Remove the final delimiter
            result = result.TrimEnd(',');
            return result;
        }

        public static int GetDataLength(Grammar grammar, Tuple<Node, int>[] parseTreesWithCounts)
        {
            var sum = 0;
            foreach (var parseTree in parseTreesWithCounts)
            {
                var root = parseTree.Item1; //the node.
                sum += root.Bits * parseTree.Item2;
                //Item2 = number of occurrences of that sentence in the corpus
            }
            return sum;
        }
    }
}
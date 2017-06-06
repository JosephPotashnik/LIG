using System.Collections.Generic;
using System.Linq;

namespace LIGParser
{
    public class LeftCorner
    {
        public Dictionary<int, HashSet<int>> ComputeLeftCorner(Grammar grammar)
        {
            var rules = grammar.ruleNumberDictionary.Values.ToList();
            //key - nonterminal, value - set of the numbers of reachable rules by transitive left corner.
            var leftCorners = new Dictionary<int, HashSet<int>>();

            foreach (var item in rules)
            {
                var ruleNumber = item.Number;
                if (!leftCorners.ContainsKey(ruleNumber))
                    leftCorners[ruleNumber] = new HashSet<int>();


                var ruleList = grammar[item.Production[0].NonTerminal];
                if (ruleList != null)
                {
                    foreach (var predicted in ruleList)
                    {
                        if (!leftCorners[ruleNumber].Contains(predicted.Number))
                            leftCorners[ruleNumber].Add(predicted.Number);
                    }
                }
            }

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var item in leftCorners)
                {
                    var ruleNumber = item.Key;
                    var reachableRules = item.Value;

                    foreach (var reachable in reachableRules)
                    {
                        var reachablesFromReachable = leftCorners[reachable];

                        foreach (var reachreach in reachablesFromReachable)
                        {
                            if (!leftCorners[ruleNumber].Contains(reachreach))
                            {
                                leftCorners[ruleNumber].Add(reachreach);
                                changed = true;
                            }
                        }
                    }
                }
            }
            return leftCorners;
        }
    }
}
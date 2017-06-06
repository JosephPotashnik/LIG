using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LIGParser
{
    public class NonTerminalCounts
    {
        public int lhsCounts;
        public int rhsCounts;

        public NonTerminalCounts()
        {
            lhsCounts = 0;
            rhsCounts = 0;
        }

        public NonTerminalCounts(NonTerminalCounts other)
        {
            lhsCounts = other.lhsCounts;
            rhsCounts = other.rhsCounts;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Grammar
    {
        public const string Epsilon = "#epsilon#";
        public const int ScanRuleNumber = 0;
        private readonly Stack<int> availableRuleNumbers;
        private readonly Dictionary<InverseKeyType, string> inverseRules;
        private readonly Random rand = new Random();
        public readonly Dictionary<int, Rule> ruleNumberDictionary;
        public Dictionary<string, NonTerminalCounts> nonTerminalCounts;
        public Dictionary<string, HashSet<Tuple<int, int>>> RHSDictionary; //first int = rule number, second int = position of the key in RHS of rule.
        public Dictionary<NonTerminalObject, float> nullableProductions;
        private int numberOfRules;

        public Grammar()
        {
            StartSymbol = "START";
            Rules = new Dictionary<string, List<Rule>>();
            Moveables = new HashSet<string>();
            LandingSites = new HashSet<string>();
            POSTypes = new HashSet<string>();
            availableRuleNumbers = new Stack<int>();
            numberOfRules = 0;
            NonTerminalsTypeDictionary = new Dictionary<string, string>();
            inverseRules = new Dictionary<InverseKeyType, string>();
            ruleNumberDictionary = new Dictionary<int, Rule>();
            nonTerminalCounts = new Dictionary<string, NonTerminalCounts>();
            RHSDictionary = new Dictionary<string, HashSet<Tuple<int,int>>>();
            nullableProductions = new Dictionary<NonTerminalObject, float>(new NonTerminalObjectStackComparer());
            //key - string that expand to the empty string. value - the probability.
        }

        public Grammar(Vocabulary voc) : this()
        {
            Vocabulary = voc;
        }

        public Grammar(Grammar otherGrammar)
        {
            Rules = otherGrammar.Rules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            var rules = Rules.Values.SelectMany(l => l).ToArray();
            ruleNumberDictionary =
                rules.Select(x => new KeyValuePair<int, Rule>(x.Number, x)).ToDictionary(x => x.Key, x => x.Value);
            nonTerminalCounts = otherGrammar.nonTerminalCounts.ToDictionary(x => x.Key,
                x => new NonTerminalCounts(x.Value));
            RHSDictionary = otherGrammar.RHSDictionary.ToDictionary(x => x.Key, x => new HashSet<Tuple<int, int>>(x.Value));

            if (otherGrammar.Moveables != null)
                Moveables = new HashSet<string>(otherGrammar.Moveables);
            if (otherGrammar.LandingSites != null)
                LandingSites = new HashSet<string>(otherGrammar.LandingSites);
            StartSymbol = otherGrammar.StartSymbol;
            POSTypes = new HashSet<string>(otherGrammar.POSTypes);
            Vocabulary = otherGrammar.Vocabulary;
            if (otherGrammar.availableRuleNumbers != null)
                availableRuleNumbers = new Stack<int>(otherGrammar.availableRuleNumbers);
            numberOfRules = otherGrammar.numberOfRules;

            NonTerminalsTypeDictionary = otherGrammar.NonTerminalsTypeDictionary.ToDictionary(x => x.Key, x => x.Value);
            inverseRules = otherGrammar.inverseRules.ToDictionary(x => new InverseKeyType(x.Key), x => x.Value);

            nullableProductions = otherGrammar.nullableProductions.ToDictionary(x => x.Key, x => x.Value);
        }

        [JsonProperty]
        public string StartSymbol { get; set; }

        public Vocabulary Vocabulary { get; set; }

        [JsonProperty]
        public Dictionary<string, List<Rule>> Rules { get; set; }

        [JsonProperty]
        public HashSet<string> Moveables { get; set; }

        [JsonProperty]
        public HashSet<string> LandingSites { get; set; }

        [JsonProperty]
        public HashSet<string> POSTypes { get; set; }

        [JsonProperty]
        public Dictionary<string, string> NonTerminalsTypeDictionary { get; set; }

        public List<Rule> this[string lhs]
        {
            get { return Rules.ContainsKey(lhs) ? Rules[lhs] : null; }
        }

        public static Grammar GetGrammarFromFile(string jsonFileName, Vocabulary voc)
        {
            var grammar = JsonConvert.DeserializeObject<Grammar>(File.ReadAllText(jsonFileName));
            grammar.Vocabulary = voc;
            grammar.PopulateDependentJsonPropertys();
            return grammar;
        }

        private void PopulateDependentJsonPropertys()
        {
            var rules = Rules.Values.SelectMany(l => l).ToArray();
            Rules = new Dictionary<string, List<Rule>>();

            foreach (var r in rules)
                AddRule(r);
        }


        public void DeleteRule(Rule rule)
        {
            //delete from rules dictionary
            var lhs = rule.Name.NonTerminal;
            var rulesOfLHS = Rules[lhs];

            rulesOfLHS.Remove(rule);
            var numberOfRemainingRules = rulesOfLHS.Count;

            if (numberOfRemainingRules == 0)
                Rules.Remove(lhs);

            //delete from inverse rules dictionary
            if (!rule.IsEpsilonRule())
            {
                var inverseKey = new InverseKeyType(rule.Production, rule.HeadPosition,
                    rule.Name.NonTerminal == StartSymbol);
                inverseRules.Remove(inverseKey);
            }

            RemoveNonTerminalCounts(rule);
            ruleNumberDictionary.Remove(rule.Number);
            numberOfRules--;
            ReturnUnusedRuleNumber(rule.Number);
        }


        public void RemoveNonTerminalCounts(Rule rule)
        {
            var lhs = rule.Name.NonTerminal;

            if (!nonTerminalCounts.ContainsKey(lhs))
                throw new Exception(
                    string.Format("nonterminal {0} in rule {1} is missing from NonTerminalCounts dictionary", lhs, rule));

            nonTerminalCounts[lhs].lhsCounts--;

            var productionNonterminals = new List<string>();
            if (!rule.IsEpsilonRule())
            {
                int i = 0;

                foreach (var item in rule.Production)
                {
                    var rhs = item.NonTerminal;
                    productionNonterminals.Add(rhs);
                    if (!nonTerminalCounts.ContainsKey(rhs))
                        throw new Exception(
                            string.Format("nonterminal {0} in rule {1} is missing from NonTerminalCounts dictionary",
                                lhs, rule));
                    nonTerminalCounts[rhs].rhsCounts--;

                    if (!RHSDictionary.ContainsKey(rhs))
                        throw new Exception(string.Format("nonterminal {0} in rule {1} is missing from RHSDictionary dictionary",
                                                        rhs, rule));

                    //remove from RHSDictionary.
                    RHSDictionary[rhs].Remove(new Tuple<int,int>(rule.Number, i));
                    i++;
                }
            }

            var lhsCountsOfLHS = nonTerminalCounts[lhs]; //the counts of the Left-hand sided of the rule.

            //if the removed rule has a LHS that no longer has any other LHS appearances, we can replace all its RHS appearances with another nonterminal,
            //because we cannot invoke that non-terminal anymore.

            if (lhsCountsOfLHS.lhsCounts == 0 && lhsCountsOfLHS.rhsCounts > 0 && !POSTypes.Contains(lhs) &&
                lhs != StartSymbol)
            {
                //alternatively - do nothing. The resulting grammar will not use these rules.
            }

            var lhStoDelete = new List<string>();
            if (!rule.IsEpsilonRule())
            {
                foreach (var item in productionNonterminals)
                {
                    var rhsCountsofRhs = nonTerminalCounts[item]; //the counts of the right-hand sided of the rule terms

                    //if the removed rule has a specific RHS, X, that no longer has any other RHS appearances, we can delete the rules that has X as their LHS
                    //because they will not be triggered (orphaned rules)
                    if (rhsCountsofRhs.rhsCounts == 0 && rhsCountsofRhs.lhsCounts > 0 && !POSTypes.Contains(item) &&
                        item != StartSymbol)
                    {
                        var possiblePOS = item.Substring(0, item.Length - 1);
                        if (!POSTypes.Contains(possiblePOS))
                        {
                            lhStoDelete.Add(item);
                        }
                    }
                }
            }

            foreach (var item in lhStoDelete)
            {
                var rules = Rules[item];
                var removedRulesNumbers = rules.Select(x => x.Number);

                foreach (var removedRuleNumber in removedRulesNumbers.ToArray())
                    DeleteRule(ruleNumberDictionary[removedRuleNumber]);
            }

            //after updating all counts of nonterminals in the rule, check if the type is still used.
            //if not, remove it.
            if (lhsCountsOfLHS.lhsCounts == 0 && lhsCountsOfLHS.rhsCounts == 0 && !POSTypes.Contains(lhs) &&
                lhs != StartSymbol)
            {
                var possiblePOS = lhs.Substring(0, lhs.Length - 1);
                if (!POSTypes.Contains(possiblePOS))
                {
                    Console.WriteLine("removed type {0}", lhs);
                    NonTerminalsTypeDictionary.Remove(lhs);
                    nonTerminalCounts.Remove(lhs);
                }
            }
        }

        public void AddNonTerminalCounts(Rule rule)
        {
            var lhs = rule.Name.NonTerminal;

            if (!nonTerminalCounts.ContainsKey(lhs))
                nonTerminalCounts[lhs] = new NonTerminalCounts();

            nonTerminalCounts[lhs].lhsCounts++;

            if (!rule.IsEpsilonRule())
            {
                int i = 0;
                foreach (var item in rule.Production)
                {

                    var rhs = item.NonTerminal;
                    if (!nonTerminalCounts.ContainsKey(rhs))
                        nonTerminalCounts[rhs] = new NonTerminalCounts();
                    nonTerminalCounts[rhs].rhsCounts++;

                    if (!RHSDictionary.ContainsKey(rhs))
                        RHSDictionary[rhs] = new HashSet<Tuple<int,int>>();
                    RHSDictionary[rhs].Add(Tuple.Create(rule.Number, i));
                    i++;

                }
            }
        }

        public void RemoveNonTerminalFromLandingSites(string nonTerminal)
        {
            if (!LandingSites.Contains(nonTerminal)) return;
            LandingSites.Remove(nonTerminal);
        }

        public void RemoveMoveable(string nonTerminal)
        {
            if (!Moveables.Contains(nonTerminal)) return;
            Moveables.Remove(nonTerminal);
        }

        public void AddAllPOSTypesToDictionary(string[] l)
        {
            foreach (var nonTerminal in l)
                AddPOSToTypeDictionary(nonTerminal);
        }

        public void AddPOSToTypeDictionary(string nonTerminal)
        {
            var posType = nonTerminal;
            NonTerminalsTypeDictionary[nonTerminal] = posType;
            POSTypes.Add(nonTerminal);

            //also add the POS projection type.
            var projectionNonTerminal = nonTerminal + "P";
            AddProjectionTypeToTypeDictionary(projectionNonTerminal, posType);
        }

        public void AddProjectionTypeToTypeDictionary(string nonTerminal, string nonTerminalType)
        {
            NonTerminalsTypeDictionary[nonTerminal] = nonTerminalType;
        }

        // if the head is some non-POS projection, then the LHS of the rule is of the same projection!
        // for instance, if X -> NP ADJUNCT, and NP is the head, then X = NP, i.e. NP -> NP ADJUNCT
        // another example: X -> NP VP. if VP is the head, then X = VP, i.e VP -> NP VP.
        public void EnforceHeadRelations(Rule rule)
        {
            var oldRuleName = rule.Name.NonTerminal;
            string projectionType = null;
            if (oldRuleName != StartSymbol && rule.HeadTerm != Epsilon)
            {
                var headType = NonTerminalsTypeDictionary[rule.HeadTerm];
                if (oldRuleName != null)
                {
                    if (!NonTerminalsTypeDictionary.ContainsKey(oldRuleName))
                    {
                        NonTerminalsTypeDictionary[oldRuleName] = headType;
                        //Console.WriteLine("added type {0} for the rule {1}", oldRuleName, rule);
                    }

                    projectionType = NonTerminalsTypeDictionary[oldRuleName];
                }

                if (oldRuleName == null || projectionType != headType)
                    rule.Name.NonTerminal = headType + "P";
            }
        }

        public int GetNextAvailableRuleNumber()
        {
            return availableRuleNumbers.Any() ? availableRuleNumbers.Pop() : numberOfRules;
        }

        public void ReturnUnusedRuleNumber(int number)
        {
            availableRuleNumbers.Push(number);
        }

        public void GenerateInitialRulesFromDerivedRules()
        {
            if (!LandingSites.Any() || !Moveables.Any()) return;

            var initialRules = ruleNumberDictionary.Values.ToList();
            var derivedRules = initialRules.Where(x => !x.IsInitialRule()).ToList();

            var toRemove = derivedRules.ToList();

            foreach (var item in toRemove)
                DeleteRule(item);
        }

        public void GenerateDerivedRulesFromSchema()
        {
            if (!LandingSites.Any() || !Moveables.Any()) return;

            var toAdd = new List<Rule>();

            foreach (var moveable in Moveables)
            {
                var pop1 = new Rule(1, moveable, new[] { Epsilon }, 0, 0);
                pop1.Name.Stack = new NonTerminalStack(moveable);
                toAdd.Add(pop1);

                foreach (var landingSiteNonTerminal in LandingSites)
                {
                    var push = new Rule(1, landingSiteNonTerminal, new[] { moveable, landingSiteNonTerminal }, 1, 1);
                    push.Name.Stack = new NonTerminalStack(".");
                    push.Production[1].Stack = new NonTerminalStack(".");
                    push.Production[1].Stack = push.Production[1].Stack.Push(moveable);
                    toAdd.Add(push);
                }

                var pop2 = new Rule(1, "IP", new[] { moveable, "VP" }, 1, 1);
                pop2.Name.Stack = new NonTerminalStack(".");
                pop2.Name.Stack = pop2.Name.Stack.Push(moveable);
                pop2.Production[0].Stack = new NonTerminalStack(moveable);
                pop2.Production[1].Stack = new NonTerminalStack(".");
                toAdd.Add(pop2);
            }

            foreach (var item in toAdd)
                AddRule(item);
        }


        public string AddRule(Rule rule)
        {
            EnforceHeadRelations(rule);

            // if production already exists under some other rule name, do not re-add the rule
            //that is, if exists A->BC and we encounter D->BC, then A=D.
            var inverseKey = new InverseKeyType(rule.Production, rule.HeadPosition, rule.Name.NonTerminal == StartSymbol);
            var isEpislonRule = rule.IsEpsilonRule();

            if (isEpislonRule || !inverseRules.ContainsKey(inverseKey))
            {
                // add the rule:
                //1) to inverse rules dictionary:
                numberOfRules++;
                rule.Number = GetNextAvailableRuleNumber(); //note: depends on value of self.numberOfRules

                if (!isEpislonRule)
                    inverseRules[inverseKey] = rule.Name.NonTerminal;
                //if the rule is epsilon rule, it does not have inverse key.
                else
                    nullableProductions[rule.Name] = 1.0f; //TODO - temporary probabilioty of 1.0.

                AddNonTerminalCounts(rule);

                //3) to rules dictionary
                if (!Rules.ContainsKey(rule.Name.NonTerminal))
                {
                    Rules[rule.Name.NonTerminal] = new List<Rule>();
                    rule.Occurrences = 1;
                }
                else
                {
                    if (rule.Occurrences == 0) //if rule does not come with positive occurrences (= 0):
                    {
                        //make the occurrences average of the current occurrences.
                        var l = Rules[rule.Name.NonTerminal];
                        var count = l.Count;
                        rule.Occurrences = l.Sum(x => x.Occurrences) / count;
                    }
                }

                Rules[rule.Name.NonTerminal].Add(rule);
                ruleNumberDictionary[rule.Number] = rule;
            }
            //for the sake of convenience, return the rule name that was added
            //it is useful when replacing no longer used symbols with the
            //new rule names.
            if (!isEpislonRule)
                return inverseRules[inverseKey];
            return rule.Name.NonTerminal;
        }

        public void AddNonTerminalToLandingSites(string nonTerminal)
        {
            if (!LandingSites.Contains(nonTerminal))
                LandingSites.Add(nonTerminal);
        }

        public void AddMoveable(string symbol)
        {
            if (!Moveables.Contains(symbol))
                Moveables.Add(symbol);
        }

        //return log probab
        public static double GetProbabilitySumOfTwoLogProbabilities(double logProb1, double logProb2)
        {
            var prob1 = GetProbabilityFromLogProbability(logProb1);
            var prob2 = GetProbabilityFromLogProbability(logProb2);

            return GetLogProbabilityFromProbability(prob1 + prob2);
        }


        public static double GetProbabilityFromLogProbability(double logProb)
        {
            return Math.Pow(2, -logProb);
        }

        public static double GetLogProbabilityFromProbability(double prob)
        {
            return -Math.Log(prob, 2);
        }

        public Rule GetRandomRuleForAGivenLHS(string lhs, List<Rule> candidates)
        {
            var sum = 0;
            var l = candidates ?? Rules[lhs];
            var max = l.Sum(x => x.Occurrences);
            var r = rand.Next(max);

            foreach (var rule in l)
            {
                var current = rule.Occurrences;
                if (sum + current > r)
                    return rule;
                sum += current;
            }
            return null;
        }

        public string GetRandomNonTerminal(bool isStoachstic = false)
        {
            var index = rand.Next(nonTerminalCounts.Count);
            return nonTerminalCounts.Keys.ElementAt(index);
        }

        public Rule GetRandomRule(bool isStoachstic = false)
        {
            var index = rand.Next(ruleNumberDictionary.Values.Count);
            return ruleNumberDictionary.Values.ElementAt(index);
        }

        public bool IsPOS(string nonTerminal)
        {
            return POSTypes.Contains(nonTerminal);
        }

        public bool AreHeadRelationsConsistent(Rule rule)
        {
            var b1 = IsPOS(rule.HeadTerm);
            var b2 = false;
            if (rule.Production.Length > 1)
                b2 = !IsPOS(rule.NonHeadTerm);

            return b1 || b2;
        }

        public override string ToString()
        {
            var rules = ruleNumberDictionary.Values;

            var sortedRules = rules.OrderBy(x => x.Number);
            var ruleTable = string.Join("\r\n", sortedRules);

            var landingSites = "Landing Sites: " + string.Join(" ", LandingSites) + "\r\n";
            var moveables = "Moveables: " + string.Join(" ", Moveables) + "\r\n";

            return ruleTable + "\r\n" + landingSites + moveables;
        }

        public string GrammarWithRuleUsages(Dictionary<int, int> usagesDic)
        {
            var rules = ruleNumberDictionary.Where(x => usagesDic.ContainsKey(x.Key)).Select(x => x.Value);

            var sortedRules = rules.OrderBy(x => x.Number);
            var ruleTable = string.Join("\r\n", sortedRules);

            var landingSites = "Landing Sites: " + string.Join(" ", LandingSites) + "\r\n";
            var moveables = "Moveables: " + string.Join(" ", Moveables) + "\r\n";

            return ruleTable + "\r\n" + landingSites + moveables;
        }

        public string GetNextAvailableProjectionName()
        {
            var nextSymbol = 'A';
            while (true)
            {
                var projection = nextSymbol + "P";
                if (NonTerminalsTypeDictionary.ContainsKey(projection))
                    nextSymbol = (char)(nextSymbol + 1);
                else
                    return projection;
            }
        }

        public bool AllowsTracesPathBetweenLandingSites(Rule landingSiteRule, HashSet<string> tracesTerms,
            HashSet<string> landinbgsites)
        {
            var visitedRules = new HashSet<int>();

            var toVisit = new Queue<string>();
            toVisit.Enqueue(landingSiteRule.ComplementTerm);

            while (toVisit.Any())
            {
                var currentLHS = toVisit.Dequeue();
                if (Rules.ContainsKey(currentLHS))
                {
                    var ruleList = Rules[currentLHS];

                    foreach (var r in ruleList)
                    {
                        if (visitedRules.Contains(r.Number)) continue;
                        visitedRules.Add(r.Number);

                        if ((r.ComplementPosition == 1 && tracesTerms.Contains(r.NonComplementTerm))
                            || r.ComplementPosition == 0)
                        {
                            if (landinbgsites.Contains(r.ComplementTerm))
                                return true;
                            toVisit.Enqueue(r.ComplementTerm);
                        }
                    }
                }
            }
            return false;
        }

        public Dictionary<int, double> GetLogProbabilitiesOfRules()
        {
            //returns dictionary of key = rule.ID, value = logProbability of that rule.
            return ruleNumberDictionary.ToDictionary(x => x.Key,
                x =>
                    GetLogProbabilityFromProbability(x.Value.Occurrences /
                                                     (double)Rules[x.Value.Name.NonTerminal].Sum(y => y.Occurrences)));
        }


        public bool DoesGrammarAllowInfiniteMovement()
        {
            //if (!LandingSites.Any()) return false;

            //var landingSitesLHSTerms = new HashSet<string>(LandingSites.Select(r => r.Name.NonTerminal));
            //var tracesTerms = new HashSet<string>(LandingSites.Select(r => r.NonComplementTerm));

            //foreach (var rule in LandingSites)
            //{
            //    if (AllowsTracesPathBetweenLandingSites(rule, Moveables, LandingSites))
            //        return true;
            //}
            return false;
        }
    }
}
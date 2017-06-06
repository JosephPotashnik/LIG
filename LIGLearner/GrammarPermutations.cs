using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LIGParser;
using Newtonsoft.Json;

namespace LIGLearner
{
    public class GrammarPermutations
    {
        public delegate bool GrammarMutation(Grammar grammar);

        private const int NumberOfRetries = 10;
        private static Tuple<GrammarMutation, int>[] _mutations;
        private static Random _rand;
        private static int _totalWeights;

        public GrammarPermutations()
        {
            _rand = new Random();

            List<GrammarMutationData> l;
            using (var file = File.OpenText(@"MutationWeights.json"))
            {
                var serializer = new JsonSerializer();
                l = (List<GrammarMutationData>)serializer.Deserialize(file, typeof(List<GrammarMutationData>));
            }

            _mutations = new Tuple<GrammarMutation, int>[l.Count];

            var typeInfo = GetType().GetTypeInfo();

            for (var i = 0; i < l.Count; i++)
            {
                foreach (var method in typeInfo.GetDeclaredMethods(l[i].Mutation))
                {
                    var m = (GrammarMutation)method.CreateDelegate(typeof(GrammarMutation), this);
                    _mutations[i] = new Tuple<GrammarMutation, int>(m, l[i].MutationWeight);
                }
            }

            _totalWeights = 0;
            foreach (var mutation in _mutations)
                _totalWeights += mutation.Item2;
        }

        public static GrammarMutation GetWeightedRandomMutation()
        {
            var r = _rand.Next(_totalWeights);
            var sum = 0;
            foreach (var mutation in _mutations)
            {
                if (sum + mutation.Item2 > r)
                    return mutation.Item1;
                sum += mutation.Item2;
            }
            return null;
        }

        public static GrammarMutation GetRandomMutation()
        {
            var r = _rand.Next(_mutations.Length);
            return _mutations[r].Item1;
        }

        //generate a new rule from random existing productions.
        public bool InsertRule(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var productions = new List<string>();
                var randomDaughter = grammar.StartSymbol;
                while (randomDaughter == grammar.StartSymbol)
                    randomDaughter = grammar.GetRandomNonTerminal(); //the first daughter is never the start symbol.

                productions.Add(randomDaughter);

                if (_rand.NextDouble() < 0.5f)
                    productions.Add(grammar.GetRandomNonTerminal());

                var newRule = new Rule();
                newRule.Occurrences = 1;
                newRule.Production = productions.Select(x => new NonTerminalObject(x)).ToArray();

                newRule.HeadPosition = _rand.Next(newRule.Production.Length);
                newRule.ComplementPosition = _rand.Next(newRule.Production.Length);

                if (newRule.HeadTerm == grammar.StartSymbol)
                    //never let the head be the start symbol. the start symbol can only be the second term(see above).
                    newRule.HeadPosition = 0;


                var ruleName = grammar.StartSymbol;
                if (_rand.NextDouble() < 0.9f)
                //90% probability of projecting regular head stucture. 10% allow to project to the START symbol.
                {
                    try
                    {
                        ruleName = grammar.NonTerminalsTypeDictionary[newRule.HeadTerm] + "P";
                    }
                    catch
                    {
                        throw new Exception(string.Format("rule head term not found", newRule.HeadTerm));
                    }
                }
                newRule.Name = new NonTerminalObject(ruleName);

                if (grammar.AreHeadRelationsConsistent(newRule))
                {
                    grammar.AddRule(newRule);
                    return true;
                }
            }
            return false;
        }

        public bool DeleteRule(Grammar grammar)
        {
            var rule = grammar.GetRandomRule();
            grammar.DeleteRule(rule);
            return true;
        }

        public bool ChangeComplementOfRule(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var rule = grammar.GetRandomRule();

                if (!rule.IsInitialRule()) continue; //do not change complements of schematic rules. (push/pop)

                if (rule.Production.Length > 1)
                {
                    rule.ComplementPosition = (rule.ComplementPosition + 1) % rule.Production.Length;
                    return true;
                }
            }
            return false;
        }


        public bool ChangeProbabilityOfRule(Grammar grammar)
        {
            /*for (var i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                var changedRule = grammar.GetRandomRule();
                var LHS = changedRule.Name.NonTerminal;

                //if there is more than one rule with the same LHS, we can change the probability
                var numOfRulesWithLHS = grammar.Rules[LHS].Count;
                if (numOfRulesWithLHS > 1)
                {
                    // giving the rule a new probability, picked from a normal distibution
                    // where the mean is the old probability of the rule.
                    // the deviation is relative to the number of the rules co-existing with the same LHS

                    var mean = Grammar.GetProbabilityFromLogProbability(changedRule.LogProbability);
                    var stdDev = mean / numOfRulesWithLHS;

                    var newProb = 0.0f;

                    while (newProb < 0.01 || newProb > 0.99)
                    {
                        var u1 = rand.NextDouble();
                        var u2 = rand.NextDouble();
                        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                            Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
                        newProb = (float)(mean + stdDev * randStdNormal);
                    }

                    var oldProb = mean;
                    var addition = newProb - oldProb;
                    changedRule.LogProbability = Grammar.GetLogProbabilityFromProbability(newProb);

                    foreach (var rule in grammar.Rules[LHS])
                    {
                        if (rule.Equals(changedRule)) continue;

                        //the addition of the probability to the other rules is made relatively to
                        //their summed probabilities (excluding the original probability of the changed rule)
                        var ruleProbability = Grammar.GetProbabilityFromLogProbability(rule.LogProbability);
                        var relevantProb = ruleProbability / (1.0f - oldProb);


                        ruleProbability -= relevantProb * addition;
                        rule.LogProbability = Grammar.GetLogProbabilityFromProbability(ruleProbability);

                    }
                    return true;
                }
            }*/
            return false;
        }


        public bool AddRuleToLandingSites(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var rule = grammar.GetRandomRule();
                //var movementSymbol = rule.NonComplementTerm;

                //if movement symbol is LHS for some rule..
                if (!grammar.LandingSites.Contains(rule.Name.NonTerminal))
                {
                    grammar.AddNonTerminalToLandingSites(rule.Name.NonTerminal);
                    return true;
                }
            }
            return false;
        }

        public bool RemoveRuleFromLandingSites(Grammar grammar)
        {
            if (grammar.LandingSites.Any())
            {
                var nonTerminal = grammar.LandingSites.ElementAt(_rand.Next(grammar.LandingSites.Count));
                grammar.RemoveNonTerminalFromLandingSites(nonTerminal);
                return true;
            }
            return false;
        }


        public bool AddMoveable(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var rule = grammar.GetRandomRule();

                //if movement symbol is LHS for some rule..
                if (!grammar.Moveables.Contains(rule.Name.NonTerminal))
                {
                    grammar.AddMoveable(rule.Name.NonTerminal);
                    return true;
                }
            }
            return false;
        }

        public bool RemoveMoveable(Grammar grammar)
        {
            if (grammar.Moveables.Any())
            {
                var nonTerminal = grammar.Moveables.ElementAt(_rand.Next(grammar.Moveables.Count));
                grammar.RemoveMoveable(nonTerminal);
                return true;
            }
            return false;
        }

        public bool ChangeHeadOfRule(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var rule = grammar.GetRandomRule();
                if (!rule.IsInitialRule()) continue; //do not change complements of schematic rules. (push/pop)

                if (rule.Production.Length > 1)
                {
                    var newRule = new Rule(rule);
                    newRule.HeadPosition = (rule.HeadPosition + 1) % rule.Production.Length;

                    if (grammar.AreHeadRelationsConsistent(newRule))
                    {
                        grammar.DeleteRule(rule);
                        grammar.AddRule(newRule);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool InsertRuleWithANewSymbol(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var existingLHSNonTerminal = grammar.GetRandomRule().Name.NonTerminal;
                if (existingLHSNonTerminal == grammar.StartSymbol) continue;

                var newLHSNonTerminal = grammar.GetNextAvailableProjectionName();

                var typeOfExistingLHS = grammar.NonTerminalsTypeDictionary[existingLHSNonTerminal];

                grammar.AddProjectionTypeToTypeDictionary(newLHSNonTerminal, typeOfExistingLHS);

                //grammar.AddRule(new Rule(0, grammar.StartSymbol, new[] { newLHSNonTerminal, grammar.StartSymbol }, 0, 1));
                // grammar.AddRule(new Rule(0, grammar.StartSymbol, new[] { newLHSNonTerminal }, 0, 0));

                grammar.AddRule(new Rule(0, newLHSNonTerminal, new[] { existingLHSNonTerminal }, 0, 0));


                return true;
            }
            return false;
        }

        public bool InsertRuleWithANewSymbol_old(Grammar grammar)
        {
            //grammar.CleanOrphanedRules();
            //for (var i = 0; i < NUMBER_OF_RETRIES; i++)
            //{
            //    var fatherRule = grammar.GetRandomRule();
            //    var daughterPos = rand.Next(fatherRule.Production.Length);
            //    var daughter = fatherRule.Production[daughterPos].NonTerminal;

            //    if (grammar.isPOS(daughter)) continue; //needs to be a projection

            //    var newLHSSymbol = grammar.GetNextAvailableProjectionName();
            //    var daughterRule = grammar.GetRandomRuleForAGivenLHS(daughter);
            //    if (daughterRule.Production.Length < 2) continue; //the daughter rule must have two daughters.

            //    //pick out (in random) a rule out of the 4 below which keep the head relations consistent,
            //    var arr = new Rule[4];
            //    var randomProjection = grammar.GetRandomRule().Name.NonTerminal;
            //    if (randomProjection == grammar.StartSymbol) continue; //all projections except START

            //    arr[0] = new Rule(1.0f, newLHSSymbol, new[] { daughter, randomProjection }, 0, 1);
            //    arr[1] = new Rule(1.0f, newLHSSymbol, new[] { randomProjection, daughter }, 1, 0);
            //    arr[2] = new Rule(1.0f, newLHSSymbol, new[] { daughter, randomProjection }, 1, 0);
            //    arr[3] = new Rule(1.0f, newLHSSymbol, new[] { randomProjection, daughter }, 0, 1);

            //    var newRule = arr[rand.Next(4)];
            //    if (!grammar.AreHeadRelationsConsistent(newRule)) continue;

            //    var oldFatherProductions = new string[fatherRule.Production.Length];
            //    fatherRule.Production.CopyTo(oldFatherProductions, 0);
            //    oldFatherProductions[daughterPos] = newLHSSymbol;

            //    var newFatherRule = new Rule(fatherRule);
            //    newFatherRule.Production = oldFatherProductions.Select(x => new NonTerminalObject(x));

            //    if (!grammar.AreHeadRelationsConsistent(newFatherRule)) continue;
            //    var ruleName = grammar.AddRule(newRule);
            //    if (ruleName != newRule.Name.NonTerminal) continue; // in case that the new rule that was added already exists in the grammar.
            //    grammar.AddRule(newFatherRule);
            //    return true;
            //}
            return false;
        }

        public bool ChangeLeftToRightBranch(Grammar grammar)
        {
            /*
            for (var i = 0; i < NUMBER_OF_RETRIES; i++)
            {
                var outerRule = grammar.GetRandomRule();

                if (outerRule.Production.Length < 2) continue;

                var D = outerRule.Production[0];
                var C = outerRule.Production[1];

                if (!grammar.Rules.ContainsKey(D.NonTerminal)) continue;
                var innerRule = grammar.GetRandomRuleForAGivenLHS(D.NonTerminal);
                if (innerRule.Production.Length < 2) continue;
                if (outerRule.Equals(innerRule)) continue;

                var A = innerRule.Production[0];
                var B = innerRule.Production[1];

                var oldOuterRuleName = outerRule.Name.NonTerminal;

                var newOuterRule = new Rule(outerRule);
                newOuterRule.Production = new[] { A, D };
                var newInnerRule = new Rule(innerRule);
                innerRule.Name = D;
                newInnerRule.Production = new[] { B, C };

                // the new rules may be inconsistent with head-driven grammar.
                // for instance, switching branches may create an inconsistent rule  NP -> NP V.
                if (!grammar.AreHeadRelationsConsistent(newInnerRule) ||
                    !grammar.AreHeadRelationsConsistent(newOuterRule)) continue;

                // Console.WriteLine("found HeadRelation consistent");
                var landingSiteInner = grammar.LandingSites.Contains(innerRule);
                var landingSiteOuter = grammar.LandingSites.Contains(outerRule);

                grammar.DeleteRule(innerRule);
                grammar.DeleteRule(outerRule);
                //the grammar may change the rule name according to the head-driven logic.
                var newInnerRuleName = grammar.AddRule(newInnerRule, landingSiteInner);
                newOuterRule.Production = new[] { A, new NonTerminalObject(newInnerRuleName) };
                var newOuterRuleName = grammar.AddRule(newOuterRule, landingSiteOuter);

                //print("change_left_branch_to_right_branch::new outer rule (inserted): {0} new inner rule (inserted) {1}"\
                //    .format(new_outer_rule,new_inner_rule))

                return true;
            }
            */
            return false;
        }

        public bool ChangeRightToLeftBranch(Grammar grammar)
        {
            /* for (var i = 0; i < NUMBER_OF_RETRIES; i++)
             {
                 var outerRule = grammar.GetRandomRule();

                 if (outerRule.Production.Length < 2) continue;

                 var A = outerRule.Production[0];
                 var D = outerRule.Production[1];

                 if (!grammar.Rules.ContainsKey(D.NonTerminal)) continue;

                 var innerRule = grammar.GetRandomRuleForAGivenLHS(D.NonTerminal);
                 if (innerRule.Production.Length < 2) continue;
                 if (outerRule.Equals(innerRule)) continue;

                 var B = innerRule.Production[0];
                 var C = innerRule.Production[1];

                 var oldOuterRuleName = outerRule.Name.NonTerminal;

                 var newOuterRule = new Rule(outerRule);
                 newOuterRule.Production = new[] { D, C };
                 var newInnerRule = new Rule(innerRule);
                 innerRule.Name = D;
                 newInnerRule.Production = new[] { A, B };

                 // the new rules may be inconsistent with head-driven grammar.
                 // for instance, switching branches may create an inconsistent rule  NP -> NP V.
                 if (!grammar.AreHeadRelationsConsistent(newInnerRule) ||
                     !grammar.AreHeadRelationsConsistent(newOuterRule)) continue;
                 //Console.WriteLine("found HeadRelation consistent");

                 var landingSiteInner = grammar.LandingSites.Contains(innerRule);
                 var landingSiteOuter = grammar.LandingSites.Contains(outerRule);

                 grammar.DeleteRule(innerRule);
                 grammar.DeleteRule(outerRule);
                 //the grammar may change the rule name according to the head-driven logic.
                 var newInnerRuleName = grammar.AddRule(newInnerRule, landingSiteInner);
                 newOuterRule.Production = new[] { new NonTerminalObject(newInnerRuleName), C };
                 var newOuterRuleName = grammar.AddRule(newOuterRule, landingSiteOuter);

                 //print("change_left_branch_to_right_branch::new outer rule (inserted): {0} new inner rule (inserted) {1}"\
                 //    .format(new_outer_rule,new_inner_rule))

                 return true;
             }*/
            return false;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class GrammarMutationData
        {
            public GrammarMutationData()
            {
            }

            public GrammarMutationData(string m, int w)
            {
                Mutation = m;
                MutationWeight = w;
            }

            [JsonProperty]
            public string Mutation { get; set; }

            [JsonProperty]
            public int MutationWeight { get; set; }
        }
    }
}
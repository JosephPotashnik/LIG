using System;
using System.Collections.Generic;
using System.Linq;

namespace LIGParser
{
    internal enum ParsingOperation
    {
        Predict,
        Scan,
        TraceComplete,
        Complete
    }

    public class LogException : Exception
    {
        public LogException(string str) : base(str)
        {
        }
    }

    public class GenerateException : Exception
    {
    }

    public class Parser
    {
        private const string GammaRule = "Gamma";
        private const float ChanceToGenerateConstituent = 0.4f; //for running the parser as a generator.
        private readonly Random rand = new Random(); //for running the parser as a generator.
        private readonly Dictionary<int, double> ruleLogProbabilities;
        private bool generator;

        public Parser(Grammar g, bool debug = false)
        {
            Grammar = g;
            Debug = debug;
            ruleLogProbabilities = Grammar.GetLogProbabilitiesOfRules();
        }

        public bool Debug { get; set; }

        public Grammar Grammar { get; set; }

        private RuleConsistentWithDerivation IsPredictedRuleConsistentWithCurrentDerivation(
            NonTerminalObject currObject, Rule rule)
        {
            //TODO: change later to skip -all- rules whose derivation leads to the empty string.
            //I.e,  A-> B.C , C -> D E. D -> epsilon, E -> epsilon. C itself is not an epsilon rule.

            //1. states that are the result of a spontenous dot shift (due to nullable production) 
            //have already been added to the agendas in Column.Add()
            if (rule.IsEpsilonRule() && Grammar.nullableProductions.ContainsKey(currObject))
                return RuleConsistentWithDerivation.SkipGeneration;

            //2. if current stack is empty but predicted stack is not, mismatch - do not predict this rule.
            if (currObject.IsStackEmpty() && !rule.IsInitialOrDotStack())
                return RuleConsistentWithDerivation.RuleInconsistent;

            if (rule.IsInitialRule())
            {
                var complementPositionObject = rule.Production[rule.ComplementPosition];

                //3. if current stack is not empty, but the complement position does not allow for stacks (POS),
                //mismatch - do not predict this rule.
                if (!currObject.IsStackEmpty() && Grammar.IsPOS(complementPositionObject.NonTerminal))
                    return RuleConsistentWithDerivation.RuleInconsistent;
            }
            else
            {
                //4. if tops of the stacks do not match, continue. e.g created rule PP[PP] -> epsilon, current object: PP[NP].
                if (rule.Name.Stack.Peek() != "." &&
                    !currObject.Stack.GetListOfTopSymbols().Contains(rule.Name.Stack.Peek()))
                    return RuleConsistentWithDerivation.RuleInconsistent;
            }
            return RuleConsistentWithDerivation.RuleConsistent;
        }

        private void Predict(Column col, List<Rule> ruleList, State state, NonTerminalObject currObject)
        {
            //if (generator)
            //{
            //    var rule = Grammar.GetRandomRuleForAGivenLHS(currObject.NonTerminal, Grammar.Rules[currObject.NonTerminal]);
            //    ruleList = new List<Rule> { rule };
            //}

            //var preds = GenerateSetOfPredictions();

            if (generator)
            {
                Rule rule = null;

                var l = Grammar.Rules[currObject.NonTerminal];
                var feasibleRules = new List<Rule>();

                //when generating, predict only rules that terminate the derivation successfully.
                foreach (var candidate in l)
                {
                    var c = IsPredictedRuleConsistentWithCurrentDerivation(currObject, candidate);
                    if (c == RuleConsistentWithDerivation.SkipGeneration) return;

                    //temporary: do not push another symbol if the current stack already contains a symbol
                    //in other words: allow only one symbol. 
                    var complementPositionObject = candidate.Production[candidate.ComplementPosition];
                    var isPushRule = !complementPositionObject.IsStackEmpty() &&
                                     complementPositionObject.Stack.Top != Grammar.Epsilon;


                    if (!currObject.IsStackEmpty() && isPushRule) continue;

                    if (c == RuleConsistentWithDerivation.RuleConsistent)
                        feasibleRules.Add(candidate);
                }

                if (feasibleRules.Count == 0) //no feasible rules to predict
                    return;

                rule = Grammar.GetRandomRuleForAGivenLHS(currObject.NonTerminal, feasibleRules);
                ruleList = new List<Rule> { rule };
            }

            foreach (var rule in ruleList)
            {
                if (IsPredictedRuleConsistentWithCurrentDerivation(currObject, rule) !=
                    RuleConsistentWithDerivation.RuleConsistent) continue;

                //prepare new rule based on the stack information contained in the current state
                //and based on the predicted rule.
                var createdRule = new Rule(rule);

                //if the rule is not a stack manipulating rule, 
                if (rule.IsInitialRule())
                {
                    var complementPositionObject = createdRule.Production[createdRule.ComplementPosition];
                    //the stack of the LHS of the created rule is the stack of the current object:
                    createdRule.Name = currObject;
                    //copy the stack to the complement position.
                    complementPositionObject.Stack = currObject.Stack;
                }
                else
                {
                    //create left hand side of new rule.
                    createdRule.Name.Stack = currObject.Stack;
                    createdRule.Name.NonTerminal = currObject.NonTerminal;

                    //create right hand side of new rule.
                    NonTerminalStack contentOfDot;
                    if (rule.Name.Stack.Peek() == ".")
                        contentOfDot = currObject.Stack; //e.g. A[..]
                    else
                        contentOfDot = currObject.Stack.GetPrefixListStackObjectOfGivenTop(rule.Name.Stack.Peek());
                    //e.g A[..X]

                    for (var i = 0; i < rule.Production.Length; i++)
                    {
                        var s = rule.Production[i].Stack;

                        if (s != null)
                        {
                            if (s.Peek() == ".")
                                createdRule.Production[i].Stack = contentOfDot; // e.g, A[..] pop rule.
                            else if (s.PrefixList == null)
                                createdRule.Production[i].Stack = s; //e.g. A[X] //secondary constituent.
                            else
                                createdRule.Production[i].Stack = new NonTerminalStack(s.Peek(), contentOfDot);
                            //e.g. A[..X] - push rule.

                            //calculate the new weight of the top of the stack from the weights of its sons.
                            //if (createdRule.Production[i].Stack != null)
                            //    createdRule.Production[i].Stack.Weight = createdRule.Production[i].Stack.PrefixList != null ? createdRule.Production[i].Stack.PrefixList.Sum(x => x.Weight) : 1;
                        }
                    }
                }

                var newState = new State(createdRule, 0, col, null) { LogProbability = ruleLogProbabilities[rule.Number] };

                if (newState.LogProbability < 0)
                    throw new Exception("wrong probability");

                var added = col.AddState(newState, ParsingOperation.Predict);

                if (Debug)
                    Console.WriteLine("{0} & {1} & {2} & Predicted from State {3}, added: {4}\\\\", newState.StateNumber,
                        newState,
                        col.Index, state.StateNumber, added);
            }
        }

        private void Scan(Column col, State state, string term, string token)
        {
            //if there is nonempty stack arriving to this part of speech term, stop here - the derivation is wrong.
            //SEFI - note: this is a restriction on the form of your grammar.
            //consider removing it to see the conequences.

            //if (Grammar.isPOS(term))
            {
                if (!state.NextProductionTerm().IsStackEmpty()) return;
            }

            var v = new Node(term, col.Index - 1, col.Index)
            {
                AssociatedTerminal = token,
                LogProbability = 0.0f,
                Bits = 1
            };
            var y = State.MakeNode(state, col.Index, v);
            var newState = new State(state.Rule, state.DotIndex + 1, state.StartColumn, y);

            col.AddState(newState, ParsingOperation.Scan);
            if (Debug)
                Console.WriteLine("{0} & {1} & {2} & Scanned from State {3}, word: {4}\\\\", newState.StateNumber,
                    newState, col.Index,
                    state.StateNumber, token);

            if (newState.Node.LogProbability < 0)
            {
                throw new LogException(string.Format("scanarrrr! NODE log probability lower than 0: {0}, state: {1}",
                    newState.Node.LogProbability, newState));
            }
        }

        private bool IsCompletedTermConsistentWithNextTerm(NonTerminalObject completedTerm, NonTerminalObject nextTerm,
            out NonTerminalStack intersection)
        {
            intersection = null;
            if (nextTerm == null) return false;
            //the above case happens where the rule to be continued is already completed at the start column (next term = null).
            //this can only happen in a single case: that rule was an epsilon rule.

            if (completedTerm.NonTerminal != nextTerm.NonTerminal)
                return false;

            if (nextTerm.Stack == null && completedTerm.Stack == null) return true;

            var intersectAlgo = new StackGraphIntersect();
            intersection = intersectAlgo.Execute(nextTerm.Stack, completedTerm.Stack);

            return intersection != null;
        }

        private void Complete(Column col, State state)
        {
            if (state.Rule.Name.NonTerminal == GammaRule)
            {
                col.GammaStates.Add(state);
                return;
            }

            foreach (var st in state.StartColumn)
            {
                var term = st.NextProductionTerm();
                NonTerminalStack intersection;
                if (IsCompletedTermConsistentWithNextTerm(state.Rule.Name, term, out intersection))
                {
                    if (state.Node.LogProbability < 0)
                    {
                        throw new LogException(
                            string.Format(
                                "trrrr! NODE log probability lower than 0: {0}, reductor state: {1}, predecessor state {2}",
                                state.Node.LogProbability, state, st));
                    }
                    var y = State.MakeNode(st, state.EndColumn.Index, state.Node);
                    var newState = new State(st.Rule, st.DotIndex + 1, st.StartColumn, y);
                    newState.Rule.Production[st.DotIndex].Stack = intersection;

                    col.AddState(newState, ParsingOperation.Complete);
                    if (Debug)
                        Console.WriteLine("{0} & {1} & {2} & Completed from States {3} and {4}\\\\",
                            newState.StateNumber,
                            newState, col.Index, st.StateNumber, state.StateNumber);
                }
            }
        }

        private void TestForTooManyStatesInColumn(int count, bool debug)
        {
            if (count > 10000 && !debug)
            {
                Console.WriteLine("More than 10000 states in a single column. Suspicious. Grammar is : {0}",
                    Grammar);
                Debug = true;
                throw new Exception("Grammar with infinite parse. abort this grammar..");
            }
        }

        public string[] GenerateSentences(int numberOfSentences)
        {
            var sentences = new string[numberOfSentences];
            for (var i = 0; i < numberOfSentences; i++)
            {
                Node n = null;
                while (n == null)
                {
                    try
                    {
                        n = ParseSentence(null);
                    }
                    catch (Exception e)
                    {
                        n = null;
                    }
                }

                sentences[i] = n.GetTerminalStringUnderNode();
            }
            //var y = (float)sentences.Select(x => x.Length).Sum() / numberOfSentences;
            //Console.WriteLine($"average characters in generated sentence: {y}");
            return sentences;
        }

        private HashSet<NonTerminalObject> GenerateSetOfPredictions(int maxElementsInStack = 2)
        {
            Queue<NonTerminalObject> queue = new Queue<NonTerminalObject>();
            HashSet<NonTerminalObject> visitedNonTerminalObjects = new HashSet<NonTerminalObject>(new NonTerminalObjectStackComparer());

            NonTerminalObject o = null;

            foreach (var moveable in Grammar.Moveables)
            {
                o = new NonTerminalObject(moveable);
                o.Stack = new NonTerminalStack(moveable);
                queue.Enqueue(o);
            }

            while (queue.Any())
            {
                var rhs = queue.Dequeue();
                visitedNonTerminalObjects.Add(rhs);

                var rulesforRHS = Grammar.RHSDictionary[rhs.NonTerminal].ToList();
                foreach (var item in rulesforRHS)
                {
                    o = null;
                    var rule = Grammar.ruleNumberDictionary[item.Item1];
                    int RHSPosition = item.Item2;
                    if (rule.Name.NonTerminal == Grammar.StartSymbol) continue;

                    if (rule.IsInitialRule())
                    {
                        if (rule.ComplementPosition == RHSPosition)
                        {
                            o = new NonTerminalObject(rule.Name.NonTerminal);
                            o.Stack = new NonTerminalStack(rhs.Stack);
                            if (!visitedNonTerminalObjects.Contains(o))
                                queue.Enqueue(o);
                        }
                    }
                    else
                    {
                        NonTerminalStack contentOfDot;

                        var s = rule.Production[RHSPosition].Stack;
                        if (s != null)
                        {
                            if (s.Peek() == ".")
                                contentOfDot = rhs.Stack;
                            else if (s.PrefixList != null)
                                contentOfDot = rhs.Stack.GetPrefixListStackObjectOfGivenTop(rhs.Stack.Peek());
                            else
                                continue;
                            //assumption: if this is a secondary constituent (i.e, s.PrefixList == null), does not participate in the sharing of stacks,
                            //the LHS will be handled through the primary constituent (the distinguished descendant).


                            o = new NonTerminalObject(rule.Name.NonTerminal);
                            if (rule.Name.Stack.Peek() == ".")
                            {
                                if (contentOfDot != null)
                                    o.Stack = new NonTerminalStack(contentOfDot);
                            }
                            else
                            {
                                if (contentOfDot == null || contentOfDot.Depth() < maxElementsInStack)
                                    o.Stack = new NonTerminalStack(rule.Name.Stack.Peek(), contentOfDot);
                            }
                            if (o.Stack!= null && !visitedNonTerminalObjects.Contains(o))
                                queue.Enqueue(o);
                        }

                    }
                  
                }
            }

            return visitedNonTerminalObjects;
        }

        public Node ParseSentence(string text)
        {
            string[] arr;
            if (text == null)
            {
                generator = true;
                arr = Enumerable.Repeat("", 100).ToArray();
            }
            else
                arr = text.Split();


            //check below that the text appears in the vocabulary
            if (!generator && arr.Any(str => !Grammar.Vocabulary.ContainsWord(str)))
                throw new Exception("word in text does not appear in the vocabulary.");

            var table = new Column[arr.Length + 1];
            for (var i = 1; i < table.Length; i++)
                table[i] = new Column(i, arr[i - 1], Grammar);
            table[0] = new Column(0, "", Grammar);
            State.stateCounter = 0;
            var startRule = new Rule(0, GammaRule, new[] { Grammar.StartSymbol }, 0, 0);
            //startRule.Production[0].Stack = new NonTerminalStack(Grammar.EPSILON);
            var startState = new State(startRule, 0, table[0], null);
            startState.LogProbability = 0.0f;
            Node.grammar = Grammar;
            table[0].AddState(startState, ParsingOperation.Scan);
            var finalColumn = table[table.Length - 1];
            try
            {
                foreach (var col in table)
                {
                    var count = 0;
                    if (generator && !col.States.Any())
                    {
                        finalColumn = table[col.Index - 1];
                        break;
                    }
                    //1. complete
                    while (col.ActionableCompleteStates.Any())
                    {
                        count++;
                        TestForTooManyStatesInColumn(count, Debug);

                        var states = col.ActionableCompleteStates.First().Value;

                        var state = states.Dequeue();
                        if (!states.Any())
                            col.ActionableCompleteStates.Remove(state);

                        if (generator)
                            state.LogProbability = 0;

                        Complete(col, state);
                    }

                    //2. predict after complete:
                    while (col.ActionableNonCompleteStates.Any())
                    {
                        if (col.ActionableCompleteStates.Any())
                            throw new Exception(
                                "completed states queue should always be empty while processing predicted states.");
                        count++;
                        TestForTooManyStatesInColumn(count, Debug);

                        var state = col.ActionableNonCompleteStates.Dequeue();
                        if (generator)
                        {
                            state.LogProbability = 0;

                            //if generated sentence is too long, ignore it - too taxing on computational resources.
                            if (col.Index > 9)
                                throw new GenerateException();
                        }

                        var currObject = state.NextProductionTerm();
                        var term = currObject.NonTerminal;
                        var ruleList = Grammar[term];

                        if (ruleList != null)
                            Predict(col, ruleList, state, currObject);
                    }
                    //3. scan after predict.
                    foreach (var state in col)
                    {
                        if (!state.IsCompleted())
                        {
                            var currObject = state.NextProductionTerm();
                            var term = currObject.NonTerminal;

                            if (!generator)
                            {
                                if (col.Index + 1 < table.Length &&
                                    Grammar.Vocabulary[table[col.Index + 1].Token].Contains(term))
                                    Scan(table[col.Index + 1], state, term, table[col.Index + 1].Token);
                            }
                            else
                            {
                                if (Grammar.Vocabulary.POSWithPossibleWords.ContainsKey(term))
                                {
                                    var ruleList = Grammar[term];
                                    //if the term is a constituent, generate it given some probability. otherwise continue.
                                    if (ruleList != null)
                                    {
                                        if (rand.NextDouble() > ChanceToGenerateConstituent) continue;
                                        if (ruleList[0].IsEpsilonRule())
                                            continue;
                                        //if we generated a predicted epsilon rule for that constituent, don't scan.
                                    }

                                    //selecting random word from vocabulary: (uncomment the next line)
                                    //var index = rand.Next(Vocabulary.POSWithPossibleWords[currentNode.Name].Count);

                                    //always selecting the same word from vocabulary is considerably faster because I do not re-parse the same sentence
                                    //but keep the counts of appearances of the sentence.
                                    //the parse of two sentences with identical sequence of POS is the same - regardless of the actual word selected.

                                    if (table[col.Index + 1].Token == "")
                                    //if the token was already written by a previous scan 
                                    //(for instance NP -> John, NP -> D N, D -> the, "John" was already written before "the")
                                    {
                                        var index = 0;
                                        table[col.Index + 1].Token =
                                            Grammar.Vocabulary.POSWithPossibleWords[term].ElementAt(index);
                                        Scan(table[col.Index + 1], state, term, table[col.Index + 1].Token);
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var state in finalColumn.GammaStates)
                    return state.Node.Children[0];
            }
            catch (LogException e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
                Console.WriteLine(string.Format("sentence: {0}, grammar: {1}", text, Grammar));
            }
            catch (GenerateException e)
            {
            }
            catch (Exception e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
            }

            if (!generator)
                throw new Exception("Parsing Failed!");
            throw new Exception("Generating Failed!");
        }

        private enum RuleConsistentWithDerivation
        {
            RuleConsistent,
            RuleInconsistent,
            SkipGeneration
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;

namespace LIGParser
{
    internal class CompletedStateComparer : IComparer<State>
    {
        public int Compare(State x, State y)
        {
            if (x.StartColumn.Index > y.StartColumn.Index)
                return -1;
            if (x.StartColumn.Index < y.StartColumn.Index)
                return 1;
            return 0;
        }
    }

    internal class CompletedStatesEquality : IEqualityComparer<State>
    {
        bool IEqualityComparer<State>.Equals(State x, State y)
        {
            return x.StartColumn == y.StartColumn && x.Rule.Name.NonTerminal == y.Rule.Name.NonTerminal;
        }

        int IEqualityComparer<State>.GetHashCode(State obj)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + obj.Rule.Name.NonTerminal.GetHashCode();
                hash = hash * 23 + obj.StartColumn.Index;
                return hash;
            }
        }
    }

    internal class Column : IEnumerable<State>
    {
        private readonly bool debug = false;
        private readonly Grammar grammar;

        public Column(int index, string token, Grammar g)
        {
            Index = index;
            Token = token;

            States = new List<State>();
            ActionableNonCompleteStates = new Queue<State>();
            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates = new SortedDictionary<State, Queue<State>>(new CompletedStateComparer());

            StatesDictionary = new Dictionary<State, State>();
            //completed states are equal if they span the same input indices and have the same LHS side.
            StateViterbiCompletedDictionary = new Dictionary<State, State>(new CompletedStatesEquality());

            GammaStates = new List<State>();
            grammar = g;
            //SchematicRules = new HashSet<Rule>(new SchematicRulesComparer());  //in predict, we compare schematic rules without regard to their stacks.
            //so we can check whether the same schematic rule has been added to the same column (= cycle), similarly to the simpler CFG left recursion case.
        }

        public int Index { get; set; }
        public string Token { get; set; }

        public List<State> States { get; set; }
        public Queue<State> ActionableNonCompleteStates { get; set; }
        public SortedDictionary<State, Queue<State>> ActionableCompleteStates { get; set; }

        public List<State> GammaStates { get; set; }
        public Dictionary<State, State> StateViterbiCompletedDictionary { get; set; }
        public Dictionary<State, State> StatesDictionary { get; set; }


        public HashSet<Rule> SchematicRules { get; set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<State> GetEnumerator()
        {
            for (var i = 0; i < States.Count; i++)
            {
                yield return States[i];
            }
        }


        private void EpsilonComplete(State state)
        {
            var v = new Node("trace", Index, Index)
            {
                LogProbability = 0.0f,
                Bits = 1
            };
            var y = State.MakeNode(state, Index, v);
            var newState = new State(state.Rule, state.DotIndex + 1, state.StartColumn, y);

            AddState(newState, ParsingOperation.TraceComplete);
            //if (true)
            //    Console.WriteLine("{0} & {1} & {2} & EpsilonComplete from State {3}\\\\", newState.StateNumber, newState,
            //        Index, state.StateNumber);

            if (newState.Node.LogProbability < 0)
            {
                throw new LogException(
                    string.Format("EpsilonCompletearrrr! NODE log probability lower than 0: {0}, state: {1}",
                        newState.Node.LogProbability, newState));
            }
        }

        private static void Merge(State oldState, State newState)
        {
            StackGraphAlgoBase mergeAlgo = new StackGraphMerge();

            //the state has already been predicted modulo the stack. we need to share stacks:
            oldState.Rule.Name.Stack = mergeAlgo.Execute(oldState.Rule.Name.Stack, newState.Rule.Name.Stack);

            for (var i = 0; i < oldState.Rule.Production.Length; i++)
            {
                mergeAlgo.Clear();
                oldState.Rule.Production[i].Stack = mergeAlgo.Execute(oldState.Rule.Production[i].Stack,
                    newState.Rule.Production[i].Stack);
            }
        }


        public bool AddState(State newState, ParsingOperation op)
        {
            var addedToStates = false;
            var addedToCompletedViterbi = false;
            State oldState;
            var oldOrNewState = false;


            var isCompleted = newState.IsCompleted();


            //step 1: check in states dictionary
            //if does not exist, add. otherwise, merge.
            if (!StatesDictionary.TryGetValue(newState, out oldState))
            {
                StatesDictionary.Add(newState, newState);
                newState.EndColumn = this;
                States.Add(newState);
                addedToStates = true;
            }
            else
            {
                if (op != ParsingOperation.Scan)
                {
                    //the state is already present, modulo the stack. we need to share stacks:
                    Merge(oldState, newState); //merging into the old state.
                    oldOrNewState = true;

                    //inner probability of completed, but not predicted, item changes upon merge
                    //(see stolcke 1995)
                    if (oldState.Node != null && op == ParsingOperation.Complete)
                    {
                        oldState.Node.LogProbability =
                            Grammar.GetProbabilitySumOfTwoLogProbabilities(oldState.Node.LogProbability,
                                newState.Node.LogProbability);

                        if (oldState.Node.LogProbability < 0)
                            throw new Exception(
                                string.Format(
                                    "case2: impossible probability resulted from summation of two log probabilities {0},",
                                    oldState.Node.LogProbability));
                    }
                }
                else
                {
                    throw new Exception("scan - identical states - not supposed to happen?!?");
                }
            }


            //step 2: if completed, check in completed dictionary.
            //if does not exist, add. otherwise, replace with old state.
            if (isCompleted)
            {
                var stateTocheck = oldOrNewState ? oldState : newState;

                State oldStateViterbi;
                if (!StateViterbiCompletedDictionary.TryGetValue(stateTocheck, out oldStateViterbi))
                {
                    StateViterbiCompletedDictionary.Add(stateTocheck, stateTocheck);
                    addedToCompletedViterbi = true;
                }
                else
                {
                    if (stateTocheck.Node.LogProbability < oldStateViterbi.Node.LogProbability)
                    {
                        oldStateViterbi.Node = stateTocheck.Node;

                        if (oldStateViterbi.Node.LogProbability < 0)
                        {
                            throw new LogException(
                                string.Format(
                                    "columnarrr! NODE log probability lower than 0: {0}, reductor state: {1}",
                                    oldStateViterbi.Node.LogProbability, oldStateViterbi));
                        }
                        if (debug)
                            Console.WriteLine("state {0} has higher probability than state {1}. ",
                                stateTocheck.StateNumber,
                                oldStateViterbi.StateNumber);
                    }
                }
            }

            var finalAdd = false;

            //step 3: determine whether the state needs to be added to the agendas.
            if (!isCompleted && addedToStates)
            {
                ActionableNonCompleteStates.Enqueue(newState);

                var term = newState.NextProductionTerm();

                //check if the next nonterminal leads to an expansion of null production, if yes, insert it to the 
                //completed rules.

                //initially just check if the next nonterminal is nullable production
                if (grammar.nullableProductions.ContainsKey(term))
                {
                    //spontaneous dot shift.
                    EpsilonComplete(newState);
                }

                finalAdd = true;
            }
            else if (isCompleted && addedToCompletedViterbi)
            {
                //note: it is possible that a new state has been added to the state dictionary
                //but it is not added to the viterbi completed state dictionary.
                //in such a case, do not push the item to the agenda.
                //with the same LHS side
                if (!ActionableCompleteStates.ContainsKey(newState))
                    ActionableCompleteStates[newState] = new Queue<State>();

                ActionableCompleteStates[newState].Enqueue(newState);

                finalAdd = true;
            }


            return finalAdd;
        }
    }
}
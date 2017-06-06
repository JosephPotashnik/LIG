using System;
using System.Linq;

namespace LIGParser
{
    internal class State
    {
        public static int stateCounter;

        public State(Rule r, int dotIndex, Column c, Node n)
        {
            Rule = r;
            DotIndex = dotIndex;
            StartColumn = c;
            EndColumn = null;
            Node = n;
            StateNumber = stateCounter;
            stateCounter += 1;
            LogProbability = -1;
        }


        public Rule Rule { get; set; }
        public Column StartColumn { get; set; }
        public Column EndColumn { get; set; }
        public int DotIndex { get; set; }
        public Node Node { get; set; }
        public double LogProbability { get; set; }

        public int StateNumber { get; set; }

        public static int RequiredBitsGivenLogProbability(double logprobability)
        {
            return (int)Math.Ceiling(logprobability) + 1;
        }

        private static string RuleWithDotNotation(Rule rule, int dotIndex)
        {
            var terms = rule.Production.Select(x => x.ToString()).ToList();
            terms.Insert(dotIndex, "$");
            return string.Format("{0} -> {1}", rule.Name, string.Join(" ", terms));
        }

        public bool IsCompleted()
        {
            return DotIndex >= Rule.Production.Length;
        }

        public NonTerminalObject NextProductionTerm()
        {
            if (IsCompleted())
                return null;
            return Rule.Production[DotIndex];
        }

        public override string ToString()
        {
            var endColumnIndex = "None";
            if (EndColumn != null)
                endColumnIndex = EndColumn.Index.ToString();
            return string.Format("{0} [{1}-{2}]", RuleWithDotNotation(Rule, DotIndex),
                StartColumn.Index, endColumnIndex);
        }

        public override bool Equals(object obj)
        {
            var s = obj as State;
            if (s == null)
                return false;
            var val = Rule.Equals(s.Rule) && (DotIndex == s.DotIndex) && (StartColumn.Index == s.StartColumn.Index);

            if (Node == null || s.Node == null)
                return val;
            return val && Node.Equals(s.Node);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Rule.GetHashCode();
                hash = hash * 23 + DotIndex;
                hash = hash * 23 + StartColumn.Index;
                return hash;
            }
        }

        public static Node MakeNode(State predecessorState, int endIndex, Node reductor)
        {
            Node y;
            var nextDotIndex = predecessorState.DotIndex + 1;
            var nodeName = RuleWithDotNotation(predecessorState.Rule, nextDotIndex);

            if (nextDotIndex == 1 && predecessorState.Rule.Production.Length > 1)
            {
                y = reductor;
                if (predecessorState.Node == null)
                {
                    y.LogProbability = predecessorState.LogProbability + reductor.LogProbability;
                    y.Bits = RequiredBitsGivenLogProbability(predecessorState.LogProbability) + reductor.Bits;
                    if (predecessorState.LogProbability < 0 || reductor.LogProbability < 0)
                    {
                        throw new Exception(
                            string.Format("first case y NODE log probability lower than 0: {0} < , {1} , {2}",
                                y.LogProbability, predecessorState.LogProbability, reductor.LogProbability));
                    }
                }
                else
                    throw new Exception("arrived in a clause that should not be possible. make_node");
            }
            else
            {
                y = new Node(nodeName, predecessorState.StartColumn.Index, endIndex);
                if (!y.HasChildren())
                    y.AddChildren(reductor, predecessorState.Node);
                if (predecessorState.Node == null)
                {
                    y.LogProbability = predecessorState.LogProbability + reductor.LogProbability;
                    y.Bits = RequiredBitsGivenLogProbability(predecessorState.LogProbability) + reductor.Bits;

                    if (predecessorState.LogProbability < 0 || reductor.LogProbability < 0)
                    {
                        throw new Exception(
                            string.Format("second case y NODE log probability lower than 0: {0} = , {1} + {2}",
                                y.LogProbability, predecessorState.LogProbability, reductor.LogProbability));
                    }
                }
                else
                {
                    y.LogProbability = predecessorState.Node.LogProbability + reductor.LogProbability;
                    y.Bits = predecessorState.Node.Bits + reductor.Bits;
                    if (predecessorState.Node.LogProbability < 0 || reductor.LogProbability < 0)
                    {
                        throw new Exception(
                            string.Format("third case y NODE log probability lower than 0: {0} = , {1} + {2}",
                                y.LogProbability, predecessorState.Node.LogProbability, reductor.LogProbability));
                    }
                }


                y.RuleNumber = predecessorState.Rule.Number;
            }

            return y;
        }
    }
}
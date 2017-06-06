using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LIGParser
{
    public class InputNonTerminalStack
    {
        public NonTerminalStack Stack { get; set; }
        public int GraqphNumber { get; set; }

        public InputNonTerminalStack(NonTerminalStack s, int number)
        {
            Stack = s;
            GraqphNumber = number;
        }

        public override bool Equals(object obj)
        {
            var p = obj as InputNonTerminalStack;
            if (p == null)
                return false;

            //Stack comparisons = by reference.
            return (p.Stack == Stack) && (p.GraqphNumber == GraqphNumber);
        }

        public override int GetHashCode()
        {
            return Stack.GetHashCode() + GraqphNumber * 23;

        }
    }
    public abstract class StackGraphAlgoBase
    {

        protected StackGraphAlgoBase()
        {
            Mappings = new Dictionary<InputNonTerminalStack, List<NonTerminalStack>>();
            SonsOfInputVertices = new Dictionary<NonTerminalStack, SonDataFromInputGraphs>();
        }

        public void Clear()
        {
            Mappings.Clear();
            SonsOfInputVertices.Clear();
        }

        public Dictionary<InputNonTerminalStack, List<NonTerminalStack>> Mappings { get; set; }
        public Dictionary<NonTerminalStack, SonDataFromInputGraphs> SonsOfInputVertices { get; set; }

        public void MapVertices(InputNonTerminalStack vertex1, NonTerminalStack target)
        {

            if (Mappings.ContainsKey(vertex1) && Mappings[vertex1].Contains(target))
                return;

            if (!Mappings.ContainsKey(vertex1))
                Mappings[vertex1] = new List<NonTerminalStack>();

            Mappings[vertex1].Add(target);
        }

        public abstract NonTerminalStack Execute(NonTerminalStack stack1, NonTerminalStack stack2);

        protected abstract bool CheckForInternalLeaves(InputNonTerminalStack stack1, InputNonTerminalStack stack2,
            NonTerminalStack outputStack, out NonTerminalStack res);

        protected abstract void TraverseRemainingList(bool moveNext, List<NonTerminalStack>.Enumerator iterator,
            NonTerminalStack outputStack, int inputGraphNumber);

        protected abstract NonTerminalStack AddSubtree(InputNonTerminalStack inputStack, int inputGraphNumber);

        protected void RecordSonDataOfInputGraphs(NonTerminalStack outputStack, int inputGraphNumber)
        {
            if (inputGraphNumber == 0)
                SonsOfInputVertices[outputStack] = new SonDataFromInputGraphs(false, false);
            else if (inputGraphNumber == 1)
            {
                if (!SonsOfInputVertices.ContainsKey(outputStack))
                    SonsOfInputVertices[outputStack] = new SonDataFromInputGraphs(true, false);
                SonsOfInputVertices[outputStack].VertexFromGraph1HasSon = true;
            }
            else if (inputGraphNumber == 2)
            {
                if (!SonsOfInputVertices.ContainsKey(outputStack))
                    SonsOfInputVertices[outputStack] = new SonDataFromInputGraphs(false, true);
                SonsOfInputVertices[outputStack].VertexFromGraph2HasSon = true;
            }
            else if (inputGraphNumber == 3)
                SonsOfInputVertices[outputStack] = new SonDataFromInputGraphs(true, true);
        }

        public bool CheckForFinalLeaves(InputNonTerminalStack v1, InputNonTerminalStack v2, NonTerminalStack outputStack)
        {
            //both null prefix lists and identical tops = return stack1 / stack2. (the leaf).
            if (v1.Stack.PrefixList == null && v2.Stack.PrefixList == null)
            {
                //if we already visited the output stack, if it has sons we cannot use it, we need to create new node.
                if (SonsOfInputVertices.ContainsKey(outputStack))
                {
                    if (SonsOfInputVertices[outputStack].VertexFromGraph1HasSon ||
                        SonsOfInputVertices[outputStack].VertexFromGraph2HasSon)
                        outputStack = new NonTerminalStack(v1.Stack.Top);
                }

                MapVertices(v1, outputStack);
                MapVertices(v2, outputStack);
                RecordSonDataOfInputGraphs(outputStack, 0);
                return true;
            }
            return false;
        }

        public NonTerminalStack InnerExecute(InputNonTerminalStack v1, InputNonTerminalStack v2)
        {
            if (!v1.Stack.Top.Equals(v2.Stack.Top))
                throw new Exception($"tops should equal! {v1.Stack.Top} != {v2.Stack.Top}");

            var outputStack = GetOrCreateOutputStack(v1, v2);

            if (CheckForFinalLeaves(v1, v2, outputStack)) return outputStack;

            NonTerminalStack res;
            if (CheckForInternalLeaves(v1, v2, outputStack, out res)) return res;

            var iterator1 = v1.Stack.PrefixList.GetEnumerator();
            var iterator2 = v2.Stack.PrefixList.GetEnumerator();

            var moveNext1 = iterator1.MoveNext();
            var moveNext2 = iterator2.MoveNext();

            //if MoveNext() passes the end of the list, it returns false
            while (moveNext1 && moveNext2)
            {
                InputNonTerminalStack vcurrent1 = new InputNonTerminalStack(iterator1.Current, 1);
                InputNonTerminalStack vcurrent2 = new InputNonTerminalStack(iterator2.Current, 2);

                if (Mappings.ContainsKey(vcurrent1) && Mappings.ContainsKey(vcurrent2))
                {
                    var intersection = Mappings[vcurrent1].Intersect(Mappings[vcurrent2]);
                    if (intersection.Any())
                    {
                        if (outputStack.PrefixList == null)
                            outputStack.PrefixList = new List<NonTerminalStack>();
                        outputStack.PrefixList.Add(intersection.Last());

                        moveNext1 = iterator1.MoveNext();
                        moveNext2 = iterator2.MoveNext();
                        continue;
                    }
                }

                NonTerminalStack son;
                var compare = string.CompareOrdinal(vcurrent1.Stack.Top, vcurrent2.Stack.Top);
                if (compare > 0)
                {
                    son = AddSubtree(vcurrent2, 2);
                    if (son != null)
                        RecordSonDataOfInputGraphs(outputStack, 2);
                    moveNext2 = iterator2.MoveNext();
                }
                else if (compare < 0)
                {
                    son = AddSubtree(vcurrent1, 1);
                    if (son != null)
                        RecordSonDataOfInputGraphs(outputStack, 1);
                    moveNext1 = iterator1.MoveNext();
                }
                else //equal - call recursively.
                {
                    son = InnerExecute(vcurrent1, vcurrent2);
                    if (son != null)
                    {
                        RecordSonDataOfInputGraphs(outputStack, 3);

                        MapVertices(vcurrent1, son);
                        MapVertices(vcurrent2, son);
                    }

                    moveNext1 = iterator1.MoveNext();
                    moveNext2 = iterator2.MoveNext();
                }

                if (son != null)
                {
                    if (outputStack.PrefixList == null)
                        outputStack.PrefixList = new List<NonTerminalStack>();
                    if (!outputStack.PrefixList.Contains(son))
                        outputStack.PrefixList.Add(son);
                }

            }

            //if there is no common son to both stacks, there is no intersection down the subtrees.
            //in union, it is guarantted that there is a son (having reached this point, traversing son lists above)
            if (outputStack.PrefixList == null) return null;

            TraverseRemainingList(moveNext1, iterator1, outputStack, 1);
            TraverseRemainingList(moveNext2, iterator2, outputStack, 2);
            return outputStack;
        }

        private NonTerminalStack GetOrCreateOutputStack(InputNonTerminalStack v1, InputNonTerminalStack v2)
        {
            NonTerminalStack outputStack = null;

            if (Mappings.ContainsKey(v1))
                outputStack = Mappings[v1].Last();
            if (Mappings.ContainsKey(v2))
            {
                if (outputStack != null)
                    throw new Exception("both vertices were already visited");

                outputStack = Mappings[v2].Last();
            }

            if (outputStack == null)
                outputStack = new NonTerminalStack(v1.Stack.Top);
            return outputStack;
        }

        public class SonDataFromInputGraphs
        {
            public SonDataFromInputGraphs(bool Graph1HasSon, bool Graph2HasSon)
            {
                VertexFromGraph1HasSon = Graph1HasSon;
                VertexFromGraph2HasSon = Graph2HasSon;
            }

            public bool VertexFromGraph1HasSon { get; set; }
            public bool VertexFromGraph2HasSon { get; set; }
        }
    }

    public class StackGraphMerge : StackGraphAlgoBase
    {
        public override NonTerminalStack Execute(NonTerminalStack arg1, NonTerminalStack arg2)
        {
            var stackToMerge = arg1;
            var otherStackToMerge = arg2;
            if (otherStackToMerge == null) return stackToMerge;
            if (stackToMerge == null) return otherStackToMerge;

            if (arg1.Top != arg2.Top)
            {
                if (arg1.Top != Grammar.Epsilon)
                {
                    stackToMerge = new NonTerminalStack(Grammar.Epsilon, arg1);
                    //stackToMerge.Weight = arg1.Weight;
                }
                if (arg2.Top != Grammar.Epsilon)
                {
                    otherStackToMerge = new NonTerminalStack(Grammar.Epsilon, arg2);
                    //otherStackToMerge.Weight = arg2.Weight;
                }
            }

            InputNonTerminalStack v1 = new InputNonTerminalStack(stackToMerge, 1);
            InputNonTerminalStack v2 = new InputNonTerminalStack(otherStackToMerge, 2);
            return InnerExecute(v1, v2);
        }

        protected override bool CheckForInternalLeaves(InputNonTerminalStack stack1, InputNonTerminalStack stack2,
            NonTerminalStack outputStack, out NonTerminalStack res)
        {
            var retVal = false;
            res = null;

            if (stack1.Stack.IsInternalLeaf || stack2.Stack.IsInternalLeaf)
                outputStack.IsInternalLeaf = true;

            if (stack1.Stack.PrefixList == null && stack2.Stack.PrefixList != null)
            {
                RecordSonDataOfInputGraphs(outputStack, 2);

                if (stack1.Stack.IsInternalLeaf)
                    throw new Exception("3: internal leaf, but prefix list is null; contradiction");
                res = AddSubtree(stack2, 2);
                res.IsInternalLeaf = true;
                retVal = true;
            }
            if (stack2.Stack.PrefixList == null && stack1.Stack.PrefixList != null)
            {
                RecordSonDataOfInputGraphs(outputStack, 1);

                if (stack2.Stack.IsInternalLeaf)
                    throw new Exception("4: internal leaf, but prefix list is null; contradiction");
                res = AddSubtree(stack1, 1);
                res.IsInternalLeaf = true;
                retVal = true;
            }
            return retVal;
        }

        protected override void TraverseRemainingList(bool moveNext, List<NonTerminalStack>.Enumerator iterator,
            NonTerminalStack outputStack, int inputGraphNumber)
        {
            var bRecorded = false;

            //map subtrees left after simulatenous traversal of the two lists ends (with the shorter list).
            while (moveNext)
            {
                var current = iterator.Current;
                InputNonTerminalStack vcurrent = new InputNonTerminalStack(current, inputGraphNumber);
                var son = AddSubtree(vcurrent, inputGraphNumber);
                if (outputStack.PrefixList == null)
                    outputStack.PrefixList = new List<NonTerminalStack>();
                outputStack.PrefixList.Add(son);
                if (!bRecorded)
                {
                    RecordSonDataOfInputGraphs(outputStack, inputGraphNumber);
                    bRecorded = true;
                }
                moveNext = iterator.MoveNext();
            }
        }

        protected override NonTerminalStack AddSubtree(InputNonTerminalStack inputStack, int inputGraphNumber)
        {
            if (Mappings.ContainsKey(inputStack))
            {
                if (inputGraphNumber == 1 &&
                    SonsOfInputVertices.ContainsKey(Mappings[inputStack].Last()) &&
                    !SonsOfInputVertices[Mappings[inputStack].Last()].VertexFromGraph2HasSon)
                    return Mappings[inputStack].Last();

                if (inputGraphNumber == 2 &&
                    SonsOfInputVertices.ContainsKey(Mappings[inputStack].Last()) &&
                    !SonsOfInputVertices[Mappings[inputStack].Last()].VertexFromGraph1HasSon)
                    return Mappings[inputStack].Last();
            }

            var newStack = new NonTerminalStack(inputStack.Stack.Top);

            if (inputStack.Stack.PrefixList != null)
            {
                foreach (var vertex in inputStack.Stack.PrefixList)
                {
                    var vv = new InputNonTerminalStack(vertex, inputGraphNumber);
                    var subtree = AddSubtree(vv, inputGraphNumber);

                    if (newStack.PrefixList == null)
                        newStack.PrefixList = new List<NonTerminalStack>();
                    newStack.PrefixList.Add(subtree);
                }

                RecordSonDataOfInputGraphs(newStack, inputGraphNumber);
            }
            else
                RecordSonDataOfInputGraphs(newStack, 0);

            MapVertices(inputStack, newStack);
            return newStack;
        }
    }

    public class StackGraphIntersect : StackGraphAlgoBase
    {
        public override NonTerminalStack Execute(NonTerminalStack arg1, NonTerminalStack arg2)
        {
            var stackToIntersect = arg1;
            var otherStackToIntersect = arg2;

            if (arg1 == null || arg2 == null) return null;

            if (arg1.Top != arg2.Top)
            {
                if (arg1.Top != Grammar.Epsilon)
                {
                    stackToIntersect = new NonTerminalStack(Grammar.Epsilon, arg1);
                    //stackToMerge.Weight = arg1.Weight;
                }
                if (arg2.Top != Grammar.Epsilon)
                {
                    otherStackToIntersect = new NonTerminalStack(Grammar.Epsilon, arg2);
                    //otherStackToMerge.Weight = arg2.Weight;
                }
            }

            InputNonTerminalStack v1 = new InputNonTerminalStack(stackToIntersect, 1);
            InputNonTerminalStack v2 = new InputNonTerminalStack(otherStackToIntersect, 2);
            return InnerExecute(v1, v2);
        }

        protected override bool CheckForInternalLeaves(InputNonTerminalStack stack1, InputNonTerminalStack stack2,
            NonTerminalStack outputStack, out NonTerminalStack res)
        {
            var retVal = false;
            res = null;

            if (stack1.Stack.IsInternalLeaf && stack2.Stack.IsInternalLeaf)
                outputStack.IsInternalLeaf = true;

            //one stack is leaf, the other is not => empty intersection.
            if (stack1.Stack.PrefixList == null && stack2.Stack.PrefixList != null)
            {
                RecordSonDataOfInputGraphs(outputStack, 2);

                if (stack1.Stack.IsInternalLeaf)
                    throw new Exception("1: internal leaf, but prefix list is null; contradiction");
                res = stack2.Stack.IsInternalLeaf ? outputStack : null;
                retVal = true;
            }
            else if (stack2.Stack.PrefixList == null && stack1.Stack.PrefixList != null)
            {
                RecordSonDataOfInputGraphs(outputStack, 1);

                if (stack2.Stack.IsInternalLeaf)
                    throw new Exception("2:internal leaf, but prefix list is null; contradiction");
                res = stack1.Stack.IsInternalLeaf ? outputStack : null;
                retVal = true;
            }
            return retVal;
        }

        protected override void TraverseRemainingList(bool moveNext, List<NonTerminalStack>.Enumerator iterator,
            NonTerminalStack outputStack, int inputGraphNumber)
        {
        }

        protected override NonTerminalStack AddSubtree(InputNonTerminalStack inputStack, int inputGraphNumber)
        {
            return null;
        }
    }
}
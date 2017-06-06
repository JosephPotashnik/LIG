using System;
using System.Collections.Generic;
using System.Linq;

namespace LIGParser
{
    public class NonTerminalStack
    {
        // uncomment for debugging purposes (commented to maximize efficiency)
        //public static int IDCounter;
        //public int ID { get; set; }
        //public int Weight { get; set; }
        //private void GetID()
        //{
        //    ID = IDCounter;
        //    IDCounter += 1;
        //}

        public NonTerminalStack(string term, NonTerminalStack prefixStack)
        {
            Top = term;
            if (prefixStack != null)
            {
                PrefixList = new List<NonTerminalStack>();

                if (prefixStack.Top == Grammar.Epsilon)
                {
                    PrefixList = prefixStack.PrefixList;
                }
                else
                {
                    PrefixList.Add(prefixStack);
                }
            }
            //GetID();
        }

        public NonTerminalStack(string term)
        {
            Top = term;
            //GetID();
        }

        //shallow copy.
        public NonTerminalStack(NonTerminalStack otherStack)
        {
            if (otherStack == null) return;

            Top = otherStack.Top;
            //Weight = otherStack.Weight;
            if (otherStack.PrefixList != null)
                PrefixList = otherStack.PrefixList; //shallow copy.
            //GetID();
        }

        // ReSharper disable once UnusedParameter.Local
        //public NonTerminalStack(NonTerminalStack otherStack, bool deepCopy)
        //{
        //    if (otherStack == null) return;

        //    Top = otherStack.Top;
        //    //Weight = otherStack.Weight;
        //    if (otherStack.PrefixList != null)
        //    {
        //        PrefixList = new List<NonTerminalStack>(otherStack.PrefixList.Count);
        //        PrefixList = otherStack.PrefixList.Select(x => new NonTerminalStack(x, deepCopy)).ToList();
        //    }
        //}


        public List<NonTerminalStack> PrefixList { get; set; }
        public string Top { get; set; }
        public bool IsInternalLeaf { get; set; }

        //StackEquality here is used to identify the exact same structure - used
        //only in specific EqualityComparer (NonTerminalObjectStackComparer).
        //Equals is not implemented - the regular behavior of NonTerminalStack is ByReference.
        public static bool StackEquality(NonTerminalStack x, NonTerminalStack y)
        {
            if (x.Top != y.Top) return false;

            if (x.PrefixList != null && y.PrefixList != null)
            {
                if (x.PrefixList.Count != y.PrefixList.Count) return false;
                for (var i = 0; i < x.PrefixList.Count; i++)
                    if (!StackEquality(x.PrefixList[i], y.PrefixList[i])) return false;
                return true;
            }
            return x.PrefixList == null && y.PrefixList == null;
        }

        //if the top symbol is epsilon, then the actual top symbols are contained below it.
        //i.e for instance, Epsilon -> (NP, PP) 
        public List<string> GetListOfTopSymbols()
        {
            var l = new List<string>();
            if (Top != Grammar.Epsilon)
                l.Add(Top);
            else
                l = PrefixList.Select(x => x.Top).ToList();
            return l;
        }

        public NonTerminalStack GetPrefixListStackObjectOfGivenTop(string top)
        {
            if (top != Top && Top != Grammar.Epsilon)
                throw new Exception(
                    string.Format("popping {0} from stack graph headed by {1}, should be headed by EPSILON", top, Top));

            var newPrefixList = PrefixList;
            if (Top == Grammar.Epsilon)
            {
                if (PrefixList != null)
                    newPrefixList = PrefixList.Where(x => x.Top == top).ToList();
            }
            if (PrefixList == null)
                return null;

            if (newPrefixList.Count == 1)
                return newPrefixList.First();

            var newStack = new NonTerminalStack(Grammar.Epsilon);
            newStack.PrefixList = newPrefixList;


            return newStack;
        }

        public int Depth()
        {
            if (PrefixList == null)
                return 1;
            else
                return 1 + PrefixList[0].Depth();
        }
        //warning: this function potentially revisited shared leaves.
        //improve it by implementing DFS.
        private void RootToLeaf(List<List<string>> allPaths, List<string> a)
        {
            a.Add(Top);
            if (PrefixList != null)
            {
                foreach (var item in PrefixList)
                    item.RootToLeaf(allPaths, new List<string>(a));
            }
            else
                allPaths.Add(a);
        }

        public override string ToString()
        {
            var allPaths = new List<List<string>>();
            RootToLeaf(allPaths, new List<string>());
            var stacks = allPaths.Select(x => string.Join(" ", x));
            return "{" + string.Join(",", stacks) + "}";
        }

        public string Peek()
        {
            return Top;
        }

        public NonTerminalStack Push(string term)
        {
            if (Top == Grammar.Epsilon)
            {
                Top = term;
                return this;
            }
            return new NonTerminalStack(term, this);
        }
    }
}
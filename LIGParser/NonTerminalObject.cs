using System.Collections.Generic;

namespace LIGParser
{
    public class NonTerminalObjectStackComparer : IEqualityComparer<NonTerminalObject>
    {
        public bool Equals(NonTerminalObject x, NonTerminalObject y)
        {
            var b = x.NonTerminal == y.NonTerminal;
            if (!b) return false;

            if (x.Stack == null && y.Stack == null) return true;
            if (x.Stack != null && y.Stack != null)
                return NonTerminalStack.StackEquality(x.Stack, y.Stack);
            return false; //one of the stack is null and the other is not.
        }

        public int GetHashCode(NonTerminalObject obj)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + obj.NonTerminal.GetHashCode();
                if (obj.Stack != null)
                {
                    hash = hash * 23 + obj.Stack.Top.GetHashCode();
                    if (obj.Stack.PrefixList != null)
                    {
                        foreach (var item in obj.Stack.PrefixList)
                            hash = hash * 23 + item.GetHashCode();
                    }
                }
                return hash;
            }
        }
    }

    public class NonTerminalObject
    {
        public NonTerminalObject(string nonTerminal)
        {
            NonTerminal = nonTerminal;
        }

        public NonTerminalObject(NonTerminalObject otherNonTerminalObject)
        {
            NonTerminal = otherNonTerminalObject.NonTerminal;
            if (otherNonTerminalObject.Stack != null)
                Stack = otherNonTerminalObject.Stack; //shallow copy
        }

        //the nonterminal associated with the object i.e, A in A[X]
        public string NonTerminal { get; set; }
        //the Stack graph associated with the object, i.e. X in A[X]
        public NonTerminalStack Stack { get; set; }


        public override string ToString()
        {
            if (Stack != null)
                return string.Format("{0}{1}", NonTerminal, Stack.ToString());
            return string.Format("{0}", NonTerminal);
        }


        public override bool Equals(object obj)
        {
            var p = obj as NonTerminalObject;
            if (p == null)
                return false;
            var b = NonTerminal == p.NonTerminal;
            if (!b) return false;

            if (Stack == null && p.Stack == null) return true;
            if (Stack != null && p.Stack != null)
                return Stack.Equals(p.Stack);
            //NonTerminalStack does not implement Equals so equality is by reference here.
            return false; //one of the stack is null and the other is not.
        }

        public bool IsStackEmpty()
        {
            return Stack == null;
        }

        public bool IsStackEmptyOrDot()
        {
            return IsStackEmpty() || Stack.Peek() == ".";
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + NonTerminal.GetHashCode();
                if (Stack != null)
                    hash = hash * 23 + Stack.GetHashCode();
                return hash;
            }
        }
    }
}
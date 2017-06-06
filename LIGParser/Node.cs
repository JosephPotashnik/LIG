using System;
using System.Collections.Generic;

namespace LIGParser
{
    public class Node
    {
        private const int ScanRuleNumber = 0;
        public static Grammar grammar = null;

        public Node()
        {
        }

        public Node(string nodeName, int startIndex, int endIndex)
        {
            Name = nodeName;
            StartIndex = startIndex;
            EndIndex = endIndex;
            RuleNumber = ScanRuleNumber;
            LogProbability = 0.0f;
            Bits = 1;
        }

        public double LogProbability { get; set; }
        public int Bits { get; set; }
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public List<Node> Children { get; set; }
        public int RuleNumber { get; set; }
        public string AssociatedTerminal { get; set; }

        public override bool Equals(object obj)
        {
            var n = obj as Node;
            if (n == null)
                return false;

            return (Name == n.Name) && (StartIndex == n.StartIndex) && (EndIndex == n.EndIndex);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Name.GetHashCode();
                hash = hash * 23 + StartIndex;
                hash = hash * 23 + EndIndex;
                return hash;
            }
        }

        public bool HasChildren()
        {
            return Children != null;
        }

        public void AddChildren(Node v, Node w = null)
        {
            if (Children == null)
                Children = new List<Node>();
            Children.Add(v);
            if (w != null)
            {
                Children.Insert(0, w);
            }
        }

        public override string ToString()
        {
            return $"{AssociatedTerminal ?? ""} {Name} [{StartIndex}-{EndIndex}] [p:{LogProbability}] -{RuleNumber}-";
        }

        public void Print(int level = 0)
        {
            Console.WriteLine(ToString().PadLeft(level * 4, '_'));
            if (Children == null) return;
            foreach (var child in Children)
                child.Print(level + 1);
        }

        public string GetTerminalStringUnderNode()
        {
            var leaves = new List<string>();
            GetTerminalStringUnderNode(leaves);
            return string.Join(" ", leaves);
        }


        private void GetTerminalStringUnderNode(List<string> leavesList)
        {
            if (Children == null)
            {
                if (Name != null && AssociatedTerminal != null)
                    leavesList.Add(AssociatedTerminal);
            }
            else
            {
                foreach (var child in Children)
                    child.GetTerminalStringUnderNode(leavesList);
            }
        }
    }
}
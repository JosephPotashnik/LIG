using System.Linq;

namespace LIGParser
{
    internal class InverseKeyType
    {
        public InverseKeyType(NonTerminalObject[] p, int h, bool isStart)
        {
            Production = p.Select(nonterminal => new NonTerminalObject(nonterminal)).ToArray();
            HeadPosition = h;
            IsStartSymbol = isStart;
        }

        public InverseKeyType(InverseKeyType anotherInverseKey)
        {
            Production =
                anotherInverseKey.Production.Select(nonterminal => new NonTerminalObject(nonterminal)).ToArray();
            HeadPosition = anotherInverseKey.HeadPosition;
            IsStartSymbol = anotherInverseKey.IsStartSymbol;
        }

        public int HeadPosition { get; set; }
        public bool IsStartSymbol { get; set; }
        public NonTerminalObject[] Production { get; set; }

        public override bool Equals(object obj)
        {
            var p = obj as InverseKeyType;
            if (p == null)
                return false;

            return Production.SequenceEqual(p.Production) && (IsStartSymbol == p.IsStartSymbol) &&
                   (HeadPosition == p.HeadPosition);
        }

        public override string ToString()
        {
            var p = Production.Select(x => x.ToString()).ToArray();

            return string.Format("{0} {1} {2}", string.Join(" ", p), HeadPosition, IsStartSymbol);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + IsStartSymbol.GetHashCode();
                hash = hash * 23 + HeadPosition.GetHashCode();
                for (var i = 0; i < Production.Length; i++)
                    hash = hash * 23 + Production[i].GetHashCode();
                return hash;
            }
        }
    }
}
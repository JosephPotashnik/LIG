using System;
using System.Linq;
using Newtonsoft.Json;

namespace LIGParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Rule
    {
        private readonly Random rs = new Random();

        public Rule()
        {
        }

        public Rule(int occurrences, string name, string[] prod, int headPos = 0, int compPos = 1, int num = -1)
        {
            Name = new NonTerminalObject(name);
            if (prod != null)
                Production = prod.Select(nonterminal => new NonTerminalObject(nonterminal)).ToArray();
            HeadPosition = headPos;
            ComplementPosition = compPos;
            Number = num;
            Occurrences = occurrences;
        }

        public Rule(Rule otherRule)
        {
            Name = new NonTerminalObject(otherRule.Name);
            Production = otherRule.Production.Select(nonterminal => new NonTerminalObject(nonterminal)).ToArray();
            HeadPosition = otherRule.HeadPosition;
            ComplementPosition = otherRule.ComplementPosition;
            Number = otherRule.Number;
            Occurrences = otherRule.Occurrences;
        }

        [JsonProperty]
        public int Occurrences { get; set; }

        [JsonProperty]
        public NonTerminalObject Name { get; set; }

        [JsonProperty]
        public NonTerminalObject[] Production { get; set; }

        [JsonProperty]
        public int HeadPosition { get; set; }

        [JsonProperty]
        public int ComplementPosition { get; set; }

        public int Number { get; set; }

        public string HeadTerm => Production[HeadPosition].NonTerminal;

        public string NonHeadTerm => Production[(HeadPosition + 1) % Production.Length].NonTerminal;

        public string ComplementTerm => Production[ComplementPosition].NonTerminal;

        public string NonComplementTerm => Production[(ComplementPosition + 1) % Production.Length].NonTerminal;

        public override string ToString()
        {
            var p = Production.Select(x => x.ToString()).ToArray();
            return $"{Number}.{Name}->{string.Join(" ", p)} {HeadPosition}{ComplementPosition} {Occurrences}";
        }

        public override bool Equals(object obj)
        {
            var p = obj as Rule;
            if (p == null)
                return false;

            return Number == p.Number;

            //even if the rules have the same rule number, their dynamic stacks might differ at a given time.
            //in such a case, they're unequal. the logic is treated in Column.Add().
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return Number;
        }

        public bool IsInitialRule()
        {
            return Name.IsStackEmpty();
        }

        public bool IsInitialOrDotStack() //return true if rule has no stack or the stack contains only ["."]
        {
            return Name.IsStackEmpty() || Name.Stack.Peek() == ".";
        }

        public bool IsEpsilonRule()
        {
            return Production[0].NonTerminal == Grammar.Epsilon;
        }

        public string GetRandomDaughther()
        {
            return Production[rs.Next(Production.Length)].NonTerminal;
        }
    }
}
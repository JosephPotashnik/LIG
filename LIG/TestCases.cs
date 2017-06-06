using LIGParser;

namespace LIG
{
    public class TestCases
    {
        public static void CreatePalindromeGrammar(Grammar grammar)
        {
            grammar.AddAllPOSTypesToDictionary(new[] { "A", "B" });

            grammar.AddRule(new Rule(2, "START", new[] { "A", "A" }, 0, 1));
            grammar.AddRule(new Rule(2, "START", new[] { "B", "B" }, 0, 1));

            grammar.AddRule(new Rule(2, "START", new[] { "AP", "A" }, 1, 1));
            grammar.AddRule(new Rule(2, "START", new[] { "BP", "B" }, 1, 1));

            grammar.AddRule(new Rule(1, "AP", new[] { "A", "START" }, 0, 0));
            grammar.AddRule(new Rule(1, "BP", new[] { "B", "START" }, 0, 0));
        }

        private static void TestAmbiguityGrammar()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);


            grammar.AddRule(new Rule(0, "START", new[] { "VP" }, 0, 0));
            grammar.AddRule(new Rule(1, "VP", new[] { "V1", "NP" }, 0, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "VP", "NP" }, 0, 0));
            grammar.AddRule(new Rule(0, "NP", new[] { "NP", "NP" }, 0, 0));


            var parser = new Parser(grammar, true);
            var n = parser.ParseSentence("kissed John David");
            n.Print();
        }


        private static void SimpleMovementTest()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);

            grammar.AddProjectionTypeToTypeDictionary("CP", "V"); // no overt POS here, head of IP is V.
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.
            grammar.AddRule(new Rule(1, "START", new[] { "CP" }, 0, 0));
            grammar.AddRule(new Rule(1, "CP", new[] { "IP" }, 0, 0));
            grammar.AddRule(new Rule(1, "IP", new[] { "NP", "VP" }, 1, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "V1", "NP" }, 0, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "V2", "PP" }, 0, 1));
            grammar.AddRule(new Rule(1, "PP", new[] { "P", "NP" }, 0, 1));
            grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddNonTerminalToLandingSites("CP");
            grammar.AddMoveable("NP");
            grammar.AddMoveable("PP");
            grammar.GenerateDerivedRulesFromSchema();

            var parser = new Parser(grammar);
            var n = parser.ParseSentence("Who the bells toll for");
            n.Print();
            n = parser.ParseSentence("Who a man arrived from");
            n.Print();
            n = parser.ParseSentence("from Who a man arrived");
            n.Print();
            n = parser.ParseSentence("John David kissed");
            n.Print();
        }


        private static void ComplexMovementTest()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);

            grammar.AddProjectionTypeToTypeDictionary("CP", "N"); // no overt POS here, head of IP is V.
            grammar.AddProjectionTypeToTypeDictionary("IP", "N"); // no overt POS here, head of IP is V.
            grammar.AddRule(new Rule(1, "START", new[] { "CP" }, 0, 0));
            grammar.AddRule(new Rule(1, "START", new[] { "IP" }, 0, 0));

            //grammar.AddRule(new Rule(1, "CP", new[] { "IP" }, 0, 0));
            //grammar.AddRule(new Rule(1, "IP", new[] { "NP" }, 0, 0));
            //grammar.AddRule(new Rule(1, "VP", new[] { "V1", "NP" }, 0, 1));
            //grammar.AddRule(new Rule(1, "VP", new[] { "V2", "PP" }, 0, 1));
            //grammar.AddRule(new Rule(1, "PP", new[] { "P", "NP" }, 0, 1));
            //grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddNonTerminalToLandingSites("CP");
            grammar.AddNonTerminalToLandingSites("IP");
            grammar.AddMoveable("NP");
            grammar.AddMoveable("PP");
            grammar.GenerateDerivedRulesFromSchema();

            var parser = new Parser(grammar, true);
            var n = parser.ParseSentence("Who Who Who");
            n.Print();
        }

        private static void TestMovement2()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);

            grammar.AddProjectionTypeToTypeDictionary("CP", "V"); // no overt POS here, head of IP is V.
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.
            grammar.AddRule(new Rule(1, "START", new[] { "VP", "VP" }, 1, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "VP", "PP" }, 0, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "NP", "V1" }, 1, 0));
            grammar.AddRule(new Rule(1, "PP", new[] { "V2", "P" }, 1, 0));
            grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 0));


            var parser = new Parser(grammar, true);
            var n = parser.ParseSentence("the man arrived to Mary"); //supposed to fail in parsing!!
            n.Print();
        }

        private static void TestMovement3()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);

            grammar.AddProjectionTypeToTypeDictionary("CP", "V"); // no overt POS here, head of IP is V.
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.
            grammar.AddRule(new Rule(1, "START", new[] { "VP", "NP" }, 1, 0));
            grammar.AddRule(new Rule(1, "VP", new[] { "NP", "VP" }, 1, 0));
            grammar.AddRule(new Rule(1, "NP", new[] { "NP", "CP" }, 0, 1));
            grammar.AddRule(new Rule(1, "CP", new[] { "C", "NP" }, 0, 0));
            grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddRule(new Rule(1, "VP", new[] { "V1" }, 0, 0));


            var parser = new Parser(grammar, true);
            var n = parser.ParseSentence("the man that Mary loved"); //supposed to fail in parsing!!
            n.Print();
        }


        private static Grammar InfiniteMovementGrammar1()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"..\..\..\Input\Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);
            grammar.AddRule(new Rule(1, "START", new[] { "NP" }, 0, 0));
            grammar.AddRule(new Rule(1, "NP", new[] { "PP", "NP" }, 1, 0));
            grammar.AddRule(new Rule(1, "PP", new[] { "NP", "PP" }, 1, 0));
            grammar.AddRule(new Rule(1, "PP", new[] { "V2", "P" }, 1, 0));
            var does = grammar.DoesGrammarAllowInfiniteMovement();
            return grammar;
        }

        private static Grammar InfiniteMovementGrammar2()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"..\..\..\Input\Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);
            grammar.AddProjectionTypeToTypeDictionary("CP", "V"); // no overt POS here, head of IP is V.
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.

            grammar.AddRule(new Rule(1, "START", new[] { "CP" }, 0, 0));
            grammar.AddRule(new Rule(1, "CP", new[] { "NP", "IP" }, 1, 1));
            grammar.AddRule(new Rule(1, "IP", new[] { "NP", "CP" }, 1, 1));
            grammar.AddRule(new Rule(1, "PP", new[] { "V2", "P" }, 1, 0));
            grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 0));
            var does = grammar.DoesGrammarAllowInfiniteMovement();
            return grammar;
        }

        private static Grammar InfiniteMovementGrammar3()
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"..\..\..\Input\Vocabulary.json");
            var grammar = new Grammar(voc);
            Program.AddTypesToGrammar(grammar);
            grammar.AddProjectionTypeToTypeDictionary("CP", "V"); // no overt POS here, head of IP is V.
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.
            grammar.AddRule(new Rule(1, "NP", new[] { "V0P", "NP" }, 1, 0));

            grammar.AddRule(new Rule(1, "V0P", new[] { "V2" }, 0, 0));
            grammar.AddRule(new Rule(1, "START", new[] { "NP", "V0" }, 1, 1));
            grammar.AddRule(new Rule(1, "START", new[] { "V0", "NP" }, 0, 1));
            grammar.AddRule(new Rule(1, "START", new[] { "PP", "NP" }, 0, 1));
            grammar.AddRule(new Rule(1, "START", new[] { "NP", "NP" }, 1, 1));
            grammar.AddRule(new Rule(1, "V0P", new[] { "V1" }, 0, 0));
            grammar.AddRule(new Rule(1, "PP", new[] { "V0P", "P" }, 1, 1));

            grammar.AddRule(new Rule(1, "V0P", new[] { "NP", "V0P" }, 1, 1));
            grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 1));
            var does = grammar.DoesGrammarAllowInfiniteMovement();
            return grammar;
        }

        public static void CreateMovementGrammarCompetitor(Grammar grammar)
        {
            Program.AddTypesToGrammar(grammar);

            grammar.AddRule(new Rule(1, "START", new[] { "NP", "VP" }, 0, 0));
            grammar.AddRule(new Rule(1, "START", new[] { "C", "START" }, 0, 1));

            grammar.AddRule(new Rule(0, "CP", new[] { "NP", "C" }, 1, 0));
            grammar.AddRule(new Rule(0, "CP", new[] { "PP", "C" }, 1, 0));

            grammar.AddRule(new Rule(0, "VP", new[] { "V1", "NP" }, 0, 1));
            grammar.AddRule(new Rule(0, "VP", new[] { "V2", "PP" }, 0, 1));
            grammar.AddRule(new Rule(0, "VP", new[] { "V2" }, 0, 0));
            grammar.AddRule(new Rule(0, "VP", new[] { "V1" }, 0, 0));
            grammar.AddRule(new Rule(0, "VP", new[] { "V2", "P" }, 0, 1));
            grammar.AddRule(new Rule(0, "VP", new[] { "V3", "START" }, 0, 1));

            grammar.AddRule(new Rule(0, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddRule(new Rule(0, "NP", new[] { "CP", "NP" }, 1, 1));


            grammar.AddRule(new Rule(0, "PP", new[] { "P", "NP" }, 0, 1));
        }

        public static void CreateMovementGrammarCompetitor_old(Grammar grammar)
        {
            Program.AddTypesToGrammar(grammar);

            grammar.AddRule(new Rule(1, "START", new[] { "NP", "VP" }, 0, 0));
            grammar.AddRule(new Rule(1, "START", new[] { "P", "START" }, 0, 0));

            grammar.AddRule(new Rule(0, "CP", new[] { "NP", "C" }, 1, 1)); //1.58496250072116
            grammar.AddRule(new Rule(0, "CP", new[] { "VP", "C" }, 1, 0));
            grammar.AddRule(new Rule(0, "CP", new[] { "C" }, 0, 0));


            grammar.AddRule(new Rule(0, "VP", new[] { "V1", "DP" }, 0, 1)); //2.80735493280681
            grammar.AddRule(new Rule(0, "VP", new[] { "V2", "P" }, 0, 0));
            grammar.AddRule(new Rule(0, "VP", new[] { "V2" }, 0, 0));
            grammar.AddRule(new Rule(0, "VP", new[] { "V1" }, 0, 0));

            grammar.AddRule(new Rule(0, "VP", new[] { "VP", "NP" }, 0, 1));
            grammar.AddRule(new Rule(0, "VP", new[] { "NP", "V3" }, 1, 0));
            grammar.AddRule(new Rule(0, "VP", new[] { "DP", "V3" }, 1, 1));


            grammar.AddRule(new Rule(0, "NP", new[] { "D", "N" }, 1, 0)); //1
            grammar.AddRule(new Rule(0, "NP", new[] { "CP", "NP" }, 1, 1));
            grammar.AddRule(new Rule(0, "DP", new[] { "D", "N" }, 0, 1)); //0
        }

        public void TestComplexMovement2()
        {
            //var grammarCompete = new Grammar(voc);
            //CreateMovementGrammarCompetitor(grammarCompete);

            //Parser p1 = new Parser(grammar);
            //Parser p2 = new Parser(grammarCompete);

            //Node n1 = p1.ParseSentence("John that John went to");
            //Node n2 = p2.ParseSentence("John that John went to");


            //var len1 = lengthOfTree(grammar, n1);
            //var len2 = lengthOfTree(grammarCompete, n2);

            /*
            List<string[]> datum = new List<string[]>();


            datum.Add(Enumerable.Repeat("the man kissed the man", 10).ToArray()); //regular transitive
            datum.Add(Enumerable.Repeat("John kissed John", 10).ToArray());
            datum.Add(Enumerable.Repeat("the man kissed John", 10).ToArray());
            datum.Add(Enumerable.Repeat("John kissed the man", 10).ToArray());
            datum.Add(Enumerable.Repeat("that the man kissed the man", 10).ToArray());
            datum.Add(Enumerable.Repeat("that John kissed John", 10).ToArray());
            datum.Add(Enumerable.Repeat("that the man kissed John", 10).ToArray());
            datum.Add(Enumerable.Repeat("that John kissed the man", 10).ToArray());

            datum.Add(Enumerable.Repeat("the man that the man kissed", 20).ToArray()); //NP Move
            datum.Add(Enumerable.Repeat("John that John kissed", 20).ToArray());
            datum.Add(Enumerable.Repeat("John that the man kissed", 20).ToArray());
            datum.Add(Enumerable.Repeat("the man that John kissed", 20).ToArray());

            datum.Add(Enumerable.Repeat("the man went to the man", 10).ToArray()); //regular PP
            datum.Add(Enumerable.Repeat("John went to John", 10).ToArray());
            datum.Add(Enumerable.Repeat("the man went to John", 10).ToArray());
            datum.Add(Enumerable.Repeat("John went to the man", 10).ToArray());
            datum.Add(Enumerable.Repeat("that the man went to the man", 10).ToArray());
            datum.Add(Enumerable.Repeat("that John went to John", 10).ToArray());
            datum.Add(Enumerable.Repeat("that the man went to John", 10).ToArray());
            datum.Add(Enumerable.Repeat("that John went to the man", 10).ToArray());

            datum.Add(Enumerable.Repeat("the man that the man went to", 20).ToArray()); //Preposition strading (NP move)
            datum.Add(Enumerable.Repeat("John that John went to", 20).ToArray());
            datum.Add(Enumerable.Repeat("John that the man went to", 20).ToArray());
            datum.Add(Enumerable.Repeat("the man that John went to", 20).ToArray());

            datum.Add(Enumerable.Repeat("to the man that the man went", 20).ToArray()); //PP move
            datum.Add(Enumerable.Repeat("to John that John went", 20).ToArray());
            datum.Add(Enumerable.Repeat("to John that the man went", 20).ToArray());
            datum.Add(Enumerable.Repeat("to the man that John went", 20).ToArray());


            datum.Add(Enumerable.Repeat("the man knows that the man kissed the man", 20).ToArray()); //regular embedded transitive
            datum.Add(Enumerable.Repeat("John knows that John kissed John", 20).ToArray());

            datum.Add(Enumerable.Repeat("John that the man knows that the man kissed", 20).ToArray()); //NP move inside embedded
            datum.Add(Enumerable.Repeat("the man that the man knows that the man kissed", 20).ToArray());

            datum.Add(Enumerable.Repeat("the man knows that the man went to the man", 20).ToArray()); //regular embedded PP
            datum.Add(Enumerable.Repeat("John knows that John went to John", 20).ToArray());

            datum.Add(Enumerable.Repeat("John that the man knows that the man went to", 20).ToArray()); //NP move inside embedded PP
            datum.Add(Enumerable.Repeat("the man that the man knows that the man went to", 20).ToArray());

            datum.Add(Enumerable.Repeat("to John that the man knows that the man went", 20).ToArray()); //PP move inside embedded PP
            datum.Add(Enumerable.Repeat("to the man that the man knows that the man went", 20).ToArray());


            var data = datum.SelectMany(i => i).ToArray();
            */
        }
    }
}
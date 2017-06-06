using System;
using System.Diagnostics;
using System.IO;
using LIGParser;
using LIGLearner;
using Newtonsoft.Json;

namespace LIG
{
    internal class Program
    {
        public static void StopWatch(Stopwatch stopWatch)
        {
            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            var s = "Overall session RunTime " + elapsedTime;
            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
        }

        public static Stopwatch StartWatch()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            return stopWatch;
        }

        public static void AddTypesToGrammar(Grammar grammar)
        {
            grammar.AddAllPOSTypesToDictionary(new[] { "D", "N", "V0", "V1", "V2", "V3", "P", "C", "V", "I", "L", "M" });
            grammar.NonTerminalsTypeDictionary["V0"] = "V";
            grammar.NonTerminalsTypeDictionary["V1"] = "V";
            grammar.NonTerminalsTypeDictionary["V2"] = "V";
            grammar.NonTerminalsTypeDictionary["V3"] = "V";
            grammar.NonTerminalsTypeDictionary["V4"] = "V";
            grammar.NonTerminalsTypeDictionary["V5"] = "V";

            grammar.NonTerminalsTypeDictionary["VP"] = "V";
            grammar.NonTerminalsTypeDictionary["V0P"] = "V";
            grammar.NonTerminalsTypeDictionary["V1P"] = "V";
            grammar.NonTerminalsTypeDictionary["V2P"] = "V";
            grammar.NonTerminalsTypeDictionary["V3P"] = "V";
            grammar.NonTerminalsTypeDictionary["V4P"] = "V";
            grammar.NonTerminalsTypeDictionary["V5P"] = "V";

        }


        public static void CreateSimpleGrammar(Grammar grammar)
        {
            AddTypesToGrammar(grammar);
            grammar.AddRule(new Rule(1, "START", new[] { "NP", "VP" }, 1, 1));

            grammar.AddRule(new Rule(1, "VP", new[] { "V0" }, 0, 0));
            grammar.AddRule(new Rule(1, "VP", new[] { "V1", "NP" }, 0, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "V2", "PP" }, 0, 1));
            grammar.AddRule(new Rule(1, "VP", new[] { "V3", "START" }, 0, 1));

            grammar.AddRule(new Rule(1, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddRule(new Rule(1, "PP", new[] { "P", "NP" }, 0, 1));
        }
        public static void CreateCanonicalMovementGrammar(Grammar grammar)
        {
            AddTypesToGrammar(grammar);
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.

            grammar.AddRule(new Rule(1, "START", new[] { "IP" }, 0, 0));
            grammar.AddRule(new Rule(1, "START", new[] { "CP" }, 0, 0));

            grammar.AddRule(new Rule(0, "CP", new[] { "C", "IP" }, 0, 1));
            grammar.AddRule(new Rule(0, "IP", new[] { "NP", "VP" }, 1, 1));

            grammar.AddRule(new Rule(0, "VP", new[] { "V1", "NP" }, 0, 1));
            grammar.AddRule(new Rule(0, "VP", new[] { "V2", "PP" }, 0, 1));
            grammar.AddRule(new Rule(2, "VP", new[] { "V3", "CP" }, 0, 1));



            grammar.AddRule(new Rule(0, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddRule(new Rule(0, "PP", new[] { "P", "NP" }, 0, 1));


            grammar.AddNonTerminalToLandingSites("CP");
            grammar.AddMoveable("NP");
            grammar.AddMoveable("PP");


        }

        public static void CreateMovementGrammar(Grammar grammar)
        {
            AddTypesToGrammar(grammar);
            grammar.AddProjectionTypeToTypeDictionary("IP", "V"); // no overt POS here, head of IP is V.

            grammar.AddRule(new Rule(1, "START", new[] { "IP" }, 0, 0));
            grammar.AddRule(new Rule(1, "START", new[] { "CP" }, 0, 0));

            grammar.AddRule(new Rule(0, "CP", new[] { "C", "IP" }, 0, 1));
            grammar.AddRule(new Rule(0, "IP", new[] { "NP", "VP" }, 1, 1));

            grammar.AddRule(new Rule(0, "VP", new[] { "V1", "NP" }, 0, 1));
            grammar.AddRule(new Rule(0, "VP", new[] { "V2", "PP" }, 0, 1));
            grammar.AddRule(new Rule(2, "VP", new[] { "V3", "CP" }, 0, 1));
            grammar.AddRule(new Rule(2, "VP", new[] { "V4", "LP" }, 0, 1));
            grammar.AddRule(new Rule(2, "VP", new[] { "V5", "MP" }, 0, 1));


            grammar.AddRule(new Rule(0, "NP", new[] { "D", "N" }, 1, 0));
            grammar.AddRule(new Rule(0, "PP", new[] { "P", "NP" }, 0, 1));
            grammar.AddRule(new Rule(0, "LP", new[] { "L", "PP" }, 0, 1));
            grammar.AddRule(new Rule(0, "MP", new[] { "M", "LP" }, 0, 1));

            grammar.AddNonTerminalToLandingSites("CP");
            grammar.AddMoveable("NP");
            grammar.AddMoveable("PP");
            grammar.AddMoveable("LP");
            grammar.AddMoveable("MP");

        }

        private static void Main(string[] args)
        {
            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var grammar = new Grammar(voc);

            ProgramParams programParams;
            using (var file = File.OpenText(@"ProgramParameters.json"))
            {
                var serializer = new JsonSerializer();
                programParams = (ProgramParams)serializer.Deserialize(file, typeof(ProgramParams));
            }


            if (programParams.DataWithMovement)
                CreateMovementGrammar(grammar);
            else
                CreateSimpleGrammar(grammar);
            grammar.GenerateDerivedRulesFromSchema();

            var p = new Parser(grammar);
            var data = p.GenerateSentences(programParams.NumberOfDataSentences);

            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine("-------------------");
                sw.WriteLine("Session {0} ", DateTime.Now.ToString("MM/dd/yyyy h:mm tt"));
                sw.WriteLine("sentences: {0}, runs: {1}, movement: {2}", programParams.NumberOfDataSentences,
                    programParams.NumberOfRuns, programParams.DataWithMovement);
            }

            var stopWatch = StartWatch();

            var learner = new Learner(voc, grammar.NonTerminalsTypeDictionary, grammar.POSTypes, data, grammar);

            learner.originalGrammar.GenerateDerivedRulesFromSchema();
            var targetGrammarEnergy = learner.Energy(learner.originalGrammar);
            learner.originalGrammar.GenerateInitialRulesFromDerivedRules();
            var s = string.Format("Target Hypothesis:\r\n{0} with energy: {1}\r\n", learner.originalGrammar,
                targetGrammarEnergy);

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }

            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                var sa = new SimulatedAnnealing(learner);
                sa.Run();
            }
            StopWatch(stopWatch);
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class ProgramParams
        {
            [JsonProperty]
            public int NumberOfDataSentences { get; set; }

            [JsonProperty]
            public bool DataWithMovement { get; set; }

            [JsonProperty]
            public int NumberOfRuns { get; set; }
        }
    }
}
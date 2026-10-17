using System;
using System.Collections.Generic;
using DeliveryQuoteEngine;

namespace DeliveryQuoteEngine.Tests
{
    public static class TestProgram
    {
        static int passed = 0, total = 0;

        static readonly string[] SampleData =
        {
            "R001|Alice|5.0|3.0",
            "R002|Bob|2.0|1.5",
            "R003|Carol|bad|2.0",
            "R004|Dave|10.0|5.0",
            "R005|Eve|0.5|0.0"
        };

        static void Check(string label, bool ok)
        {
            total++;
            if (ok) passed++;
            Console.WriteLine($"{(ok ? "Pass" : "Fail")} | {label}");
        }

        public static void Main()
        {
            // 1. We test the standard policy (Normal workflow)
            var standard = QuoteEngine.RunPipeline(SampleData, QuoteEngine.StandardPolicy);
            Check("Normal: 4 valid, 1 invalid, Standard total = 3750",
                standard.AcceptedCount == 4
                && standard.RejectedCount == 1
                && standard.TotalRevenue == 3750m);

            // 2. We test the express policy (Normal workflow)
            var express = QuoteEngine.RunPipeline(SampleData, QuoteEngine.ExpressPolicy);
            Check("Normal: Express total = 5825",
                express.AcceptedCount == 4 && express.TotalRevenue == 5825m);

            // 3. We test the empty dataset
            var empty = QuoteEngine.RunPipeline(Array.Empty<string>(), QuoteEngine.StandardPolicy);
            Check("Empty dataset: no quotes, zero revenue, no crash",
                empty.AcceptedCount == 0
                && empty.RejectedCount == 0
                && empty.TotalRevenue == 0m
                && empty.Quotes.Count == 0);

            // 4. We test boundary value (exact 2 km - we match the assignment's example)
            var boundaryStd = QuoteEngine.RunPipeline(new[] { "B|Test|2.0|1.0" }, QuoteEngine.StandardPolicy);
            var boundaryExp = QuoteEngine.RunPipeline(new[] { "B|Test|2.0|1.0" }, QuoteEngine.ExpressPolicy);
            Check("Boundary: 2 km -> Standard 700, Express 1100",
                boundaryStd.TotalRevenue == 700m && boundaryExp.TotalRevenue == 1100m);

            // 5. We reject distance below minimum
            var below = QuoteEngine.RunPipeline(new[] { "B|Test|0.49|1.0" }, QuoteEngine.StandardPolicy);
            Check("Below minimum: 0.49 km rejected",
                below.AcceptedCount == 0 && below.RejectedCount == 1);

            // 6. Invalid input - malformed, empty ID, empty name
            var invalid = QuoteEngine.RunPipeline(new[]
            {
                "R1|A|bad|1.0",
                "|B|2.0|1.0",
                "R3||2.0|1.0"
            }, QuoteEngine.StandardPolicy);
            Check("Invalid: all 3 records rejected",
                invalid.AcceptedCount == 0 && invalid.RejectedCount == 3);

            // 7. Two policies give different totals on the same dataset
            Check("Two policies differ on the same input",
                standard.TotalRevenue != express.TotalRevenue);

            // 8. Purity: input collection is unchanged after running the pipeline
            var input = new List<string>(SampleData);
            int originalCount = input.Count;
            string originalFirst = input[0];
            QuoteEngine.RunPipeline(input, QuoteEngine.StandardPolicy);
            Check("Purity: input collection unchanged",
                input.Count == originalCount && input[0] == originalFirst);

            Console.WriteLine();
            Console.WriteLine($"Passed: {passed}/{total}");
        }
    }
}
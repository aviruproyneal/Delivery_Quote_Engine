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
            
            var standard = QuoteEngine.RunPipeline(SampleData, QuoteEngine.StandardPolicy);
            Check("A5 Normal: 4 valid, 1 invalid, Standard total = 3750",
                standard.AcceptedCount == 4
                && standard.RejectedCount == 1
                && standard.TotalRevenue == 3750m);
            
            var express = QuoteEngine.RunPipeline(SampleData, QuoteEngine.ExpressPolicy);
            Check("A5 Normal: Express total = 5825",
                express.AcceptedCount == 4 && express.TotalRevenue == 5825m);
            
            var empty = QuoteEngine.RunPipeline(Array.Empty<string>(), QuoteEngine.StandardPolicy);
            Check("A5 Empty dataset: no quotes, zero revenue, no crash",
                empty.AcceptedCount == 0
                && empty.RejectedCount == 0
                && empty.TotalRevenue == 0m
                && empty.Quotes.Count == 0);
            
            var boundaryStd = QuoteEngine.RunPipeline(new[] { "B|Test|2.0|1.0" }, QuoteEngine.StandardPolicy);
            var boundaryExp = QuoteEngine.RunPipeline(new[] { "B|Test|2.0|1.0" }, QuoteEngine.ExpressPolicy);
            Check("A5 Boundary: 2 km -> Standard 700, Express 1100",
                boundaryStd.TotalRevenue == 700m && boundaryExp.TotalRevenue == 1100m);
            
            var below = QuoteEngine.RunPipeline(new[] { "B|Test|0.49|1.0" }, QuoteEngine.StandardPolicy);
            Check("A5 Below minimum: 0.49 km rejected",
                below.AcceptedCount == 0 && below.RejectedCount == 1);
            
            var invalid = QuoteEngine.RunPipeline(new[]
            {
                "R1|A|bad|1.0",
                "|B|2.0|1.0",
                "R3||2.0|1.0"
            }, QuoteEngine.StandardPolicy);
            Check("A5 Invalid: all 3 records rejected",
                invalid.AcceptedCount == 0 && invalid.RejectedCount == 3);
            
            Check("A5 Two policies differ on the same input",
                standard.TotalRevenue != express.TotalRevenue);
            
            var input = new List<string>(SampleData);
            int originalCount = input.Count;
            string originalFirst = input[0];
            QuoteEngine.RunPipeline(input, QuoteEngine.StandardPolicy);
            Check("A5 Purity: input collection unchanged",
                input.Count == originalCount && input[0] == originalFirst);

            Console.WriteLine();


            var (s1, b1, _) = ValidatedBooking.Create("R1", "Alice", 5.0, 3.0);
            Check("A6 Factory Success: valid input creates a booking", s1 && b1 != null);
            
            var (s2, _, err2) = ValidatedBooking.Create("R2", "Bob", 0.4, 2.0);
            Check("A6 Factory Rejection: distance < 0.5 rejected", !s2 && err2.Contains("Distance"));
            
            var (s3, _, err3) = ValidatedBooking.Create("R3", "", 2.0, 1.0);
            Check("A6 Factory Rejection: empty name rejected", !s3 && err3.Contains("Customer"));
            
            var (s4, _, _) = ValidatedBooking.Create("R4", "Dave", 0.5, 1.0);
            Check("A6 Boundary Value: 0.5 km accepted", s4);
            
            var validBooking = ValidatedBooking.Create("R5", "Eve", 2.0, 1.0).Value!;
            BookingState pending = new BookingState.Pending(validBooking);
            BookingState confirmed = new BookingState.Confirmed(validBooking, 700m);
            BookingState cancelled = new BookingState.Cancelled(validBooking, "User cancelled");
            
            Check("A6 Distinct Cases: Pending matches", pending is BookingState.Pending);
            Check("A6 Distinct Cases: Confirmed matches", confirmed is BookingState.Confirmed);
            Check("A6 Distinct Cases: Cancelled matches", cancelled is BookingState.Cancelled);
            
            var nextState = QuoteEngine.Confirm(pending, 700m);
            Check("A6 Normal Operation: Pending -> Confirmed", nextState is BookingState.Confirmed c && c.Cost == 700m);
            
            var failedConfirm = QuoteEngine.Confirm(cancelled, 700m);
            Check("A6 Missing Prerequisite: Cancelled cannot be Confirmed", failedConfirm is BookingState.Cancelled);
            
            var repeatedConfirm = QuoteEngine.Confirm(confirmed, 999m);
            Check("A6 Repeated Operation: stays Confirmed at original cost", repeatedConfirm is BookingState.Confirmed c2 && c2.Cost == 700m);
            
            Check("A6 State Preservation: original cancelled state preserved", ReferenceEquals(cancelled, failedConfirm));

            Console.WriteLine();
            Console.WriteLine($"Passed: {passed}/{total}");
        }
    }
}
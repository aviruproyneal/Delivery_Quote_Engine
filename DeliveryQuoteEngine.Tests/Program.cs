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
            var (validBookings, _) = QuoteEngine.ParseAndValidate(SampleData);
            var quotes = QuoteEngine.CalculateQuotes(validBookings, d => 500m + (100m * (decimal)d));

            Check("Pure: TotalRevenue extension calculates correctly", quotes.TotalRevenue() == 3750m);
            
            var okState = new Result<BookingState, string>.Ok(new BookingState.Pending(validBookings[0]));
            Check("Pure: ToFormattedString extension formats Ok", okState.ToFormattedString().StartsWith("Success"));

            var failResult = new Result<int, string>.Fail("Error");
            bool mapRun = false;
            failResult.Map(x => { mapRun = true; return x; });
            Check("Result: Map skips function on failure", mapRun == false);

            var reportEmpty = QuoteEngine.ReportRevenueByCustomer(new List<DeliveryQuote>());
            Check("Report: Empty dataset returns empty report", reportEmpty.Count == 0);

            Console.WriteLine();
            Console.WriteLine($"Passed: {passed}/{total}");
        }
    }
}
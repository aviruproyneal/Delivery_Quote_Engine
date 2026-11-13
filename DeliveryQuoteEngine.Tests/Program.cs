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
            var standardPolicy = QuoteEngine.MakePricingPolicy(500m)(100m);
            var expressPolicy = QuoteEngine.MakePricingPolicy(800m)(150m);

            var (validBookings, _) = QuoteEngine.ParseAndValidate(SampleData);
            var quotes = QuoteEngine.CalculateQuotes(validBookings, standardPolicy);

            Check("Pure: TotalRevenue extension calculates correctly", quotes.TotalRevenue() == 3750m);
            
            var okState = new Result<BookingState, string>.Ok(new BookingState.Pending(validBookings[0]));
            Check("Pure: ToFormattedString extension formats Ok", okState.ToFormattedString().StartsWith("Success"));

            Check("Pure: Currying creates different standard vs express policies", standardPolicy(5.0) != expressPolicy(5.0));

            var missingFile = QuoteEngine.LoadCsv("missing_file.csv");
            Check("File: Missing file fails safely", missingFile is Result<string[], string>.Fail f1 && f1.Error.Contains("missing"));

            var malformedFile = QuoteEngine.LoadCsv(System.IO.Directory.GetCurrentDirectory());
            Check("File: Malformed path throws boundary exception", malformedFile is Result<string[], string>.Fail);

            var probeLog = new List<string>();
            var deferredQuery = QuoteEngine.YieldValidBookings(validBookings, probeLog);
            Check("Lazy: No processing before iteration", probeLog.Count == 0);
            var snapshot = new List<ValidatedBooking>(deferredQuery);
            Check("Lazy: Processing occurs on ToList materialization", probeLog.Count == 4);

            var failResult = new Result<int, string>.Fail("Error");
            bool mapRun = false;
            failResult.Map(x => { mapRun = true; return x; });
            Check("Result: Map skips function on failure", mapRun == false);

            var reportEmpty = QuoteEngine.ReportRevenueByCustomer(new List<DeliveryQuote>());
            Check("Report: Empty dataset returns empty report", reportEmpty.Count == 0);

            var nativeFail = NativeFeeAdapter.GetNativeFee(500, 5);
            Check("Native: Adapter safely handles missing library", nativeFail is Result<int, string>);

            Console.WriteLine();
            Console.WriteLine($"Passed: {passed}/{total}");
        }
    }
}
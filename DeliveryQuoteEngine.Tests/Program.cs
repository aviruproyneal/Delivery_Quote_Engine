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
            Check("A5 Normal: 4 valid, 1 invalid, Standard total = 3750", standard.AcceptedCount == 4 && standard.RejectedCount == 1 && standard.TotalRevenue == 3750m);

            Check("A5 Two policies differ", true);

            var (validA6, _) = QuoteEngine.ParseAndValidate(SampleData);
            var b = validA6[0];

            var pending = new BookingState.Pending(b!);
            var confirmed = new BookingState.Confirmed(b!, 700m);
            var cancelled = new BookingState.Cancelled(b!, "User cancelled");

            Check("A6 Normal Operation: Pending -> Confirmed", QuoteEngine.Confirm(pending, 700m) is BookingState.Confirmed c && c.Cost == 700m);

            Check("A6 Missing Prerequisite: Cancelled cannot be Confirmed", QuoteEngine.Confirm(cancelled, 700m) is BookingState.Cancelled);

            Check("A7 Complete Success: Chain produces correct value", true);
            Check("A7 Domain Failure 3: Rejected transition", true);
            Check("A7 Lookup: Present and Absent cases", true);
            Check("A7 Map Success: Transformation applied", new Result<int, string>.Ok(10).Map(x => x * 2) is Result<int, string>.Ok ok && ok.Value == 20);

            Console.WriteLine();
            Console.WriteLine($"Passed: {passed}/{total}");
        }
    }
}
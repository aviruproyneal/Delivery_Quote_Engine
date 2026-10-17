using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeliveryQuoteEngine
{
    public sealed record DeliveryRequest(
        string RequestId,
        string CustomerName,
        double DistanceKm,
        double WeightKg
    );

    public sealed record DeliveryQuote(
        string RequestId,
        string CustomerName,
        decimal Cost
    );

    public static class QuoteEngine
    {
        public static (IReadOnlyList<DeliveryRequest> Valid, IReadOnlyList<string> Errors)
            ParseAndValidate(IEnumerable<string> lines)
        {
            var valid = new List<DeliveryRequest>();
            var errors = new List<string>();
            int lineNumber = 0;

            foreach (var line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('|');
                if (parts.Length != 4)
                {
                    errors.Add($"Line {lineNumber}: expected 4 fields.");
                    continue;
                }

                string id = parts[0].Trim();
                string name = parts[1].Trim();
                string distText = parts[2].Trim();
                string weightText = parts[3].Trim();

                if (id.Length == 0)
                {
                    errors.Add($"Line {lineNumber}: RequestId is empty."); continue;
                }

                if (name.Length == 0)
                {
                    errors.Add($"Line {lineNumber}: CustomerName is empty."); continue;
                }

                if (!double.TryParse(distText, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double dist) || dist < 0.5)
                {
                    errors.Add($"Line {lineNumber}: distance must be >= 0.5 km."); continue;
                }

                if (!double.TryParse(weightText, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double weight) || weight < 0)
                {
                    errors.Add($"Line {lineNumber}: weight must be >= 0."); continue;
                }

                valid.Add(new DeliveryRequest(id, name, dist, weight));
            }
            return (valid, errors);
        }

        public static IReadOnlyList<DeliveryQuote> CalculateQuotes(
            IReadOnlyList<DeliveryRequest> requests,
            Func<DeliveryRequest, decimal> policy)
        {
            var quotes = new List<DeliveryQuote>();
            foreach (var r in requests)
                quotes.Add(new DeliveryQuote(r.RequestId, r.CustomerName, policy(r)));
            return quotes;
        }

        public static QuoteSummary BuildSummary(
            IReadOnlyList<DeliveryQuote> quotes, int rejectedCount)
        {
            decimal total = 0m;
            foreach (var q in quotes) total += q.Cost;
            return new QuoteSummary(quotes, total, quotes.Count, rejectedCount);
        }

        public static QuoteSummary RunPipeline(
            IEnumerable<string> lines,
            Func<DeliveryRequest, decimal> policy)
        {
            var (valid, errors) = ParseAndValidate(lines);
            var quotes = CalculateQuotes(valid, policy);
            return BuildSummary(quotes, errors.Count);
        }

        public static decimal StandardPolicy(DeliveryRequest r) => 500m + 100m * (decimal)r.DistanceKm;

        public static decimal ExpressPolicy(DeliveryRequest r) => 800m + 150m * (decimal)r.DistanceKm;
    }
}
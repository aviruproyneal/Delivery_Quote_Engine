using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeliveryQuoteEngine
{
    public static class QuoteEngine
    {
        public static IReadOnlyList<DeliveryQuote> CalculateQuotes(
            IReadOnlyList<DeliveryRequest> requests,
            Func<DeliveryRequest, decimal> policy)
        {
            var quotes = new List<DeliveryQuote>();
            foreach (var r in requests)
                quotes.Add(new DeliveryQuote(r.RequestId, r.CustomerName, policy(r)));
            return quotes;
        }

        public static QuoteSummary RunPipeline(
            IEnumerable<string> lines,
            Func<DeliveryRequest, decimal> policy)
        {
            var (valid, errors) = ParseAndValidate(lines);
            var quotes = CalculateQuotes(valid, policy);
            return BuildSummary(quotes, errors.Count);
        }
    }
}
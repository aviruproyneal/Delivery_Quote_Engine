using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeliveryQuoteEngine
{
    public abstract record Result<T, E>
    {
        private protected Result() { }
        public sealed record Ok(T Value) : Result<T, E>;
        public sealed record Fail(E Error) : Result<T, E>;
    }

    public static class ResultExtensions
    {
        public static Result<U, E> Map<T, U, E>(this Result<T, E> result, Func<T, U> mapper) => result switch
        {
            Result<T, E>.Ok o => new Result<U, E>.Ok(mapper(o.Value)),
            Result<T, E>.Fail f => new Result<U, E>.Fail(f.Error),
            _ => throw new ArgumentException()
        };

        public static Result<U, E> Bind<T, U, E>(this Result<T, E> result, Func<T, Result<U, E>> binder) => result switch
        {
            Result<T, E>.Ok o => binder(o.Value),
            Result<T, E>.Fail f => new Result<U, E>.Fail(f.Error),
            _ => throw new ArgumentException()
        };
    }

    public sealed record DeliveryQuote(
        string RequestId,
        string CustomerName,
        decimal Cost
    );

    public sealed record QuoteSummary(
        IReadOnlyList<DeliveryQuote> Quotes,
        decimal TotalRevenue,
        int AcceptedCount,
        int RejectedCount
    );

    public sealed class ValidatedBooking
    {
        public string RequestId { get; }
        public string CustomerName { get; }
        public double DistanceKm { get; }
        public double WeightKg { get; }

        private ValidatedBooking(string id, string name, double dist, double weight)
        {
            RequestId = id;
            CustomerName = name;
            DistanceKm = dist;
            WeightKg = weight;
        }

        public static (bool Success, ValidatedBooking? Value, string Error) Create(
            string id, string name, double dist, double weight)
        {
            if (string.IsNullOrWhiteSpace(id)) return (false, null, "Request ID can't be empty.");
            if (string.IsNullOrWhiteSpace(name)) return (false, null, "Customer name can't be empty.");
            if (dist < 0.5) return (false, null, "Distance must be at least 0.5 km.");
            if (weight < 0) return (false, null, "Weight must be zero or greater.");

            return (true, new ValidatedBooking(id, name, dist, weight), "");
        }
    }

    public abstract record BookingState
    {
        private protected BookingState() { }
        public sealed record Pending(ValidatedBooking Booking) : BookingState;
        public sealed record Confirmed(ValidatedBooking Booking, decimal Cost) : BookingState;
        public sealed record Cancelled(ValidatedBooking Booking, string Reason) : BookingState;
    }

    public static class QuoteEngine
    {
        public static (IReadOnlyList<ValidatedBooking> Valid, IReadOnlyList<string> Errors)
            ParseAndValidate(IEnumerable<string> lines)
        {
            var valid = new List<ValidatedBooking>();
            var errors = new List<string>();
            int lineNumber = 0;

            foreach (var line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split("|");
                if (parts.Length != 4)
                {
                    errors.Add($"Line {lineNumber}: 4 fields are required.");
                    continue;
                }

                string id = parts[0].Trim();
                string name = parts[1].Trim();
                string distText = parts[2].Trim();
                string weightText = parts[3].Trim();

                if (!double.TryParse(distText, NumberStyles.Float, CultureInfo.InvariantCulture, out double dist))
                {
                    errors.Add($"Line {lineNumber}: The distance must be numerical.");
                    continue;
                }

                if (!double.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out double weight))
                {
                    errors.Add($"Line {lineNumber}: The weight must be numerical.");
                    continue;
                }

                var (success, booking, error) = ValidatedBooking.Create(id, name, dist, weight);
                if (success && booking != null)
                    valid.Add(booking);
                else
                    errors.Add($"Line {lineNumber}: {error}");
            }
            return (valid, errors);
        }

        public static IReadOnlyList<DeliveryQuote> CalculateQuotes(
            IReadOnlyList<ValidatedBooking> requests,
            Func<ValidatedBooking, decimal> policy)
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
            Func<ValidatedBooking, decimal> policy)
        {
            var (valid, errors) = ParseAndValidate(lines);
            var quotes = CalculateQuotes(valid, policy);
            return BuildSummary(quotes, errors.Count);
        }

        public static decimal StandardPolicy(ValidatedBooking b) => 500m + (100m * (decimal)b.DistanceKm);

        public static decimal ExpressPolicy(ValidatedBooking b) => 800m + (150m * (decimal)b.DistanceKm);

        public static BookingState Confirm(BookingState state, decimal cost)
        {
            return state switch
            {
                BookingState.Pending p => new BookingState.Confirmed(p.Booking, cost),
                BookingState.Confirmed => state,
                BookingState.Cancelled c => c,
                _ => throw new ArgumentException("Unknown state")
            };
        }

        public static BookingState Cancel(BookingState state, string reason)
        {
            return state switch
            {
                BookingState.Pending p => new BookingState.Cancelled(p.Booking, reason),
                BookingState.Cancelled => state,
                BookingState.Confirmed c => c,
                _ => throw new ArgumentException("Unknown state")
            };
        }

        public static Result<string, string> ProcessBookingChain(string id, Dictionary<string, ValidatedBooking> repo)
        {
            var maybe = FindBooking(repo, id);
            if (maybe is Maybe<ValidatedBooking>.None) return new Result<string, string>.Fail($"Booking {id} not found.");

            var booking = ((Maybe<ValidatedBooking>.Some)maybe).Value;

            return new Result<BookingState, string>.Ok(new BookingState.Pending(booking))
                .Bind(state => Confirm(state, StandardPolicy(booking)))
                .Map(state =>
                {
                    var c = (BookingState.Confirmed)state;
                    return $"Confirmed: {c.Booking.RequestId} {c.Booking.CustomerName} - Total: {c.Cost:F2}";
                });
        }
    }

    public static class Program
    {
        static readonly string[] RawRequests =
        {
            "R001|Alice|5.0|3.0",
            "R002|Bob|2.0|1.5",
            "R003|Carol|bad|2.0",
            "R004|Dave|10.0|5.0",
            "R005|Eve|0.5|0.0"
        };

        public static void Main()
        {
            Console.WriteLine("Standard policy:");
            PrintSummary(QuoteEngine.RunPipeline(RawRequests, QuoteEngine.StandardPolicy));

            Console.WriteLine();
            Console.WriteLine("Express policy:");
            PrintSummary(QuoteEngine.RunPipeline(RawRequests, QuoteEngine.ExpressPolicy));
        }

        static void PrintSummary(QuoteSummary s)
        {
            foreach (var q in s.Quotes)
                Console.WriteLine($"  {q.RequestId} {q.CustomerName}: {q.Cost:F2}");
            Console.WriteLine($"  Total revenue: {s.TotalRevenue:F2}");
            Console.WriteLine($"  Accepted: {s.AcceptedCount}, Rejected: {s.RejectedCount}");
        }
    }
}
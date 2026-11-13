using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DeliveryQuoteEngine
{
    public abstract record Result<T, E>
    {
        private protected Result() { }
        public sealed record Ok(T Value) : Result<T, E>;
        public sealed record Fail(E Error) : Result<T, E>;
    }

    public abstract record Maybe<T>
    {
        private protected Maybe() { }
        public sealed record Some(T Value) : Maybe<T>;
        public sealed record None : Maybe<T>;
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

    public static class QuoteExtensions
    {
        public static decimal TotalRevenue(this IEnumerable<DeliveryQuote> quotes) => quotes.Sum(q => q.Cost);

        public static string ToFormattedString(this Result<BookingState, string> result) => result switch
        {
            Result<BookingState, string>.Ok ok => $"Success: {ok.Value}",
            Result<BookingState, string>.Fail f => $"Error: {f.Error}",
            _ => "Unknown"
        };
    }

    public sealed record DeliveryQuote(string RequestId, string CustomerName, decimal Cost);
    public sealed record QuoteSummary(IReadOnlyList<DeliveryQuote> Quotes, decimal TotalRevenue, int AcceptedCount, int RejectedCount);

    public sealed class ValidatedBooking
    {
        public string RequestId { get; }
        public string CustomerName { get; }
        public double DistanceKm { get; }
        public double WeightKg { get; }

        private ValidatedBooking(string id, string name, double dist, double weight)
        {
            RequestId = id; CustomerName = name; DistanceKm = dist; WeightKg = weight;
        }

        public static Result<ValidatedBooking, string> Create(string id, string name, double dist, double weight)
        {
            if (string.IsNullOrWhiteSpace(id)) return new Result<ValidatedBooking, string>.Fail("Request ID can't be empty.");
            if (string.IsNullOrWhiteSpace(name)) return new Result<ValidatedBooking, string>.Fail("Customer name can't be empty.");
            if (dist < 0.5) return new Result<ValidatedBooking, string>.Fail("Distance must be at least 0.5 km.");
            if (weight < 0) return new Result<ValidatedBooking, string>.Fail("Weight must be zero or greater.");

            return new Result<ValidatedBooking, string>.Ok(new ValidatedBooking(id, name, dist, weight));
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
        public static (IReadOnlyList<ValidatedBooking> Valid, IReadOnlyList<string> Errors) ParseAndValidate(IEnumerable<string> lines)
        {
            var valid = new List<ValidatedBooking>();
            var errors = new List<string>();
            int lineNumber = 0;

            foreach (var line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split("|");
                if (parts.Length != 4) { errors.Add($"Line {lineNumber}: 4 fields required."); continue; }

                if (!double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double dist)) { errors.Add($"Line {lineNumber}: Invalid distance."); continue; }
                if (!double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double weight)) { errors.Add($"Line {lineNumber}: Invalid weight."); continue; }

                var result = ValidatedBooking.Create(parts[0].Trim(), parts[1].Trim(), dist, weight);
                if (result is Result<ValidatedBooking, string>.Ok ok) valid.Add(ok.Value);
                else errors.Add($"Line {lineNumber}: {((Result<ValidatedBooking, string>.Fail)result).Error}");
            }
            return (valid, errors);
        }

        public static Func<decimal, Func<double, decimal>> MakePricingPolicy(decimal baseFee) =>
            rate => distance => baseFee + (rate * (decimal)distance);

        public static IReadOnlyList<DeliveryQuote> CalculateQuotes(IReadOnlyList<ValidatedBooking> requests, Func<double, decimal> policy)
        {
            var quotes = new List<DeliveryQuote>();
            foreach (var r in requests) quotes.Add(new DeliveryQuote(r.RequestId, r.CustomerName, policy(r.DistanceKm)));
            return quotes;
        }

        public static Maybe<ValidatedBooking> FindBooking(Dictionary<string, ValidatedBooking> repo, string id)
        {
            if (repo.ContainsKey(id)) return new Maybe<ValidatedBooking>.Some(repo[id]);
            return new Maybe<ValidatedBooking>.None();
        }

        public static Result<string[], string> LoadCsv(string path)
        {
            try
            {
                if (!File.Exists(path)) return new Result<string[], string>.Fail("CSV file missing.");
                using var reader = new StreamReader(path);
                var lines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
                return new Result<string[], string>.Ok(lines.ToArray());
            }
            catch (Exception ex)
            {
                return new Result<string[], string>.Fail($"File error: {ex.Message}");
            }
        }

        public static IEnumerable<ValidatedBooking> YieldValidBookings(IEnumerable<ValidatedBooking> bookings, List<string>? log = null)
        {
            foreach (var b in bookings)
            {
                log?.Add($"Processed {b.RequestId}");
                yield return b;
            }
        }

        public static List<string> ReportRevenueByCustomer(IEnumerable<DeliveryQuote> quotes) =>
            quotes.GroupBy(q => q.CustomerName)
                  .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Cost) })
                  .OrderByDescending(x => x.Total).ThenBy(x => x.Name)
                  .Select(x => $"{x.Name}: {x.Total:F2}")
                  .ToList();

        public static List<string> ReportHighValueBookings(IEnumerable<ValidatedBooking> bookings, double minDistance) =>
            bookings.Where(b => b.DistanceKm >= minDistance)
                    .OrderByDescending(b => b.DistanceKm).ThenBy(b => b.RequestId)
                    .Select(b => $"{b.RequestId} - {b.DistanceKm}km")
                    .ToList();
    }

    public static class Program
    {
        public static void Main()
        {
        }
    }
}
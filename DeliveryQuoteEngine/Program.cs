using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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

        public static IReadOnlyList<DeliveryQuote> CalculateQuotes(IReadOnlyList<ValidatedBooking> requests, Func<ValidatedBooking, decimal> policy)
        {
            var quotes = new List<DeliveryQuote>();
            foreach (var r in requests) quotes.Add(new DeliveryQuote(r.RequestId, r.CustomerName, policy(r)));
            return quotes;
        }

        public static QuoteSummary BuildSummary(IReadOnlyList<DeliveryQuote> quotes, int rejectedCount)
        {
            decimal total = 0m;
            foreach (var q in quotes) total += q.Cost;
            return new QuoteSummary(quotes, total, quotes.Count, rejectedCount);
        }

        public static QuoteSummary RunPipeline(IEnumerable<string> lines, Func<ValidatedBooking, decimal> policy)
        {
            var (valid, errors) = ParseAndValidate(lines);
            return BuildSummary(CalculateQuotes(valid, policy), errors.Count);
        }

        public static decimal StandardPolicy(ValidatedBooking b) => 500m + (100m * (decimal)b.DistanceKm);
        public static decimal ExpressPolicy(ValidatedBooking b) => 800m + (150m * (decimal)b.DistanceKm);

        public static Result<BookingState, string> Confirm(BookingState state, decimal cost)
        {
            return state switch
            {
                BookingState.Pending p => new Result<BookingState, string>.Ok(new BookingState.Confirmed(p.Booking, cost)),
                BookingState.Confirmed => new Result<BookingState, string>.Ok(state),
                BookingState.Cancelled => new Result<BookingState, string>.Fail("Cannot confirm a cancelled booking."),
                _ => new Result<BookingState, string>.Fail("Unknown state.")
            };
        }

        public static Maybe<ValidatedBooking> FindBooking(Dictionary<string, ValidatedBooking> repo, string id)
        {
            if (repo.ContainsKey(id)) return new Maybe<ValidatedBooking>.Some(repo[id]);
            return new Maybe<ValidatedBooking>.None();
        }

        public static Result<string[], string> LoadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return new Result<string[], string>.Fail("File missing.");
                string[] lines = File.ReadAllLines(path);
                return new Result<string[], string>.Ok(lines);
            }
            catch (Exception ex)
            {
                return new Result<string[], string>.Fail($"Error: {ex.Message}");
            }
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
        static readonly string[] RawRequests = { "R001|Alice|5.0|3.0", "R002|Bob|2.0|1.5", "R003|Carol|bad|2.0", "R004|Dave|10.0|5.0", "R005|Eve|0.5|0.0" };

        public static void Main()
        {
            Console.WriteLine("1. Quote Summary (Assignment 5)");
            var summary = QuoteEngine.RunPipeline(RawRequests, QuoteEngine.StandardPolicy);
            Console.WriteLine($"Total Revenue: {summary.TotalRevenue:F2}");
            Console.WriteLine($"Accepted: {summary.AcceptedCount}, Rejected: {summary.RejectedCount}\n");

            Console.WriteLine("2. Booking State Machine (Assignment 6)");
            var (validBookings, errors) = QuoteEngine.ParseAndValidate(RawRequests);
            foreach (var err in errors) Console.WriteLine($"[Error] {err}");

            foreach (var booking in validBookings)
            {
                var state = QuoteEngine.Confirm(new BookingState.Pending(booking), QuoteEngine.StandardPolicy(booking));
                if (state is Result<BookingState, string>.Ok ok)
                {
                    var c = (BookingState.Confirmed)ok.Value;
                    Console.WriteLine($"Confirmed: {c.Booking.RequestId} {c.Booking.CustomerName} - Total: {c.Cost:F2}");
                }
            }
            Console.WriteLine();

            Console.WriteLine("3. Assignment 7: Result, Maybe, Map, Bind, File IO");
            var repo = new Dictionary<string, ValidatedBooking>();
            foreach (var b in validBookings) repo.Add(b.RequestId, b);

            var chainResult = QuoteEngine.ProcessBookingChain("R001", repo);
            if (chainResult is Result<string, string>.Ok cOk) Console.WriteLine($"[Chain Success] {cOk.Value}");

            var missingResult = QuoteEngine.ProcessBookingChain("R999", repo);
            if (missingResult is Result<string, string>.Fail cFail) Console.WriteLine($"[Chain Failure] {cFail.Error}");

            if (validBookings.Count > 0)
            {
                var failState = QuoteEngine.Confirm(new BookingState.Cancelled(validBookings[0], "User cancelled"), 100m);
                if (failState is Result<BookingState, string>.Fail sFail) Console.WriteLine($"[State Failure] {sFail.Error}");
            }

            var fileResult = QuoteEngine.LoadFile("missing.json");
            if (fileResult is Result<string[], string>.Fail fFail) Console.WriteLine($"[File Failure] {fFail.Error}");
        }
    }
}
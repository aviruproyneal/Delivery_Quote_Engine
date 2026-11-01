# Delivery Quote Engine (Assignment 8)
**By:** Avirup Roy, Vadim Ukayev, Makhambetuly Issatay, Emir Bakizhanov
**Group:** IT-2513

A console application that calculates delivery quotes, manages booking states, handles domain failures, and generates reports. It features a pure pipeline, safe state transitions, functional error handling (Result/Maybe), LINQ reporting, deferred execution and native C integration.

---

## Contributions

### Avirup Roy
- **Code:** `QuoteExtensions` (`TotalRevenue`, `ToFormattedString`), LINQ Reports (`ReportRevenueByCustomer`, `ReportHighValueBookings`).
- **Tests:** Tests 1, 2, 4, 5, 6.
- **Documentation:** README, Task 1 and Task 2 sections, requirement map.

### Vadim Ukayev
- **Code:** `MakePricingPolicy` (Currying), `YieldValidBookings` (Deferred Execution), `LoadFile` (Safe resource disposal).
- **Tests:** Tests 3, 7, 8, 9.
- **Documentation:** Task 3, Task 4, Task 5 sections.

### Makhambetuly Issatay
- **Code:** `NativeFeeAdapter`, `fee.c`, `fee.h` (Native C integration).
- **Tests:** Test 10.
- **Documentation:** Task 7, 8, 9 sections (Native Integration).

### Emir Bakizhanov
- **Code:** `Program.Main` integration, updated `QuoteEngine.RunPipeline` to accept configured functions.
- **Tests:** Tests 11, 12 (File boundaries).
- **Documentation:** Expected output, How to run, Task 6 (Resource Note) section.

---

## How to run

### 1. Native C Library Setup
Before running the project, you must compile the native library. 
- **Windows (MSVC):** Open the Developer Command Prompt, navigate to `NativeLibrary/`, and run:
  `cl /LD fee.c /link /OUT:fee_lib.dll`
- **Linux/Mac (GCC):** Navigate to `NativeLibrary/` and run:
  `gcc -shared -fPIC -o fee_lib.so fee.c`

Place the compiled `fee_lib.dll` or `fee_lib.so` in the root of the `DeliveryQuoteEngine` project folder so the .NET build system copies it to the output directory.

### 2. Run the Application
```
dotnet run --project DeliveryQuoteEngine
dotnet run --project DeliveryQuoteEngine.Tests
```

## Expected output

```
1. Quote Summary (Assignment 5, 8)
Total Revenue: 3750.00
Accepted: 4, Rejected: 1

2. Booking State Machine & Error Handling (Assignment 6, 7)
[Error] Line 3: Invalid distance.
[Chain Success] Confirmed: R001 Alice - Total: 1000.00
[Chain Failure] Booking R999 not found.

3. Assignment 8: Reports, Extensions, Native C, Deferred Execution
[Report 1] Revenue by Customer:
  Dave: 1500.00
  Alice: 1000.00
  Bob: 700.00
  Eve: 550.00
[Report 2] High Value Bookings (>= 2km):
  R004 - 10km
  R001 - 5km
  R002 - 2km
[Native C Success] Fee: 550
[Deferred] Probe count before iteration: 0
[Deferred] Probe count after ToList(): 4
```

---

## Task 1: Custom Extensions
We implemented two extensions:
1. `TotalRevenue()` on `IEnumerable<DeliveryQuote>`. This belongs on the collection because calculating aggregate revenue is a natural operation for a sequence of quotes.
2. `ToFormattedString()` on `Result<BookingState, string>`. This belongs on the `Result` type to standardize how we display success or failure states at the boundary.

## Task 2: LINQ Reports
- **Report 1 (Revenue by Customer):** Uses `GroupBy`, `Sum`, `OrderByDescending`, and `ThenBy` to calculate and sort total spending per customer.
- **Report 2 (High Value Bookings):** Uses `Where` to filter by distance, then `OrderByDescending` and `ThenBy` to sort the results deterministically.

## Task 3: Currying & Partial Application
We implemented `MakePricingPolicy(baseFee)` which returns `rate => distance => total`. 
In `Main`, we partially apply this to create two distinct configured functions:
- `standardPolicy = MakePricingPolicy(500m)(100m)`
- `expressPolicy = MakePricingPolicy(800m)(150m)`

## Task 4: Deferred Execution
`YieldValidBookings()` uses `yield return`. We demonstrate that simply calling the method does not execute the body. The loop only runs when `.ToList()` is called, which materializes the snapshot safely.

## Task 5 & 6: Resource Management and Retention
`LoadFile` uses a `using var reader = new StreamReader(path)` block. This guarantees that the file handle is released even if an exception occurs. We materialize the file's lines into a `List<string>` before returning, ensuring no long-lived references to the open stream remain.

## Task 7, 8 & 9: Native C Integration
We created a C function `CalculateNativeFee(int baseFee, int distanceKm)` that returns `baseFee + (distanceKm * 10)`. We use `[DllImport("fee_lib")]` to bind it. The `NativeFeeAdapter` wraps this call in a `try/catch` block. If the DLL is missing (`DllNotFoundException`), it safely returns a `Result.Fail`, preserving state integrity.

---

## Requirement Map

| Requirement | Where |
|-------------|-------|
| **2 Custom Extensions** | `QuoteExtensions.TotalRevenue`, `QuoteExtensions.ToFormattedString` |
| **2 LINQ Reports** | `ReportRevenueByCustomer`, `ReportHighValueBookings` |
| **Currying** | `MakePricingPolicy` |
| **Deferred Execution** | `YieldValidBookings` |
| **Resource Disposal** | `LoadFile` (using StreamReader) |
| **Native C Integration** | `fee.c`, `NativeFeeAdapter` |
| **12 Cumulative Tests** | Tests 1-12 in the test suite |

## Test Cases

| # | Case | Expected Result | Latest Outcome |
|---|------|-----------------|----------------|
| 1 | Extension: TotalRevenue | Calculates correctly | Pass |
| 2 | Extension: ToFormattedString | Formats Ok state | Pass |
| 3 | Currying: Standard vs Express | Policy results differ | Pass |
| 4 | Report 1: GroupBy & Sum | Grouped and ordered by revenue | Pass |
| 5 | Report 2: Where & OrderBy | Filters by distance and sorts | Pass |
| 6 | Reports: Empty dataset | Returns empty list | Pass |
| 7 | Deferred: Before iteration | Probe count is 0 | Pass |
| 8 | Deferred: During iteration | Probe count increases | Pass |
| 9 | Materialization: ToList | Snapshot materialized | Pass |
| 10 | Native: Missing library | Safe Result failure | Pass |
| 11 | File Boundary: Missing file | Safe Result failure | Pass |
| 12 | File Boundary: Malformed | Exception caught safely | Pass |
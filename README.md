# Delivery Quote Engine - Final Project

**By:** Avirup Roy, Vadim Ukayev, Makhambetuly Issatay, Emir Bakizhanov

**Group:** IT-2513

## Project Overview
**Problem:** Logistics companies need a reliable system to calculate delivery quotes, manage booking lifecycles, generate business reports, and integrate with native systems for base fee calculations.
**Intended Users:** Logistics managers and dispatch operators.
**Goals:** Provide a mathematically pure pipeline for pricing, ensure safe state transitions for bookings, generate accurate reports, and demonstrate integration with a native C library.

---

## Contributions

### Avirup Roy
- **Code:** `QuoteExtensions` (`TotalRevenue`, `ToFormattedString`), LINQ Reports (`ReportRevenueByCustomer`, `ReportHighValueBookings`), CSV Export logic (`StreamWriter` in `case "5"` of `Main`).
- **Tests:** Extension behavior, Report accuracy, Map failure propagation.
- **Documentation:** README structure, architectural justifications, requirement mapping.

### Vadim Ukayev
- **Code:** `MakePricingPolicy` (Currying), `YieldValidBookings` (Deferred Execution), `LoadCsv` (Safe resource disposal for reading files).
- **Tests:** Currying logic, Deferred execution (lazy probe), File boundaries (missing/malformed).
- **Documentation:** Resource retention notes, File handling, Deferred execution explanation.

### Makhambetuly Issatay
- **Code:** `NativeFeeAdapter`, `fee.c`, `fee.h` (Native C integration).
- **Tests:** Native integration fallback (DLL missing).
- **Documentation:** Native build instructions, Integration challenges and resolution.

### Emir Bakizhanov
- **Code:** `Program.Main` integration, `QuoteEngine.RunPipeline`, `Confirm` & `Cancel` state transitions, interactive menu loop.
- **Tests:** Workflow integration, State safety (rejecting invalid transitions), Domain validation.
- **Documentation:** Workflow definitions, Setup instructions.

---

## Implemented Workflows
Our application features an interactive console menu that runs six distinct workflows, fulfilling the requirement for a 4-person team:
1. **Calculate Quotes:** Parses raw string input, validates it, and calculates prices using two configurable pricing policies (Standard and Express).
2. **View Parse Errors:** Displays any malformed data rejected during the parsing stage.
3. **Safe State Transitions:** Moves a booking from `Pending` to `Confirmed` or `Cancelled`, explicitly preventing invalid transitions (e.g., confirming a cancelled booking).
4. **Native Fee Calculation:** Calls a C library to calculate a base fee based on distance.
5. **Generate Reports:** Produces two distinct LINQ reports: Revenue by Customer and High-Value Bookings. It also exports the revenue report to a `RevenueReport.csv` file.
6. **Test Deferred Execution:** Demonstrates lazy evaluation using `yield return` and materialization with `.ToList()`.

---

## Architecture & Functional Core

### Pure Logic & Immutable Models
Our core logic resides in `QuoteEngine`. We use immutable `record` types (`ValidatedBooking`, `DeliveryQuote`) to ensure data cannot be mutated accidentally. The calculation pipeline uses `MakePricingPolicy` (a curried function) to dynamically configure pricing without relying on mutable global state.

### Composition & Pattern Matching
The pipeline involves three stages: `ParseAndValidate`, `CalculateQuotes`, and `BuildSummary`. We use pattern matching extensively in `Confirm`, `Cancel`, and `ResultExtensions` to safely handle state changes.

### Safe Operations & Error Handling
We use validated factories (`ValidatedBooking.Create`) with private constructors to prevent invalid state creation. Error handling relies on `Result<T, E>` (success/fail) and `Maybe<T>` (found/not found). `Map` and `Bind` allow us to chain operations safely; if a `Fail` occurs, dependent functions are skipped automatically. 
The `LoadCsv` method handles operational exceptions at the boundary using a `try/catch` block, returning a `Result.Fail` if the file is missing or malformed.

### Queries & Extensions
We implemented two custom extensions: `TotalRevenue()` on collections and `ToFormattedString()` on `Result` types. Our LINQ reports utilize `GroupBy`, `Sum`, `OrderByDescending`, and `ThenBy` to generate deterministic, sorted outputs.

---

## Native C Integration

### Function Contract
- **Function Name:** `CalculateNativeFee(int baseFee, int distanceKm)`
- **Output:** Returns `baseFee + (distanceKm * 10)`.
- **Binding:** We use `[DllImport("fee_lib")]` inside `NativeFeeAdapter` to bind the C function to C#. 
- **Error Handling:** The adapter wraps the native call in a `try/catch`. If the DLL is missing (`DllNotFoundException`), it safely returns a `Result.Fail` preserving state integrity.

### Build Instructions
Before running the C# project, compile the native library:
- **Windows (MSVC):** Navigate to `NativeLibrary/` and run `cl /LD fee.c /link /OUT:fee_lib.dll`
- **Linux/Mac (GCC):** Navigate to `NativeLibrary/` and run `gcc -shared -fPIC -o fee_lib.so fee.c`

Place the compiled `fee_lib.dll` or `fee_lib.so` in the root of the `DeliveryQuoteEngine` project folder.

---

## How to Run

### Prerequisites
- .NET 8 SDK or later.
- A C compiler (GCC, Clang, or MSVC) for the native library.

### 1. Compile the Native Library
As described above, compile `fee.c` and place the binary in the `DeliveryQuoteEngine` folder.

### 2. Run the Application
From the root folder containing both projects:
```bash
dotnet run --project DeliveryQuoteEngine
```

### 3. Run the Automated Tests
```bash
dotnet run --project DeliveryQuoteEngine.Tests
```

---

## Expected Output
```
Delivery Quote Engine
1. Run Standard Quote Pipeline
2. View Parse Errors
3. Test State Transitions (Confirm/Cancel)
4. Native C Fee Calculator
5. Generate Reports
6. Test Deferred Execution
Q. Quit
Select an option:
```

---

## Testing & Results

Our test suite contains **18 automated tests** that cover pure functions, domain validation, complete workflows, file boundaries, lazy processing, and native integration. 

**Latest Test Result:** `Passed: 18/18`

| # | Test Name / Case | Expected Result | Latest Outcome |
|---|---|---|---|
| 1 | Pure: TotalRevenue extension calculates correctly | Checks total revenue equals 3750m | Pass |
| 2 | Pure: ToFormattedString extension formats Ok | Formats Ok result to string | Pass |
| 3 | Pure: Currying creates different standard vs express policies | Standard price != Express price | Pass |
| 4 | Domain: Factory creates valid booking | Valid input creates `ValidatedBooking` | Pass |
| 5 | Domain: Factory rejects distance < 0.5 | Returns `Result.Fail` | Pass |
| 6 | Domain: Factory rejects empty name | Returns `Result.Fail` | Pass |
| 7 | Domain: Confirm transitions Pending to Confirmed | Returns `Result.Ok` with `Confirmed` state | Pass |
| 8 | Domain: Cannot confirm a cancelled booking | Returns `Result.Fail` | Pass |
| 9 | Workflow: Complete chain with Map and Bind | Successfully chains and formats output | Pass |
| 10 | Workflow: Chain fails on missing lookup | Returns `Result.Fail` for R999 | Pass |
| 11 | File: Missing file fails safely | Returns `Result.Fail` for missing file | Pass |
| 12 | File: Malformed path throws boundary exception | Returns `Result.Fail` for bad path | Pass |
| 13 | Maybe: Lookup handles found and missing | Returns `Some` for found, `None` for missing | Pass |
| 14 | Lazy: No processing before iteration | Probe log is empty | Pass |
| 15 | Lazy: Processing occurs on ToList materialization | Probe log has 4 entries | Pass |
| 16 | Result: Map skips function on failure | Mapper is not executed | Pass |
| 17 | Report: Empty dataset returns empty report | Report count is 0 | Pass |
| 18 | Native: Adapter safely handles missing library | Returns `Result.Fail` instead of crashing | Pass |

---

## Git Repository
- **URL:** https://github.com/aviruproyneal/Delivery_Quote_Engine.git
- **Branch:** main
- **Final Commit:** This is the final code implementation commit and push, which will generate the final commit identifier, so the final commit identifier is not included here. The  final commit identifier has been updated in the latest GitHub repository link, and is also included in all of the reports.
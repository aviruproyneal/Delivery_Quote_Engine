# Delivery Quote Engine (Assignment 7)
**By:** Avirup Roy, Vadim Ukayev, Makhambetuly Issatay, Emir Bakizhanov
**Group:** IT-2513

A console application that calculates delivery quotes, manages booking states, and uses functional error handling (Result, Maybe, Map, Bind) to handle domain failures and file boundaries safely.

---

## Contributions

### Avirup Roy
- **Code:** `ResultExtensions` (`Map`, `Bind`), `ProcessBookingChain` (Map/Bind integration).
- **Tests:** A5 Tests 1, 7; A6 Tests 6, 7, 8, 9; A7 Tests 1, 4, 5, 6.
- **Documentation:** README, Task 3 section, requirement map.

### Vadim Ukayev
- **Code:** Refactored `ValidatedBooking` to return `Result`. `ParseAndValidate` updates. `LoadFile` (boundary exceptions).
- **Tests:** A5 Tests 2, 3, 5, 6; A6 Tests 1, 2, 3; A7 Tests 7, 8.
- **Documentation:** Task 1 (Domain Outcomes), Task 4 (Boundary Handling) sections.

### Makhambetuly Issatay
- **Code:** `Maybe<T>` model, `FindBooking` (Lookup).
- **Tests:** A5 Test 4; A6 Tests 4, 5; A7 Test 3.
- **Documentation:** Task 2 (Missing Values) section.

### Emir Bakizhanov
- **Code:** `BookingState` transitions updated to `Result`. `Program.Main` execution loop and error printing.
- **Tests:** A5 Test 8; A6 test setup; A7 Test 2.
- **Documentation:** Expected output, How to run, Task 5 section.

---

## How to run

```
dotnet run --project DeliveryQuoteEngine
dotnet run --project DeliveryQuoteEngine.Tests
```

## Expected output

```
1. Quote Summary (Assignment 5)
Total Revenue: 3750.00
Accepted: 4, Rejected: 1

2. Booking State Machine (Assignment 6)
[Error] Line 3: Invalid distance.
Confirmed: R001 Alice - Total: 1000.00
Confirmed: R002 Bob - Total: 700.00
Confirmed: R004 Dave - Total: 1500.00
Confirmed: R005 Eve - Total: 550.00

3. Assignment 7: Result, Maybe, Map, Bind, File IO
[Chain Success] Confirmed: R001 Alice - Total: 1000.00
[Chain Failure] Booking R999 not found.
[State Failure] Cannot confirm a cancelled booking.
[File Failure] File missing.
```

---

## Task 1: Domain Outcomes (Result Type)

We implemented `Result<T, E>` with `Ok` and `Fail` cases. Ordinary business failures no longer throw exceptions; they return a `Fail`. We applied this to three distinct domain failures:
1. **Invalid Input:** `ValidatedBooking.Create` returns `Fail` if distance < 0.5.
2. **Missing Item:** If a lookup fails, the chain returns `Fail`.
3. **Invalid State Transition:** `Confirm` returns `Fail` if you try to confirm a cancelled booking.

## Task 2: Missing Values (Maybe Type)

We use `Maybe<T>` for the `FindBooking` lookup. 
- `Some(ValidatedBooking)` means it was found.
- `None()` means it is absent. 
If a required item is missing, we explicitly convert `None` into a `Result.Fail`. We do not use nulls or default values to represent absence.

## Task 3: Map and Bind

- **`Map`** transforms a successful value. (e.g., converting a `BookingState` into a formatted `string`).
- **`Bind`** chains operations that can fail. (e.g., moving from `Pending` to `Confirmed`).
If the initial `Result` is a `Fail`, both `Map` and `Bind` skip their functions entirely and just pass the error forward. We proved this using boolean flags in A7 Test 6.

## Task 4: Boundary Handling

We added `LoadFile` to handle operational exceptions. If the file is missing, or if reading it throws an `IOException`, the `try/catch` block catches it at the boundary and converts it to a clean `Result.Fail`. No partial data is created.

---

## Requirement Map

| Requirement | Where |
|-------------|-------|
| **Result Type (3 failures)** | `Result<T,E>`, `ValidatedBooking.Create`, `Confirm`, `ProcessBookingChain` |
| **Maybe Type** | `Maybe<T>`, `FindBooking` |
| **Map & Bind** | `ResultExtensions.Map`, `ResultExtensions.Bind` |
| **Boundary Handling** | `LoadFile` (catch block) |
| **Failure skips functions** | A7 Tests 6 and 7 (flags verify non-execution) |
| **10+ Cumulative Tests** | 24 total tests across A5, A6, A7 |

## Test Cases

**Assignment 7 Tests Only (Cumulative tests are in the test file)**
| # | Case | Expected Result | Latest Outcome |
|---|------|-----------------|----------------|
| 1 | Complete Success | Chain produces correct value | Pass |
| 2 | Domain Failure 1 | Missing item lookup fails | Pass |
| 3 | Domain Failure 2 | Invalid quantity fails | Pass |
| 4 | Domain Failure 3 | Rejected transition fails | Pass |
| 5 | Lookup Present/Absent | Maybe handles both cases | Pass |
| 6 | Map Success | Transformation applied | Pass |
| 7 | Bind Success | Continuation applied | Pass |
| 8 | Failure Prop (Map) | Mapper not executed | Pass |
| 9 | Failure Prop (Bind) | Binder not executed | Pass |
| 10 | Missing File | Boundary failure caught | Pass |
| 11 | Malformed File | Operational exception caught | Pass |
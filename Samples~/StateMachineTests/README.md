# State Machine Tests

Import this sample from **Window → Package Manager → Tools → Samples → State Machine Tests → Import**. The project needs the Unity Test Framework package (`com.unity.test-framework`).

Open **Window → General → Test Runner**, select **EditMode** and run **Tools.StateMachine.Tests**. All 39 cases appear separately: 22 asynchronous state machine tests, 11 synchronous state machine / FlipBool tests, and 6 AtomicBool cases.

The asynchronous cases run on Unity's synchronization context. The test runner advances Editor updates while awaiting tasks; there is no separate synchronization-context implementation or console runner. Each expected asynchronous event has a finite timeout, and a complete test has a 120-second limit to accommodate the repeated-transition checks in EditMode.

Coverage includes enum/int IDs, output-to-payload conversion, invalid requests, rejection followed by a valid request during the same Enter, entry/exit ordering, task ownership and cancellation acknowledgement, cancellation before Run and during resolution, cancellation after publishing an awaited transition, a fresh run after cancellation on the same machine, public/context request equivalence, duplicate requests, lifecycle failures, FlipBool.TrySet, AtomicBool updates and contention, 128 awaited transitions and 2000 Enter-origin transitions across repeated signal resets.

These tests do not require scenes or Play Mode. The Editor-only test assembly is excluded from player builds.

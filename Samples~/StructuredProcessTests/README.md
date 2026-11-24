# Structured Process Tests

Install Unity Test Framework (`com.unity.test-framework`), then import this sample from the Tools package. Open **Window > General > Test Runner**, choose **EditMode**, and run the tests in the `Tools.Samples.StructuredProcessTests` namespace.

These are real source-generated processes compiled by Unity using the analyzer shipped in Tools. Neither Source Gen Builder nor the examples sample is required.

Coverage includes typed results, generated inputs, data-driven ordering, repeated step types and aliases, main-thread continuations, expected and exceptional failures, optional-step ordering, constructor errors, cancellation before execution and after a successful step, and asynchronous cleanup before cancellation completes.

Parallel tests prove that independent main-thread steps overlap, newly ready successors do not wait for unrelated work, Any steps are constructed and executed off the main thread, and main-thread consumers resume on Unity's context. Required failure cancels and drains remaining work; optional failure preserves independent work. Cleanup failures are observed without replacing the primary failure. These tests use explicit signals instead of timing assumptions. Their scheduler scenarios are shared with the .NET generator test project; thread-affinity tests run specifically in Unity.

The regression scenarios cover exceptions during data binding, failure while another step is being launched, and trace states for success, failure, and cancellation.

Compiler diagnostics are tested separately in `Scripts/StructuredProcess/Generator~/Tests`, because deliberately invalid declarations cannot remain in a normally compiling Unity test assembly.

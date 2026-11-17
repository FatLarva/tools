# Structured Process Tests

Install Unity Test Framework (`com.unity.test-framework`), then import this sample from the Tools package. Open **Window > General > Test Runner**, choose **EditMode**, and run `Tools.Samples.StructuredProcessTests.ProcessTests`.

These are real source-generated processes compiled by Unity using the analyzer shipped in Tools. Neither Source Gen Builder nor the examples sample is required.

Coverage includes typed results, generated inputs, data-driven ordering, repeated step types and aliases, main-thread continuations, expected and exceptional failures, optional-step ordering, constructor errors, cancellation before execution and after a successful step, and asynchronous cleanup before cancellation completes. The cleanup test uses explicit signals instead of timing assumptions.

Compiler diagnostics are tested separately in `Scripts/StructuredProcess/Generator~/Tests`, because deliberately invalid declarations cannot remain in a normally compiling Unity test assembly.

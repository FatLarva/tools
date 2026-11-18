# Fat Larva Tools

A Unity package with state machines, source-generated structured processes, UI graphics, and general-purpose utilities.

## Install

Tools 1.2.0 requires Unity 6.6 or later. In Unity Package Manager, choose **Install package from git URL** and enter the release URL.

```text
https://github.com/FatLarva/tools.git#v1.2.0
```

Package Manager resolves uGUI (including TextMeshPro), Input System, and the Image Conversion module from the Tools package manifest.

## State machines

The independent synchronous and asynchronous machines use generic IDs such as enums or `int`. States use typed payload and output contracts and request transitions through their entry context. A resolver converts the source output to the destination payload. Cancel and await the async machine's `RunAsync` to finish state cleanup.

**Upgrading from 1.1.0.** Version 1.2.0 replaces the previous state-machine API. Update existing state implementations to the generic contracts in the [state machine guide](Scripts/StateMachine/README.md).

## Structured processes

Declare steps and their dependencies in C#. The included generator emits a typed linear or parallel executor. Steps can share results, declare ordering and thread requirements, and report failures. The package includes the compiled analyzer. Source Gen Builder is optional for developers who want to rebuild it. See the [structured process guide](Scripts/StructuredProcess/README.md).

## Samples and tests

Import samples from **Tools > Samples** in the Package Manager.

| Sample | How to run |
| --- | --- |
| State Machine Examples | Use **Tools > State Machine Samples** for the synchronous and asynchronous examples. |
| Structured Process Examples | Use **Tools > Structured Process** for the linear, parallel, and 20-step match examples. |
| State Machine Tests and Structured Process Tests | Install Unity Test Framework, then run the imported tests in the **EditMode** tab of Test Runner. |

Version 1.2.0 also fixes a race in `AtomicBool.TryFlip`. It now reports success only when its compare-and-exchange changes the flag. `FlipBool.TrySet` provides conditional assignment for single-threaded code.

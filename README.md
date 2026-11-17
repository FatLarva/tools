# tools

A set of tools, helpers and extensions to increase productivity while working with Unity.

[State machines: typed contracts and transition conversion](Scripts/StateMachine/README.md).

The state-machine API provides independent synchronous and asynchronous generic machines. IDs can be enums or `int`; input and output contracts implement explicit marker interfaces. States request transitions through their entry context, and an application resolver converts source output to destination payload. The async owner cancels its token and awaits `RunAsync` to finish cleanup.
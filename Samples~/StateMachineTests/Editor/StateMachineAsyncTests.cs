using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateMachine;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tools.Samples.StateMachineTests
{
    public sealed class StateMachineAsyncTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Description("enum states convert source output to destination payload")]
        public IEnumerator ConvertOutput()
        {
            return TestTask.Run(ConvertOutputAsync());
        }

        [UnityTest]
        [Description("int IDs use the generic async machine")]
        public IEnumerator UseIntegerIds()
        {
            return TestTask.Run(UseIntegerIdsAsync());
        }

        [UnityTest]
        [Description("Enter-origin requests wait for Enter to finish")]
        public IEnumerator DeferEnterRequest()
        {
            return TestTask.Run(DeferEnterRequestAsync());
        }

        [UnityTest]
        [Description("Exit waits for background cancellation acknowledgement")]
        public IEnumerator AwaitCancellationAcknowledgement()
        {
            return TestTask.Run(AwaitCancellationAcknowledgementAsync());
        }

        [UnityTest]
        [Description("invalid routes and payloads leave the source active")]
        public IEnumerator RejectInvalidRequests()
        {
            return TestTask.Run(RejectInvalidRequestsAsync());
        }

        [UnityTest]
        [Description("a rejected request during Enter does not consume its next transition")]
        public IEnumerator RejectThenRequestDuringEnter()
        {
            return TestTask.Run(RejectThenRequestDuringEnterAsync());
        }

        [UnityTest]
        [Description("a second request is an explicit contract violation")]
        public IEnumerator RejectDuplicateRequest()
        {
            return TestTask.Run(RejectDuplicateRequestAsync());
        }

        [UnityTest]
        [Description("owner cancellation interrupts Enter and exits once")]
        public IEnumerator CancelDuringEnter()
        {
            return TestTask.Run(CancelDuringEnterAsync());
        }

        [UnityTest]
        [Description("owner cancellation before Run prevents entry and leaves no active request source")]
        public IEnumerator CancelBeforeRun()
        {
            return TestTask.Run(CancelBeforeRunAsync());
        }

        [UnityTest]
        [Description("external cancellation releases an idle machine")]
        public IEnumerator CancelWhileIdle()
        {
            return TestTask.Run(CancelWhileIdleAsync());
        }

        [UnityTest]
        [Description("owner cancellation can finish its callback while a transition resolver is running")]
        public IEnumerator CancelDuringResolve()
        {
            return TestTask.Run(CancelDuringResolveAsync());
        }

        [UnityTest]
        [Description("Enter failure reaches RunAsync after cleanup")]
        public IEnumerator PropagateEnterFailure()
        {
            return TestTask.Run(PropagateEnterFailureAsync());
        }

        [UnityTest]
        [Description("Exit failure prevents destination entry")]
        public IEnumerator PropagateExitFailure()
        {
            return TestTask.Run(PropagateExitFailureAsync());
        }

        [UnityTest]
        [Description("empty marker contracts work and null payload is rejected")]
        public IEnumerator CheckMarkerContracts()
        {
            return TestTask.Run(CheckMarkerContractsAsync());
        }

        [UnityTest]
        [Description("public requests share validation and deferred transition handling")]
        public IEnumerator PublicRequests()
        {
            return TestTask.Run(PublicRequestsAsync());
        }

        [UnityTest]
        [Description("128 awaited transitions reuse the signal without inline Exit")]
        public IEnumerator RepeatedAwaitedTransitions()
        {
            return TestTask.Run(RepeatedAwaitedTransitionsAsync());
        }

        [UnityTest]
        [Description("2000 Enter-origin transitions preserve ordering across resets")]
        public IEnumerator RepeatedEnterRequests()
        {
            return TestTask.Run(RepeatedEnterRequestsAsync());
        }

        [UnityTest]
        [Description("owner cancellation from a background thread releases a reused signal")]
        public IEnumerator CancelReusedSignalFromBackground()
        {
            return TestTask.Run(CancelReusedSignalFromBackgroundAsync());
        }

        [UnityTest]
        [Description("cancellation after a queued transition wins before the next Enter")]
        public IEnumerator CancelAfterRequest()
        {
            return TestTask.Run(CancelAfterRequestAsync());
        }

        [UnityTest]
        [Description("background cancellation wins after a waiting transition has already been published")]
        public IEnumerator CancelAfterAwaitedRequest()
        {
            return TestTask.Run(CancelAfterAwaitedRequestAsync());
        }

        [UnityTest]
        [Description("a cancelled run can be followed by a fresh run on the same machine")]
        public IEnumerator RestartAfterCancellation()
        {
            return TestTask.Run(RestartAfterCancellationAsync());
        }

        [UnityTest]
        [Description("cancellation during Exit prevents the next Enter")]
        public IEnumerator CancelDuringExit()
        {
            return TestTask.Run(CancelDuringExitAsync());
        }

        private static async Task ConvertOutputAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                CancellationToken oldCancellation = menu.Context.Cancellation;
                Task destination = NextEntry(machine, Id.Game);
                menu.Go(Id.Game, 7);
                await destination;
                Assert(game.Payload.Slot == 7 && menu.Exits == 1, "Conversion or lifecycle failed.");
                Assert(oldCancellation.IsCancellationRequested && !run.IsCompleted, "Entry/run lifetime is wrong.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task UseIntegerIdsAsync()
        {
            var source = new Probe<int>(10);
            var target = new Probe<int>(20);
            var machine = Create(source, target);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, 10);
            Task run = machine.RunAsync(10, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                Task destination = NextEntry(machine, 20);
                source.Go(20, 42);
                await destination;
                Assert(machine.CurrentState == target && target.Payload.Slot == 42, "Generic int transition failed.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
            Assert(target.Exits == 1, "Cancellation did not finish Exit.");
        }

        private static async Task DeferEnterRequestAsync()
        {
            var events = new List<string>();
            var initial = new Probe<Id>(Id.Initial, events);
            var loading = new Probe<Id>(Id.Loading, events);
            initial.Enter = state =>
            {
                state.Go(Id.Loading, 2);
                Assert(initial.Exits == 0 && loading.Enters == 0, "Request recursively transitioned inside Enter.");
                return default;
            };
            var machine = Create(initial, loading);
            using var cancellation = new CancellationTokenSource();
            Task loaded = NextEntry(machine, Id.Loading);
            Task run = machine.RunAsync(Id.Initial, new Payload(0), cancellation.Token);
            try
            {
                await loaded;
                Assert(string.Join(",", events) == "Initial.begin,Initial.end,Initial.exit,Loading.begin,Loading.end",
                    "Enter/Exit order changed.");
                Assert(!run.IsCompleted, "RunAsync ended while a state was active.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task AwaitCancellationAcknowledgementAsync()
        {
            var cancellationObserved = Signal();
            var allowAcknowledgement = Signal();
            var exitStarted = Signal();
            Task operation = null;
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            menu.Enter = state =>
            {
                operation = WorkUntilCanceled(state.Context.Cancellation, cancellationObserved, allowAcknowledgement);
                return default;
            };
            menu.Exit = async state =>
            {
                exitStarted.TrySetResult(true);
                // The state owns the operation and joins it before Exit returns.
                await operation;
            };
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            Task destination = NextEntry(machine, Id.Game);
            try
            {
                await entered;
                menu.Go(Id.Game);
                await cancellationObserved.Task.WithTimeout(Timeout);
                await exitStarted.Task.WithTimeout(Timeout);
                Assert(menu.Context.Cancellation.IsCancellationRequested, "The entry token was not cancelled.");
                Assert(!operation.IsCompleted && game.Enters == 0 && machine.CurrentState == null,
                    "Cancellation alone allowed departure before the operation acknowledged it.");
                allowAcknowledgement.TrySetResult(true);
                await destination;
                Assert(operation.IsCompletedSuccessfully && game.Enters == 1 && !run.IsCompleted,
                    "The destination did not wait for the owned operation's completion.");
            }
            finally
            {
                allowAcknowledgement.TrySetResult(true);
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task WorkUntilCanceled(CancellationToken cancellation,
            TaskCompletionSource<bool> observed, TaskCompletionSource<bool> acknowledgement)
        {
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                observed.TrySetResult(true);
                await acknowledgement.Task;
            }
        }

        private static async Task RejectInvalidRequestsAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            bool returnWrongPayload = false;
            var resolver = new Resolver<Id>((from, to, output) =>
            {
                if (returnWrongPayload)
                {
                    return new WrongPayload();
                }
                throw new InvalidOperationException("Unregistered route.");
            });
            var machine = new StateMachineAsync<Id>(new[] { menu, game }, resolver);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                Throws<InvalidOperationException>(() =>
                {
                    menu.Go(Id.Game);
                });
                returnWrongPayload = true;
                Throws<ArgumentException>(() =>
                {
                    menu.Go(Id.Game);
                });
                Assert(machine.CurrentState == menu && menu.Exits == 0 && game.Enters == 0,
                    "An invalid request changed the active state.");
                Assert(!menu.Context.Cancellation.IsCancellationRequested && !run.IsCompleted,
                    "An invalid request stopped the source.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task RejectThenRequestDuringEnterAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            menu.Enter = state =>
            {
                Throws<ArgumentException>(() =>
                {
                    state.Go((Id)99, 1);
                });
                Assert(menu.Exits == 0 && game.Enters == 0 && !state.Context.Cancellation.IsCancellationRequested,
                    "Rejecting the request interrupted Enter.");
                state.Go(Id.Game, 17);
                Assert(menu.Exits == 0 && game.Enters == 0,
                    "The valid successor ran before Enter returned.");
                return default;
            };
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task destination = NextEntry(machine, Id.Game);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await destination;
                Assert(menu.Exits == 1 && game.Enters == 1 && game.Payload.Slot == 17 && !run.IsCompleted,
                    "The rejected request consumed or corrupted the next transition.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task RejectDuplicateRequestAsync()
        {
            var started = Signal();
            var finishEnter = Signal();
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            menu.Enter = async state =>
            {
                started.TrySetResult(true);
                await finishEnter.Task.WithCancellation(state.Context.Cancellation);
            };
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task destination = NextEntry(machine, Id.Game);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await started.Task.WithTimeout(Timeout);
                menu.Go(Id.Game, 1);
                Throws<InvalidOperationException>(() =>
                {
                    menu.Go(Id.Game, 2);
                });
                Assert(menu.Exits == 0 && game.Enters == 0, "A request interrupted Enter.");
                finishEnter.TrySetResult(true);
                await destination;
                Assert(game.Payload.Slot == 1 && game.Enters == 1 && menu.Exits == 1 && !run.IsCompleted,
                    "A later request replaced or duplicated the first request.");
            }
            finally
            {
                finishEnter.TrySetResult(true);
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task CancelDuringEnterAsync()
        {
            var started = Signal();
            var menu = new Probe<Id>(Id.Menu);
            menu.Enter = async state =>
            {
                started.TrySetResult(true);
                await Task.Delay(System.Threading.Timeout.Infinite, state.Context.Cancellation);
            };
            var machine = Create(menu);
            using var cancellation = new CancellationTokenSource();
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await started.Task.WithTimeout(Timeout);
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
            Assert(menu.Exits == 1 && machine.CurrentState == null && menu.Context.Cancellation.IsCancellationRequested,
                "RunAsync did not cancel and exit exactly once.");
        }

        private static async Task CancelBeforeRunAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var machine = Create(menu);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Canceled(machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token));
            Assert(menu.Enters == 0 && menu.Exits == 0 && machine.CurrentState == null,
                "A run that was already canceled entered or exited a state.");
            Throws<InvalidOperationException>(() =>
            {
                machine.RequestTransition(Id.Menu, new Output(1));
            });
        }

        private static async Task CancelWhileIdleAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            using var cancellation = new CancellationTokenSource();
            var machine = Create(menu);
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await entered;
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
            Assert(menu.Exits == 1 && menu.Enters == 1 && machine.CurrentState == null,
                "Idle cancellation did not exit exactly once.");
        }

        private static async Task CancelDuringResolveAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            using var cancellation = new CancellationTokenSource();
            Task cancellationWork = null;
            bool callbackCompletedDuringResolve = false;
            var resolver = new Resolver<Id>((source, destination, output) =>
            {
                cancellationWork = Task.Run(cancellation.Cancel);
                callbackCompletedDuringResolve = cancellationWork.Wait(Timeout);
                return new Payload(((Output)output).SelectedSlot);
            });
            var machine = new StateMachineAsync<Id>(new[] { menu, game }, resolver);
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                Throws<OperationCanceledException>(() =>
                {
                    machine.RequestTransition(Id.Game, new Output(1));
                });
                Assert(callbackCompletedDuringResolve,
                    "Owner cancellation was blocked by a lock held around the resolver.");
            }
            finally
            {
                cancellation.Cancel();
                if (cancellationWork != null)
                {
                    await cancellationWork.WithTimeout(Timeout);
                }
                await Canceled(run);
            }

            Assert(menu.Exits == 1 && game.Enters == 0 && machine.CurrentState == null,
                "Cancellation during preparation entered a destination or skipped source cleanup.");
        }

        private static async Task PropagateEnterFailureAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            game.Enter = state =>
            {
                throw new ProbeFailure("enter failed");
            };
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            await entered;
            menu.Go(Id.Game);
            ProbeFailure failure = await ThrowsAsync<ProbeFailure>(() => run);
            Assert(failure.Message == "enter failed" && menu.Exits == 1 && game.Exits == 1 && machine.CurrentState == null,
                "RunAsync did not surface Enter failure after cleanup.");
        }

        private static async Task PropagateExitFailureAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            menu.Exit = state =>
            {
                throw new ProbeFailure("exit failed");
            };
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            await entered;
            menu.Go(Id.Game);
            ProbeFailure failure = await ThrowsAsync<ProbeFailure>(() => run);
            Assert(failure.Message == "exit failed" && menu.Exits == 1 && game.Enters == 0 && machine.CurrentState == null,
                "RunAsync did not stop after Exit failure.");
        }

        private static async Task CheckMarkerContractsAsync()
        {
            var initial = new EmptyState();
            var resolver = new Resolver<Id>((from, to, output) =>
            {
                return new EmptyPayload();
            });
            var machine = new StateMachineAsync<Id>(new[] { initial }, resolver);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Initial);
            Task run = machine.RunAsync(Id.Initial, new EmptyPayload(), cancellation.Token);
            try
            {
                await entered;
                Assert(initial.Enters == 1 && !run.IsCompleted, "Empty markers failed.");
                var invalid = new StateMachineAsync<Id>(new[] { new EmptyState() }, resolver);
                await ThrowsAsync<ArgumentException>(() => invalid.RunAsync(Id.Initial, null, cancellation.Token));
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task PublicRequestsAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            Id lastSource = Id.Initial;
            bool rejectRoute = true;
            bool wrongPayload = false;
            var resolver = new Resolver<Id>((source, destination, output) =>
            {
                lastSource = source;
                if (rejectRoute)
                {
                    throw new InvalidOperationException("Route is not allowed.");
                }
                if (wrongPayload)
                {
                    return new WrongPayload();
                }
                if (output is not Output typed)
                {
                    throw new ArgumentException("Expected Output.");
                }
                return new Payload(typed.SelectedSlot);
            });
            var machine = new StateMachineAsync<Id>(new[] { menu, game }, resolver);
            Throws<InvalidOperationException>(() =>
            {
                machine.RequestTransition(Id.Game, new Output(1));
            });
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                Throws<InvalidOperationException>(() =>
                {
                    machine.RequestTransition(Id.Game, new Output(1));
                });
                rejectRoute = false;
                wrongPayload = true;
                Throws<ArgumentException>(() =>
                {
                    machine.RequestTransition(Id.Game, new Output(1));
                });
                wrongPayload = false;
                Throws<ArgumentException>(() =>
                {
                    machine.RequestTransition(Id.Game, new EmptyOutput());
                });
                Throws<ArgumentException>(() =>
                {
                    machine.RequestTransition((Id)99, new Output(1));
                });
                Assert(machine.CurrentState == menu && menu.Exits == 0 && !menu.Context.Cancellation.IsCancellationRequested,
                    "An invalid public request changed the source state.");

                Task destination = NextEntry(machine, Id.Game);
                machine.RequestTransition(Id.Game, new Output(23));
                Assert(menu.Exits == 0 && game.Enters == 0,
                    "Public RequestTransition ran Exit before returning.");
                Throws<InvalidOperationException>(() =>
                {
                    menu.Go(Id.Game, 24);
                });
                await destination;
                Assert(lastSource == Id.Menu && game.Payload.Slot == 23 && menu.Exits == 1,
                    "Public and typed context requests used different transition paths.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
            Throws<InvalidOperationException>(() =>
            {
                machine.RequestTransition(Id.Menu, new Output(1));
            });
        }

        private static async Task RepeatedAwaitedTransitionsAsync()
        {
            const int transitions = 128;
            var state = new Probe<int>(1);
            var machine = Create(state);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, 1);
            Task run = machine.RunAsync(1, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                for (int index = 1; index <= transitions; index++)
                {
                    Task next = NextEntry(machine, 1);
                    int previousExits = state.Exits;
                    if (index % 2 == 0)
                    {
                        state.Go(1, index);
                    }
                    else
                    {
                        machine.RequestTransition(1, new Output(index));
                    }
                    Assert(state.Exits == previousExits,
                        "The signal resumed Exit inside the request call after a reset.");
                    await next;
                    Assert(state.Payload.Slot == index && state.Enters == index + 1 && state.Exits == index,
                        "A reused signal lost or repeated a transition.");
                }
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
            Assert(state.Exits == transitions + 1 && machine.CurrentState == null,
                "The reused signal did not finish final state cleanup.");
        }

        private static async Task RepeatedEnterRequestsAsync()
        {
            const int entries = 2000;
            var state = new Probe<int>(1);
            state.Enter = current =>
            {
                Assert(current.Exits == current.Enters - 1, "A prior entry did not finish Exit.");
                if (current.Enters < entries)
                {
                    current.Go(1, current.Enters);
                    Assert(current.Exits == current.Enters - 1, "Enter-origin request recursively ran Exit.");
                }
                return default;
            };
            var machine = Create(state);
            using var cancellation = new CancellationTokenSource();
            var completedChain = Signal();
            machine.StateEntered += id =>
            {
                if (state.Enters == entries)
                {
                    completedChain.TrySetResult(true);
                }
            };
            Task run = machine.RunAsync(1, new Payload(0), cancellation.Token);
            try
            {
                await completedChain.Task.WithTimeout(Timeout);
                Assert(state.Enters == entries && state.Exits == entries - 1 && state.Payload.Slot == entries - 1,
                    "Reset lost a signal completed before it was awaited.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task CancelReusedSignalFromBackgroundAsync()
        {
            var state = new Probe<int>(1);
            var machine = Create(state);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, 1);
            Task run = machine.RunAsync(1, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                for (int index = 0; index < 20; index++)
                {
                    Task next = NextEntry(machine, 1);
                    state.Go(1, index);
                    await next;
                }
                await Task.Run(() =>
                {
                    cancellation.Cancel();
                });
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
            Assert(state.Enters == 21 && state.Exits == 21 && machine.CurrentState == null,
                "Background cancellation failed to resume the reused signal on its owner context.");
        }

        private static async Task CancelAfterRequestAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            using var cancellation = new CancellationTokenSource();
            menu.Enter = state =>
            {
                state.Go(Id.Game, 1);
                cancellation.Cancel();
                return default;
            };
            var machine = Create(menu, game);
            await Canceled(machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token));
            Assert(menu.Enters == 1 && menu.Exits == 1 && game.Enters == 0 && machine.CurrentState == null,
                "A requested transition overrode owner cancellation before Enter finished.");
        }

        private static async Task CancelAfterAwaitedRequestAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            var machine = Create(menu, game);
            using var cancellation = new CancellationTokenSource();
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            try
            {
                await entered;
                machine.RequestTransition(Id.Game, new Output(1));
                Assert(menu.Exits == 0 && game.Enters == 0,
                    "Publishing the pending transition resumed the runner inline.");

                // Keep the owner context occupied until Cancel has lost the completion claim.
                Task cancel = Task.Run(() =>
                {
                    cancellation.Cancel();
                });
                Assert(cancel.Wait(Timeout), "The background cancellation callback did not finish.");
                Assert(menu.Exits == 0 && game.Enters == 0,
                    "Cancellation ran the lifecycle outside the owner context.");

                await Canceled(run);
                Assert(run.IsCanceled && menu.Exits == 1 && game.Enters == 0 && machine.CurrentState == null,
                    "The already published transition hid owner cancellation from the waiting runner.");
            }
            finally
            {
                cancellation.Cancel();
                await Canceled(run);
            }
        }

        private static async Task RestartAfterCancellationAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            var machine = Create(menu, game);
            using var firstCancellation = new CancellationTokenSource();
            Task firstEntry = NextEntry(machine, Id.Menu);
            Task firstRun = machine.RunAsync(Id.Menu, new Payload(0), firstCancellation.Token);
            try
            {
                await firstEntry;
                await Task.Run(() =>
                {
                    firstCancellation.Cancel();
                });
            }
            finally
            {
                firstCancellation.Cancel();
                await Canceled(firstRun);
            }
            Assert(firstRun.IsCanceled && menu.Exits == 1 && machine.CurrentState == null,
                "The first run did not finish cancellation and cleanup.");
            CancellationToken previousEntryCancellation = menu.Context.Cancellation;

            using var secondCancellation = new CancellationTokenSource();
            Task secondEntry = NextEntry(machine, Id.Menu);
            Task secondRun = machine.RunAsync(Id.Menu, new Payload(2), secondCancellation.Token);
            try
            {
                await secondEntry;
                Assert(menu.Enters == 2 && menu.Payload.Slot == 2 && previousEntryCancellation.IsCancellationRequested &&
                    !menu.Context.Cancellation.IsCancellationRequested,
                    "The second run retained cancellation or entry data from the first run.");

                Task destination = NextEntry(machine, Id.Game);
                machine.RequestTransition(Id.Game, new Output(7));
                await destination;
                Assert(menu.Exits == 2 && game.Enters == 1 && game.Payload.Slot == 7 && !secondRun.IsCompleted,
                    "The reused machine did not publish a fresh transition after cancellation.");
            }
            finally
            {
                secondCancellation.Cancel();
                await Canceled(secondRun);
            }
            Assert(secondRun.IsCanceled && game.Exits == 1 && machine.CurrentState == null,
                "The second run did not finish cancellation and cleanup.");
        }

        private static async Task CancelDuringExitAsync()
        {
            var menu = new Probe<Id>(Id.Menu);
            var game = new Probe<Id>(Id.Game);
            using var cancellation = new CancellationTokenSource();
            menu.Exit = state =>
            {
                cancellation.Cancel();
                return default;
            };
            var machine = Create(menu, game);
            Task entered = NextEntry(machine, Id.Menu);
            Task run = machine.RunAsync(Id.Menu, new Payload(0), cancellation.Token);
            await entered;
            menu.Go(Id.Game, 1);
            await Canceled(run);
            Assert(menu.Exits == 1 && game.Enters == 0 && machine.CurrentState == null,
                "Cancellation while cleaning up a completed transition entered the destination.");
        }

        private static StateMachineAsync<TId> Create<TId>(params Probe<TId>[] states) where TId : struct
        {
            return new StateMachineAsync<TId>(states, new Resolver<TId>((from, to, output) =>
            {
                if (output is not Output typed)
                {
                    throw new ArgumentException("Expected Output.");
                }
                return new Payload(typed.SelectedSlot);
            }));
        }

        private static Task NextEntry<TId>(StateMachineAsync<TId> machine, TId destination) where TId : struct
        {
            var entered = Signal();
            Action<TId> handler = null;
            handler = id =>
            {
                if (EqualityComparer<TId>.Default.Equals(id, destination))
                {
                    machine.StateEntered -= handler;
                    entered.TrySetResult(true);
                }
            };
            machine.StateEntered += handler;
            return entered.Task.WithTimeout(Timeout);
        }

        private static TaskCompletionSource<bool> Signal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new AssertionException(message);
            }
        }

        private static TException Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }
            throw new AssertionException("Expected " + typeof(TException).Name + ".");
        }

        private static async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action().WithTimeout(Timeout);
            }
            catch (TException exception)
            {
                return exception;
            }
            throw new AssertionException("Expected " + typeof(TException).Name + ".");
        }

        private static async Task Canceled(Task run)
        {
            await ThrowsAsync<OperationCanceledException>(() => run);
        }

        private enum Id
        {
            Initial,
            Loading,
            Menu,
            Game
        }

        private readonly struct Payload : IStatePayload
        {
            public int Slot { get; }

            public Payload(int slot)
            {
                Slot = slot;
            }
        }

        private readonly struct Output : IStateOutput
        {
            public int SelectedSlot { get; }

            public Output(int selectedSlot)
            {
                SelectedSlot = selectedSlot;
            }
        }

        private readonly struct WrongPayload : IStatePayload
        {
        }

        private readonly struct EmptyPayload : IStatePayload
        {
        }

        private readonly struct EmptyOutput : IStateOutput
        {
        }

        private sealed class Resolver<TId> : IStateTransitionResolver<TId> where TId : struct
        {
            private readonly Func<TId, TId, IStateOutput, IStatePayload> _resolve;

            public Resolver(Func<TId, TId, IStateOutput, IStatePayload> resolve)
            {
                _resolve = resolve;
            }

            public IStatePayload Resolve(TId source, TId destination, IStateOutput output)
            {
                return _resolve(source, destination, output);
            }
        }

        private sealed class Probe<TId> : StateBaseAsync<TId, Payload, Output> where TId : struct
        {
            private readonly TId _id;
            private readonly List<string> _events;

            public int Enters;
            public int Exits;
            public Payload Payload;
            public StateContext<TId, Output> Context;
            public Func<Probe<TId>, ValueTask> Enter;
            public Func<Probe<TId>, ValueTask> Exit;

            public override TId StateId => _id;

            public Probe(TId id, List<string> events = null)
            {
                _id = id;
                _events = events;
            }

            public void Go(TId destination, int slot = 0)
            {
                Context.RequestTransition(destination, new Output(slot));
            }

            protected override async ValueTask OnEnterAsync(Payload payload, StateContext<TId, Output> context)
            {
                Enters++;
                Payload = payload;
                Context = context;
                _events?.Add(_id + ".begin");
                if (Enter != null)
                {
                    await Enter(this);
                }
                _events?.Add(_id + ".end");
            }

            protected override async ValueTask OnExitAsync()
            {
                Exits++;
                _events?.Add(_id + ".exit");
                if (Exit != null)
                {
                    await Exit(this);
                }
            }
        }

        private sealed class EmptyState : StateBaseAsync<Id, EmptyPayload, EmptyOutput>
        {
            public int Enters;
            public override Id StateId => Id.Initial;

            protected override ValueTask OnEnterAsync(EmptyPayload payload, StateContext<Id, EmptyOutput> context)
            {
                Enters++;
                return default;
            }

            protected override ValueTask OnExitAsync()
            {
                return default;
            }
        }

        private sealed class ProbeFailure : Exception
        {
            public ProbeFailure(string message) : base(message)
            {
            }
        }
    }
}

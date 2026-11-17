using System;
using System.Collections.Generic;
using StateMachine;
using Tools;
using NUnit.Framework;

namespace Tools.Samples.StateMachineTests
{
    public sealed class StateMachineTests
    {
        [Test]
        [Description("generic<int> converts output and cancels entry work before Exit")]
        public void IntTransition()
        {
            var source = new Probe<int>(1);
            var target = new Probe<int>(2);
            using var machine = new StateMachine<int>(new[] { source, target }, new Routes<int>());
            machine.Start(1, new Input(3));
            var original = source.Context;
            source.Context.RequestTransition(2, new Output(7));
            Assert(target.Value == 7 && source.Exits == 1, "Output was not converted into the destination input.");
            Assert(original.Cancellation.IsCancellationRequested, "Previous entry was not cancelled.");
            Assert(machine.CurrentState == target, "Target state did not become current.");
        }

        [Test]
        [Description("enum identities work directly")]
        public void EnumTransition()
        {
            var source = new Probe<Id>(Id.Menu);
            var target = new Probe<Id>(Id.Game);
            using var machine = new StateMachine<Id>(new[] { source, target }, new Routes<Id>());
            machine.Start(Id.Menu, new Input(0));
            source.Context.RequestTransition(Id.Game, new Output(11));
            Assert(target.Value == 11 && machine.CurrentState.StateId == Id.Game, "Enum identity was lost.");
        }

        [Test]
        [Description("Enter completes before Exit and duplicate requests are rejected")]
        public void DeferredTransition()
        {
            var events = new List<string>();
            var source = new Probe<int>(1, events);
            var first = new Probe<int>(2, events);
            var second = new Probe<int>(3, events);
            source.EnterAction = state =>
            {
                state.Context.RequestTransition(2, new Output(4));
                Throws<InvalidOperationException>(() =>
                {
                    state.Context.RequestTransition(3, new Output(8));
                });
                Assert(state.Exits == 0 && first.Enters == 0, "Transition ran inside Enter.");
            };
            using var machine = new StateMachine<int>(new[] { source, first, second }, new Routes<int>());
            machine.StateEntered += id =>
            {
                if (id == 1)
                {
                    Throws<InvalidOperationException>(() =>
                    {
                        source.Context.RequestTransition(3, new Output(9));
                    });
                }
            };
            machine.Start(1, new Input(0));
            Assert(string.Join(",", events) == "1.begin,1.end,1.exit,2.begin,2.end", "Lifecycle order changed.");
            Assert(first.Value == 4 && second.Enters == 0, "A later request replaced the first request.");
        }

        [Test]
        [Description("unknown route and wrong payload preserve the current state")]
        public void InvalidTransition()
        {
            var source = new Probe<int>(1);
            var target = new Probe<int>(2);
            var routes = new Routes<int>();
            using var machine = new StateMachine<int>(new[] { source, target }, routes);
            machine.Start(1, new Input(0));
            routes.Convert = (_, _, _) =>
            {
                throw new InvalidOperationException("Route is not allowed.");
            };
            Throws<InvalidOperationException>(() =>
            {
                source.Context.RequestTransition(2, new Output(1));
            });
            Assert(source.Exits == 0 && machine.CurrentState == source, "Rejected route exited the source.");
            routes.Convert = (_, _, _) =>
            {
                return new OtherInput();
            };
            Throws<ArgumentException>(() =>
            {
                source.Context.RequestTransition(2, new Output(1));
            });
            Assert(source.Exits == 0 && !source.Context.Cancellation.IsCancellationRequested, "Wrong input invalidated the source.");
            routes.Convert = (_, _, output) =>
            {
                return new Input(((Output)output).Value);
            };
            Throws<ArgumentException>(() =>
            {
                source.Context.RequestTransition(999, new Output(1));
            });
            source.Context.RequestTransition(2, new Output(2));
            Assert(target.Value == 2, "A rejected transition prevented a later valid request.");
        }

        [Test]
        [Description("Stop inside Enter defers Exit and prevents another entry")]
        public void StopDuringEnter()
        {
            var events = new List<string>();
            var source = new Probe<int>(1, events);
            var target = new Probe<int>(2, events);
            using var machine = new StateMachine<int>(new[] { source, target }, new Routes<int>());
            source.EnterAction = state =>
            {
                state.Context.RequestTransition(2, new Output(0));
                machine.Stop();
                Assert(state.Context.Cancellation.IsCancellationRequested, "Stop did not cancel the active entry.");
                Assert(state.Exits == 0, "Stop invoked Exit before Enter returned.");
            };
            machine.Start(1, new Input(0));
            Assert(string.Join(",", events) == "1.begin,1.end,1.exit", "Stop reordered or duplicated lifecycle.");
            Assert(machine.CurrentState == null && target.Enters == 0, "Stopped machine entered its pending destination.");
        }

        [Test]
        [Description("Enter failure exits once and stops the machine")]
        public void EnterFailure()
        {
            var source = new Probe<int>(1);
            source.EnterAction = _ =>
            {
                throw new InvalidOperationException("Expected Enter failure.");
            };
            var machine = new StateMachine<int>(new[] { source }, new Routes<int>());
            Throws<InvalidOperationException>(() =>
            {
                machine.Start(1, new Input(0));
            });
            Assert(source.Exits == 1 && machine.CurrentState == null, "Failed entry was not cleaned up.");
            Assert(source.Context.Cancellation.IsCancellationRequested, "Failed entry retained a live context.");
            machine.Dispose();
            Assert(source.Exits == 1, "Dispose exited a failed state twice.");
        }

        [Test]
        [Description("Exit failure cannot enter the destination or exit twice")]
        public void ExitFailure()
        {
            var source = new Probe<int>(1);
            var target = new Probe<int>(2);
            source.ExitAction = _ =>
            {
                throw new InvalidOperationException("Expected Exit failure.");
            };
            var machine = new StateMachine<int>(new[] { source, target }, new Routes<int>());
            machine.Start(1, new Input(0));
            Throws<InvalidOperationException>(() =>
            {
                source.Context.RequestTransition(2, new Output(0));
            });
            Assert(source.Exits == 1 && target.Enters == 0 && machine.CurrentState == null,
                "Exit failure entered a destination or repeated Exit.");
            machine.Dispose();
            Assert(source.Exits == 1, "Dispose repeated a failed Exit.");
        }

        [Test]
        [Description("Start is one-shot and Dispose is idempotent")]
        public void StartOnce()
        {
            var state = new Probe<int>(1);
            var machine = new StateMachine<int>(new[] { state }, new Routes<int>());
            Throws<ArgumentException>(() =>
            {
                machine.Start(1, new OtherInput());
            });
            machine.Start(1, new Input(0));
            Throws<InvalidOperationException>(() =>
            {
                machine.Start(1, new Input(0));
            });
            machine.Dispose();
            machine.Dispose();
            Assert(state.Enters == 1 && state.Exits == 1, "Repeated start/dispose changed lifecycle.");
        }

        [Test]
        [Description("long Enter-request chain does not recurse")]
        public void LongTransitionChain()
        {
            var state = new Probe<int>(1);
            state.EnterAction = current =>
            {
                if (current.Enters < 2000)
                {
                    current.Context.RequestTransition(1, new Output(current.Enters));
                }
            };
            using var machine = new StateMachine<int>(new[] { state }, new Routes<int>());
            machine.Start(1, new Input(0));
            Assert(state.Enters == 2000 && state.Exits == 1999, "Long transition chain did not finish.");
            Assert(state.MaximumEnterDepth == 1, "Enter requests recursed.");
        }

        [Test]
        [Description("FlipBool reports only actual changes through TrySet")]
        public void FlipBoolChanges()
        {
            var flag = new FlipBool(false);
            Assert(!flag.TrySet(false) && !flag.Value, "An unchanged false flag reported a change.");
            Assert(flag.TrySet(true) && flag.Value, "Setting true did not report a change.");
            Assert(!flag.TrySet(true) && flag.Value, "An unchanged true flag reported a change.");
            Assert(flag.TrySet(false) && !flag.Value, "Resetting false did not report a change.");
        }

        [Test]
        [Description("public requests use the same synchronous transition handling")]
        public void PublicRequests()
        {
            var source = new Probe<int>(1);
            var target = new Probe<int>(2);
            using var machine = new StateMachine<int>(new[] { source, target }, new Routes<int>());
            Throws<InvalidOperationException>(() =>
            {
                machine.RequestTransition(2, new Output(1));
            });
            machine.Start(1, new Input(0));
            Throws<ArgumentException>(() =>
            {
                machine.RequestTransition(99, new Output(1));
            });
            Assert(source.Exits == 0 && machine.CurrentState == source,
                "Invalid public destination left the active state.");
            machine.RequestTransition(2, new Output(7));
            Assert(target.Value == 7 && source.Exits == 1 && machine.CurrentState == target,
                "Public request did not use the resolver and synchronous transition lifecycle.");
            machine.Stop();
            Throws<InvalidOperationException>(() =>
            {
                machine.RequestTransition(1, new Output(0));
            });
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new AssertionException(message);
            }
        }

        private static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new AssertionException($"Expected {typeof(TException).Name}.");
        }

        private enum Id
        {
            Menu,
            Game
        }

        private readonly struct Input : IStatePayload
        {
            public int Value { get; }

            public Input(int value)
            {
                Value = value;
            }
        }

        private readonly struct OtherInput : IStatePayload
        {
        }

        private readonly struct Output : IStateOutput
        {
            public int Value { get; }

            public Output(int value)
            {
                Value = value;
            }
        }

        private sealed class Routes<TId> : IStateTransitionResolver<TId> where TId : struct
        {
            public Func<TId, TId, IStateOutput, IStatePayload> Convert;

            public IStatePayload Resolve(TId source, TId destination, IStateOutput output)
            {
                if (Convert != null)
                {
                    return Convert(source, destination, output);
                }

                return new Input(((Output)output).Value);
            }
        }

        private sealed class Probe<TId> : StateBase<TId, Input, Output> where TId : struct
        {
            private readonly TId _id;
            private readonly List<string> _events;
            private int _enterDepth;

            public int Enters;
            public int Exits;
            public int Value;
            public int MaximumEnterDepth;
            public StateContext<TId, Output> Context;
            public Action<Probe<TId>> EnterAction;
            public Action<Probe<TId>> ExitAction;

            public override TId StateId => _id;

            public Probe(TId id, List<string> events = null)
            {
                _id = id;
                _events = events;
            }

            protected override void OnEnter(Input payload, StateContext<TId, Output> context)
            {
                Enters++;
                _enterDepth++;
                MaximumEnterDepth = Math.Max(MaximumEnterDepth, _enterDepth);
                Value = payload.Value;
                Context = context;
                _events?.Add(_id + ".begin");

                try
                {
                    EnterAction?.Invoke(this);
                    _events?.Add(_id + ".end");
                }
                finally
                {
                    _enterDepth--;
                }
            }

            protected override void OnExit()
            {
                Exits++;
                _events?.Add(_id + ".exit");
                Assert(Context.Cancellation.IsCancellationRequested, "Exit ran with a live context.");
                ExitAction?.Invoke(this);
            }
        }
    }
}

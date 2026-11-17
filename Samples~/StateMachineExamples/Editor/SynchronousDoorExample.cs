using System;
using StateMachine;
using UnityEditor;
using UnityEngine;

namespace FatLarva.Tools.Samples.StateMachines
{
    public static class SynchronousDoorExample
    {
        [MenuItem("Tools/State Machine Samples/Run Synchronous Example")]
        public static void Run()
        {
            var closed = new ClosedState();
            var open = new OpenState();
            using var machine = new StateMachine<DoorId>(
                new StateBase<DoorId>[] { closed, open }, new DoorTransitions());

            machine.Start(DoorId.Closed, new ClosedInput(isLocked: false));

            // An external controller supplies the active Closed state's output.
            machine.RequestTransition(DoorId.Open, new ClosedOutput("external controller"));

            // The active Open state uses its own typed context for the return trip.
            open.Close(lockOnClose: true);

            // These operations have already finished: Enter/Exit are synchronous.
            machine.Stop();
            Debug.Log("[State machine sample] Synchronous example complete.");
        }

        private enum DoorId
        {
            Closed,
            Open
        }

        private readonly struct ClosedInput : IStatePayload
        {
            public bool IsLocked { get; }

            public ClosedInput(bool isLocked)
            {
                IsLocked = isLocked;
            }
        }

        private readonly struct ClosedOutput : IStateOutput
        {
            public string OpenedBy { get; }

            public ClosedOutput(string openedBy)
            {
                OpenedBy = openedBy;
            }
        }

        private readonly struct OpenInput : IStatePayload
        {
            public string OpenedBy { get; }

            public OpenInput(string openedBy)
            {
                OpenedBy = openedBy;
            }
        }

        private readonly struct OpenOutput : IStateOutput
        {
            public bool LockOnClose { get; }

            public OpenOutput(bool lockOnClose)
            {
                LockOnClose = lockOnClose;
            }
        }

        private sealed class ClosedState : StateBase<DoorId, ClosedInput, ClosedOutput>
        {
            public override DoorId StateId => DoorId.Closed;

            protected override void OnEnter(ClosedInput payload, StateContext<DoorId, ClosedOutput> context)
            {
                Debug.Log($"[State machine sample] Closed.Enter: locked={payload.IsLocked}.");
            }

            protected override void OnExit()
            {
                Debug.Log("[State machine sample] Closed.Exit.");
            }
        }

        private sealed class OpenState : StateBase<DoorId, OpenInput, OpenOutput>
        {
            private StateContext<DoorId, OpenOutput> _context;

            public override DoorId StateId => DoorId.Open;

            public void Close(bool lockOnClose)
            {
                _context.RequestTransition(DoorId.Closed, new OpenOutput(lockOnClose));
            }

            protected override void OnEnter(OpenInput payload, StateContext<DoorId, OpenOutput> context)
            {
                _context = context;
                Debug.Log($"[State machine sample] Open.Enter: opened by {payload.OpenedBy}.");
            }

            protected override void OnExit()
            {
                Debug.Log("[State machine sample] Open.Exit.");
            }
        }

        private sealed class DoorTransitions : IStateTransitionResolver<DoorId>
        {
            public IStatePayload Resolve(DoorId source, DoorId destination, IStateOutput output)
            {
                switch ((source, destination, output))
                {
                    case (DoorId.Closed, DoorId.Open, ClosedOutput closed):
                        return new OpenInput(closed.OpenedBy);

                    case (DoorId.Open, DoorId.Closed, OpenOutput open):
                        return new ClosedInput(open.LockOnClose);

                    default:
                        throw new ArgumentException($"Unsupported door transition {source} -> {destination}.");
                }
            }
        }
    }
}

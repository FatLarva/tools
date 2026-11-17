using System;
using System.Threading;
using System.Threading.Tasks;
using StateMachine;
using UnityEditor;
using UnityEngine;

namespace FatLarva.Tools.Samples.StateMachines
{
    public static class AsynchronousSessionExample
    {
        [MenuItem("Tools/State Machine Samples/Run Asynchronous Example")]
        public static async void Run()
        {
            // Unity menu commands have a void entry point. The owned flow below returns Task.
            try
            {
                await RunAsynchronousExampleAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static async Task RunAsynchronousExampleAsync()
        {
            var application = new DemoApplication();
            await application.RunAsync();
            Debug.Log("[State machine sample] Asynchronous example complete; all states have exited.");
        }

        private enum ScreenId
        {
            Loading,
            Menu,
            Game
        }

        private sealed class DemoApplication
        {
            private readonly CancellationTokenSource _stopping = new();
            private readonly StateMachineAsync<ScreenId> _machine;

            public DemoApplication()
            {
                _machine = new StateMachineAsync<ScreenId>(new StateBaseAsync<ScreenId>[]
                {
                    new LoadingState(),
                    new MenuState(),
                    new GameState()
                }, new SessionTransitions());
            }

            public async Task RunAsync()
            {
                _machine.StateEntered += OnStateEntered;
                try
                {
                    await _machine.RunAsync(ScreenId.Loading, new LoadingInput("Ada"), _stopping.Token);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                    // RunAsync reports cancellation after the active state's Exit has finished.
                }
                finally
                {
                    _machine.StateEntered -= OnStateEntered;
                    _stopping.Dispose();
                }
            }

            private void OnStateEntered(ScreenId state)
            {
                if (state == ScreenId.Menu)
                {
                    // This stands in for an external Play command. Output belongs to Menu.
                    _machine.RequestTransition(ScreenId.Game, new MenuOutput(selectedLevel: 3));
                }
                else if (state == ScreenId.Game)
                {
                    // The finite demo stops here; its owner is still awaiting RunAsync.
                    _stopping.Cancel();
                }
            }
        }

        private readonly struct LoadingInput : IStatePayload
        {
            public string AccountName { get; }

            public LoadingInput(string accountName)
            {
                AccountName = accountName;
            }
        }

        private readonly struct LoadingOutput : IStateOutput
        {
            public string PlayerName { get; }

            public LoadingOutput(string playerName)
            {
                PlayerName = playerName;
            }
        }

        private readonly struct MenuInput : IStatePayload
        {
            public string Greeting { get; }

            public MenuInput(string greeting)
            {
                Greeting = greeting;
            }
        }

        private readonly struct MenuOutput : IStateOutput
        {
            public int SelectedLevel { get; }

            public MenuOutput(int selectedLevel)
            {
                SelectedLevel = selectedLevel;
            }
        }

        private readonly struct GameInput : IStatePayload
        {
            public int Level { get; }

            public GameInput(int level)
            {
                Level = level;
            }
        }

        private readonly struct GameOutput : IStateOutput
        {
        }

        private sealed class LoadingState : StateBaseAsync<ScreenId, LoadingInput, LoadingOutput>
        {
            public override ScreenId StateId => ScreenId.Loading;

            protected override async ValueTask OnEnterAsync(LoadingInput payload,
                StateContext<ScreenId, LoadingOutput> context)
            {
                Debug.Log("[State machine sample] Loading.Enter: waiting for a simulated profile load.");
                await Task.Delay(100, context.Cancellation);

                // No state can leave Loading before the awaited operation finishes.
                context.RequestTransition(ScreenId.Menu, new LoadingOutput(payload.AccountName));
                Debug.Log("[State machine sample] Loading.Enter complete; Menu was requested through context.");
            }

            protected override ValueTask OnExitAsync()
            {
                Debug.Log("[State machine sample] Loading.Exit.");
                return default;
            }
        }

        private sealed class MenuState : StateBaseAsync<ScreenId, MenuInput, MenuOutput>
        {
            private Task _previewTask;

            public override ScreenId StateId => ScreenId.Menu;

            protected override ValueTask OnEnterAsync(MenuInput payload, StateContext<ScreenId, MenuOutput> context)
            {
                Debug.Log($"[State machine sample] Menu.Enter: {payload.Greeting}");
                _previewTask = RunPreviewAsync(context.Cancellation);
                return default;
            }

            protected override async ValueTask OnExitAsync()
            {
                Debug.Log("[State machine sample] Menu.Exit: waiting for the owned preview task.");
                if (_previewTask != null)
                {
                    await _previewTask;
                    _previewTask = null;
                }

                Debug.Log("[State machine sample] Menu.Exit complete.");
            }

            private static async Task RunPreviewAsync(CancellationToken cancellation)
            {
                Debug.Log("[State machine sample] Menu preview started and remains owned by Menu.");
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Debug.Log("[State machine sample] Menu preview received cancellation; cleanup is still running.");
                }

                // Simulated asynchronous resource release must finish even after cancellation.
                await Task.Delay(100);
                Debug.Log("[State machine sample] Menu preview cleanup finished.");
            }
        }

        private sealed class GameState : StateBaseAsync<ScreenId, GameInput, GameOutput>
        {
            public override ScreenId StateId => ScreenId.Game;

            protected override ValueTask OnEnterAsync(GameInput payload, StateContext<ScreenId, GameOutput> context)
            {
                Debug.Log($"[State machine sample] Game.Enter: level {payload.Level}; Menu cleanup has finished.");
                return default;
            }

            protected override ValueTask OnExitAsync()
            {
                Debug.Log("[State machine sample] Game.Exit: application shutdown.");
                return default;
            }
        }

        private sealed class SessionTransitions : IStateTransitionResolver<ScreenId>
        {
            public IStatePayload Resolve(ScreenId source, ScreenId destination, IStateOutput output)
            {
                switch ((source, destination, output))
                {
                    case (ScreenId.Loading, ScreenId.Menu, LoadingOutput loading):
                        return new MenuInput($"Welcome, {loading.PlayerName}!");

                    case (ScreenId.Menu, ScreenId.Game, MenuOutput menu):
                        return new GameInput(menu.SelectedLevel);

                    default:
                        throw new ArgumentException($"Unsupported session transition {source} -> {destination}.");
                }
            }
        }
    }
}

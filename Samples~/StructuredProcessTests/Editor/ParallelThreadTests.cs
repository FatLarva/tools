#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tools.Samples.StructuredProcessTests
{
    public sealed class ParallelThreadTests
    {
        [Test]
        public async Task MainStepsOverlapAndResumeOnUnityThread()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var left = new StepControl();
            var right = new StepControl();
            var probe = new ParallelProbe();
            var running = new ForkJoinProcess(new ForkJoinProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await ParallelProcessTests.Wait(Task.WhenAll(left.Started.Task, right.Started.Task));
                left.Release.SetResult(1);
                right.Release.SetResult(2);
                await running;
                Assert.That(new[] { left.ConstructorThread, right.ConstructorThread, left.EnterThread,
                    right.EnterThread, left.ExitThread, right.ExitThread, probe.ConsumerThread }, Is.All.EqualTo(mainThread));
            }
            finally
            {
                left.Unblock();
                right.Unblock();
                await ParallelProcessTests.Drain(running);
            }
        }

        [Test]
        public async Task AnyStepConstructsAndExecutesOffMainThenFeedsMainConsumer()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var control = new StepControl();
            var probe = new ParallelProbe();
            var running = new BackgroundProcess(new BackgroundProcess.Input { Control = control, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await ParallelProcessTests.Wait(control.Started.Task);
                control.Release.SetResult(9);
                await running;
                Assert.That(control.ConstructorThread, Is.Not.EqualTo(mainThread));
                Assert.That(control.EnterThread, Is.Not.EqualTo(mainThread));
                Assert.That(control.ExitThread, Is.Not.EqualTo(mainThread));
                Assert.That(probe.ConsumerThread, Is.EqualTo(mainThread));
            }
            finally
            {
                control.Unblock();
                await ParallelProcessTests.Drain(running);
            }
        }
    }
}
#endif

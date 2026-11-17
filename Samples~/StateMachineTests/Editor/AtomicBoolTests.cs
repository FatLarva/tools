using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tools.Samples.StateMachineTests
{
    public sealed class AtomicBoolTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [TestCase(false)]
        [TestCase(true)]
        public void TryFlipReportsOnlyAnActualChange(bool initialValue)
        {
            var flag = new AtomicBool(initialValue);
            Assert.That(flag.Value, Is.EqualTo(initialValue));
            Assert.That(flag.TryFlip(initialValue), Is.False);
            Assert.That(flag.TryFlip(!initialValue), Is.True);
            Assert.That(flag.Value, Is.EqualTo(!initialValue));
            Assert.That(flag.TryFlip(!initialValue), Is.False);
            Assert.That(flag.TryFlip(initialValue), Is.True);
            Assert.That((bool)flag, Is.EqualTo(initialValue));

            flag.Set(!initialValue);
            Assert.That(flag.Value, Is.EqualTo(!initialValue));
            Assert.That(flag.TryFlip(initialValue), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConcurrentClaimsHaveExactlyOneWinner(bool targetValue)
        {
            const int workers = 16;
            var flag = new AtomicBool(!targetValue);
            using var start = new ManualResetEventSlim();
            var attempts = new Task<bool>[workers];
            for (int index = 0; index < workers; index++)
            {
                attempts[index] = Task.Run(() =>
                {
                    start.Wait();
                    return flag.TryFlip(targetValue);
                });
            }

            start.Set();
            Assert.That(Task.WaitAll(attempts, Timeout), Is.True, "Concurrent claims did not finish.");

            int winners = 0;
            foreach (var attempt in attempts)
            {
                if (attempt.Result)
                {
                    winners++;
                }
            }

            Assert.That(winners, Is.EqualTo(1));
            Assert.That(flag.Value, Is.EqualTo(targetValue));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OpposingFlipsPreserveTheClaimedFinalValue(bool initialValue)
        {
            const int rounds = 256;
            for (int round = 0; round < rounds; round++)
            {
                var flag = new AtomicBool(initialValue);
                using var start = new ManualResetEventSlim();
                Task<bool> restore = Task.Run(() =>
                {
                    start.Wait();
                    return flag.TryFlip(initialValue);
                });
                Task<bool> change = Task.Run(() =>
                {
                    start.Wait();
                    return flag.TryFlip(!initialValue);
                });

                start.Set();
                Assert.That(Task.WaitAll(new Task[] { restore, change }, Timeout), Is.True,
                    "Opposing flips did not finish.");

                // The opposite change must happen once. Restoring can succeed only after it.
                Assert.That(change.Result, Is.True);
                bool expectedValue = restore.Result ? initialValue : !initialValue;
                Assert.That(flag.Value, Is.EqualTo(expectedValue),
                    "TryFlip reported a successful change without installing its requested value.");
            }
        }
    }
}

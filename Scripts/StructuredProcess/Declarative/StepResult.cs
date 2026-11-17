using System;

namespace Tools.StructuredProcess.Declarative
{
    /// <summary>A typed outcome shared by steps and the complete process.</summary>
    public readonly struct StepResult<TOutput> where TOutput : struct, IProcessOutput
    {
        private readonly TOutput _output;
        private readonly string _error;

        public bool IsSuccess { get; }
        public string Error => IsSuccess ? null : _error ?? "An uninitialized result was returned.";
        public Exception Exception { get; }

        public TOutput Output
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException("A failed result has no output.");
                }

                return _output;
            }
        }

        private StepResult(bool isSuccess, TOutput output, string error, Exception exception)
        {
            IsSuccess = isSuccess;
            _output = output;
            _error = error;
            Exception = exception;
        }

        public static StepResult<TOutput> Success(TOutput output)
        {
            return new StepResult<TOutput>(true, output, null, null);
        }

        public static StepResult<TOutput> Failure(string error, Exception exception = null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("A failure must have a reason.", nameof(error));
            }

            return new StepResult<TOutput>(false, default, error, exception);
        }

        public static StepResult<TOutput> Failure(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return Failure(exception.Message.Length == 0 ? exception.GetType().Name : exception.Message, exception);
        }
    }
}

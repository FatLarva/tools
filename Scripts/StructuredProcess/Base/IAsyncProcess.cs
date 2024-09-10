using System.Threading;
using System.Threading.Tasks;
using Processes.Base;

namespace Tools.StructuredProcess
{
	public interface IAsyncProcess<TResult>
		where TResult : IAsyncProcessResult
	{
		Task<TResult> ExecuteAsync(CancellationToken token);
	}
}

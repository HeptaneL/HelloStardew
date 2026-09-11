using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace HelloStardew.Bridge;

/// <summary>
/// Marshals work from background threads (HTTP handlers) onto the game's main thread.
/// Enqueue a call from any thread, then pump the queue from the game loop.
/// </summary>
internal sealed class MainThreadDispatcher
{
	private readonly ConcurrentQueue<Action> _queue = new();

	/// <summary>Run one queued item. Must be called on the game's main thread (e.g. from UpdateTicked).</summary>
	public void Pump()
	{
		while (_queue.TryDequeue(out Action? action))
		{
			// Exceptions are surfaced to the caller through the task; never let one kill the game loop.
			try
			{
				action();
			}
			catch (Exception)
			{
				// Handled by the awaiting caller.
			}
		}
	}

	/// <summary>Run a function on the game's main thread and block until it completes.</summary>
	/// <param name="func">The work to run. It must only touch game state.</param>
	/// <param name="timeoutMs">How long to wait before giving up.</param>
	public T Invoke<T>(Func<T> func, int timeoutMs = 5000)
	{
		TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

		_queue.Enqueue(() =>
		{
			try
			{
				completion.TrySetResult(func());
			}
			catch (Exception ex)
			{
				completion.TrySetException(ex);
			}
		});

		try
		{
			completion.Task.Wait(timeoutMs);
		}
		catch (AggregateException ex) when (ex.InnerException is not null)
		{
			// Rethrow the original exception (e.g. CalendarException) instead of the AggregateException wrapper.
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
		}

		if (!completion.Task.IsCompleted)
			throw new TimeoutException($"The game did not process the request within {timeoutMs}ms.");

		return completion.Task.GetAwaiter().GetResult();
	}
}

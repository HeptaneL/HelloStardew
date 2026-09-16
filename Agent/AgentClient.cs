using System.Net.Http;
using System.Net.Http.Json;

namespace HelloStardew.Agent;

/// <summary>Talks to the local agent that produces a character's spoken line.</summary>
internal sealed class AgentClient : IDisposable
{
	private readonly HttpClient _httpClient = new();

	/// <summary>Where the agent lives. Defaults to the local Python agent.</summary>
	public string Endpoint { get; set; } = "http://127.0.0.1:8000/chat";

	/// <summary>
	/// How long a single request may take before it is abandoned. The game keeps running while
	/// the request is in flight, so a stuck agent must not hang the conversation forever.
	/// </summary>
	public TimeSpan Timeout
	{
		get => this._httpClient.Timeout;
		set => this._httpClient.Timeout = value;
	}

	public async Task<string> ChatAsync(string character, string message)
	{
		ChatRequest request = new()
		{
			Character = character,
			Message = message
		};

		HttpResponseMessage response = await this._httpClient.PostAsJsonAsync(
			this.Endpoint,
			request
		);

		response.EnsureSuccessStatusCode();

		ChatResponse? result =
			await response.Content.ReadFromJsonAsync<ChatResponse>();

		return result?.Message ?? "I don't know what to say.";
	}

	public void Dispose() => this._httpClient.Dispose();

	private sealed class ChatRequest
	{
		public string Character { get; set; } = "";
		public string Message { get; set; } = "";
	}

	private sealed class ChatResponse
	{
		public string Character { get; set; } = "";
		public string Message { get; set; } = "";
	}
}

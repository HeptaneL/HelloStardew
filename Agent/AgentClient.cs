using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace HelloStardew.Agent;

internal sealed class AgentClient
{
	private readonly HttpClient _httpClient = new();

	public async Task<string> ChatAsync(string character, string message)
	{
		ChatRequest request = new()
		{
			Character = character,
			Message = message
		};

		HttpResponseMessage response = await this._httpClient.PostAsJsonAsync(
			"http://127.0.0.1:8000/chat",
			request
		);

		response.EnsureSuccessStatusCode();

		ChatResponse? result =
			await response.Content.ReadFromJsonAsync<ChatResponse>();

		return result?.Message ?? "I don't know what to say.";
	}

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

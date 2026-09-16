using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StardewValley;

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

	/// <summary>
	/// Build the id for a brand new conversation.
	/// </summary>
	/// <remarks>
	/// The agent keys its memory for a character on this string, so two conversations must never
	/// share one. Handing out a fresh id is the only way to start clean: the agent has no way to
	/// forget a thread, and a reused id would resurrect the earlier chat's history.
	/// </remarks>
	public static string NewThreadId(string character)
	{
		string slug = new(character.Where(char.IsLetterOrDigit).ToArray());
		if (slug.Length == 0)
			slug = "thread";

		// Stamped so ids sort by when the chat started, plus a salt so two chats starting in the
		// same second can't collide.
		string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
		string salt = Guid.NewGuid().ToString("N")[..6];

		return $"{slug.ToLowerInvariant()}-{stamp}-{salt}";
	}

	/// <summary>
	/// The language to ask for the reply in, as the code the agent expects: "en", "zh", and so on.
	/// </summary>
	/// <remarks>
	/// The game's own language is the answer, because that is the one the player reads every other
	/// line of the game in — and for Chinese it is the difference between a reply the player can
	/// read and one they cannot. The members of <see cref="LocalizedContentManager.LanguageCode"/>
	/// are already the codes the agent takes, so there is no table here to fall out of step with
	/// the languages the game ships. It is read per request rather than cached at startup so that
	/// a change in the options menu is picked up on the next line.
	/// </remarks>
	private static string CurrentLanguage()
	{
		return LocalizedContentManager.CurrentLanguageCode.ToString().ToLowerInvariant();
	}

	/// <summary>
	/// Ask the agent for a reply.
	/// </summary>
	/// <param name="threadId">
	/// The conversation this line belongs to. Reuse the id for every turn of one chat, and mint a
	/// new one once it ends.
	/// </param>
	public async Task<string> ChatAsync(string character, string message, string threadId)
	{
		ChatRequest request = new()
		{
			ThreadId = threadId,
			Character = character,
			Message = message,
			Language = CurrentLanguage()
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

	// Names are pinned rather than left to the serializer's camelCase policy: the agent is a
	// Python service whose fields are snake_case, and "threadId" would be rejected as missing.
	private sealed class ChatRequest
	{
		[JsonPropertyName("thread_id")]
		public string ThreadId { get; set; } = "";

		[JsonPropertyName("character")]
		public string Character { get; set; } = "";

		[JsonPropertyName("message")]
		public string Message { get; set; } = "";

		[JsonPropertyName("language")]
		public string Language { get; set; } = "";
	}

	private sealed class ChatResponse
	{
		[JsonPropertyName("character")]
		public string Character { get; set; } = "";

		[JsonPropertyName("message")]
		public string Message { get; set; } = "";
	}
}

using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HelloStardew.Calendar;
using StardewModdingAPI;
using StardewValley;

namespace HelloStardew.Bridge;

/// <summary>A read-only HTTP API over the calendar queries, for the agent's MCP server to call.</summary>
internal sealed class HttpBridge : IDisposable
{
	/*********
	** Fields
	*********/

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	private readonly IMonitor _monitor;
	private readonly MainThreadDispatcher _dispatcher;
	private readonly HttpListener _listener = new();
	private readonly string _prefix;


	/*********
	** Public methods
	*********/

	public HttpBridge(IMonitor monitor, MainThreadDispatcher dispatcher, string bindAddress, int port)
	{
		this._monitor = monitor;
		this._dispatcher = dispatcher;
		this._prefix = $"http://{bindAddress}:{port}/";
	}

	/// <summary>Start listening. Safe to call before the game is fully loaded.</summary>
	public void Start()
	{
		try
		{
			this._listener.Prefixes.Add(this._prefix);
			this._listener.Start();
			_ = Task.Run(this.ListenLoop);
			this._monitor.Log($"Calendar HTTP API listening on {this._prefix}", LogLevel.Info);
		}
		catch (Exception ex)
		{
			this._monitor.Log($"Failed to start the Calendar HTTP API on {this._prefix}: {ex.Message}", LogLevel.Error);
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		try
		{
			if (this._listener.IsListening)
				this._listener.Stop();
			this._listener.Close();
		}
		catch
		{
			// Ignore shutdown races.
		}
	}


	/*********
	** Request handling
	*********/

	private async Task ListenLoop()
	{
		while (this._listener.IsListening)
		{
			HttpListenerContext context;
			try
			{
				context = await this._listener.GetContextAsync();
			}
			catch
			{
				break; // Listener was stopped.
			}

			_ = Task.Run(() => this.HandleRequest(context));
		}
	}

	private void HandleRequest(HttpListenerContext context)
	{
		try
		{
			ApiResponse response = this.Route(context.Request);
			WriteJson(context.Response, 200, response);
		}
		catch (CalendarException ex)
		{
			WriteJson(context.Response, ex.Status, ApiResponse.Failure(ex.Code, ex.Message));
		}
		catch (TimeoutException ex)
		{
			WriteJson(context.Response, 503, ApiResponse.Failure("game_busy", ex.Message));
		}
		catch (Exception ex)
		{
			this._monitor.Log($"Calendar HTTP API request failed: {ex}", LogLevel.Error);
			WriteJson(context.Response, 500, ApiResponse.Failure("internal_error", ex.Message));
		}
	}

	private ApiResponse Route(HttpListenerRequest request)
	{
		string path = (request.Url?.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();
		NameValueCollection query = request.QueryString;

		switch (path)
		{
			case "":
			case "/health":
				return ApiResponse.Success(this.GetHealthData());

			case "/date":
				return ApiResponse.Success(this.Invoke(CalendarService.GetCurrentDate));

			case "/events/today":
				return this.WithDate(this.Invoke(CalendarService.GetEventsToday));

			case "/events/day":
				return this.WithDate(this.Invoke(() => CalendarService.GetEventsForDay(RequireSeason(query), RequireDay(query))));

			case "/birthdays/week":
				return this.WithDate(this.Invoke(() => CalendarService.GetBirthdaysForWeek(GetBool(query, "includePast"))));

			case "/birthdays/day":
				return this.WithDate(this.Invoke(() => CalendarService.GetBirthdaysForDay(RequireSeason(query), RequireDay(query))));

			case "/calendar":
				return this.WithDate(this.Invoke(() => CalendarService.GetSeasonCalendar(OptionalSeason(query))));

			default:
				throw new CalendarException("not_found", $"Unknown endpoint '{path}'.", status: 404);
		}
	}

	private T Invoke<T>(Func<T> func)
	{
		return this._dispatcher.Invoke(func);
	}

	private ApiResponse WithDate(object data)
	{
		return ApiResponse.Success(data) with { Date = this.Invoke(CalendarService.GetCurrentDate) };
	}

	private object GetHealthData()
	{
		return new
		{
			status = "ok",
			mod = "HelloStardew",
			version = "1.1.0",
			saveLoaded = Game1.hasLoadedGame && Game1.player is not null
		};
	}


	/*********
	** Query parsing
	*********/

	private static Season RequireSeason(NameValueCollection query)
	{
		string? raw = query["season"];
		if (string.IsNullOrWhiteSpace(raw))
			throw new CalendarException("missing_season", "Query parameter 'season' is required (spring/summer/fall/winter).");
		if (!CalendarService.TryParseSeason(raw, out Season season))
			throw new CalendarException("invalid_season", $"Unknown season '{raw}'. Expected spring, summer, fall, or winter.");
		return season;
	}

	private static Season OptionalSeason(NameValueCollection query)
	{
		return CalendarService.TryParseSeason(query["season"], out Season season)
			? season
			: throw new CalendarException("invalid_season", $"Unknown season '{query["season"]}'.");
	}

	private static int RequireDay(NameValueCollection query)
	{
		string? raw = query["day"];
		if (!int.TryParse(raw, out int day) || day < 1 || day > 28)
			throw new CalendarException("invalid_day", "Query parameter 'day' must be an integer between 1 and 28.");
		return day;
	}

	private static bool GetBool(NameValueCollection query, string key)
	{
		string? raw = query[key];
		return raw is not null && (raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase));
	}


	/*********
	** Response writing
	*********/

	private static void WriteJson(HttpListenerResponse response, int statusCode, ApiResponse payload)
	{
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
			response.StatusCode = statusCode;
			response.ContentType = "application/json; charset=utf-8";
			response.ContentLength64 = bytes.Length;
			response.OutputStream.Write(bytes, 0, bytes.Length);
		}
		catch
		{
			// Client disconnected; nothing useful to do.
		}
		finally
		{
			try { response.OutputStream.Close(); } catch { /* ignore */ }
			try { response.Close(); } catch { /* ignore */ }
		}
	}


	/*********
	** Response models
	*********/

	private sealed record ApiResponse(bool Ok, object? Data, DateInfo? Date, ApiError? Error)
	{
		public static ApiResponse Success(object? data) => new(true, data, null, null);
		public static ApiResponse Failure(string code, string? message) => new(false, null, null, new ApiError(code, message));
	}

	private sealed record ApiError(string Code, string? Message);
}

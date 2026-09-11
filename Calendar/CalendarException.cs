using System;

namespace HelloStardew.Calendar;

/// <summary>An error that maps to a specific HTTP status code in the bridge.</summary>
internal sealed class CalendarException : Exception
{
	/// <summary>A stable machine-readable error code for the agent.</summary>
	public string Code { get; }

	/// <summary>The HTTP status code to return.</summary>
	public int Status { get; }

	public CalendarException(string code, string? message = null, int status = 400)
		: base(message ?? code)
	{
		this.Code = code;
		this.Status = status;
	}
}

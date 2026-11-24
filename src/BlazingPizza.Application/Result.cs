using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazingPizza.Application;

public enum ResultStatus
{
	Ok,
	NotFound,
	Invalid,
	Unauthorized
}

/// <summary>
/// Outcome of an application-layer operation. Carries either a value or a list of errors,
/// and serializes cleanly to JSON so any client (web, console, service) can consume it.
/// </summary>
public sealed record Result<T>
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() },
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public ResultStatus Status { get; init; }

	public bool IsSuccess => Status == ResultStatus.Ok;

	public T? Value { get; init; }

	public IReadOnlyList<string> Errors { get; init; } = [];

	public static Result<T> Ok(T value) => new() { Status = ResultStatus.Ok, Value = value };

	public static Result<T> NotFound(string error) => Fail(ResultStatus.NotFound, [error]);

	public static Result<T> Invalid(IReadOnlyList<string> errors) => Fail(ResultStatus.Invalid, errors);

	public static Result<T> Unauthorized() => Fail(ResultStatus.Unauthorized, ["A signed-in user is required."]);

	private static Result<T> Fail(ResultStatus status, IReadOnlyList<string> errors) => new() { Status = status, Errors = errors };

	public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Stena.Canvas.Domain;

public partial record WallSpec(
	[property: Required, StringLength(64, MinimumLength = 1)] string Name,
	[property: Range(1, 1024)] int Width,
	[property: Range(1, 1024)] int Height,
	[property: Required, Length(2, 256)] string[] Palette,
	[property: Range(1, 3600)] int ChargeSeconds,
	[property: Range(1, 100)] int MaxCharges,
	DateTimeOffset StartsAt,
	DateTimeOffset EndsAt
) : IValidatableObject
{
	public IEnumerable<ValidationResult> Validate(ValidationContext context)
	{
		if (EndsAt <= StartsAt)
		{
			yield return new ValidationResult("EndsAt must be after StartsAt", [nameof(EndsAt)]);
		}

		if (Palette.Any(c => c is null || !Color().IsMatch(c)))
		{
			yield return new ValidationResult("Colors must look like #rrggbb", [nameof(Palette)]);
		}
	}

	[GeneratedRegex("^#[0-9a-f]{6}$")]
	private static partial Regex Color();
}

using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace OrderManagementAPI.Validators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class PriceRangeAttribute : ValidationAttribute
{
    private readonly decimal _min;
    private readonly decimal _max;

    public PriceRangeAttribute(double min, double max)
    {
        _min = (decimal)min;
        _max = (decimal)max;

        ErrorMessage = $"Price must be between {_min.ToString("C2", CultureInfo.CurrentCulture)} " +
                       $"and {_max.ToString("C2", CultureInfo.CurrentCulture)}.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;

        if (value is IConvertible conv &&
            decimal.TryParse(conv.ToString(CultureInfo.InvariantCulture), NumberStyles.Number,
                             CultureInfo.InvariantCulture, out var price))
        {
            if (price >= _min && price <= _max)
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult(ErrorMessage);
    }
}

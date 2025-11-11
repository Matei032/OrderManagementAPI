using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using OrderManagementAPI.Features.Orders;

namespace OrderManagementAPI.Validators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class OrderCategoryAttribute : ValidationAttribute
{
    private readonly OrderCategory[] _allowed;

    public OrderCategoryAttribute(params OrderCategory[] allowed)
    {
        _allowed = allowed;
        ErrorMessage = $"Category must be one of: {string.Join(", ", _allowed.Select(c => c.ToString()))}.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return new ValidationResult(ErrorMessage);

        OrderCategory category;

        if (value is OrderCategory oc)
        {
            category = oc;
        }
        else if (value is int i)
        {
            category = (OrderCategory)i;
        }
        else if (!Enum.TryParse(value.ToString(), ignoreCase: true, out category))
        {
            return new ValidationResult(ErrorMessage);
        }

        if (_allowed.Contains(category))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessage);
    }
}

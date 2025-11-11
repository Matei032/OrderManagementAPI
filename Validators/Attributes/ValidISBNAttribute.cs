using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace OrderManagementAPI.Validators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ValidISBNAttribute : ValidationAttribute, IClientModelValidator
{
    public ValidISBNAttribute()
    {
        ErrorMessage = "ISBN must be 10 or 13 digits (hyphens and spaces allowed).";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;

        var raw = value.ToString() ?? string.Empty;
        var normalized = Normalize(raw);

        if (normalized.All(char.IsDigit) && (normalized.Length == 10 || normalized.Length == 13))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessage);
    }

    private static string Normalize(string s) => s.Replace("-", "").Replace(" ", "");

    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-validisbn", ErrorMessage ?? "Invalid ISBN format.");
    }

    private static bool MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (attributes.ContainsKey(key))
        {
            return false;
        }

        attributes.Add(key, value);
        return true;
    }
}

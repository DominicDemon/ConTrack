using System.ComponentModel.DataAnnotations;

namespace ContractManagement.Application.Common.Behaviors;

public static class ValidationBehavior
{
    public static void ValidateObject<T>(T instance) where T : notnull
    {
        var context = new ValidationContext(instance);
        Validator.ValidateObject(instance, context, validateAllProperties: true);
    }
}


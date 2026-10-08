using System.Reflection;

namespace Mammoth.Extensions.DependencyInjection;

// Share native DI's optional-default normalization across mapped, contextual keyed
// and diagnostic activation, including the net472 DateTime reflection workaround.
internal static class ParameterDefaultValue
{
    internal static bool TryGetDefaultValue(ParameterInfo parameter, out object? value)
    {
        value = null;
        bool hasDefault;
        try { hasDefault = parameter.HasDefaultValue; }
        catch (FormatException) when (parameter.ParameterType == typeof(DateTime))
        {
            // .NET Framework reflection cannot read a default(DateTime) constant.
            value = default(DateTime);
            return true;
        }
        if (!hasDefault) return false;
        value = parameter.DefaultValue;
        var underlying = Nullable.GetUnderlyingType(parameter.ParameterType);
        if (value == null && parameter.ParameterType.IsValueType && underlying == null)
            // Array elements are zero-initialized even for structs with a public
            // parameterless constructor; optional defaults must not run user code.
            value = Array.CreateInstance(parameter.ParameterType, 1).GetValue(0);
        if (value != null && underlying?.IsEnum == true)
            value = Enum.ToObject(underlying, value);
        return true;
    }
}

namespace EasyForNetTool.Parsing;

using EasyForNetTool.Extensions;

/// <summary>
/// Base class for CLI argument parsers providing shared parsing utilities.
/// </summary>
public abstract class ParserBase
{
    /// <summary>
    /// Parses the raw CLI arguments into a typed <see cref="Argument"/>.
    /// </summary>
    public abstract Argument Parse(string[] args);

    //protected abstract void SetEndpointArguments(TArgument argument, Dictionary<string, string> endpointArguments);

    /// <summary>
    /// Sets properties on the argument object based on parsed options and argument metadata.
    /// </summary>
    protected void SetOptions(Enum argumentType, Argument argument, Dictionary<string, string> options)
    {
        var argumentInfoList = ArgumentInfo.Arguments()
            .Where(x => x.Type.Equals(argumentType))
            .ToList();

        if (argumentInfoList.Count == 0)
            throw new Exception($"No argument info object found for {argumentType}");
        if (argumentInfoList.Count > 1)
            throw new Exception($"More then one argument info objects found for {argumentType}");

        var argumentInfo = argumentInfoList[0];
        var usedOptions = new HashSet<string>();
        foreach (var opt in argumentInfo.Options)
        {
            if (!opt.IsInternal && (options.ContainsKey(opt.Name) || options.ContainsKey(opt.ShortName)))
            {
                var key = options.ContainsKey(opt.Name) ? opt.Name : opt.ShortName;
                SetProperty(argument, options[key], opt);
                usedOptions.Add(key);
            }
            else if (opt.IsInternal)
            {
                SetProperty(argument, opt.Default, opt);
            }
            else if (!opt.IsInternal && opt.Required)
                throw new UserFriendlyException($"{opt.ShortName} or {opt.Name} option can not be empty.");
        }
        foreach (var opt in options)
        {
            if (!usedOptions.Contains(opt.Key))
                throw new UserFriendlyException($"{opt.Key} is unknown option.");
        }
    }

    /// <summary>
    /// Sets a single property on the argument object from the option value.
    /// </summary>
    private static void SetProperty(Argument argument, string value, ArgumentOption opt)
    {
        value = opt.NormalizeMethod != null ? opt.NormalizeMethod.Invoke(value) : value;
        var propertyName = opt.PropertyName ?? opt.Name.Substring(2).ToPascalCase();
        var property = argument.GetType().GetProperty(propertyName);
        if (property == null)
            throw new Exception($"{propertyName} not found in {argument.GetType().FullName}");
        if (string.IsNullOrWhiteSpace(value))
            value = opt.Default;
        if (opt.Required && string.IsNullOrWhiteSpace(value))
            throw new UserFriendlyException($"{opt.ShortName} or {opt.Name} can not be empty.");
        if (property.PropertyType == typeof(bool))
        {
            if (!bool.TryParse(value, out var flag))
                throw new UserFriendlyException($"{opt.ShortName} or {opt.Name} must be true or false.");
            property.SetValue(argument, flag);
            return;
        }
        property.SetValue(argument, value);
    }

    /// <summary>
    /// Extracts the option arguments by skipping the command name.
    /// </summary>
    protected string[] GetOptions(string[] array)
    {
        if (array.Length == 1)
            throw new UserFriendlyException("Please provide options.");
        return SubArray(array, 1);
    }

    /// <summary>
    /// Returns a sub-array starting from the specified index.
    /// </summary>
    protected string[] SubArray(string[] array, int startIndex)
    {
        var subArray = new List<string>();
        for (var i = startIndex; i < array.Length; i++)
        {
            subArray.Add(array[i]);
        }
        return subArray.ToArray();
    }

    /// <summary>
    /// Converts an array of alternating key-value strings into a dictionary.
    /// </summary>
    protected Dictionary<string, string> ToKeyValue(string[] array)
    {
        if (array.Length % 2 != 0)
            throw new UserFriendlyException("Invalidate args.");

        var dict = new Dictionary<string, string>();
        for (var i = 0; i < array.Length; i += 2)
        {
            dict.Add(array[i], array[i + 1]);
        }
        return dict;
    }
}

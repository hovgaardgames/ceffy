namespace Ceffy.Bridge
{
    /// <summary>
    /// Converts C# member names to the naming convention used by the JavaScript bridge.
    /// </summary>
    public static class BridgeNaming
    {
        /// <summary>
        /// Converts a PascalCase or acronym-prefixed name to camelCase.
        /// </summary>
        public static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsUpper(value[0]))
                return value;

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var hasNext = i + 1 < chars.Length;
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                    break;

                chars[i] = char.ToLowerInvariant(chars[i]);

                if (hasNext && !char.IsUpper(chars[i + 1]))
                    break;
            }

            return new string(chars);
        }
    }
}

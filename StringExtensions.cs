namespace WebApplication1
{
    public static class StringExtensions
    {
        /// <summary>
        /// Truncates a string to <paramref name="maxLength"/> characters,
        /// appending "…" if the string was actually shortened.
        /// </summary>
        public static string Truncate(this string? value, int maxLength, string suffix = "…")
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength
                ? value
                : string.Concat(value.AsSpan(0, maxLength), suffix);
        }
    }
}

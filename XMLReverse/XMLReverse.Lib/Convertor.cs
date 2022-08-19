using System;

namespace XMLReverse.Lib
{
    public static class Convertor
    {
        public static bool IsBase64(string text)
        {
            if (!text.StartsWith("/9j/4AAQSkZJRg") && !text.EndsWith("="))
                return false;
            var buffer = new Span<byte>(new byte[text.Length]);
            return Convert.TryFromBase64String(text, buffer, out _);
        }

        public static bool IsBoolean(string text, out bool isYesNo)
        {
            var small = text.ToLowerInvariant();
            isYesNo = small.Equals("yes") || small.Equals("no");
            return isYesNo || bool.TryParse(text, out _);
        }

        public static bool IsUrl(string text)
            => Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
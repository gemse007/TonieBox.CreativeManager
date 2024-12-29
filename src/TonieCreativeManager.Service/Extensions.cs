using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;

namespace TonieCreativeManager.Service
{
    public static class Extensions
    {
        public static string GetParentPath(this string path) => path.Substring(0, Math.Max(path.LastIndexOf("/"), 0));

        public static string EncodeUrl(this string value) => HttpUtility.UrlEncode(value.Replace("+", "\\+").Replace(".","\\."));

        public static string DecodeUrl(this string value) => HttpUtility.UrlDecode(value.Replace("\\+", "%2b").Replace("\\.", "."));

        public static IEnumerable<string> GetCharacters(this string text)
        {
            if (text == null)
            {
                return Enumerable.Empty<string>();
            }

            var ca = text.ToCharArray();
            var characters = new List<string>();
            
            for (int i = 0; i < ca.Length; i++)
            {
                char c = ca[i];
                if (c > 65535)
                    continue;
                
                if (char.IsHighSurrogate(c))
                {
                    i++;
                    characters.Add(new string(new[] { c, ca[i] }));
                }
                else
                    characters.Add(new string(new[] { c }));
            }
            return characters;
        }

        public static string? ImageSvg(this string value) => string.IsNullOrWhiteSpace(value) ? null : $"data:image/svg+xml,<svg xmlns=\\'http://www.w3.org/2000/svg\\' viewBox=\\'0 0 30 30\\'>{value}</svg>";
    }
}

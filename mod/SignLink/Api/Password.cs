using System.Security.Cryptography;

namespace SignLink.Api
{
    /// <summary>The developer API's password rules (pure: mod/SignLink.Tests): at least 12 characters, plain ASCII
    /// without spaces, since the header is read as ASCII and trimmed; a new one is 18 random bytes as base64.</summary>
    internal static class Password
    {
        /// <summary>Why a password cannot be used (null: it can).</summary>
        public static string Problem(string password)
        {
            string p = password ?? "";
            if (p.Length < 12) return "is shorter than 12 characters";
            foreach (char c in p)
                if (c <= ' ' || c > '~') return "has spaces or characters other than plain letters, digits and symbols";
            return null;
        }

        /// <summary>Does the text hold the password (none set: no)? Paste refuses such a clipboard: on a sign it would be
        /// sent to every player and kept in the world save (0.20.132, review).</summary>
        public static bool In(string text, string password) =>
            !string.IsNullOrEmpty(password) && text != null && text.IndexOf(password, System.StringComparison.Ordinal) >= 0;

        public static string New()
        {
            var bytes = new byte[18];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return System.Convert.ToBase64String(bytes).Replace('+', 'k').Replace('/', 'z');
        }
    }
}

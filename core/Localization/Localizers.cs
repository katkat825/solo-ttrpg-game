using System.Collections.Generic;
using System.Globalization;

namespace Core.Localization
{
    // echoing a key is how a missing string shows up - hardcoded text reads as English, a key shows raw
    public sealed class KeyEchoLocalizer : ILocalizer
    {
        public static readonly KeyEchoLocalizer Instance = new KeyEchoLocalizer();

        KeyEchoLocalizer() { }

        public string Get(string key) => key;

        public string Format(string key, params object[] args) =>
            args == null || args.Length == 0
                ? key
                : key + "(" + string.Join(", ", args) + ")";

        public bool Has(string key) => true;
    }

    public sealed class DictionaryLocalizer : ILocalizer
    {
        readonly IReadOnlyDictionary<string, string> _strings;
        readonly ILocalizer _fallback;

        public DictionaryLocalizer(
            IReadOnlyDictionary<string, string> strings, ILocalizer fallback = null)
        {
            _strings = strings;
            _fallback = fallback ?? KeyEchoLocalizer.Instance;
        }

        public bool Has(string key) => _strings.ContainsKey(key);

        public string Get(string key) =>
            _strings.TryGetValue(key, out var v) ? v : _fallback.Get(key);

        public string Format(string key, params object[] args)
        {
            if (!_strings.TryGetValue(key, out var pattern))
                return _fallback.Format(key, args);

            return args == null || args.Length == 0
                ? pattern
                : string.Format(CultureInfo.CurrentCulture, pattern, args);
        }
    }
}

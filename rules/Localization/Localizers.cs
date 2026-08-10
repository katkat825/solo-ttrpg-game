using System.Collections.Generic;
using System.Globalization;

namespace Rules.Localization
{
    // the two ILocalizer implementations that need no engine behind them
    // one echoes the key, one reads an in-memory table
    // between them the tests and the sim never touch a translation server
    //
    // echoing is also how a missing key gets spotted in a running game
    // hardcoded text still reads as English, a key shows up as actor.barbarian.name
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

    // for tests that need real text, and the shape a campaign locale file loads into
    // an unknown key falls through to the fallback rather than throwing
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

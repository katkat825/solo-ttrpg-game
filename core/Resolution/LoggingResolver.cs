using System;
using System.Collections.Generic;
using Core.Dice;

namespace Core.Resolution
{
    public sealed class LoggingResolver : IResolver
    {
        readonly IResolver _inner;
        readonly List<string> _lines = new List<string>();
        readonly Action<string> _sink;

        public LoggingResolver(IResolver inner, Action<string> sink = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _sink = sink;
        }

        public IReadOnlyList<string> Lines => _lines;

        public void Clear() => _lines.Clear();

        public PoolResult Resolve(Pool pool)
        {
            var result = _inner.Resolve(pool);
            Write(result.ToString());
            return result;
        }

        public int RollImpact(Die impact, bool explodes = true)
        {
            int value = _inner.RollImpact(impact, explodes);
            Write($"impact {impact.Label()} -> {value}{(explodes ? "" : " (no explode)")}");
            return value;
        }

        void Write(string line)
        {
            _lines.Add(line);
            _sink?.Invoke(line);
        }
    }
}

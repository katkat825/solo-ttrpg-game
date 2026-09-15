using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Content.Monsters
{
    public sealed class Rosters : IArchetypeSource
    {
        readonly List<IArchetypeSource> _sources = new List<IArchetypeSource>();

        readonly List<string> _collisions = new List<string>();

        public Rosters(params IArchetypeSource[] sources)
        {
            foreach (IArchetypeSource source in sources ?? Array.Empty<IArchetypeSource>()) Add(source);
        }

        public void Add(IArchetypeSource source)
        {
            if (source == null) return;

            foreach (string id in source.Ids)
                if (_sources.Any(s => s.Has(id)))
                    _collisions.Add(id);

            _sources.Add(source);
        }

        // empty is expected; anything here is a namespacing failure upstream
        public IReadOnlyList<string> Collisions => _collisions;

        public IReadOnlyList<IArchetypeSource> Sources => _sources;

        // in add order, so the engine's roster is first and a report reads the way the game was assembled
        public IReadOnlyCollection<string> Ids =>
            _sources.SelectMany(s => s.Ids).Distinct(StringComparer.Ordinal).ToArray();

        public bool Has(string id) => _sources.Any(s => s.Has(id));

        // first wins; with scoped ids there's never a second, but "it depends on load order" is impossible to debug
        public Actor Create(string id)
        {
            foreach (IArchetypeSource source in _sources)
                if (source.Has(id)) return source.Create(id);

            throw new KeyNotFoundException($"No archetype '{id}'.");
        }

        public override string ToString() =>
            $"{_sources.Count} rosters, {Ids.Count} archetypes" +
            (_collisions.Count > 0 ? $", {_collisions.Count} COLLISIONS: {string.Join(", ", _collisions)}" : "");
    }
}

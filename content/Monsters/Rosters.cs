using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Content.Monsters
{
    // SEVERAL ROSTERS, ONE SEAM.
    //
    // The engine ships a shared roster - the Rabble and the Rival that belong to no campaign and
    // are un-prefixed because of it (`CONVENTIONS.md` section 7) - and every loaded campaign
    // brings its own, scoped by its id. Something has to put them side by side, and the honest
    // shape of that is another `IArchetypeSource`: everything downstream keeps holding the
    // interface and never learns that there is more than one behind it.
    //
    // IDS CANNOT COLLIDE, WHICH IS THE WHOLE POINT OF SCOPING THEM. A campaign's ids all begin
    // with its own unique id, so two subscribed campaigns' ghouls are `ashfall.ghoul` and
    // `other.ghoul` and this needs no rule about who wins. It says so anyway, out loud, because a
    // collision would mean the scoping was skipped somewhere and that is worth being told about
    // rather than resolved silently.
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

        // an id two rosters both claim. Empty is the expected state and anything in it is a
        // namespacing failure upstream (Campaigns/ContentId.cs)
        public IReadOnlyList<string> Collisions => _collisions;

        public IReadOnlyList<IArchetypeSource> Sources => _sources;

        // in the order they were added, so the engine's own roster is listed first and a report
        // reads the way the game was assembled
        public IReadOnlyCollection<string> Ids =>
            _sources.SelectMany(s => s.Ids).Distinct(StringComparer.Ordinal).ToArray();

        public bool Has(string id) => _sources.Any(s => s.Has(id));

        // FIRST ONE WINS, and with scoped ids there is never a second. Stated rather than left to
        // whichever enumeration order happened, because "it depends on load order" is exactly the
        // kind of rule that is impossible to debug from a bug report
        public Actor Create(string id)
        {
            foreach (IArchetypeSource source in _sources)
                if (source.Has(id)) return source.Create(id);

            throw new KeyNotFoundException($"No archetype '{id}'.");
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_sources.Count} rosters, {Ids.Count} archetypes" +
            (_collisions.Count > 0 ? $", {_collisions.Count} COLLISIONS: {string.Join(", ", _collisions)}" : "");
    }
}

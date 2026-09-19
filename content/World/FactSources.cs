using System;
using System.Collections.Generic;
using System.Linq;
using Content.Entities;
using Content.Places;
using Content.Quests;
using Core.Space;

namespace Content.World
{
    // WHO CAN EVER MAKE A FACT TRUE - not merely whether anything can.
    //
    // The reachability pass only ever needed the set, and asked it as one question: is there
    // anything at all behind this name. The soft-lock warning needs the other half of the same
    // derivation, because a fact can be writable today and unwritable forever after one swing: a
    // dead entity does not stand in its place any more (Place.On drops it), so every verb it was
    // offering goes with it. Talk to Bob, open Bob's strongbox, search Bob - all of them stop
    // existing the moment Bob does.
    //
    // So a source is either a LIVING one, meaning a verb on somebody who has to be alive to offer
    // it, or it is not. Killing is not a living source: the swing is what writes 'bob.dead', and
    // it survives Bob by definition.
    //
    // One derivation, two questions, so the two cannot drift - the same argument as FactName's
    // aspects. Derived, never listed.
    public sealed class FactSources
    {
        readonly Dictionary<string, HashSet<string>> _living =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // facts something other than a living entity's verb can write, which is most of them
        readonly HashSet<string> _outlives = new HashSet<string>(StringComparer.Ordinal);

        FactSources() { }

        // every fact anything in this campaign can ever make true
        public IEnumerable<string> All => _living.Keys.Concat(_outlives).Distinct(StringComparer.Ordinal);

        public bool Writable(string fact) =>
            fact != null && (_outlives.Contains(fact) || _living.ContainsKey(fact));

        // the one entity whose death takes this fact away for good, or empty when nothing does.
        // Two entities writing the same fact is enough to keep it reachable past either of them,
        // and one source that is not a person at all is enough to keep it past all of them.
        public string OnlyOnTheLivingOf(string fact)
        {
            if (fact == null || _outlives.Contains(fact)) return "";

            if (!_living.TryGetValue(fact, out HashSet<string> who) || who.Count != 1) return "";

            return who.First();
        }

        public static FactSources Of(EntityBook entities, PlaceBook places, QuestBook quests,
                                     RoadBook roads,
                                     IReadOnlyDictionary<string, MapLayout> maps = null)
        {
            var sources = new FactSources();

            foreach (Entity entity in entities?.All ?? Enumerable.Empty<Entity>())
                foreach (Interaction verb in entity.Verbs)
                {
                    string writes = verb.Writes(entity.Local);

                    if (writes.Length == 0) continue;

                    if (verb == Interaction.Attack) sources.Outliving(writes);
                    else sources.Alive(writes, entity.Local);
                }

            foreach (Place place in places?.All ?? Enumerable.Empty<Place>())
            {
                sources.Outliving(FactName.Visited(place.Id));

                if (place.IsAFight) sources.Outliving(FactName.Cleared(place.Id));

                // a standing that is somebody the author let you kill dies as a durable fact
                foreach (Standing standing in place.Standings)
                {
                    if (standing.IsAFoe) continue;

                    Entity entity = entities?.Of(standing.Entity);

                    if (entity != null && entity.Fights) sources.Outliving(FactName.Dead(entity.Local));
                }

                foreach (Trigger trigger in place.Triggers)
                    if (trigger.Sets.Length > 0) sources.Outliving(trigger.Sets);

                // a door an author opens with a fact; derived at both ends so they cannot disagree
                if (maps != null && maps.TryGetValue(place.Map, out MapLayout map))
                    foreach (Border border in map.Borders)
                        if (map.At(border) == Edge.Door)
                            sources.Outliving(Exploring.DoorFact(place.Id, border));
            }

            foreach (Quest quest in quests?.All ?? Enumerable.Empty<Quest>())
                sources.Outliving(quest.AcceptedFact);

            foreach (Road road in roads?.All ?? Enumerable.Empty<Road>())
                foreach (Happening happening in road.Wayside)
                {
                    if (happening.Once) sources.Outliving(happening.HappenedFact);

                    if (happening.Sets.Length > 0) sources.Outliving(happening.Sets);
                }

            return sources;
        }

        void Outliving(string fact)
        {
            if (!string.IsNullOrEmpty(fact)) _outlives.Add(fact);
        }

        void Alive(string fact, string entity)
        {
            if (string.IsNullOrEmpty(fact)) return;

            if (!_living.TryGetValue(fact, out HashSet<string> who))
                _living[fact] = who = new HashSet<string>(StringComparer.Ordinal);

            who.Add(entity);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{All.Count()} writable facts, {_living.Count} of them only while somebody lives";
    }
}

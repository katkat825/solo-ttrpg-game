using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Entities;
using Content.Items;
using Content.Places;
using Content.Quests;
using Core.Dice;
using Core.Space;

namespace Content.World
{
    public sealed class Exploring
    {
        readonly PlaceBook _places;
        readonly EntityBook _entities;
        readonly RoadBook _roads;
        readonly QuestBook _quests;
        readonly IReadOnlyDictionary<string, MapLayout> _maps;
        readonly IRng _rng;

        readonly Reactions _reactions = new Reactions();

        readonly List<Present> _present = new List<Present>();

        public Exploring(Package package, Facts facts, IRng rng)
            : this(package?.Id, package?.Places, package?.Entities, package?.Roads,
                   package?.Quests, package?.Maps, facts, rng)
        {
        }

        // the long form is what a test builds; the short one is what the game has
        public Exploring(string campaign, PlaceBook places, EntityBook entities, RoadBook roads,
                         QuestBook quests, IReadOnlyDictionary<string, MapLayout> maps,
                         Facts facts, IRng rng)
        {
            Campaign = campaign ?? "";
            _places = places ?? PlaceBook.Read(null);
            _entities = entities ?? EntityBook.Read(null, campaign);
            _roads = roads ?? RoadBook.Read(null);
            _quests = quests ?? QuestBook.Read(null);
            _maps = maps ?? new Dictionary<string, MapLayout>();
            _rng = rng;
            Facts = facts ?? Facts.For(ContentId.IsCampaign(campaign) ? campaign : "engine");
        }

        public string Campaign { get; }

        public Facts Facts { get; }

        public QuestBook Quests => _quests;

        // where the party is; null before the first Enter
        public Place Where { get; private set; }

        public MapLayout Map { get; private set; }

        public Cell Hero { get; private set; }

        // rebuilt on entry and after anything that changes a fact; never saved
        public IReadOnlyList<Present> OnTheTable => _present;

        public bool InAFight { get; private set; }

        public IEnumerable<Exit> Ways => Where == null
            ? Enumerable.Empty<Exit>()
            : Where.Ways(Facts);

        public IEnumerable<(Quest Quest, QuestState State)> Log => _quests.Log(Facts);


        // applies facts to the base map and returns the entered cues, plus fact cues already true on entry
        public Arrival Enter(string place, int arriving = 0)
        {
            Place going = _places.Of(place);

            if (going == null)
                return Arrival.No($"'{place}' is not a place in this campaign");

            if (!_maps.TryGetValue(going.Map, out MapLayout map))
                return Arrival.No($"'{going.Map}' is not a map in this campaign");

            Where = going;
            Map = Opened(map, going);
            InAFight = false;

            Cell? landing = Map.SpawnAt(arriving);

            Hero = landing ?? Map.Start;

            _reactions.Arrived();

            bool first = Facts.Set(FactName.Visited(going.Id));

            Rebuild();

            var arrival = new Arrival(going, Hero, first);

            foreach (Cue cue in _reactions.Cues(going, Facts, When.Entered)) arrival.Add(cue);

            Fire(going, When.Entered, arrival);

            // read after the entry triggers have run, because one of them may be what makes this
            // place the place the errand ends
            foreach (Quest quest in TurningInHere()) arrival.Nudges(quest.Id);

            return arrival;
        }

        // a door an author opened with a fact is open on the base map before anyone looks
        MapLayout Opened(MapLayout map, Place place)
        {
            foreach (Border border in map.Borders)
            {
                if (map.At(border) != Edge.Door) continue;

                // the fact is named for the place and the door's square, derived at both ends
                if (!Facts.Is(DoorFact(place.Id, border))) continue;

                // an open door is not a value on the line, it is the absence of one
                map = map.With(border, Edge.None);
            }

            return map;
        }

        // derived so a trigger that sets it and the map that reads it cannot be spelled differently
        public static string DoorFact(string place, Border border) =>
            FactName.Open($"{place}.door_{border.Cell.X}_{border.Cell.Y}_" +
                          (border.Vertical ? "v" : "h"));


        void Rebuild()
        {
            _present.Clear();

            if (Where == null) return;

            foreach (Standing standing in Where.On(Facts))
            {
                Cell? at = Map.SpawnAt(standing.Slot);

                if (at == null) continue;

                if (standing.IsAFoe)
                {
                    _present.Add(new Present(standing.Slot, at.Value, standing.Monster, null, null));
                    continue;
                }

                Entity what = _entities.Of(standing.Entity);

                if (what == null) continue;

                _present.Add(new Present(standing.Slot, at.Value, what.Local, what,
                                         what.Offers(Facts).ToArray()));
            }
        }

        // THE NUDGE: AN ACCEPTED QUEST THIS PLACE COULD MOVE ON.
        //
        // A quest does not say where it is turned in and must not - it is a named view over facts
        // and nothing else, which is the whole reason the log is never stored (Content.Quests). So
        // "turns in here" is DERIVED, the same way every other convenience in this layer is: what
        // can this place write, and is any quest waiting on one of those?
        //
        // What a place can write is a closed list because the verbs are: whatever the people
        // standing here still offer, whatever its own triggers set, and - where it is a fight - the
        // fact that clearing it writes. If none of those is a fact some accepted quest is waiting
        // on, there is nothing to mention and the companion stays quiet.
        public IEnumerable<Quest> TurningInHere()
        {
            if (Where == null) yield break;

            var writable = new HashSet<string>(Writable(), StringComparer.Ordinal);

            foreach ((Quest quest, QuestState state) in Log)
            {
                if (state != QuestState.Active) continue;

                // only what the turn-in is waiting on. A quest's other clauses are how it was
                // offered and how it fails, and neither is a reason to stop somebody in a doorway
                foreach (string fact in quest.Done.Facts)
                {
                    if (!writable.Contains(fact) || Facts.Is(fact)) continue;

                    yield return quest;
                    break;
                }
            }
        }

        IEnumerable<string> Writable()
        {
            foreach (Present present in _present)
            {
                if (present.IsAFoe) continue;

                foreach (Interaction verb in present.Offers)
                {
                    string writes = verb.Writes(present.What.Local);

                    if (writes.Length > 0) yield return writes;
                }
            }

            foreach (Trigger trigger in Where.Triggers)
                if (trigger.ThenDo == Then.Set && trigger.Sets.Length > 0)
                    yield return trigger.Sets;

            if (Where.IsAFight) yield return FactName.Cleared(Where.Id);
        }

        public Present At(Cell cell) => _present.Find(p => p.At == cell);

        public Present Standing(int slot) => _present.Find(p => p.Slot == slot);

        public bool Occupied(Cell cell) => cell != Hero && At(cell) != null;

        // the same pathfinder the fight uses, same occupancy rule; null where there is no way
        public IReadOnlyList<Cell> Route(Cell to) =>
            Map == null ? null : Core.Space.Route.Between(Map, Hero, to, Occupied);

        public bool CanSee(Cell cell) => Map != null && Sight.Clear(Map, Hero, cell);

        // exploration has no movement budget; that is a fight's rule, and this is not one
        public IReadOnlyList<Cell> Walk(Cell to)
        {
            IReadOnlyList<Cell> route = Route(to);

            if (route == null || route.Count == 0) return null;

            Hero = route[route.Count - 1];

            return route;
        }


        public Doing Do(Present it, Interaction verb)
        {
            if (it == null) return Doing.No(verb, "", "there is nothing there");

            if (InAFight) return Doing.No(verb, it.Id, "there is a fight going on");

            if (it.IsAFoe)
                return Doing.No(verb, it.Id,
                                "that is a monster standing on a map, not somebody to deal with");

            if (!it.Offering(verb))
                return Doing.No(verb, it.Id,
                                $"'{it.Id}' does not offer '{verb.Word()}' right now");

            Entity what = it.What;
            Doing did = Doing.Did(verb, what.Local);

            switch (verb)
            {
                case Interaction.Talk:
                    did.Playing(what.Named(verb));
                    break;

                case Interaction.Examine:
                    did.Saying(Cue.Narration(Campaign, what.Named(verb)));
                    break;

                case Interaction.Open:
                    break;

                case Interaction.Search:
                    string drawn = what.Loot.Draw(_rng);

                    if (drawn != null) did.Taking(drawn);
                    break;

                case Interaction.Attack:
                    did.Starting(Begin(it));
                    break;
            }

            // the verb's own fact, derived; the fight writes its own at the end instead
            string writes = verb.Writes(what.Local);

            if (verb != Interaction.Attack && writes.Length > 0 && Facts.Set(writes))
                did.Writing(writes);

            Settle(did);

            return did;
        }

        // everything still standing that could be fought joins, not just the one you swung at
        public Episode Begin(Present it)
        {
            if (Where == null) return null;

            var entities = new Dictionary<int, string>();

            int last = _present.Count == 0 ? 0 : _present.Max(p => p.Slot);
            var slots = new string[last];

            foreach (Present present in _present)
            {
                string monster;

                if (present.IsAFoe)
                {
                    monster = present.Id;
                }
                else if (it != null && present.Slot == it.Slot && present.What.Fights)
                {
                    // only the one you swung at joins as somebody; a bystander never marked attackable cannot become one
                    monster = present.What.Monster;
                    entities[present.Slot] = present.Id;
                }
                else continue;

                slots[present.Slot - 1] = ContentId.Scoped(Campaign, monster);
            }

            InAFight = true;

            return new Episode(Where.Id, slots, entities, it?.Slot ?? 0);
        }

        // turns the downed slots into durable facts and rebuilds the place; the body on the floor is not one
        public Ending Fought(Episode episode, bool won, IEnumerable<int> down = null)
        {
            InAFight = false;

            var ending = new Ending(won);

            if (episode == null || Where == null) return ending;

            int[] fell = (down ?? Array.Empty<int>()).Distinct().ToArray();

            foreach (int slot in fell.OrderBy(s => s))
            {
                if (!episode.Entities.TryGetValue(slot, out string entity)) continue;

                // death becomes a durable fact here; the engine remembers it, what it means is the author's
                if (Facts.Set(FactName.Dead(entity))) ending.Add(FactName.Dead(entity));
            }

            // a pre-armed room is cleared once and does not re-arm when you walk back in
            if (won && Where.IsAFight && Facts.Set(FactName.Cleared(Where.Id)))
                ending.Add(FactName.Cleared(Where.Id));

            Rebuild();

            foreach (Cue cue in _reactions.Cues(Where, Facts, When.Cleared)) ending.Add(cue);

            foreach (Trigger trigger in Where.Triggers)
            {
                if (trigger.WhenIt != When.Cleared || !won) continue;

                Apply(trigger, ending);
            }

            React(ending);

            return ending;
        }


        // a step through a door and a journey down a road are the same call; only the road can be interrupted
        public Going Take(Exit exit)
        {
            if (exit == null) return Going.No("there is no way there");

            if (!exit.Needs.Met(Facts)) return Going.No($"the way to '{exit.To}' is not open");

            if (!exit.IsAJourney) return new Going(exit.To, exit.Arriving, null, null);

            Road road = _roads.Of(exit.Road);

            if (road == null) return Going.No($"'{exit.Road}' is not a road in this campaign");

            Happening happening = road.Draw(_rng, Facts);

            if (happening == null) return new Going(exit.To, exit.Arriving, road, null);

            var going = new Going(happening.Stops ? happening.Place : exit.To,
                                  happening.Stops ? 0 : exit.Arriving, road, happening);

            if (happening.Once) Facts.Set(happening.HappenedFact);

            if (happening.Sets.Length > 0) Facts.Set(happening.Sets);

            return going;
        }

        // the quest log is a view, so accepting one is just a fact and nothing else happens
        public bool Accept(string quest)
        {
            Quest one = _quests.Of(quest);

            if (one == null || one.StateIn(Facts) != QuestState.Offered) return false;

            Facts.Set(one.AcceptedFact);

            React(null);

            return true;
        }

        // an author's own fact written from outside - a conversation's turn-in, a scripted beat
        public bool Learn(string fact)
        {
            if (!FactName.IsLocal(fact) || !Facts.Set(fact)) return false;

            Rebuild();
            React(null);

            return true;
        }


        void Settle(Doing did)
        {
            Rebuild();

            React(did);
        }

        // everything hung on a fact that just became true, fired once per visit
        void React(Happened onto)
        {
            if (Where == null) return;

            foreach (Cue cue in _reactions.Cues(Where, Facts, When.Fact)) onto?.Add(cue);

            foreach (Trigger trigger in _reactions.Triggers(Where, Facts))
                Apply(trigger, onto);
        }

        void Fire(Place place, When moment, Happened onto)
        {
            foreach (Trigger trigger in place.Triggers)
                if (trigger.WhenIt == moment) Apply(trigger, onto);

            React(onto);
        }

        void Apply(Trigger trigger, Happened onto)
        {
            switch (trigger.ThenDo)
            {
                case Then.Set:
                    if (Facts.Set(trigger.Sets))
                    {
                        onto?.Add(trigger.Sets);
                        Rebuild();
                    }
                    break;

                case Then.Clear:
                    if (Facts.Clear(trigger.Sets))
                    {
                        onto?.Add(trigger.Sets);
                        Rebuild();
                    }
                    break;

                default:
                    // next, goto and ends move the story on, and moving it is the caller's to do
                    onto?.Add(trigger);
                    break;
            }
        }

        public override string ToString() =>
            Where == null
                ? $"{Campaign}: nowhere yet"
                : $"{Campaign}: in {Where.Id} at {Hero}, {_present.Count} on the table, " +
                  $"{Facts.Count} fact(s)" + (InAFight ? ", mid-fight" : "");
    }
}

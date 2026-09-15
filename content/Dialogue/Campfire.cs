using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Content.Dialogue
{
    // W3. Which conversation the fire gets. Camp is "the delivery vehicle for the entire companion
    // relationship" (CORE_RULES.md section 11), so the one thing that must not happen is the same
    // scene twice running, and the second is a night with nothing to say.
    //
    // Pure, so a campaign's whole camp schedule can be walked headless before anybody sits down.
    public sealed class Campfire
    {
        readonly DialogueBook _book;

        readonly IRng _rng;

        readonly Dictionary<Topic, ShuffleBag> _bags = new Dictionary<Topic, ShuffleBag>();

        // by topic, in file order, so the deal is over a stable list
        readonly Dictionary<Topic, List<string>> _scenes = new Dictionary<Topic, List<string>>();

        public Campfire(DialogueBook book, IRng rng)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));

            foreach (string node in book.CampNodes)
            {
                if (book.TopicOf(node) is not { } topic) continue;

                if (!_scenes.TryGetValue(topic, out List<string> nodes))
                    _scenes[topic] = nodes = new List<string>();

                nodes.Add(node);
            }
        }

        public IEnumerable<Topic> Knows =>
            _scenes.Keys.OrderBy(t => (int)t);

        public int ScenesFor(Topic topic) =>
            _scenes.TryGetValue(topic, out List<string> nodes) ? nodes.Count : 0;

        public bool Has(Topic topic) => ScenesFor(topic) > 0;

        // the node to play tonight, or null where this campaign has written nothing that fits.
        // Day.Offers is deliberately short - what happened, then Quiet - because every other topic
        // would have the companion assert something about a day it did not watch.
        public string Tonight(Day day)
        {
            if (day == null) return null;

            foreach (Topic topic in day.Offers())
            {
                string node = Next(topic);

                if (node != null) return node;
            }

            return null;
        }

        // dealt, not drawn, for the same reason a bark bank is: two identical nights in a row is
        // where a companion stops being a person
        public string Next(Topic topic)
        {
            if (!_scenes.TryGetValue(topic, out List<string> nodes) || nodes.Count == 0) return null;

            if (!_bags.TryGetValue(topic, out ShuffleBag bag))
                _bags[topic] = bag = new ShuffleBag(nodes.Count, _rng);

            int at = bag.Next();

            return at == 0 ? null : nodes[at - 1];
        }

        public override string ToString() =>
            _scenes.Count == 0
                ? "no camp scenes"
                : string.Join(", ", Knows.Select(t => $"{t.Word()} x{ScenesFor(t)}"));
    }
}

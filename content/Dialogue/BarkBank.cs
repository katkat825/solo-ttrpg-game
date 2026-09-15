using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Content.Dialogue
{
    // One speaker's short reactions. The file says how many lines each bank holds and the locale
    // holds the lines - so adding a bark is one number and one row, and never a code change.
    //
    // Keyed by creature and not by class (CONVENTIONS section 7): the class picks the companion,
    // the companion owns the voice, and a campaign that writes new barks for the wolf writes them
    // for every Barbarian who ever plays it.
    public sealed class BarkBank
    {
        readonly IReadOnlyDictionary<Bark, int> _banks;

        public BarkBank(string speaker, IReadOnlyDictionary<Bark, int> banks, bool readsTheThrow = false)
        {
            Speaker = speaker ?? "";
            _banks = banks ?? new Dictionary<Bark, int>();
            ReadsTheThrow = readsTheThrow;
        }

        public string Speaker { get; }

        // whether this voice, rather than the DM, says what the dice came to (W2, DM_PRESENCE.md D5)
        public bool ReadsTheThrow { get; }

        public int Count(Bark situation) =>
            _banks.TryGetValue(situation, out int lines) ? lines : 0;

        public bool Has(Bark situation) => Count(situation) > 0;

        public IEnumerable<Bark> Situations =>
            _banks.Where(b => b.Value > 0).Select(b => b.Key).OrderBy(s => s);

        public int Lines => _banks.Values.Sum();

        // every key this speaker can put in front of a player, which is what the locale audit demands
        public IEnumerable<string> Keys()
        {
            foreach (Bark situation in Situations)
                foreach (string key in DialogueKeys.Barks(Speaker, situation, Count(situation)))
                    yield return key;

            if (!ReadsTheThrow) yield break;

            foreach (string key in DialogueKeys.ReadoutKeys(Speaker)) yield return key;
        }

        // the readout keys are the ones that format numbers in; a bank's own barks never do
        public bool TakesAnArgument(string key) =>
            ReadsTheThrow && DialogueKeys.ReadoutKeys(Speaker).Contains(key, StringComparer.Ordinal);


        // one bag per situation, each dealt rather than drawn, all off the one seeded stream
        public Speaking Open(IRng rng) => new Speaking(this, rng);

        public override string ToString() =>
            $"{Speaker}: {Lines} barks in {_banks.Count(b => b.Value > 0)} banks" +
            (ReadsTheThrow ? ", reads the throw" : "");
    }

    // a bank in use: the shuffle state that makes forty lines sound like a creature and not a table
    public sealed class Speaking
    {
        readonly BarkBank _bank;

        readonly IRng _rng;

        readonly Dictionary<Bark, ShuffleBag> _bags = new Dictionary<Bark, ShuffleBag>();

        public Speaking(BarkBank bank, IRng rng)
        {
            _bank = bank ?? throw new ArgumentNullException(nameof(bank));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public string Speaker => _bank.Speaker;

        public bool Has(Bark situation) => _bank.Has(situation);

        // the key of the next thing said, or null when this speaker has nothing for the moment
        public string Next(Bark situation)
        {
            if (!_bank.Has(situation)) return null;

            if (!_bags.TryGetValue(situation, out ShuffleBag bag))
                _bags[situation] = bag = new ShuffleBag(_bank.Count(situation), _rng);

            int index = bag.Next();

            return index == 0 ? null : DialogueKeys.Bark(_bank.Speaker, situation, index);
        }

        public string Readout(Readout which) =>
            _bank.ReadsTheThrow ? DialogueKeys.Readout(_bank.Speaker, which) : null;

        public override string ToString() => _bank.ToString();
    }
}

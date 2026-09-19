using System;
using System.Collections.Generic;
using System.Linq;
using Content.Saves;

namespace Game.Book
{
    // EVERY BOOK YOU HAVE, WHICH IS WHAT THE BOOKCASE STANDS (BK2).
    //
    // Built from two lists and nothing else: the campaigns the loader found, and the saves on the
    // shelf. An installed campaign with no saves is a book you have never opened; a save whose
    // campaign is no longer installed is a book you cannot read, and it still stands there saying
    // so, because silently dropping somebody's playthrough because they unsubscribed from a
    // Workshop campaign is how a save folder becomes untrustworthy.
    //
    // RELAUNCH CONTINUES IN PLACE. Most is the newest ribbon anywhere on the bookcase, and it is
    // what "continue where you left off" means - there is no launch prompt to answer, because the
    // room opens with the book already open at it.
    public sealed class Collection
    {
        Collection(IReadOnlyList<Volume> volumes)
        {
            Volumes = volumes;
        }

        public IReadOnlyList<Volume> Volumes { get; }

        public int Count => Volumes.Count;

        public Volume Of(string campaign) =>
            campaign == null
                ? null
                : Volumes.FirstOrDefault(v => string.Equals(v.Campaign, campaign,
                                                            StringComparison.Ordinal));

        public IEnumerable<Volume> Readable => Volumes.Where(v => v.Installed);

        public IEnumerable<Volume> Unreadable => Volumes.Where(v => !v.Installed);

        // the newest place in any book you can actually read. An uninstalled campaign is skipped
        // deliberately: continuing into a campaign that is not there would be a crash dressed as a
        // convenience
        public Volume Newest =>
            Readable.Where(v => !v.Unopened)
                    .OrderByDescending(v => v.Latest.Written)
                    .FirstOrDefault();

        public Bookmark Most => Newest?.Latest;

        // whether the game has anything to continue into, which is the only question the launch
        // asks - and it answers it itself rather than asking the player
        public bool CanContinue => Most != null;


        public static Collection Of(IEnumerable<string> installed, SaveShelf shelf)
        {
            var known = new HashSet<string>(
                (installed ?? Enumerable.Empty<string>()).Where(i => !string.IsNullOrWhiteSpace(i)),
                StringComparer.Ordinal);

            var boxes = new Dictionary<string, List<Box>>(StringComparer.Ordinal);

            foreach (Box box in shelf?.Boxes ?? (IReadOnlyList<Box>)Array.Empty<Box>())
            {
                if (box == null || box.Campaign.Length == 0) continue;

                if (!boxes.TryGetValue(box.Campaign, out List<Box> under))
                    boxes[box.Campaign] = under = new List<Box>();

                under.Add(box);
            }

            var volumes = new List<Volume>();

            // installed first, alphabetically, so a bookcase reads the same way every time it is
            // looked at rather than re-ordering itself as you play
            foreach (string campaign in known.OrderBy(c => c, StringComparer.Ordinal))
                volumes.Add(Volume.Of(campaign, true,
                                      boxes.TryGetValue(campaign, out List<Box> mine)
                                          ? mine
                                          : Enumerable.Empty<Box>()));

            foreach (string campaign in boxes.Keys
                                             .Where(c => !known.Contains(c))
                                             .OrderBy(c => c, StringComparer.Ordinal))
                volumes.Add(Volume.Of(campaign, false, boxes[campaign]));

            return new Collection(volumes);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Count == 0
                ? "an empty bookcase"
                : $"{Count} book(s), {Readable.Count()} readable" +
                  (Most == null ? ", none opened yet" : $", newest {Most}");
    }
}

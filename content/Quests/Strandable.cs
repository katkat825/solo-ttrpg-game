using System.Collections.Generic;
using Content.Entities;
using Content.World;

namespace Content.Quests
{
    // THE SOFT-LOCK WARNING, AS A QUESTION ABOUT ONE QUEST.
    //
    // "Accept bring the rock to Bob, then murder Bob" resolves itself at runtime - the turn-in
    // waits on Bob and 'bob.dead' makes it unreachable - but only the author can say whether that
    // was the intention. So this warns and never refuses, and it finds the two shapes an author
    // writes:
    //
    //   TOLD:  done says 'unless bob.dead'. The author knew, and the only question left is whether
    //          the quest also says what failing looks like.
    //   QUIET: done waits on 'bob.spoken', and nothing else in the campaign writes it. Nobody
    //          wrote 'bob.dead' anywhere, so nothing reads as deliberate and nothing reads as
    //          failed either - the log just sits there telling you to go back to a dead man. This
    //          is the accidental one, and it is the one worth shipping a validator for.
    //
    // Only the turn-in is asked about. A quest you can no longer be OFFERED is not stranded, it is
    // a quest you never had, and warning about it would bury the one that matters.
    public sealed class Strandable
    {
        Strandable(Quest quest, string entity, string fact, bool told, bool failable)
        {
            Quest = quest;
            Entity = entity;
            Fact = fact;
            Told = told;
            SaysHowItFails = failable;
        }

        public Quest Quest { get; }

        // the attackable entity whose death does it
        public string Entity { get; }

        // the clause that stops being satisfiable; empty for a Told one, which names no such fact
        public string Fact { get; }

        public bool Told { get; }

        // whether the quest's own 'failed' clause reads this death, and so the log will say so
        public bool SaysHowItFails { get; }

        // where the author should be looking, as a json path
        public string Where => Told ? "done.unless" : "done.when";

        public static IEnumerable<Strandable> In(Quest quest, EntityBook entities,
                                                 FactSources sources)
        {
            if (quest == null || entities == null) yield break;

            var named = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (string fact in quest.Done.Unless)
            {
                string subject = FactName.SubjectOf(fact, FactName.DeadAspect);

                if (subject.Length == 0 || !Attackable(entities, subject)) continue;

                named.Add(subject);

                yield return new Strandable(quest, subject, "", told: true, Fails(quest, subject));
            }

            if (sources == null) yield break;

            foreach (string fact in quest.Done.When)
            {
                string subject = sources.OnlyOnTheLivingOf(fact);

                // an unwritable fact is a fault the reachability pass already names; saying it
                // again in different words would send the author looking for a second mistake
                if (subject.Length == 0 || !Attackable(entities, subject)) continue;

                // the author already said it in the clause above; one warning per entity
                if (!named.Add(subject)) continue;

                yield return new Strandable(quest, subject, fact, told: false, Fails(quest, subject));
            }
        }

        static bool Attackable(EntityBook entities, string local)
        {
            Entity entity = entities.Of(local);

            return entity != null && entity.Allows(Interaction.Attack);
        }

        // asked as 'does the clause name the death' rather than evaluated against a made-up set of
        // facts: a 'failed' waiting on two things is still a quest that says what failing looks like
        static bool Fails(Quest quest, string entity)
        {
            if (!quest.CanFail) return false;

            string dead = FactName.Dead(entity);

            foreach (string fact in quest.Failed.When)
                if (string.Equals(fact, dead, System.StringComparison.Ordinal)) return true;

            return false;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Quest?.Id}: killing {Entity} strands it" +
            (Told ? " (the author said so)" : $" through '{Fact}'") +
            (SaysHowItFails ? ", and it reads as failed" : ", and nothing says it failed");
    }
}

using System.Collections.Generic;

namespace Rules.Characters
{
    // where statblocks come from - the content seam
    // the only implementation today is hard-coded C#, which is a placeholder
    // statblocks are content and belong in campaign data
    // a data-backed source drops in here and no consumer of actors changes
    public interface IArchetypeSource
    {
        // always a new instance - actors are mutable
        Actor Create(string id);

        bool Has(string id);

        IReadOnlyCollection<string> Ids { get; }
    }
}

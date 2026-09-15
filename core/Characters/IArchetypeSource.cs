using System.Collections.Generic;

namespace Core.Characters
{
    public interface IArchetypeSource
    {
        // always a new instance - actors are mutable
        Actor Create(string id);

        bool Has(string id);

        IReadOnlyCollection<string> Ids { get; }
    }
}

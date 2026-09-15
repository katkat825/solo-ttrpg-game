using System.Collections.Generic;

namespace Content.Minis
{
    public static class SharedMinis
    {
        public const string Hero = "barbarian";

        public const string Rabble = "rabble";

        public const string Rival = "rival";

        public const string Dread = "dread";

        // heights matter: a variant of one is arithmetic on this number ("a head taller")
        public static IReadOnlyList<MiniManifest> All { get; } = new[]
        {
            Standing(Hero, 0.075f),
            Standing(Rabble, 0.05f),
            Standing(Rival, 0.062f),
            Standing(Dread, 0.09f),
        };

        static MiniManifest Standing(string id, float height) =>
            new MiniManifest(id, variant: null, model: null, Fit.Height, height, foot: 0f,
                             Tint.None, clips: null, foley: null);

        public static MiniCatalogue Catalogue { get; } = MiniCatalogue.Of(All);

        public static bool Has(string id) => Catalogue.Has(id);
    }
}

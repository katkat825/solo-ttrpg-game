namespace Rules.Localization
{
    // turns a key into display text
    // barely used inside rules/ - the rules emit keys and never resolve them
    // it exists for presentation and content tooling
    // and lives down here so both can depend on it without depending on each other
    // the Godot implementation forwards to tr(), which brings CSV import and fallback
    public interface ILocalizer
    {
        string Get(string key);

        // positional arguments only
        // never build a sentence from separately translated fragments
        // word order differs by language and the result is untranslatable
        // one key, one whole sentence, {0} placeholders
        string Format(string key, params object[] args);

        bool Has(string key);
    }
}

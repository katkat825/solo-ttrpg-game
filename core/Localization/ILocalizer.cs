namespace Core.Localization
{
    public interface ILocalizer
    {
        string Get(string key);

        // positional args only - never assemble a sentence from translated fragments, word order differs by language
        string Format(string key, params object[] args);

        bool Has(string key);
    }
}

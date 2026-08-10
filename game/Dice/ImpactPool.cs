using System.Collections.Generic;
using Godot;

// one folder of impact samples, loaded once and shared by everything that names it
// THE FOLDER IS THE LIST - nothing anywhere names a sample file, because the pool grows
// a hard-coded list is a second description of the folder, and when the two drift the
// failure is SILENCE, which looks exactly like a die that never hit anything
// cached by path: a pool is immutable once built, and asking twice shouldn't read the disk twice

public sealed class ImpactPool
{
    public const string Default = "res://audio/dice_wood/";   // what a surface gets if it names none

    static readonly Dictionary<string, ImpactPool> Loaded = new();

    // lazy rather than static-initialised - GD.Load during static construction runs before
    // the resource system is necessarily up
    public static ImpactPool For(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) folder = Default;

        if (!folder.EndsWith("/")) folder += "/";

        if (Loaded.TryGetValue(folder, out ImpactPool pool)) return pool;

        pool = new ImpactPool(folder);
        Loaded[folder] = pool;
        return pool;
    }

    // an AudioStreamRandomizer over every sample in the folder
    public AudioStream Stream { get; }

    // zero means the tray is going to be silent
    public int Count { get; }

    ImpactPool(string folder)
    {
        // the randomizer is where variation comes from with no code
        // RandomNoRepeats rather than Random - samples chosen with replacement audibly double up
        var randomizer = new AudioStreamRandomizer
        {
            PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,

            // a multiplier, so 1.08 is roughly a semitone either way - enough that no two
            // throws are the same, not enough to sound like a different die
            RandomPitch = 1.08f,
            RandomVolumeOffsetDb = 2f,
        };

        foreach (string file in Files(folder))
        {
            var stream = GD.Load<AudioStream>(folder + file);

            if (stream == null)
            {
                // developer diagnostic, not player-facing
                // silence looks exactly like "it works but nothing hit anything", so it has to say so
                GD.PushError($"impact pool: {folder}{file} did not load - one impact short");
                continue;
            }

            randomizer.AddStream(-1, stream);
        }

        Stream = randomizer;
        Count = randomizer.StreamsCount;

        if (Count == 0) GD.PushError($"impact pool: no samples in {folder} - hits there will be silent");
    }

    // every wav in the folder, sorted, with none named twice
    // three spellings have to be folded together: in the source tree a sample is x.wav beside
    // its x.wav.import, in an exported build only x.wav.remap is listed
    // handling one of the three works right up until the export, the worst moment to find out
    // subfolders are skipped, which keeps the slicer's _review/ quarantine out of the game
    static SortedSet<string> Files(string folder)
    {
        var names = new SortedSet<string>();

        using DirAccess dir = DirAccess.Open(folder);

        if (dir == null)
        {
            GD.PushError($"impact pool: cannot open {folder} - {DirAccess.GetOpenError()}");
            return names;
        }

        foreach (string entry in dir.GetFiles())
        {
            string name = entry;

            if (name.EndsWith(".import") || name.EndsWith(".remap")) name = name.GetBaseName();

            if (name.EndsWith(".wav")) names.Add(name);
        }

        return names;
    }
}

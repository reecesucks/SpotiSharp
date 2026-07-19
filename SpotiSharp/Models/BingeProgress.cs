namespace SpotiSharp.Models;

public class BingeProgress
{
    // position counted from the show's first episode; everything at or below
    // this index is considered finished and is never searched again
    public int LastFinishedIndexFromOldest { get; set; }

    // shown in the radio settings so the current binge position is visible
    public string NextEpisodeName { get; set; }
}

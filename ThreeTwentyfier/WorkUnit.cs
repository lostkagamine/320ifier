using System.Diagnostics;

namespace ThreeTwentyfier;

public class WorkUnit
{
    public ProcessStartInfo Info = null!;
    public uint ThreadId;
    public string FileName = "";
    public uint WorkId;
}
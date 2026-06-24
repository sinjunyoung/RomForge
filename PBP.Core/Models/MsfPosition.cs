using System.Diagnostics;

namespace PBP.Core.Models;

[DebuggerDisplay("{Minutes}:{Seconds}:{Frames}")]
public partial class MsfPosition
{
    public MsfPosition() { }

    public MsfPosition(int minutes, int seconds, int frames)
    {
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
    }

    public int Minutes { get; set; }

    public int Seconds { get; set; }

    public int Frames { get; set; }

    public override string ToString() => $"{Minutes:00}:{Seconds:00}:{Frames:00}";

    public static MsfPosition operator +(MsfPosition a, MsfPosition b)
    {
        var frames = a.Frames + b.Frames;
        var framesCarry = 0;

        if (frames >= 75)
        {
            framesCarry = frames / 75; 
            frames %= 75; 
        }

        var seconds = a.Seconds + b.Seconds + framesCarry;
        var secondsCarry = 0;

        if (seconds >= 60)
        {
            secondsCarry = seconds / 60; 
            seconds %= 60; 
        }

        return new MsfPosition(a.Minutes + b.Minutes + secondsCarry, seconds, frames);
    }

    public static MsfPosition operator -(MsfPosition a, MsfPosition b)
    {
        var frames = a.Frames - b.Frames;
        var secondsBorrow = 0;

        if (frames < 0) 
        { 
            secondsBorrow = 1; 
            frames += 75;
        }

        var seconds = a.Seconds - b.Seconds - secondsBorrow;
        var minutesBorrow = 0;

        if (seconds < 0) 
        { 
            minutesBorrow = 1; 
            seconds += 60; 
        }

        return new MsfPosition(a.Minutes - b.Minutes - minutesBorrow, seconds, frames);
    }

    public static MsfPosition operator -(MsfPosition a, int framesB)
    {
        var mm = framesB / (60 * 75);
        var ss = (framesB - mm * 60 * 75) / 75;
        var ff = framesB % 75;
        var frames = a.Frames - ff;
        var secondsBorrow = 0;

        if (frames < 0) 
        { 
            secondsBorrow = 1; 
            frames += 75; 
        }

        var seconds = a.Seconds - ss - secondsBorrow;
        var minutesBorrow = 0;

        if (seconds < 0) 
        { 
            minutesBorrow = 1; 
            seconds += 60; 
        }

        return new MsfPosition(a.Minutes - mm - minutesBorrow, seconds, frames);
    }

    public static MsfPosition operator +(MsfPosition a, int framesB)
    {
        var frames = a.Frames + framesB;
        var framesCarry = 0;

        if (frames >= 75) 
        { 
            framesCarry = frames / 75;
            frames %= 75; 
        }

        var seconds = a.Seconds + framesCarry;
        var secondsCarry = 0;

        if (seconds >= 60) 
        { 
            secondsCarry = seconds / 60; 
            seconds %= 60;
        }

        return new MsfPosition(a.Minutes + secondsCarry, seconds, frames);
    }

}
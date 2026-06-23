using System.Diagnostics;

namespace LUmaKE.Utility;

/// <summary>
///   Regulates framerate and computes the delta time between frames.
/// </summary>
public class FramerateClock
{
    public readonly long HardwareFrequency;
    private readonly double _tickToSecondMultiplier;

    private long _frameStartTick;
    private long _previousTick;

    public long FrameStartTick { get => _frameStartTick; }
    
    public FramerateClock()
    {
        HardwareFrequency = Stopwatch.Frequency;
        _tickToSecondMultiplier = 1.0 / HardwareFrequency;
        
        long initial = Stopwatch.GetTimestamp();
        _previousTick = initial;
        _frameStartTick = initial;
    }

    /// <summary>
    ///   Computes the delta based on when the frame actually starts processing.
    /// </summary>
    public double ComputeDelta()
    {
        // Capture the moment the engine actually begins execution for this frame
        _frameStartTick = Stopwatch.GetTimestamp();

        // Calculate time passed since the previous frame completely finished (including any regulation/VSync stalls)
        double delta = (_frameStartTick - _previousTick) * _tickToSecondMultiplier;
        return delta;
    }

    /// <summary>
    ///   Regulate the framerate by pausing the main thread.
    /// </summary>
    public void RegulateFramerate(double fps)
    {
        long frameEndTick = Stopwatch.GetTimestamp();
        
        long targetTicks    = (long)((1.0 / fps) * HardwareFrequency);
        long usedTicks      = frameEndTick - _frameStartTick;
        long remainingTicks = targetTicks - usedTicks;

        if (remainingTicks > 0)
        {
            double msPerTick = 1000.0 / HardwareFrequency;
            double remainingTimeMs = remainingTicks * msPerTick;

            if (remainingTimeMs > 2.0)
                Thread.Sleep((int)(remainingTimeMs - 2.0));

            long targetTimestamp = _frameStartTick + targetTicks;
            while (Stopwatch.GetTimestamp() < targetTimestamp)
            {
                Thread.SpinWait(1);
            }
        }
    }

    /// <summary>
    ///   Commit the final frame by resetting the previous tick capture.
    /// </summary>
    public void CommitFrame()
    {
        // Anchor the baseline clock to the exact moment this frame finishes all work and waiting.
        _previousTick = Stopwatch.GetTimestamp();
    }
}
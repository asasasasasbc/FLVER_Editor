using System;
namespace MySFformat
{
    public sealed class HkxPlaybackState
    {
        public float Duration { get; private set; }
        public int Fps { get; private set; }
        public int LastFrame { get { return (int)Math.Ceiling(Duration * Fps - 0.0001f); } }
        public float Time { get; private set; }
        public int Frame { get { return Math.Min(LastFrame, (int)Math.Round(Time * Fps)); } }
        public bool Playing { get; private set; }
        public bool Loop { get; set; } = true;
        public float Speed { get; set; } = 1;
        public HkxPlaybackState(float duration, int fps)
        { if (duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration) || fps <= 0) throw new ArgumentOutOfRangeException(); Duration = duration; Fps = fps; }
        public void SeekFrame(int frame) { Time = Math.Min(Duration, Math.Max(0, frame) / (float)Fps); }
        public void Play() { if (Time >= Duration) Time = 0; Playing = true; }
        public void Pause() { Playing = false; }
        public void Advance(float seconds)
        {
            if (!Playing) return;
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || Speed <= 0 || float.IsNaN(Speed) || float.IsInfinity(Speed)) throw new ArgumentOutOfRangeException();
            Time += seconds * Speed;
            if (Time >= Duration) { if (Loop) Time %= Duration; else { Time = Duration; Playing = false; } }
        }
    }
}

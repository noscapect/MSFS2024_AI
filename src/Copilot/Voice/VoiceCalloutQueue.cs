using System.Speech.Synthesis;

namespace Msfs2024Ai.Copilot.Voice;

internal sealed class VoiceCalloutQueue : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer;
    private readonly List<QueuedCallout> _pending = new();
    private readonly object _sync = new();
    private long _sequence;
    private bool _speaking;

    public VoiceCalloutQueue()
    {
        _synthesizer = new SpeechSynthesizer
        {
            Rate = 0,
            Volume = 100
        };
        _synthesizer.SpeakCompleted += OnSpeakCompleted;
    }

    public void Enqueue(string phrase, int priority)
    {
        lock (_sync)
        {
            _pending.Add(new QueuedCallout(phrase, priority, _sequence++));
            TrySpeakNext();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _pending.Clear();
            _speaking = false;
        }
        _synthesizer.SpeakAsyncCancelAll();
    }

    private void TrySpeakNext()
    {
        if (_speaking || _pending.Count == 0)
        {
            return;
        }

        var next = _pending
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Sequence)
            .First();
        _pending.Remove(next);
        _speaking = true;
        _synthesizer.SpeakAsync(next.Phrase);
    }

    private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
    {
        lock (_sync)
        {
            _speaking = false;
            TrySpeakNext();
        }
    }

    public void Dispose()
    {
        _synthesizer.SpeakCompleted -= OnSpeakCompleted;
        _synthesizer.SpeakAsyncCancelAll();
        _synthesizer.Dispose();
    }

    private sealed class QueuedCallout
    {
        public QueuedCallout(string phrase, int priority, long sequence)
        {
            Phrase = phrase;
            Priority = priority;
            Sequence = sequence;
        }

        public string Phrase { get; }
        public int Priority { get; }
        public long Sequence { get; }
    }
}

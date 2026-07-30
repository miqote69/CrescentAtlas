using CrescentAtlas.Data;

namespace CrescentAtlas.Contracts;

public interface IObservationSink
{
    string SessionId { get; }

    string OutputDirectory { get; }

    void Record(ObservationRecord observation);

    void Flush();
}

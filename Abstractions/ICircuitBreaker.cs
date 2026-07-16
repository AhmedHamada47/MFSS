namespace MFSS.Abstractions;

public interface ICircuitBreaker
{
    bool AllowRequest();
    void RecordSuccess();
    void RecordFailure();
}

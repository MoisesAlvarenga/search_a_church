namespace SearchAChurch.Api.Features.Maps.Gateways;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public interface ICircuitBreaker
{
    CircuitState State { get; }
    bool CanExecute();
    void RecordSuccess();
    void RecordFailure();
    void Reset();
}

public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _syncLock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _lastStateChange;

    public CircuitBreaker(
        int failureThreshold = 3,
        TimeSpan? breakDuration = null,
        TimeProvider? timeProvider = null)
    {
        _failureThreshold = failureThreshold > 0 ? failureThreshold : 3;
        _breakDuration = breakDuration ?? TimeSpan.FromSeconds(30);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastStateChange = _timeProvider.GetUtcNow();
    }

    public CircuitState State
    {
        get
        {
            lock (_syncLock)
            {
                CheckHalfOpenTransition();
                return _state;
            }
        }
    }

    public bool CanExecute()
    {
        lock (_syncLock)
        {
            CheckHalfOpenTransition();
            return _state != CircuitState.Open;
        }
    }

    public void RecordSuccess()
    {
        lock (_syncLock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
            _lastStateChange = _timeProvider.GetUtcNow();
        }
    }

    public void RecordFailure()
    {
        lock (_syncLock)
        {
            _consecutiveFailures++;

            if (_state == CircuitState.HalfOpen || _consecutiveFailures >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _lastStateChange = _timeProvider.GetUtcNow();
            }
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
            _lastStateChange = _timeProvider.GetUtcNow();
        }
    }

    private void CheckHalfOpenTransition()
    {
        if (_state == CircuitState.Open)
        {
            var elapsed = _timeProvider.GetUtcNow() - _lastStateChange;
            if (elapsed >= _breakDuration)
            {
                _state = CircuitState.HalfOpen;
                _lastStateChange = _timeProvider.GetUtcNow();
            }
        }
    }
}

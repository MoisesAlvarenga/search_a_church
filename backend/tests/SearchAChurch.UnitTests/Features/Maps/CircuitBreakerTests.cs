using FluentAssertions;
using SearchAChurch.Api.Features.Maps.Gateways;

namespace SearchAChurch.UnitTests.Features.Maps;

[Trait("Category", "Unit")]
public class CircuitBreakerTests
{
    private readonly TestTimeProvider _fakeTime = new();

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    [Fact]
    public void InitialState_ShouldBeClosed()
    {
        // Arrange & Act
        var cb = new CircuitBreaker(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);

        // Assert
        cb.State.Should().Be(CircuitState.Closed);
        cb.CanExecute().Should().BeTrue();
    }

    [Fact]
    public void RecordSuccess_WhenClosed_KeepsStateClosed()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);

        // Act
        cb.RecordSuccess();

        // Assert
        cb.State.Should().Be(CircuitState.Closed);
        cb.CanExecute().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_BelowThreshold_KeepsStateClosed()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);

        // Act
        cb.RecordFailure();
        cb.RecordFailure();

        // Assert
        cb.State.Should().Be(CircuitState.Closed);
        cb.CanExecute().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_ReachingThreshold_TransitionsToOpen()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);

        // Act
        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();

        // Assert
        cb.State.Should().Be(CircuitState.Open);
        cb.CanExecute().Should().BeFalse();
    }

    [Fact]
    public void CanExecute_WhenOpenAndDurationNotElapsed_ReturnsFalse()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);
        cb.RecordFailure();
        cb.RecordFailure();

        // Act
        _fakeTime.Advance(TimeSpan.FromSeconds(15));

        // Assert
        cb.State.Should().Be(CircuitState.Open);
        cb.CanExecute().Should().BeFalse();
    }

    [Fact]
    public void CanExecute_WhenOpenAndDurationElapsed_TransitionsToHalfOpen()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);
        cb.RecordFailure();
        cb.RecordFailure();

        // Act
        _fakeTime.Advance(TimeSpan.FromSeconds(31));

        // Assert
        cb.CanExecute().Should().BeTrue();
        cb.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void RecordSuccess_WhenHalfOpen_TransitionsBackToClosed()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);
        cb.RecordFailure();
        cb.RecordFailure();
        _fakeTime.Advance(TimeSpan.FromSeconds(31));
        cb.CanExecute(); // Transitions to HalfOpen

        // Act
        cb.RecordSuccess();

        // Assert
        cb.State.Should().Be(CircuitState.Closed);
        cb.CanExecute().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_WhenHalfOpen_TransitionsBackToOpen()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);
        cb.RecordFailure();
        cb.RecordFailure();
        _fakeTime.Advance(TimeSpan.FromSeconds(31));
        cb.CanExecute(); // Transitions to HalfOpen

        // Act
        cb.RecordFailure();

        // Assert
        cb.State.Should().Be(CircuitState.Open);
        cb.CanExecute().Should().BeFalse();
    }

    [Fact]
    public void Reset_WhenOpen_TransitionsToClosedAndAllowsExecution()
    {
        // Arrange
        var cb = new CircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(30), timeProvider: _fakeTime);
        cb.RecordFailure();
        cb.RecordFailure();
        cb.State.Should().Be(CircuitState.Open);

        // Act
        cb.Reset();

        // Assert
        cb.State.Should().Be(CircuitState.Closed);
        cb.CanExecute().Should().BeTrue();
    }
}

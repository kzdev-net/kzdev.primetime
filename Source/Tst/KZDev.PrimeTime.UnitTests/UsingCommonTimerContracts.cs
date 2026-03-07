// Copyright (c) Kevin Zehrer
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using KZDev.PrimeTime.Tests;

namespace KZDev.PrimeTime.UnitTests;

/// <summary>
///   Unit tests for Phase 1 Common timer contracts, enums, and options. Verifies each type
///   exists with the specified members, enums have expected values, and option types can be
///   constructed with expected defaults.
/// </summary>
public class UsingCommonTimerContracts : UnitTestBase
{
    /// <summary>
    ///   Initializes a new instance of the <see cref="UsingCommonTimerContracts"/> class.
    /// </summary>
    /// <param name="xUnitTestOutputHelper">
    ///   The Xunit test output helper that can be used to output test messages.
    /// </param>
    public UsingCommonTimerContracts (ITestOutputHelper xUnitTestOutputHelper)
        : base(xUnitTestOutputHelper)
    {
    }

    #region ITimer contract

    /// <summary>
    ///   Verifies that <see cref="ITimer"/> is an interface and extends <see cref="IDisposable"/>.
    /// </summary>
    [Fact]
    public void ITimer_ExistsAndIsInterface ()
    {
        typeof(ITimer).IsInterface.Should().BeTrue();
        typeof(ITimer).GetInterfaces().Should().Contain(typeof(IDisposable));
    }

    /// <summary>
    ///   Verifies that <see cref="ITimer"/> declares all required properties (Id, IsCancelled, State, etc.).
    /// </summary>
    [Fact]
    public void ITimer_DeclaresRequiredProperties ()
    {
        Type timerType = typeof(ITimer);
        string[] requiredProperties = ["Id", "IsCancelled", "IsTimeOfDay", "IsRepeating", "IsLocalTimeRepresentation",
            "IsActive", "State", "CallbacksProcessing", "Enabled"];
        foreach (string name in requiredProperties)
        {
            PropertyInfo? prop = timerType.GetProperty(name);
            prop.Should().NotBeNull($"ITimer should declare property {name}");
        }
    }

    /// <summary>
    ///   Verifies that <see cref="ITimer"/> declares Cancel, Stop, and Start methods.
    /// </summary>
    [Fact]
    public void ITimer_DeclaresRequiredMethods ()
    {
        Type timerType = typeof(ITimer);
        timerType.GetMethod("Cancel").Should().NotBeNull();
        timerType.GetMethod("Stop").Should().NotBeNull();
        timerType.GetMethod("Start").Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that <see cref="ITimer.Id"/> is of type <see cref="int"/>.
    /// </summary>
    [Fact]
    public void ITimer_Id_IsInt ()
    {
        PropertyInfo? prop = typeof(ITimer).GetProperty("Id");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(int));
    }

    /// <summary>
    ///   Verifies that <see cref="ITimer.State"/> is of type <see cref="TimerState"/>.
    /// </summary>
    [Fact]
    public void ITimer_State_IsTimerState ()
    {
        PropertyInfo? prop = typeof(ITimer).GetProperty("State");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(TimerState));
    }

    #endregion ITimer contract

    #region IDayTimeTimer contract

    /// <summary>
    ///   Verifies that <see cref="IDayTimeTimer"/> extends <see cref="ITimer"/>.
    /// </summary>
    [Fact]
    public void IDayTimeTimer_ExtendsITimer ()
    {
        typeof(IDayTimeTimer).GetInterfaces().Should().Contain(typeof(ITimer));
    }

    /// <summary>
    ///   Verifies that <see cref="IDayTimeTimer"/> declares ConcurrentTriggerProcessing, SkippedTimeBehavior, and DuplicateTimeBehavior.
    /// </summary>
    [Fact]
    public void IDayTimeTimer_DeclaresRequiredProperties ()
    {
        Type dayTimeType = typeof(IDayTimeTimer);
        dayTimeType.GetProperty("ConcurrentTriggerProcessing").Should().NotBeNull();
        dayTimeType.GetProperty("SkippedTimeBehavior").Should().NotBeNull();
        dayTimeType.GetProperty("DuplicateTimeBehavior").Should().NotBeNull();
    }

    #endregion IDayTimeTimer contract

    #region IIntervalTimer contract

    /// <summary>
    ///   Verifies that <see cref="IIntervalTimer"/> extends <see cref="ITimer"/>.
    /// </summary>
    [Fact]
    public void IIntervalTimer_ExtendsITimer ()
    {
        typeof(IIntervalTimer).GetInterfaces().Should().Contain(typeof(ITimer));
    }

    /// <summary>
    ///   Verifies that <see cref="IIntervalTimer"/> declares IsResetAfterCallback, ElapsedTime, and TimeUntilNextCallback.
    /// </summary>
    [Fact]
    public void IIntervalTimer_DeclaresRequiredProperties ()
    {
        Type intervalType = typeof(IIntervalTimer);
        intervalType.GetProperty("IsResetAfterCallback").Should().NotBeNull();
        intervalType.GetProperty("ElapsedTime").Should().NotBeNull();
        intervalType.GetProperty("TimeUntilNextCallback").Should().NotBeNull();
    }

    /// <summary>
    ///   Verifies that <see cref="IIntervalTimer.ElapsedTime"/> is of type <see cref="long"/>.
    /// </summary>
    [Fact]
    public void IIntervalTimer_ElapsedTime_IsLong ()
    {
        PropertyInfo? prop = typeof(IIntervalTimer).GetProperty("ElapsedTime");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long));
    }

    /// <summary>
    ///   Verifies that <see cref="IIntervalTimer.TimeUntilNextCallback"/> is of type <see cref="long"/>.
    /// </summary>
    [Fact]
    public void IIntervalTimer_TimeUntilNextCallback_IsLong ()
    {
        PropertyInfo? prop = typeof(IIntervalTimer).GetProperty("TimeUntilNextCallback");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long));
    }

    #endregion IIntervalTimer contract

    #region TimerState enum

    /// <summary>
    ///   Verifies that <see cref="TimerState"/> defines all eight expected enum values.
    /// </summary>
    [Fact]
    public void TimerState_HasExpectedValues ()
    {
        string[] expected = ["Active", "Cancelled", "Completed", "Disabled", "Disposed", "ProcessingCallback",
            "RepeatCycle", "RepeatProcessingCallback"];
        Enum.GetNames(typeof(TimerState)).Should().BeEquivalentTo(expected);
    }

    /// <summary>
    ///   Verifies that <see cref="TimerState.Active"/> has the value zero.
    /// </summary>
    [Fact]
    public void TimerState_Active_IsZero ()
    {
        ((int)TimerState.Active).Should().Be(0);
    }

    #endregion TimerState enum

    #region ConcurrentTriggerProcessing enum

    /// <summary>
    ///   Verifies that <see cref="ConcurrentTriggerProcessing"/> defines Skip, RunConcurrently, and RunSequentially.
    /// </summary>
    [Fact]
    public void ConcurrentTriggerProcessing_HasExpectedValues ()
    {
        string[] expected = ["Skip", "RunConcurrently", "RunSequentially"];
        Enum.GetNames(typeof(ConcurrentTriggerProcessing)).Should().BeEquivalentTo(expected);
    }

    #endregion ConcurrentTriggerProcessing enum

    #region SkippedTimeBehavior enum

    /// <summary>
    ///   Verifies that <see cref="SkippedTimeBehavior"/> defines Skip, RunAfter, and RunBefore.
    /// </summary>
    [Fact]
    public void SkippedTimeBehavior_HasExpectedValues ()
    {
        string[] expected = ["Skip", "RunAfter", "RunBefore"];
        Enum.GetNames(typeof(SkippedTimeBehavior)).Should().BeEquivalentTo(expected);
    }

    #endregion SkippedTimeBehavior enum

    #region DuplicateTimeBehavior enum

    /// <summary>
    ///   Verifies that <see cref="DuplicateTimeBehavior"/> defines RunLast and RunFirst.
    /// </summary>
    [Fact]
    public void DuplicateTimeBehavior_HasExpectedValues ()
    {
        string[] expected = ["RunLast", "RunFirst"];
        Enum.GetNames(typeof(DuplicateTimeBehavior)).Should().BeEquivalentTo(expected);
    }

    #endregion DuplicateTimeBehavior enum

    #region TimerCallbackExecutionContext enum

    /// <summary>
    ///   Verifies that <see cref="TimerCallbackExecutionContext"/> defines Capture and Unsafe.
    /// </summary>
    [Fact]
    public void TimerCallbackExecutionContext_HasExpectedValues ()
    {
        string[] expected = ["Capture", "Unsafe"];
        Enum.GetNames(typeof(TimerCallbackExecutionContext)).Should().BeEquivalentTo(expected);
    }

    #endregion TimerCallbackExecutionContext enum

    #region TimerOptions

    /// <summary>
    ///   Verifies that <see cref="TimerOptions"/> is abstract.
    /// </summary>
    [Fact]
    public void TimerOptions_IsAbstract ()
    {
        typeof(TimerOptions).IsAbstract.Should().BeTrue();
    }

    /// <summary>
    ///   Verifies that <see cref="TimerOptions"/> declares LocalTimeRepresentation and CallbackExecutionContext properties.
    /// </summary>
    [Fact]
    public void TimerOptions_DeclaresLocalTimeRepresentationAndCallbackExecutionContext ()
    {
        typeof(TimerOptions).GetProperty("LocalTimeRepresentation").Should().NotBeNull();
        typeof(TimerOptions).GetProperty("CallbackExecutionContext").Should().NotBeNull();
    }

    #endregion TimerOptions

    #region IntervalTimerOptions

    /// <summary>
    ///   Verifies that <see cref="IntervalTimerOptions"/> extends <see cref="TimerOptions"/>.
    /// </summary>
    [Fact]
    public void IntervalTimerOptions_ExtendsTimerOptions ()
    {
        typeof(IntervalTimerOptions).BaseType.Should().Be(typeof(TimerOptions));
    }

    /// <summary>
    ///   Verifies that default <see cref="IntervalTimerOptions"/> has ResetIntervalAfterCallback false and CallbackExecutionContext Capture.
    /// </summary>
    [Fact]
    public void IntervalTimerOptions_CanBeConstructedWithDefaults ()
    {
        IntervalTimerOptions options = new();
        options.ResetIntervalAfterCallback.Should().BeFalse();
        options.CallbackExecutionContext.Should().Be(TimerCallbackExecutionContext.Capture);
    }

    /// <summary>
    ///   Verifies that <see cref="IntervalTimerOptions"/> can be constructed with ResetIntervalAfterCallback set to true.
    /// </summary>
    [Fact]
    public void IntervalTimerOptions_CanBeConstructedWithResetIntervalAfterCallback ()
    {
        IntervalTimerOptions options = new() { ResetIntervalAfterCallback = true };
        options.ResetIntervalAfterCallback.Should().BeTrue();
    }

    #endregion IntervalTimerOptions

    #region DayTimeTimerOptions

    /// <summary>
    ///   Verifies that <see cref="DayTimeTimerOptions"/> extends <see cref="TimerOptions"/>.
    /// </summary>
    [Fact]
    public void DayTimeTimerOptions_ExtendsTimerOptions ()
    {
        typeof(DayTimeTimerOptions).BaseType.Should().Be(typeof(TimerOptions));
    }

    /// <summary>
    ///   Verifies that default <see cref="DayTimeTimerOptions"/> has expected SkippedTimeBehavior, DuplicateTimeBehavior, and CallbackExecutionContext.
    /// </summary>
    [Fact]
    public void DayTimeTimerOptions_CanBeConstructedWithDefaults ()
    {
        DayTimeTimerOptions options = new();
        options.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunAfter);
        options.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunLast);
        options.CallbackExecutionContext.Should().Be(TimerCallbackExecutionContext.Capture);
    }

    /// <summary>
    ///   Verifies that <see cref="DayTimeTimerOptions"/> can be constructed with all option properties set.
    /// </summary>
    [Fact]
    public void DayTimeTimerOptions_CanBeConstructedWithAllOptions ()
    {
        DayTimeTimerOptions options = new()
        {
            ConcurrentTriggerProcessing = ConcurrentTriggerProcessing.RunSequentially,
            SkippedTimeBehavior = SkippedTimeBehavior.RunBefore,
            DuplicateTimeBehavior = DuplicateTimeBehavior.RunFirst
        };
        options.ConcurrentTriggerProcessing.Should().Be(ConcurrentTriggerProcessing.RunSequentially);
        options.SkippedTimeBehavior.Should().Be(SkippedTimeBehavior.RunBefore);
        options.DuplicateTimeBehavior.Should().Be(DuplicateTimeBehavior.RunFirst);
    }

    #endregion DayTimeTimerOptions
}

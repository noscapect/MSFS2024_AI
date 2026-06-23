namespace Msfs2024Ai.Copilot.Procedures;

internal sealed class ProcedureRunner
{
    private static readonly TimeSpan AutomaticActionCadence =
        TimeSpan.FromSeconds(1);
    private readonly Action<string> _executeCommand;
    private readonly Func<Domain.AutomationPolicy> _automationPolicy;
    private ProcedureDefinition? _definition;
    private int _stepIndex;
    private bool _manualConfirmationReceived;
    private string? _lastAutomaticStepId;
    private DateTime _nextAutomaticActionUtc;

    public ProcedureRunner(
        Action<string> executeCommand,
        Func<Domain.AutomationPolicy> automationPolicy)
    {
        _executeCommand = executeCommand;
        _automationPolicy = automationPolicy;
    }

    public ProcedureStatus Status { get; private set; } = ProcedureStatus.Idle;
    public ProcedureDefinition? Definition => _definition;
    public ProcedureStep? CurrentStep =>
        _definition != null && _stepIndex < _definition.Steps.Count
            ? _definition.Steps[_stepIndex]
            : null;
    public string? Message { get; private set; }
    public int CompletedStepCount => _stepIndex;

    public event Action? Changed;
    public event Action<ProcedureStep>? StepCompleted;

    public void Start(ProcedureDefinition definition, AircraftState state)
    {
        _definition = definition;
        _stepIndex = 0;
        _manualConfirmationReceived = false;
        _lastAutomaticStepId = null;
        _nextAutomaticActionUtc = DateTime.MinValue;
        Status = ProcedureStatus.Running;
        Message = $"Started {definition.Name}.";
        Advance(state);
    }

    public void Update(AircraftState state)
    {
        if (Status is ProcedureStatus.Running
            or ProcedureStatus.WaitingForManualAction
            or ProcedureStatus.WaitingForVerification)
        {
            Advance(state);
        }
    }

    public void ConfirmManualStep(AircraftState state)
    {
        if (Status != ProcedureStatus.WaitingForManualAction || CurrentStep == null)
        {
            Message = "No manual step is awaiting confirmation.";
            Changed?.Invoke();
            return;
        }

        _manualConfirmationReceived = true;
        Message = $"Pilot confirmed: {CurrentStep.Label}.";
        Status = ProcedureStatus.Running;
        Advance(state);
    }

    public void Pause()
    {
        if (Status is ProcedureStatus.Running
            or ProcedureStatus.WaitingForManualAction
            or ProcedureStatus.WaitingForVerification)
        {
            Status = ProcedureStatus.Paused;
            Message = "Procedure paused.";
            Changed?.Invoke();
        }
    }

    public void Resume(AircraftState state)
    {
        if (Status != ProcedureStatus.Paused)
        {
            return;
        }

        Status = ProcedureStatus.Running;
        Message = "Procedure resumed.";
        Advance(state);
    }

    public void Cancel()
    {
        _definition = null;
        _stepIndex = 0;
        _manualConfirmationReceived = false;
        _lastAutomaticStepId = null;
        _nextAutomaticActionUtc = DateTime.MinValue;
        Status = ProcedureStatus.Idle;
        Message = "Procedure cancelled.";
        Changed?.Invoke();
    }

    public void Fail(string message)
    {
        if (_definition == null
            || Status is ProcedureStatus.Completed or ProcedureStatus.Failed)
        {
            return;
        }

        Status = ProcedureStatus.Failed;
        Message = message;
        Changed?.Invoke();
    }

    private void Advance(AircraftState state)
    {
        if (_definition == null || Status == ProcedureStatus.Paused)
        {
            return;
        }

        while (_stepIndex < _definition.Steps.Count)
        {
            var step = _definition.Steps[_stepIndex];
            if (step.IsComplete(state))
            {
                var automaticActionWasIssued =
                    step.Kind == ProcedureStepKind.AutomaticAction
                    && string.Equals(
                        _lastAutomaticStepId,
                        step.Id,
                        StringComparison.Ordinal);
                _stepIndex++;
                _manualConfirmationReceived = false;
                _lastAutomaticStepId = null;
                if (automaticActionWasIssued)
                {
                    _nextAutomaticActionUtc =
                        DateTime.UtcNow.Add(AutomaticActionCadence);
                }
                Message =
                    step.Kind == ProcedureStepKind.AutomaticAction
                    && !automaticActionWasIssued
                        ? $"Already set: {step.Label}."
                        : $"Completed: {step.Label}.";
                if (step.Kind is ProcedureStepKind.AutomaticAction
                    or ProcedureStepKind.Observe)
                {
                    Changed?.Invoke();
                }
                StepCompleted?.Invoke(step);
                continue;
            }

            switch (step.Kind)
            {
                case ProcedureStepKind.Observe:
                    SetWaitingState(
                        ProcedureStatus.WaitingForVerification,
                        $"Waiting for condition: {step.Label}.");
                    return;

                case ProcedureStepKind.ManualAction:
                    if (_manualConfirmationReceived)
                    {
                        _stepIndex++;
                        _manualConfirmationReceived = false;
                        Message = $"Manually completed: {step.Label}.";
                        continue;
                    }

                    SetWaitingState(
                        ProcedureStatus.WaitingForManualAction,
                        step.ManualInstruction ?? $"Complete manually: {step.Label}.");
                    return;

                case ProcedureStepKind.AutomaticAction:
                    if (DateTime.UtcNow < _nextAutomaticActionUtc)
                    {
                        return;
                    }

                    if (_automationPolicy() == Domain.AutomationPolicy.MonitorOnly)
                    {
                        if (_manualConfirmationReceived)
                        {
                            _stepIndex++;
                            _manualConfirmationReceived = false;
                            Message = $"Pilot confirmed manual completion: {step.Label}.";
                            continue;
                        }

                        SetWaitingState(
                            ProcedureStatus.WaitingForManualAction,
                            $"Monitor-only mode: complete manually — {step.Label}.");
                        return;
                    }

                    if (_automationPolicy() == Domain.AutomationPolicy.AlwaysAskBeforeAction
                        && !_manualConfirmationReceived)
                    {
                        SetWaitingState(
                            ProcedureStatus.WaitingForManualAction,
                            $"Confirm automatic action: {step.Label}.");
                        return;
                    }

                    _manualConfirmationReceived = false;
                    var actionMessage = $"Executing: {step.Label}.";
                    var actionStateChanged =
                        Status != ProcedureStatus.WaitingForVerification
                        || !string.Equals(Message, actionMessage, StringComparison.Ordinal);
                    Status = ProcedureStatus.WaitingForVerification;
                    Message = actionMessage;
                    if (_lastAutomaticStepId != step.Id && step.Command != null)
                    {
                        _lastAutomaticStepId = step.Id;
                        _executeCommand(step.Command);
                    }

                    if (actionStateChanged)
                    {
                        Changed?.Invoke();
                    }
                    return;
            }
        }

        Status = ProcedureStatus.Completed;
        Message = $"{_definition.Name} completed.";
        Changed?.Invoke();
    }

    private void SetWaitingState(ProcedureStatus status, string message)
    {
        if (Status == status
            && string.Equals(Message, message, StringComparison.Ordinal))
        {
            return;
        }

        Status = status;
        Message = message;
        Changed?.Invoke();
    }
}

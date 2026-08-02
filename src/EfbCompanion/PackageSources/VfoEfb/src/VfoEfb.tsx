import {
  App,
  AppBootMode,
  AppInstallProps,
  AppSuspendMode,
  AppView,
  AppViewProps,
  Efb,
  RequiredProps,
  TVNode,
} from "@efb/efb-api";
import {
  FSComponent,
  NodeReference,
  VNode,
} from "@microsoft/msfs-sdk";

import "./VfoEfb.scss";

declare const BASE_URL: string;
declare const Include: { addScript(path: string): void };

interface CommBusListener {
  on(eventName: string, callback: (payload: string) => void): void;
  off(eventName: string, callback: (payload: string) => void): void;
  callSimConnect(eventName: string, payload: string): Promise<unknown>;
}

declare function RegisterCommBusListener(
  callback?: () => void
): CommBusListener;

Include.addScript("/JS/Services/CommBus.js");

const COMMAND_EVENT = "VFO_EFB_COMMAND_V2";
const STATE_EVENT = "VFO_EFB_STATE_V2";
const PROTOCOL_VERSION = 2;

interface FlowListItem {
  id: string;
  name: string;
  automationSummary: string;
  state: "done" | "current" | "next" | "upcoming";
}

interface CompanionState {
  protocolVersion: number;
  kind: "state";
  companionVersion: string;
  connected: boolean;
  aircraftReady: boolean;
  aircraft: {
    title: string;
    family: string;
    supported: boolean;
    warning: string | null;
    phase: string;
  };
  telemetry: {
    aglFeet: number;
    altitudeFeet: number;
    airspeedKnots: number;
    verticalSpeedFpm: number;
  };
  flow: {
    id: string | null;
    name: string;
    status: string;
    currentStepId: string | null;
    currentStep: string;
    assignedRole: string;
    completedSteps: number;
    totalSteps: number;
    waitingFor: string;
    canStart: boolean;
    canConfirm: boolean;
    canPause: boolean;
    canResume: boolean;
    canCancel: boolean;
  };
  flows?: FlowListItem[];
  gsx: {
    summary: string;
    passengerOperation?: string;
    passengerProgress: string | null;
    passengerPercent: number;
    actionRequired: string | null;
    hasActionRequired: boolean;
    activeServices: string[];
    promptTitle?: string | null;
    choices?: string[];
    canOpenMenu?: boolean;
  };
}

interface CommandResult {
  protocolVersion: number;
  kind: "commandResult";
  requestId: string;
  accepted: boolean;
  message: string;
}

class VfoEfbView extends AppView<RequiredProps<AppViewProps, "bus">> {
  private readonly connectionRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly aircraftRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly phaseRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly telemetryRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly aircraftWarningRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly flowNameRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly flowStatusRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly stepRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly roleRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly waitingRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly progressLabelRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly progressBarRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly flowListRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly flowSelectRef =
    FSComponent.createRef<HTMLSelectElement>();
  private readonly startButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly nextFlowButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly nextFlowHitboxRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly confirmButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly pauseButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly resumeButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly cancelButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly gsxSummaryRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly gsxOperationRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly gsxPassengerRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly gsxProgressRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly gsxActionRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly gsxPromptRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly gsxChoicesRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly gsxOpenMenuButtonRef =
    FSComponent.createRef<HTMLButtonElement>();
  private readonly resultRef =
    FSComponent.createRef<HTMLDivElement>();

  private listener?: CommBusListener;
  private lastState?: CompanionState;
  private lastStateReceivedAt = 0;
  private lastDesktopContactAt = 0;
  private stateRequestPending = false;
  private lastStateRequestAt = 0;
  private staleTimer?: number;
  private readonly receiveEnvelope =
    (payload: string): void => this.onEnvelope(payload);
  private readonly handleNextFlowClick =
    (): void => {
      if (!this.nextFlowButtonRef.instance.disabled) {
        this.startNextFlow();
      }
    };
  private readonly handleStartFlowClick =
    (): void => this.startSelectedFlow();
  private readonly handleConfirmClick =
    (): void => this.sendCommand("confirm");
  private readonly handlePauseClick =
    (): void => this.sendCommand("pause");
  private readonly handleResumeClick =
    (): void => this.sendCommand("resume");
  private readonly handleCancelClick =
    (): void => this.cancelFlow();
  private readonly handleGsxOpenMenuClick =
    (): void => this.sendCommand("gsx_open_menu");

  public onAfterRender(_node: VNode): void {
    // The MSFS SDK FSComponent renderer treats raw JSX event props as string
    // attributes. Wire native controls explicitly so Coherent receives clicks.
    this.nextFlowHitboxRef.instance.addEventListener(
      "click",
      this.handleNextFlowClick
    );
    this.startButtonRef.instance.addEventListener(
      "click",
      this.handleStartFlowClick
    );
    this.confirmButtonRef.instance.addEventListener(
      "click",
      this.handleConfirmClick
    );
    this.pauseButtonRef.instance.addEventListener(
      "click",
      this.handlePauseClick
    );
    this.resumeButtonRef.instance.addEventListener(
      "click",
      this.handleResumeClick
    );
    this.cancelButtonRef.instance.addEventListener(
      "click",
      this.handleCancelClick
    );
    this.gsxOpenMenuButtonRef.instance.addEventListener(
      "click",
      this.handleGsxOpenMenuClick
    );
    this.initializeCommBus();
    this.staleTimer = window.setInterval(
      () => this.updateStaleState(),
      1000
    );
  }

  public onOpen(): void {
    this.requestState();
  }

  public onResume(): void {
    this.requestState();
  }

  public onClose(): void {
    this.nextFlowHitboxRef.instance.removeEventListener(
      "click",
      this.handleNextFlowClick
    );
    this.startButtonRef.instance.removeEventListener(
      "click",
      this.handleStartFlowClick
    );
    this.confirmButtonRef.instance.removeEventListener(
      "click",
      this.handleConfirmClick
    );
    this.pauseButtonRef.instance.removeEventListener(
      "click",
      this.handlePauseClick
    );
    this.resumeButtonRef.instance.removeEventListener(
      "click",
      this.handleResumeClick
    );
    this.cancelButtonRef.instance.removeEventListener(
      "click",
      this.handleCancelClick
    );
    this.gsxOpenMenuButtonRef.instance.removeEventListener(
      "click",
      this.handleGsxOpenMenuClick
    );
    if (this.staleTimer !== undefined) {
      window.clearInterval(this.staleTimer);
      this.staleTimer = undefined;
    }
    if (this.listener) {
      this.listener.off(STATE_EVENT, this.receiveEnvelope);
    }
  }

  private initializeCommBus(attempt = 0): void {
    if (typeof RegisterCommBusListener !== "function") {
      if (attempt < 30) {
        window.setTimeout(
          () => this.initializeCommBus(attempt + 1),
          250
        );
      } else {
        this.showConnection(
          "CommBus unavailable",
          "error"
        );
      }
      return;
    }

    this.listener = RegisterCommBusListener(() => {
      this.showConnection("Waiting for desktop companion", "waiting");
      this.requestState();
    });
    this.listener.on(STATE_EVENT, this.receiveEnvelope);
  }

  private requestState(): void {
    if (!this.listener) {
      return;
    }
    const now = Date.now();
    if (now - this.lastStateRequestAt < 3000) {
      return;
    }
    this.stateRequestPending = true;
    this.lastStateRequestAt = now;
    const request = {
      protocolVersion: PROTOCOL_VERSION,
      requestId: `state-${now}`,
      action: "request_state",
    };
    this.listener
      .callSimConnect(COMMAND_EVENT, JSON.stringify(request))
      .catch(() => {
        this.stateRequestPending = false;
        this.showConnection("Desktop companion unavailable", "error");
      });
  }

  private sendCommand(
    action: string,
    flowId?: string,
    choiceIndex?: number
  ): void {
    if (!this.listener) {
      this.showResult(false, "Desktop companion is not connected.");
      return;
    }

    const requestId =
      `${Date.now()}-${Math.floor(Math.random() * 100000)}`;
    const command = {
      protocolVersion: PROTOCOL_VERSION,
      requestId,
      action,
      ...(flowId ? { flowId } : {}),
      ...(choiceIndex !== undefined ? { choiceIndex } : {}),
    };
    this.showResult(true, "Sending command...");
    this.listener
      .callSimConnect(COMMAND_EVENT, JSON.stringify(command))
      .catch(() =>
        this.showResult(false, "Could not send command to desktop companion.")
      );
  }

  private onEnvelope(payload: string): void {
    let envelope: CompanionState | CommandResult;
    try {
      envelope = JSON.parse(payload) as CompanionState | CommandResult;
    } catch {
      // A busy SimConnect frame can occasionally deliver one incomplete
      // background snapshot. Preserve the last authoritative UI state and
      // ask for a clean replacement instead of surfacing a pilot-facing
      // command error.
      this.stateRequestPending = false;
      this.requestState();
      if (!this.lastState) {
        this.showConnection("Waiting for desktop state", "waiting");
      }
      return;
    }

    if (envelope.protocolVersion !== PROTOCOL_VERSION) {
      this.showResult(false, "Desktop and EFB protocol versions do not match.");
      return;
    }

    this.lastDesktopContactAt = Date.now();

    if (envelope.kind === "commandResult") {
      // Desktop builds before 0.2.6 acknowledged background state requests.
      // Do not display or follow those acknowledgements with another refresh.
      if (envelope.requestId.startsWith("state-")) {
        this.stateRequestPending = false;
        return;
      }
      this.showResult(envelope.accepted, envelope.message);
      window.setTimeout(() => this.requestState(), 200);
      return;
    }

    this.stateRequestPending = false;
    try {
      this.renderState(envelope);
      this.lastState = envelope;
      this.lastStateReceivedAt = Date.now();
    } catch {
      this.showConnection("EFB display update failed", "error");
      this.showResult(
        false,
        "The EFB could not display the latest desktop state."
      );
    }
  }

  private renderState(state: CompanionState): void {
    // Keep the primary launcher independent from the secondary cards. Even if
    // an optional state section is absent, starting the next flow must remain
    // available.
    const flows = Array.isArray(state.flows) ? state.flows : [];
    this.renderNextFlowAction(flows, Boolean(state.flow?.canStart));
    this.renderFlowSelect(flows, Boolean(state.flow?.canStart));
    this.renderFlowList(flows);

    this.showConnection(
      state.connected && state.aircraftReady
        ? `Connected · desktop v${state.companionVersion}`
        : state.connected
          ? "Connected · waiting for aircraft"
          : "Desktop companion disconnected",
      state.connected ? "connected" : "error"
    );

    this.aircraftRef.instance.textContent = state.aircraft.title;
    this.phaseRef.instance.textContent = state.aircraft.phase;
    this.telemetryRef.instance.textContent =
      `AGL ${Math.round(state.telemetry.aglFeet)} ft  ·  ` +
      `ALT ${Math.round(state.telemetry.altitudeFeet)} ft  ·  ` +
      `IAS ${Math.round(state.telemetry.airspeedKnots)} kt  ·  ` +
      `VS ${Math.round(state.telemetry.verticalSpeedFpm)} fpm`;
    this.aircraftWarningRef.instance.textContent =
      state.aircraft.warning ?? "";
    this.aircraftWarningRef.instance.style.display =
      state.aircraft.warning ? "block" : "none";

    this.flowNameRef.instance.textContent = state.flow.name;
    this.flowStatusRef.instance.textContent = state.flow.status;
    this.flowStatusRef.instance.className =
      `status-pill ${this.statusClass(state.flow.status)}`;
    this.stepRef.instance.textContent = state.flow.currentStep;
    this.roleRef.instance.textContent = state.flow.assignedRole;
    this.waitingRef.instance.textContent = state.flow.waitingFor;

    const progress = state.flow.totalSteps === 0
      ? 0
      : Math.round(
          state.flow.completedSteps * 100 / state.flow.totalSteps
        );
    this.progressLabelRef.instance.textContent =
      state.flow.totalSteps === 0
        ? "No flow active"
        : `${state.flow.completedSteps} of ${state.flow.totalSteps} steps · ${progress}%`;
    this.progressBarRef.instance.style.width = `${progress}%`;

    this.confirmButtonRef.instance.disabled = !state.flow.canConfirm;
    this.pauseButtonRef.instance.disabled = !state.flow.canPause;
    this.resumeButtonRef.instance.disabled = !state.flow.canResume;
    this.cancelButtonRef.instance.disabled = !state.flow.canCancel;

    this.renderGsx(state);
  }

  private renderNextFlowAction(
    flows: FlowListItem[],
    canStart: boolean
  ): void {
    const nextFlow =
      flows.find(flow => flow.state === "next")
      ?? flows.find(
        flow => flow.state !== "done" && flow.state !== "current"
      );
    const button = this.nextFlowButtonRef.instance;
    button.textContent = nextFlow
      ? `Start ${nextFlow.name}`
      : "Start next flow";
    button.disabled = !canStart;
  }

  private renderFlowList(flows: FlowListItem[]): void {
    const container = this.flowListRef.instance;
    // Coherent GT in MSFS does not consistently implement replaceChildren().
    // Clearing textContent is supported by the in-simulator browser.
    container.textContent = "";
    for (const flow of flows) {
      const row = document.createElement("div");
      row.className = `flow-row ${flow.state}`;

      const marker = document.createElement("span");
      marker.className = "flow-marker";
      marker.textContent = flow.state === "done" ? "✓" : "•";

      const name = document.createElement("span");
      name.className = "flow-row-name";
      name.textContent = flow.name;

      const state = document.createElement("span");
      state.className = "flow-row-state";
      state.textContent = flow.state.toUpperCase();

      row.append(marker, name, state);
      container.appendChild(row);
    }
  }

  private renderFlowSelect(
    flows: FlowListItem[],
    canStart: boolean
  ): void {
    const select = this.flowSelectRef.instance;
    const previousValue = select.value;
    select.options.length = 0;

    for (const flow of flows) {
      if (flow.state !== "next") {
        continue;
      }
      const option = document.createElement("option");
      option.value = flow.id;
      option.textContent =
        `${flow.state === "next" ? "Next · " : ""}${flow.name}`;
      select.appendChild(option);
    }
    if (
      previousValue
      && Array.from(select.options).some(
        option => option.value === previousValue
      )
    ) {
      select.value = previousValue;
    }

    select.disabled = !canStart || select.options.length === 0;
    this.startButtonRef.instance.disabled = select.disabled;
  }

  private renderGsx(state: CompanionState): void {
    this.gsxSummaryRef.instance.textContent = state.gsx.summary;
    this.gsxOperationRef.instance.textContent =
      state.gsx.passengerOperation ?? "Passengers";
    this.gsxPassengerRef.instance.textContent =
      state.gsx.passengerProgress ?? "Passenger progress unavailable";
    this.gsxProgressRef.instance.style.width =
      `${Math.max(0, Math.min(100, state.gsx.passengerPercent))}%`;
    this.gsxActionRef.instance.textContent =
      state.gsx.hasActionRequired
        ? `Action required · ${state.gsx.actionRequired}`
        : "No GSX action required";
    this.gsxActionRef.instance.className =
      state.gsx.hasActionRequired
        ? "gsx-action required"
        : "gsx-action";

    const choices = Array.isArray(state.gsx.choices)
      ? state.gsx.choices
      : [];
    const hasPrompt = Boolean(state.gsx.promptTitle) && choices.length > 0;
    const openMenuButton = this.gsxOpenMenuButtonRef.instance;
    openMenuButton.disabled = !Boolean(state.gsx.canOpenMenu);
    openMenuButton.style.display = hasPrompt ? "none" : "block";
    this.gsxPromptRef.instance.textContent = hasPrompt
      ? state.gsx.promptTitle ?? "GSX response required"
      : "";
    this.gsxPromptRef.instance.style.display = hasPrompt ? "block" : "none";

    const choicesContainer = this.gsxChoicesRef.instance;
    choicesContainer.textContent = "";
    choicesContainer.style.display = hasPrompt ? "grid" : "none";
    if (hasPrompt) {
      choices.forEach((label, choiceIndex) => {
        const button = document.createElement("button");
        button.className = "gsx-choice-button";
        button.textContent = label;
        button.addEventListener(
          "click",
          () => this.sendCommand(
            "gsx_menu_choice",
            undefined,
            choiceIndex
          )
        );
        choicesContainer.appendChild(button);
      });
    }
  }

  private updateStaleState(): void {
    const stateAge = this.lastStateReceivedAt === 0
      ? Number.POSITIVE_INFINITY
      : Date.now() - this.lastStateReceivedAt;
    if (stateAge > 2000) {
      this.requestState();
    }
    const contactAge = this.lastDesktopContactAt === 0
      ? Number.POSITIVE_INFINITY
      : Date.now() - this.lastDesktopContactAt;
    if (
      this.lastDesktopContactAt !== 0
      && contactAge > 12000
    ) {
      this.showConnection("Desktop companion not responding", "error");
      this.disableActions();
    }
  }

  private disableActions(): void {
    this.startButtonRef.instance.disabled = true;
    this.nextFlowButtonRef.instance.disabled = true;
    this.confirmButtonRef.instance.disabled = true;
    this.pauseButtonRef.instance.disabled = true;
    this.resumeButtonRef.instance.disabled = true;
    this.cancelButtonRef.instance.disabled = true;
    this.gsxOpenMenuButtonRef.instance.disabled = true;
  }

  private showConnection(
    text: string,
    state: "connected" | "waiting" | "error"
  ): void {
    this.connectionRef.instance.textContent = text;
    this.connectionRef.instance.className = `connection ${state}`;
  }

  private showResult(accepted: boolean, message: string): void {
    this.resultRef.instance.textContent = message;
    this.resultRef.instance.className =
      accepted ? "command-result accepted" : "command-result rejected";
  }

  private statusClass(status: string): string {
    const normalized = status.toLowerCase();
    if (normalized.includes("waiting")) {
      return "waiting";
    }
    if (normalized.includes("failed")) {
      return "failed";
    }
    if (normalized.includes("complete")) {
      return "complete";
    }
    if (normalized.includes("running") || normalized.includes("monitor")) {
      return "running";
    }
    return "idle";
  }

  private startSelectedFlow(): void {
    const flowId = this.flowSelectRef.instance.value;
    if (flowId) {
      this.sendCommand("start_flow", flowId);
    }
  }

  private startNextFlow(): void {
    this.sendCommand("start_next_flow");
  }

  private cancelFlow(): void {
    if (window.confirm("Cancel the active Virtual First Officer flow?")) {
      this.sendCommand("cancel");
    }
  }

  public render(): VNode {
    return (
      <div class="vfo-efb">
        <header class="app-header">
          <div>
            <div class="eyebrow">MSFS 2024 - EFB build 0.2.10</div>
            <h1>Virtual First Officer</h1>
          </div>
          <div ref={this.connectionRef} class="connection waiting">
            Connecting to desktop companion
          </div>
        </header>

        <section class="quick-start">
          <div>
            <div class="section-label">Recommended action</div>
            <div class="quick-start-copy">
              Continue the gate-to-gate sequence
            </div>
          </div>
          <div ref={this.nextFlowHitboxRef} class="next-flow-hitbox">
            <button
              ref={this.nextFlowButtonRef}
              class="button next-flow"
              disabled
            >
              Waiting for flow state
            </button>
          </div>
        </section>

        <main class="dashboard-grid">
          <section class="card current-action">
            <div class="section-heading">
              <div>
                <span class="section-label">Current flow</span>
                <div ref={this.flowNameRef} class="flow-name">
                  No active flow
                </div>
              </div>
              <span ref={this.flowStatusRef} class="status-pill idle">
                Idle
              </span>
            </div>

            <div class="step-card">
              <div class="step-meta">
                <span>Current action</span>
                <span ref={this.roleRef}></span>
              </div>
              <div ref={this.stepRef} class="step-title">
                Waiting for desktop companion
              </div>
              <div ref={this.waitingRef} class="waiting-reason">
                Start the desktop application to connect.
              </div>
            </div>

            <div class="progress-header">
              <span>Flow progress</span>
              <span ref={this.progressLabelRef}>No flow active</span>
            </div>
            <div class="progress-track">
              <div ref={this.progressBarRef} class="progress-fill"></div>
            </div>

            <div class="primary-actions">
              <button
                ref={this.confirmButtonRef}
                class="button primary"
                disabled
              >
                Confirm action
              </button>
              <button
                ref={this.pauseButtonRef}
                class="button secondary"
                disabled
              >
                Pause
              </button>
              <button
                ref={this.resumeButtonRef}
                class="button secondary"
                disabled
              >
                Resume
              </button>
              <button
                ref={this.cancelButtonRef}
                class="button danger"
                disabled
              >
                Cancel
              </button>
            </div>
            <div ref={this.resultRef} class="command-result"></div>
          </section>

          <section class="card aircraft-card">
            <div class="section-label">Aircraft</div>
            <div ref={this.aircraftRef} class="aircraft-name">
              Waiting for aircraft
            </div>
            <div class="phase-line">
              Phase <span ref={this.phaseRef}>Unknown</span>
            </div>
            <div ref={this.telemetryRef} class="telemetry">
              AGL 0 ft · ALT 0 ft · IAS 0 kt · VS 0 fpm
            </div>
            <div
              ref={this.aircraftWarningRef}
              class="aircraft-warning"
              style="display: none"
            ></div>
          </section>

          <section class="card gsx-card">
            <div class="section-label">GSX ground services</div>
            <div ref={this.gsxSummaryRef} class="gsx-summary">
              Waiting for GSX status
            </div>
            <div class="progress-header">
              <span ref={this.gsxOperationRef}>Passengers</span>
              <span ref={this.gsxPassengerRef}>
                Passenger progress unavailable
              </span>
            </div>
            <div class="progress-track compact">
              <div ref={this.gsxProgressRef} class="progress-fill gsx"></div>
            </div>
            <div ref={this.gsxActionRef} class="gsx-action">
              No GSX action required
            </div>
            <button
              ref={this.gsxOpenMenuButtonRef}
              class="button gsx-open-menu"
              disabled
            >
              Open GSX menu
            </button>
            <div ref={this.gsxPromptRef} class="gsx-prompt"></div>
            <div ref={this.gsxChoicesRef} class="gsx-choices"></div>
          </section>

          <section class="card flow-list-card">
            <div class="section-label">Gate-to-gate progress</div>
            <div ref={this.flowListRef} class="flow-list"></div>
            <div class="start-flow">
              <select ref={this.flowSelectRef} disabled></select>
              <button
                ref={this.startButtonRef}
                class="button start"
                disabled
              >
                Start flow
              </button>
            </div>
          </section>
        </main>
      </div>
    );
  }
}

// The class name is the EFB internal app identity and is applied to the host
// `.efb-view` element. Version it together with the CSS scope when forcing
// MSFS to discard a cached app and stylesheet.
class VfoEfbV11 extends App {
  public get name(): string {
    return "Virtual First Officer";
  }

  public get icon(): string {
    return `${BASE_URL}/Assets/app-icon.svg`;
  }

  public BootMode = AppBootMode.WARM;
  public SuspendMode = AppSuspendMode.SLEEP;

  public async install(_props: AppInstallProps): Promise<void> {
    Efb.loadCss(`${BASE_URL}/VfoEfb.css`);
    return Promise.resolve();
  }

  public get compatibleAircraftModels(): string[] | undefined {
    return undefined;
  }

  public render(): TVNode<VfoEfbView> {
    return <VfoEfbView bus={this.bus} />;
  }
}

Efb.use(VfoEfbV11);

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

const COMMAND_EVENT = "MSFS2024_AI_EFB_COMMAND_V1";
const STATE_REQUEST_EVENT = "MSFS2024_AI_EFB_STATE_REQUEST_V1";
const STATE_EVENT = "MSFS2024_AI_EFB_STATE_V1";
const PROTOCOL_VERSION = 1;

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
  flows: FlowListItem[];
  gsx: {
    summary: string;
    passengerProgress: string | null;
    passengerPercent: number;
    actionRequired: string | null;
    hasActionRequired: boolean;
    activeServices: string[];
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
  private readonly gsxPassengerRef =
    FSComponent.createRef<HTMLSpanElement>();
  private readonly gsxProgressRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly gsxActionRef =
    FSComponent.createRef<HTMLDivElement>();
  private readonly resultRef =
    FSComponent.createRef<HTMLDivElement>();

  private listener?: CommBusListener;
  private lastState?: CompanionState;
  private lastStateReceivedAt = 0;
  private staleTimer?: number;
  private readonly receiveEnvelope =
    (payload: string): void => this.onEnvelope(payload);

  public onAfterRender(_node: VNode): void {
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
    this.listener
      .callSimConnect(STATE_REQUEST_EVENT, "{}")
      .catch(() =>
        this.showConnection("Desktop companion unavailable", "error")
      );
  }

  private sendCommand(action: string, flowId?: string): void {
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
      this.showResult(false, "Received an invalid companion response.");
      return;
    }

    if (envelope.protocolVersion !== PROTOCOL_VERSION) {
      this.showResult(false, "Desktop and EFB protocol versions do not match.");
      return;
    }

    if (envelope.kind === "commandResult") {
      this.showResult(envelope.accepted, envelope.message);
      window.setTimeout(() => this.requestState(), 200);
      return;
    }

    this.lastState = envelope;
    this.lastStateReceivedAt = Date.now();
    this.renderState(envelope);
  }

  private renderState(state: CompanionState): void {
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

    this.renderFlowList(state.flows);
    this.renderFlowSelect(state.flows, state.flow.canStart);
    this.renderGsx(state);
  }

  private renderFlowList(flows: FlowListItem[]): void {
    const container = this.flowListRef.instance;
    container.replaceChildren();
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
    select.replaceChildren();

    for (const flow of flows) {
      if (flow.state === "done" || flow.state === "current") {
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
  }

  private updateStaleState(): void {
    if (
      this.lastStateReceivedAt !== 0
      && Date.now() - this.lastStateReceivedAt > 5000
    ) {
      this.showConnection("Desktop companion not responding", "error");
      this.disableActions();
    }
  }

  private disableActions(): void {
    this.startButtonRef.instance.disabled = true;
    this.confirmButtonRef.instance.disabled = true;
    this.pauseButtonRef.instance.disabled = true;
    this.resumeButtonRef.instance.disabled = true;
    this.cancelButtonRef.instance.disabled = true;
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
            <div class="eyebrow">MSFS 2024</div>
            <h1>Virtual First Officer</h1>
          </div>
          <div ref={this.connectionRef} class="connection waiting">
            Connecting to desktop companion
          </div>
        </header>

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
                onClick={() => this.sendCommand("confirm")}
              >
                Confirm action
              </button>
              <button
                ref={this.pauseButtonRef}
                class="button secondary"
                disabled
                onClick={() => this.sendCommand("pause")}
              >
                Pause
              </button>
              <button
                ref={this.resumeButtonRef}
                class="button secondary"
                disabled
                onClick={() => this.sendCommand("resume")}
              >
                Resume
              </button>
              <button
                ref={this.cancelButtonRef}
                class="button danger"
                disabled
                onClick={() => this.cancelFlow()}
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
              <span>Boarding</span>
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
                onClick={() => this.startSelectedFlow()}
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

class VirtualFirstOfficerEfb extends App {
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

Efb.use(VirtualFirstOfficerEfb);

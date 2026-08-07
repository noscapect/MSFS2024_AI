import { DurableObject } from "cloudflare:workers";

interface Env {
  COMPANION_SESSIONS: DurableObjectNamespace<CompanionSession>;
}

interface SocketAttachment {
  role: "desktop" | "tablet";
}

const SESSION_PATTERN = /^[a-zA-Z0-9_-]{20,80}$/;
const MAX_MESSAGE_BYTES = 64 * 1024;

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    if (url.pathname === "/health") {
      return Response.json({ status: "ok" });
    }

    const match = /^\/v1\/session\/([^/]+)$/.exec(url.pathname);
    if (!match || !SESSION_PATTERN.test(match[1])) {
      return new Response("Not found", { status: 404 });
    }
    if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") {
      return new Response("WebSocket upgrade required", { status: 426 });
    }

    const id = env.COMPANION_SESSIONS.idFromName(match[1]);
    const session = env.COMPANION_SESSIONS.get(id);
    const headers = new Headers(request.headers);
    return session.fetch(new Request(request, { headers }));
  },
} satisfies ExportedHandler<Env>;

export class CompanionSession extends DurableObject<Env> {
  constructor(
    private readonly state: DurableObjectState,
    env: Env,
  ) {
    super(state, env);
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);
    const role = url.searchParams.get("role");
    if (role !== "desktop" && role !== "tablet") {
      return new Response("Invalid peer role", { status: 400 });
    }

    const authorization = request.headers.get("Authorization");
    if (!authorization?.startsWith("Bearer ")) {
      return new Response("Missing bearer credential", { status: 401 });
    }
    const credential = authorization.slice("Bearer ".length);
    if (credential.length < 32 || credential.length > 256) {
      return new Response("Invalid bearer credential", { status: 401 });
    }

    const presentedHash = await sha256(credential);
    const storedHash = await this.state.storage.get<string>("credentialHash");
    if (storedHash === undefined) {
      if (role !== "desktop") {
        return new Response("The desktop must open the session first", { status: 409 });
      }
      await this.state.storage.put("credentialHash", presentedHash);
    } else if (!constantTimeEqual(storedHash, presentedHash)) {
      return new Response("Invalid bearer credential", { status: 401 });
    }

    const pair = new WebSocketPair();
    const client = pair[0];
    const server = pair[1];
    for (const existing of this.state.getWebSockets(role)) {
      existing.close(4001, `${role} connection replaced`);
    }
    server.serializeAttachment({ role } satisfies SocketAttachment);
    this.state.acceptWebSocket(server, [role]);

    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(socket: WebSocket, message: string | ArrayBuffer): Promise<void> {
    const attachment = socket.deserializeAttachment() as SocketAttachment | null;
    if (attachment === null) {
      socket.close(4002, "Missing peer identity");
      return;
    }

    const size = typeof message === "string"
      ? new TextEncoder().encode(message).byteLength
      : message.byteLength;
    if (size > MAX_MESSAGE_BYTES) {
      socket.close(4003, "Message exceeds limit");
      return;
    }

    const recipients = attachment.role === "desktop"
      ? this.state.getWebSockets("tablet")
      : this.state.getWebSockets("desktop");
    for (const recipient of recipients) {
      recipient.send(message);
    }
  }

  webSocketClose(
    socket: WebSocket,
    code: number,
    reason: string,
    wasClean: boolean,
  ): void {
    socket.close(code, reason.slice(0, 120));
  }

  webSocketError(socket: WebSocket): void {
    socket.close(1011, "Relay WebSocket error");
  }
}

async function sha256(value: string): Promise<string> {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}

function constantTimeEqual(left: string, right: string): boolean {
  if (left.length !== right.length) return false;
  let difference = 0;
  for (let index = 0; index < left.length; index++) {
    difference |= left.charCodeAt(index) ^ right.charCodeAt(index);
  }
  return difference === 0;
}

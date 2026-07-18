import { spawn, ChildProcessWithoutNullStreams } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

type BridgeResponse = {
  ok?: boolean;
  error?: string;
  event?: string;
  [key: string]: unknown;
};

function resolveBridgePath(): string | null {
  // When Stream Deck runs the plugin, this file lives in .../sdPlugin/bin/plugin.js
  const here = dirname(fileURLToPath(import.meta.url));
  const candidates = [
    join(here, "QuantumBridge.exe"),
    join(here, "..", "bin", "QuantumBridge.exe"),
    join(process.cwd(), "bin", "QuantumBridge.exe"),
    join(process.cwd(), "com.pj289.jbl-quantum.sdPlugin", "bin", "QuantumBridge.exe"),
  ];
  return candidates.find((p) => existsSync(p)) ?? null;
}

export class QuantumBridgeClient {
  private process: ChildProcessWithoutNullStreams | null = null;
  private pending = new Map<number, { resolve: (v: BridgeResponse) => void; reject: (e: Error) => void }>();
  private seq = 0;
  private buffer = "";
  private bridgePath: string | null = null;
  private readyWaiters: Array<{ resolve: () => void; reject: (e: Error) => void }> = [];
  private isReady = false;

  async ensureReady(): Promise<void> {
    if (this.process && this.isReady) {
      return;
    }

    if (this.process && !this.isReady) {
      await new Promise<void>((resolve, reject) => {
        this.readyWaiters.push({ resolve, reject });
      });
      return;
    }

    this.bridgePath = resolveBridgePath();
    if (!this.bridgePath) {
      throw new Error("QuantumBridge.exe not found. Run: npm run build:bridge && npm run build");
    }

    const enginePath =
      process.env.QUANTUM_ENGINE_PATH?.trim() || "C:\\Program Files\\JBL\\QuantumENGINE";

    this.isReady = false;
    this.process = spawn(this.bridgePath, [], {
      // Working directory = Quantum Engine install so native deps resolve there.
      cwd: enginePath,
      env: { ...process.env, QUANTUM_ENGINE_PATH: enginePath },
      stdio: ["pipe", "pipe", "pipe"],
    });

    this.process.stdout.on("data", (chunk: Buffer) => {
      this.buffer += chunk.toString("utf8");
      let index: number;
      while ((index = this.buffer.indexOf("\n")) >= 0) {
        const line = this.buffer.slice(0, index).trim();
        this.buffer = this.buffer.slice(index + 1);
        if (!line) continue;
        this.dispatch(JSON.parse(line) as BridgeResponse);
      }
    });

    this.process.stderr.on("data", (chunk: Buffer) => {
      console.error("[QuantumBridge]", chunk.toString("utf8"));
    });

    this.process.on("exit", () => {
      this.process = null;
      this.isReady = false;
      const startError = new Error("QuantumBridge exited");
      for (const waiter of this.readyWaiters) {
        waiter.reject(startError);
      }
      this.readyWaiters = [];
      for (const [, handlers] of this.pending) {
        handlers.reject(startError);
      }
      this.pending.clear();
    });

    await new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error("Bridge ready timeout")), 25000);
      this.readyWaiters.push({
        resolve: () => {
          clearTimeout(timer);
          resolve();
        },
        reject: (err) => {
          clearTimeout(timer);
          reject(err);
        },
      });
    });
  }

  async command(cmd: string, value?: number | string): Promise<BridgeResponse> {
    await this.ensureReady();
    if (!this.process?.stdin.writable) {
      throw new Error("Bridge stdin not writable");
    }

    const id = ++this.seq;
    const payload = value === undefined ? { cmd } : { cmd, value };

    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.process!.stdin.write(`${JSON.stringify(payload)}\n`, (err) => {
        if (err) {
          this.pending.delete(id);
          reject(err);
        }
      });
    });
  }

  private dispatch(response: BridgeResponse): void {
    if (!this.isReady) {
      if (response.ok === false) {
        const err = new Error(String(response.error ?? "Bridge failed to start"));
        for (const waiter of this.readyWaiters) {
          waiter.reject(err);
        }
        this.readyWaiters = [];
        return;
      }

      if (response.event === "ready") {
        this.isReady = true;
        for (const waiter of this.readyWaiters) {
          waiter.resolve();
        }
        this.readyWaiters = [];
        return;
      }
    }

    const first = this.pending.entries().next().value as
      | [number, { resolve: (v: BridgeResponse) => void; reject: (e: Error) => void }]
      | undefined;
    if (!first) return;
    const [id, handlers] = first;
    this.pending.delete(id);
    if (response.ok === false) {
      handlers.reject(new Error(String(response.error ?? "Bridge error")));
      return;
    }
    handlers.resolve(response);
  }

  dispose(): void {
    this.process?.kill();
    this.process = null;
    this.isReady = false;
  }
}

export const quantumBridge = new QuantumBridgeClient();

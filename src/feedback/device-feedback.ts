import { quantumBridge } from "../bridge-client.js";

export type DeviceSnapshot = {
  anc: number;
  ancName: string;
  profileName: string;
  profileColor: string;
  /** name → #RRGGBB */
  profileColors: Record<string, string>;
};

type Listener = (snap: DeviceSnapshot) => void;

const DEFAULT_PROFILE_COLOR = "#FC3F2A";

class DeviceFeedback {
  private listeners = new Set<Listener>();
  private timer: ReturnType<typeof setInterval> | null = null;
  private snap: DeviceSnapshot = {
    anc: -1,
    ancName: "",
    profileName: "",
    profileColor: DEFAULT_PROFILE_COLOR,
    profileColors: {},
  };
  private refreshing: Promise<void> | null = null;

  get current(): DeviceSnapshot {
    return this.snap;
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    this.ensurePolling();
    void this.refresh().then(() => listener(this.snap));
    return () => {
      this.listeners.delete(listener);
      if (this.listeners.size === 0) {
        this.stopPolling();
      }
    };
  }

  /** Force an immediate refresh (e.g. after a key press). */
  async refresh(): Promise<DeviceSnapshot> {
    if (this.refreshing) {
      await this.refreshing;
      return this.snap;
    }

    this.refreshing = this.pull()
      .catch((error) => {
        console.error("[feedback]", error);
      })
      .finally(() => {
        this.refreshing = null;
      });

    await this.refreshing;
    return this.snap;
  }

  private ensurePolling(): void {
    if (this.timer) return;
    this.timer = setInterval(() => {
      void this.refresh();
    }, 2500);
  }

  private stopPolling(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  private async pull(): Promise<void> {
    const [ancRes, listRes] = await Promise.all([
      quantumBridge.command("get-anc").catch(() => null),
      quantumBridge.command("list-profiles").catch(() => null),
    ]);

    const next: DeviceSnapshot = { ...this.snap, profileColors: { ...this.snap.profileColors } };

    if (ancRes) {
      next.anc = Number(ancRes.anc ?? next.anc);
      next.ancName = String(ancRes.name ?? next.ancName);
    }

    if (listRes) {
      const profiles = (listRes.profiles as Array<{ name?: string; color?: string }> | undefined) ?? [];
      const colors: Record<string, string> = {};
      for (const p of profiles) {
        if (p.name) {
          colors[p.name] = p.color?.startsWith("#") ? p.color : DEFAULT_PROFILE_COLOR;
        }
      }
      next.profileColors = colors;
      next.profileName = String(listRes.current ?? next.profileName);
      next.profileColor = colors[next.profileName] ?? DEFAULT_PROFILE_COLOR;
    }

    const changed =
      next.anc !== this.snap.anc ||
      next.profileName !== this.snap.profileName ||
      next.profileColor !== this.snap.profileColor ||
      JSON.stringify(next.profileColors) !== JSON.stringify(this.snap.profileColors);

    this.snap = next;
    if (changed) {
      for (const listener of this.listeners) {
        try {
          listener(this.snap);
        } catch (error) {
          console.error(error);
        }
      }
    }
  }
}

export const deviceFeedback = new DeviceFeedback();

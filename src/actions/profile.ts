import streamDeck, {
  DidReceiveSettingsEvent,
  KeyDownEvent,
  SendToPluginEvent,
  SingletonAction,
  WillAppearEvent,
  WillDisappearEvent,
} from "@elgato/streamdeck";
import { quantumBridge } from "../bridge-client.js";
import { deviceFeedback, type DeviceSnapshot } from "../feedback/device-feedback.js";
import { buttonImage, shortTitle } from "../visuals/button-image.js";

type ProfileSettings = {
  profileName?: string;
};

type ProfileInfo = {
  name?: string;
  color?: string;
  isSystem?: boolean;
};

type GetProfilesPayload = {
  event?: string;
};

type KeyActionLike = {
  setImage(image?: string): Promise<void>;
  setTitle(title?: string): Promise<void>;
  getSettings(): Promise<ProfileSettings>;
};

const DEFAULT_COLOR = "#FC3F2A";

function titleFromSettings(settings: ProfileSettings): string {
  const name = settings.profileName?.trim();
  return name && name.length > 0 ? shortTitle(name) : "Set";
}

async function sendProfileDatasource(): Promise<void> {
  try {
    const result = await quantumBridge.command("list-profiles");
    const profiles = (result.profiles as ProfileInfo[] | undefined) ?? [];
    const user = profiles
      .filter((p) => p.name && !p.isSystem)
      .map((p) => ({ label: p.name!, value: p.name! }));
    const system = profiles
      .filter((p) => p.name && p.isSystem)
      .map((p) => ({ label: p.name!, value: p.name! }));

    const items: Array<
      | { label: string; value: string }
      | { label: string; children: Array<{ label: string; value: string }> }
    > = [];

    if (user.length > 0) items.push({ label: "Usuario", children: user });
    if (system.length > 0) items.push({ label: "Sistema", children: system });
    if (items.length === 0) {
      for (const p of profiles) {
        if (p.name) items.push({ label: p.name, value: p.name });
      }
    }

    await streamDeck.ui.sendToPropertyInspector({ event: "getProfiles", items });
  } catch (error) {
    console.error(error);
    await streamDeck.ui.sendToPropertyInspector({ event: "getProfiles", items: [] });
  }
}

async function paintSetProfile(
  action: KeyActionLike,
  snap: DeviceSnapshot,
  settings?: ProfileSettings,
): Promise<void> {
  const cfg = settings ?? (await action.getSettings());
  const name = cfg.profileName?.trim() ?? "";
  const color = (name && snap.profileColors[name]) || DEFAULT_COLOR;
  const active = Boolean(name) && name.localeCompare(snap.profileName, undefined, { sensitivity: "accent" }) === 0;

  await action.setImage(
    buttonImage({
      color,
      glyph: name ? name.slice(0, 1).toUpperCase() : "?",
      active,
      dim: Boolean(name) && !active,
    }),
  );
  await action.setTitle(name ? shortTitle(name) : "Set");
}

async function paintCycleProfile(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
  await action.setImage(
    buttonImage({
      color: snap.profileColor || DEFAULT_COLOR,
      glyph: "▶",
      active: true,
    }),
  );
  await action.setTitle(shortTitle(snap.profileName || "Profile"));
}

export class CycleProfileAction extends SingletonAction {
  override manifestId = "com.pj289.jbl-quantum.cycle-profile";
  private unsubscribe: (() => void) | null = null;
  private visibleCount = 0;

  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    this.visibleCount += 1;
    if (!this.unsubscribe) {
      this.unsubscribe = deviceFeedback.subscribe((snap) => {
        void this.paintAll(snap);
      });
    }
    await paintCycleProfile(ev.action, deviceFeedback.current);
  }

  override onWillDisappear(_ev: WillDisappearEvent): void {
    this.visibleCount = Math.max(0, this.visibleCount - 1);
    if (this.visibleCount === 0) {
      this.unsubscribe?.();
      this.unsubscribe = null;
    }
  }

  private async paintAll(snap: DeviceSnapshot): Promise<void> {
    for (const action of this.actions) {
      await paintCycleProfile(action, snap);
    }
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("cycle-profile");
      const snap = await deviceFeedback.refresh();
      await this.paintAll(snap);
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

/** Loads a fixed Quantum Engine profile chosen in the Property Inspector. */
export class SetProfileAction extends SingletonAction<ProfileSettings> {
  override manifestId = "com.pj289.jbl-quantum.set-profile";
  private unsubscribe: (() => void) | null = null;
  private visibleCount = 0;

  override async onWillAppear(ev: WillAppearEvent<ProfileSettings>): Promise<void> {
    this.visibleCount += 1;
    if (!this.unsubscribe) {
      this.unsubscribe = deviceFeedback.subscribe((snap) => {
        void this.paintAll(snap);
      });
    }
    await paintSetProfile(ev.action, deviceFeedback.current, await ev.action.getSettings());
  }

  override onWillDisappear(_ev: WillDisappearEvent<ProfileSettings>): void {
    this.visibleCount = Math.max(0, this.visibleCount - 1);
    if (this.visibleCount === 0) {
      this.unsubscribe?.();
      this.unsubscribe = null;
    }
  }

  override async onDidReceiveSettings(ev: DidReceiveSettingsEvent<ProfileSettings>): Promise<void> {
    await paintSetProfile(ev.action, deviceFeedback.current, ev.payload.settings);
  }

  override async onSendToPlugin(
    ev: SendToPluginEvent<GetProfilesPayload, ProfileSettings>,
  ): Promise<void> {
    if (ev.payload?.event === "getProfiles") {
      await sendProfileDatasource();
    }
  }

  private async paintAll(snap: DeviceSnapshot): Promise<void> {
    for (const action of this.actions) {
      await paintSetProfile(action, snap);
    }
  }

  override async onKeyDown(ev: KeyDownEvent<ProfileSettings>): Promise<void> {
    try {
      const name = (await ev.action.getSettings()).profileName?.trim();
      if (!name) {
        await ev.action.setTitle("SET?");
        await ev.action.showAlert();
        return;
      }

      await quantumBridge.command("set-profile", name);
      const snap = await deviceFeedback.refresh();
      await this.paintAll(snap);
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

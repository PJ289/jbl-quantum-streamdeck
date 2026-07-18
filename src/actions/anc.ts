import {
  KeyDownEvent,
  SingletonAction,
  WillAppearEvent,
  WillDisappearEvent,
} from "@elgato/streamdeck";
import { quantumBridge } from "../bridge-client.js";
import { deviceFeedback, type DeviceSnapshot } from "../feedback/device-feedback.js";
import { ANC_COLORS, ANC_GLYPHS, buttonImage } from "../visuals/button-image.js";

type KeyActionLike = {
  setImage(image?: string): Promise<void>;
  setTitle(title?: string): Promise<void>;
};

async function paintAncMode(action: KeyActionLike, snap: DeviceSnapshot, mode: number, glyph: string): Promise<void> {
  const active = snap.anc === mode;
  await action.setImage(
    buttonImage({
      color: ANC_COLORS[mode] ?? "#555555",
      glyph,
      active,
      dim: !active,
    }),
  );
  await action.setTitle(active ? "●" : "");
}

async function paintToggle(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
  const on = snap.anc === 1;
  await action.setImage(
    buttonImage({
      color: on ? ANC_COLORS[1] : ANC_COLORS[0],
      glyph: on ? "ON" : "OFF",
      active: on,
      dim: !on,
    }),
  );
  await action.setTitle(on ? "ANC" : "OFF");
}

async function paintCycle(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
  const mode = snap.anc >= 0 ? snap.anc : 0;
  await action.setImage(
    buttonImage({
      color: ANC_COLORS[mode] ?? "#3564C8",
      glyph: ANC_GLYPHS[mode] ?? "ANC",
      active: true,
    }),
  );
  await action.setTitle(String(snap.ancName || ANC_GLYPHS[mode] || "ANC"));
}

abstract class FeedbackAncAction extends SingletonAction {
  private unsubscribe: (() => void) | null = null;
  private visibleCount = 0;

  protected abstract paint(action: KeyActionLike, snap: DeviceSnapshot): Promise<void>;

  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    this.visibleCount += 1;
    if (!this.unsubscribe) {
      this.unsubscribe = deviceFeedback.subscribe((snap) => {
        void this.paintAll(snap);
      });
    }
    await this.paint(ev.action, deviceFeedback.current);
  }

  override onWillDisappear(_ev: WillDisappearEvent): void {
    this.visibleCount = Math.max(0, this.visibleCount - 1);
    if (this.visibleCount === 0) {
      this.unsubscribe?.();
      this.unsubscribe = null;
    }
  }

  protected async paintAll(snap: DeviceSnapshot): Promise<void> {
    for (const action of this.actions) {
      await this.paint(action, snap);
    }
  }

  protected async afterChange(ev: KeyDownEvent): Promise<void> {
    try {
      const snap = await deviceFeedback.refresh();
      await this.paintAll(snap);
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class CycleAncAction extends FeedbackAncAction {
  override manifestId = "com.pj289.jbl-quantum.cycle-anc";

  protected paint(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
    return paintCycle(action, snap);
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("cycle-anc");
      await this.afterChange(ev);
    } catch (error) {
      console.error(error);
      await ev.action.setTitle("ERR");
      await ev.action.showAlert();
    }
  }
}

export class ToggleAncAction extends FeedbackAncAction {
  override manifestId = "com.pj289.jbl-quantum.toggle-anc";

  protected paint(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
    return paintToggle(action, snap);
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("toggle-anc");
      await this.afterChange(ev);
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class AncOffAction extends FeedbackAncAction {
  override manifestId = "com.pj289.jbl-quantum.anc-off";

  protected async paint(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
    await paintAncMode(action, snap, 0, "OFF");
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("set-anc", 0);
      await this.afterChange(ev);
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class AncOnAction extends FeedbackAncAction {
  override manifestId = "com.pj289.jbl-quantum.anc-on";

  protected async paint(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
    await paintAncMode(action, snap, 1, "ON");
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("set-anc", 1);
      await this.afterChange(ev);
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class AncAmbientAction extends FeedbackAncAction {
  override manifestId = "com.pj289.jbl-quantum.anc-ambient";

  protected async paint(action: KeyActionLike, snap: DeviceSnapshot): Promise<void> {
    await paintAncMode(action, snap, 3, "AMB");
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("set-anc", 3);
      await this.afterChange(ev);
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

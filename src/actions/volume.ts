import { KeyDownEvent, SingletonAction } from "@elgato/streamdeck";
import { quantumBridge } from "../bridge-client.js";

type VolumeSettings = {
  delta?: number;
  last?: number;
};

export class MicVolumeUpAction extends SingletonAction<VolumeSettings> {
  override manifestId = "com.pj289.jbl-quantum.mic-volume-up";

  override async onKeyDown(ev: KeyDownEvent<VolumeSettings>): Promise<void> {
    try {
      const delta = ev.payload.settings.delta ?? 5;
      const settings = await ev.action.getSettings();
      const current = Number(settings.last ?? 50);
      const next = Math.min(100, current + delta);
      await quantumBridge.command("set-mic-volume", next);
      await ev.action.setSettings({ ...ev.payload.settings, last: next });
      await ev.action.setTitle(`${next}%`);
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class MicVolumeDownAction extends SingletonAction<VolumeSettings> {
  override manifestId = "com.pj289.jbl-quantum.mic-volume-down";

  override async onKeyDown(ev: KeyDownEvent<VolumeSettings>): Promise<void> {
    try {
      const delta = ev.payload.settings.delta ?? 5;
      const current = Number((await ev.action.getSettings()).last ?? 50);
      const next = Math.max(0, current - delta);
      await quantumBridge.command("set-mic-volume", next);
      await ev.action.setSettings({ ...ev.payload.settings, last: next });
      await ev.action.setTitle(`${next}%`);
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class GameChatMoreGameAction extends SingletonAction {
  override manifestId = "com.pj289.jbl-quantum.game-chat-more-game";

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("set-game-chat-balance", 75);
      await ev.action.setTitle("GAME");
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

export class GameChatMoreChatAction extends SingletonAction {
  override manifestId = "com.pj289.jbl-quantum.game-chat-more-chat";

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      await quantumBridge.command("set-game-chat-balance", 25);
      await ev.action.setTitle("CHAT");
      await ev.action.showOk();
    } catch (error) {
      console.error(error);
      await ev.action.showAlert();
    }
  }
}

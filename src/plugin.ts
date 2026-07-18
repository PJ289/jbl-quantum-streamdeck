import streamDeck from "@elgato/streamdeck";

import {
  AncAmbientAction,
  AncOffAction,
  AncOnAction,
  CycleAncAction,
  ToggleAncAction,
} from "./actions/anc.js";
import {
  GameChatMoreChatAction,
  GameChatMoreGameAction,
  MicVolumeDownAction,
  MicVolumeUpAction,
} from "./actions/volume.js";
import { CycleProfileAction, SetProfileAction } from "./actions/profile.js";
import { quantumBridge } from "./bridge-client.js";

streamDeck.logger.setLevel("info");

streamDeck.actions.registerAction(new CycleAncAction());
streamDeck.actions.registerAction(new ToggleAncAction());
streamDeck.actions.registerAction(new AncOffAction());
streamDeck.actions.registerAction(new AncOnAction());
streamDeck.actions.registerAction(new AncAmbientAction());
streamDeck.actions.registerAction(new MicVolumeUpAction());
streamDeck.actions.registerAction(new MicVolumeDownAction());
streamDeck.actions.registerAction(new GameChatMoreGameAction());
streamDeck.actions.registerAction(new GameChatMoreChatAction());
streamDeck.actions.registerAction(new CycleProfileAction());
streamDeck.actions.registerAction(new SetProfileAction());

streamDeck.connect();

process.on("exit", () => {
  quantumBridge.dispose();
});

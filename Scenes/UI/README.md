# Editing the interface in Godot

Open `GameInterface.tscn` in the 2D editor to change the title screen, header,
dialogue panel, chat panel, spacing and button labels. The scene is instanced
under `Main.tscn > Interface`; the `Lanternwake` root's Inspector holds its
`Interface Root` reference and the other presentation dependencies.

- Edit `LanternwakeTheme.tres` in the Theme editor for fonts, colors, borders,
  panel backgrounds and button states. The HUD and modal shell share it.
- Edit `ModalWindow.tscn` for history, catalogue, settings, load and evidence
  dialog layout. Its content and available actions are supplied at runtime.
  Its hidden `HistoryTools` contains the editable search, latest-entry, chapter
  and reached-count rows; only the History reader shows it. Chapter entries come
  from authoritative metadata for the currently recorded transcript.
- Edit `ChoiceButton.tscn` for suggested replies and evidence/action rows.
  Only their count, text and callbacks are generated from current story data.
- Edit `MicrophoneConsent.tscn` for the existing local-recording confirmation.
  Consent wording must remain consistent with the 30-second local recording
  and review-before-send behavior.
- Select the `Lanternwake` root in Main's Inspector to change `Story Path`,
  `Characters Per Second` and `Start With Instant Text`.

Nodes marked with a `%` scene-unique name are the runtime binding contract.
Keep their names and expected node types; their container paths can change.
The interface and modal `RichTextLabel`s intentionally have BBCode disabled,
so authored text and model replies are displayed as plain text.

`ChatPanel` starts hidden and `TalkButton` appears only on an eligible beat.
For design preview you can toggle them in the editor; restore their authored
initial visibility before saving. Test repeated open/close and Escape after
changing focus, modal or container behavior. `--ui-smoke` exercises existing
callbacks through the real scene tree; a visual pass is still required after
layout/theme changes.

These scenes are authoring sources. There is no runtime UI builder or ongoing
scene generator whose output would overwrite your edits.

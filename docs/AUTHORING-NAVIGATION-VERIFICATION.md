# Authoring navigation visibility

Qualification base: published PR7
`65548f97e1c203884864bf4dcc1d1849db4cbcd1`.
Branch: `fix/authoring-selection-visibility`.

Searching for a late beat previously selected its stable ID and displayed its text
without scrolling the beat list to the selected row. The native Editor fixture
reproduced selected beat 25 at vertical position 558 while the scrollbar remained
at zero with a 124-pixel page. Search, save/reload and rejected draft navigation
now bring the active beat into view using Godot's native ItemList scrolling.
Changing scenes still selects the first beat.

The outer dock now follows keyboard focus. At 320 x 600, moving focus to a dossier
save button below the visible area scrolls that button into view. This adds no new
controls, changes no focus order, and preserves the existing draft/save behavior.

Executed with provisioned Godot 4.6.3 .NET and .NET SDK 8.0.425:

```sh
dotnet restore Lanternwake.csproj --configfile /workspace/.lanternwake-tools/NuGet.Config
dotnet build Lanternwake.csproj --no-restore
"$GODOT_MONO" --headless --editor --path . -- --editor-roundtrip
```

Final build: zero warnings and errors. Final Editor run: zero warning/error lines;
passed story dock, narrow layout, character authoring, selected-beat child launches
and 11-scene roundtrip markers. The focused tests use actual controls and signals,
wait for layout, account for ItemList panel margins, and verify the selected row
and focused action fit their viewports. All editing is against a disposable story
copy; canonical content is compared by the existing launch tests.

This is headless control/layout/lifecycle evidence. It does not establish graphical
appearance, a physical keyboard session, accessibility-tool coverage or human
playtime. Core tests were not repeated because no core/story/save code changed;
the exact base's prior qualification remains separate. No audio or speech code,
story data, save format, playback settings or reviewed validation branches changed.

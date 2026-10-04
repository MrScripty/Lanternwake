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

## Cold-layout successor

Successor base: `c4da1209da03f3940df0835c400e0865c29323fd`.
Branch: `fix/authoring-cold-selection`.

Independent graphical review reproduced a remaining cold-open defect twice in the
official 4.6.3 .NET Editor: at the default approximately 290-pixel right dock,
searching `ch5_s5_evidence` loaded the correct text but clipped the selected final
row. A wheel adjustment revealed it; warm navigation worked; Reload Current
Project reproduced the cold failure. The earlier headless checks did not establish
that graphical path, and must not be treated as having cleared it.

The successor coalesces selection visibility requests, waits two frame boundaries
after the latest selection/layout change and then asks ItemList to reveal its current
selected row. Resize, visibility and vertical range/page changes schedule another
bounded request if layout changes. It does not poll until a condition becomes true,
select a captured stale index or change keyboard focus. Only one frame subscription
is retained; normal completion disconnects it. Plugin exit stops/disconnects before
freeing the dock, including requests caused by disposal-time layout changes.

A new headless regression uses the actual bound dock controls, Editor theme and a
290 x 600 fixture. It searches the exact late evidence ID before settling layout,
then models a 16-pixel viewport reduction after the first list draw and checks the
entire selected row after final layout. On the old successor base this reproduced
clipping: item y=558, height=22, scrollbar=432, page=124. With the fix it passes.
This controlled late-layout reduction is a model of the reported cold path, not a
claim that headless rendering exactly reproduces the graphical Editor's timing.

The same native test alternates early/late selections 18 times without waiting,
checks the latest selection and unchanged focus, then checks late selection again.
A teardown fixture schedules a request, invokes the same stop path as plugin exit,
frees the owned ItemList and verifies the subscription/countdown are cleared.
Existing dirty-navigation, save/reload/conflict, dossier, keyboard-scroll and native
selected-beat-launch tests still pass. `scripts/verify.sh` additionally requires
`LANTERNWAKE_STORY_DOCK_COLD_SELECTION_OK`.

Executed with .NET 8.0.425 / Godot 4.6.3 .NET: native build with zero warnings/errors,
then normal headless Editor `--editor-roundtrip` with every required marker, including
11 scenes, and no warning/error lines. Core/runtime suites were not repeated because
this patch only changes the Editor plugin and its verification gate. Canonical
narrative, core/session/save logic, reading-size and validation branches are unchanged.

Genuine graphical successor retest is still required: fresh Editor/right dock,
search/click `ch5_s5_evidence`, inspect the full selected row without wheel input,
repeat after Project > Reload Current Project, rapid early/late selection, pending
draft rejection and Editor teardown/reopen. No graphical success is claimed here.

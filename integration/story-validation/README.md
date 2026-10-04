# Story structure validation

This deterministic regression harness exercises the production content validator
without Godot, audio devices, network services or model dependencies:

```sh
dotnet run --project integration/story-validation/StoryValidation.csproj -- Content/story.json
```

The schema-1 content contract requires validation before play or authoring
publication. Non-nullable C# record fields do not prevent JSON from supplying
missing collections, null collections or null entries. The validator now checks
required collection structure before traversing it or resolving references.
Malformed structure reports `InvalidDataException` rather than escaping as a null
reference or argument exception, or being accepted until later runtime use.

The harness checks eleven required collections, valid empty collections, optional
null unlock arrays, schema-1 snapshot compatibility and unchanged source bytes.
Null character knowledge is rejected as a missing required field; empty knowledge
remains valid. It does not change authored IDs, text, facts, gates or save schema.

Initial collection milestone over PR7 `65548f97e1c203884864bf4dcc1d1849db4cbcd1`:

- Initial 37-check harness reproduced 27 failures before the fix.
- Its 37 checks pass, including null entries without changing array lengths.
- Existing core suite passes 4,107 assertions across 1,424 authored beats.
- Godot 4.6.3 .NET / .NET 8.0.425 build: zero warnings and errors.
- Headless runtime traverses all 1,424 beats and reaches the authored ending.
- Editor authoring, dock, selected-beat launch and 11-scene roundtrip checks pass.
  Editor reports regenerated UID warnings for three pre-existing untracked UID
  files; no engine errors occurred. Those unrelated files are excluded from this
  change. This is not visual inspection.

## Required-text follow-up

Base: `b5dfd00d67b388a22d20702d50dce004a393fffc`. Branch:
`fix/story-required-text-validation`.

Chapter titles and scene `timeOfDay` labels are consumed by rendering string
operations; missing/null values previously passed validation then threw. Blank
evidence prompts and choices previously passed validation even when all choices
in a mandatory activity were unreadable. These required fields now reject missing,
null, empty and whitespace-only text with the entity ID and field in the error;
option errors include a zero-based index. Every choice is checked, including wrong
answers. No labels are fabricated, and no time-of-day vocabulary is introduced.

The extended harness reproduced 22 failures before the fix: 19 accepted invalid
values and three null-option diagnostics lacking an index. After the fix:

- All 63 validation checks pass. The tests also retain valid Unicode and surrounding
  whitespace exactly, preserve the answer index and verify schema-1 save compatibility.
- Existing core suite: 4,107 assertions pass; 1,424 authored beats, five chapters.
- Godot 4.6.3 .NET / .NET 8.0.425 build: zero warnings and errors.
- Runtime, UI, explicit save recovery and save-isolation smokes pass.
- Editor authoring/dock/selected-beat launch and 11-scene roundtrip checks pass.
  Three known missing-UID cache regeneration warnings remain for unrelated files;
  no engine errors were reported. This is headless evidence, not graphical review.

Commands run after this change:

```sh
dotnet run --project integration/story-validation/StoryValidation.csproj -- Content/story.json
dotnet run --project tests/Lanternwake.Tests.csproj -- Content/story.json
dotnet build Lanternwake.csproj --no-restore
# GODOT_MONO points to the provisioned 4.6.3 .NET executable.
"$GODOT_MONO" --headless --path . -- --smoke
"$GODOT_MONO" --headless --path . -- --ui-smoke
"$GODOT_MONO" --headless --path . -- --save-isolation-smoke
"$GODOT_MONO" --headless --editor --path . -- --editor-roundtrip
```

Canonical story bytes, IDs, save schema, session logic, playback and speech code
are unchanged. These checks establish structural rejection and compatibility, not literary
quality, playtime, audio qualification or speech recognition. Audio candidate
qualification remains a separate milestone.

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

Qualification over PR7 `65548f97e1c203884864bf4dcc1d1849db4cbcd1`:

- Initial 37-check harness reproduced 27 failures before the fix.
- Final 37 checks pass, including null entries without changing array lengths.
- Existing core suite passes 4,107 assertions across 1,424 authored beats.
- Godot 4.6.3 .NET / .NET 8.0.425 build: zero warnings and errors.
- Headless runtime traverses all 1,424 beats and reaches the authored ending.
- Editor authoring, dock, selected-beat launch and 11-scene roundtrip checks pass.
  Editor reports regenerated UID warnings for three pre-existing untracked UID
  files; no engine errors occurred. Those unrelated files are excluded from this
  change. This is not visual inspection.

These checks establish structural rejection and compatibility, not literary
quality, playtime, audio qualification or speech recognition. Audio candidate
qualification remains a separate milestone.

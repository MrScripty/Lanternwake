# Items, 3D staging, animation, and sound

This is production direction. Current runtime uses procedural stylized adult sculptures and reusable sets; this document does not claim bespoke recorded performances, facial rigs, or a finished score exist.

## Visual thesis

The world is spatial, but reading behaves like a 2D visual novel. Fixed composed cameras, legible foreground text, deliberate actor poses, and small environmental changes take priority over free roaming. Do not ask players to search an unbounded 3D space for a tiny pixel. Essential object information is in authored text and the evidence UI. The three-dimensional space supplies presence, continuity, and physical understanding of the bell mechanism.

**Shape language:** long verticals for the tower and responsibility; horizontal layers for sea, records, and chronology; arcs for the lens, handwriting, cup handle, and return. Avoid magical glyph language. The repeated three-stroke/circle pattern is a calibration mark, not a sigil.

**Material language:** cold painted metal, salt-whitened wood, repair cloth, cream paper, aged brass, matte wool, green translucent mineral. Repairs are visible but not fetishized. Nobody lives in a curated ruin. The working harbor has plastic crates, modern batteries, and useful ugly equipment beside old stone.

**Palette:** slate sea and charcoal structure; cream paper; amber service lamps; blue cup; green pressure inspection glass; four character accents (Ada plum, Nessa orange/navy, Tomas teal/brown, Sera mustard/green). Never encode clue correctness only through these colors. A wrong answer must have readable feedback, not merely a red flash.

## The five stage locations

### harbor

Foreground: safe public quay, mooring rope, chart board, life ring, radio table. Midground: boats secured to visible points, office door, route signs. Background: west wall and partly obscured sheltered approach, hill and tower silhouette. The working chart is distinct from a tourist map by labels and function, not age alone.

Cameras: waist-height two-person composition for Nessa/Ada; wider three-person view for route reconstruction; seated shelter composition for the conversation about living obligations. Use small parallax in idle water and distant ropes, not constant camera drift. Storm changes sky value, wind, and rain; maintain face and text contrast.

Required story affordance: charts can be described and compared in the evidence UI even if stage textures remain abstract. The headcount list is represented by authored activities, not an unreadable fake spreadsheet texture. The high hall exists offstage; radio and transition text make geography explicit. Never place an evacuee in the bypass exclusion zone for visual drama.

### keeper_house

A small practical kitchen and adjoining desk area: table with blue pencil cup, chairs, sink, south sash, postcards, boxes, household ledger. The room should feel ordinary before it feels meaningful. The blue cup is identifiable and has a repaired handle. The brass calibration plate is beneath the table crossbar, revealed through a safe photographed viewing angle after the handle falls.

Cameras: stable table two-shot; close insert for catalogued objects; wider empty chair composition during solo inventory. Do not use Ivo's portrait as a substitute living actor. Early warm light can coexist with unease. Late shutdown check uses torches and a service lamp; morning is gray-soft rather than triumphantly golden.

Object states: cup intact until the authored break; fragments then boxed; plain steel mug substitutes afterward. If the current procedural build cannot animate the break, a cut, sound caption, and authored text are preferable to an unsafe-looking physics gag. Do not let players cause the break by clicking it. It occurs at the canonical beat.

### archive

Former wireless room: shelves of boxes, central worktable, reel-to-reel machine, mismatched headphones, lamp, painted windows, source/copy/interpretation cards, kettle marked CURRENT. Objects are arranged for work, not occult atmosphere. A repaired roof leak gives conservation urgency without threatening the only evidence during the finale.

Cameras: table-side conversation; overhead/insert view for log comparison; a restrained close composition during full testimony. During grief, give space and a steady frame rather than pushing dramatically into a face. The preserved source reel and public copy must never be visually interchangeable.

Sound captions distinguish live speech, archival speech, machine noise, and music. The story's identification of overlapping channels is fully available in text. No player must hear a faint voice to solve a clue.

### lantern_room

A tall stage compressing the tower's working landing: spiral stair suggestion, central shaft, guarded access, visible red exclusion marking with text, suspended bell counterweight, lower cradle/catch basin, chronograph beneath the stair, lens and service window. Readability of the mechanical relationship matters more than architectural scale.

The counterweight is associated with the bell; its controlled descent is the literal falling bell of the line. Show a guided lowering, not an uncontrolled masonry collapse. The empty cradle must be visually plausible. The bypass linkage runs downward toward the cave system. Actors remain outside the shaft gate on the high landing.

Exact current stage event: the authored beat immediately after brake-controlled descent carries `stageCue: "bell_lowered"`. The state persists on save/load and later visits. Never reset the bell in a morning scene because a location is reconstructed.

Cameras: initial wide discovery reveals scale; close receiver/platen inserts clarify input/output; group readiness view shows all four accounted for; release uses a fixed safe viewpoint. Optional shake is minimal and disabled by reduced-motion mode. The important change is sound and position, not flashing.

### tide_cave

Basalt hollow with partial concrete reinforcement, protected high working floor, pipes, manometers, green inspection port, manual bypass wheel, reference bolts, carved toy boat in a high crevice. No adult or animal enters rising water. The timed inspection is an authored safety procedure, not a real-time player countdown.

The water column's apparent height introduces pressure difference. The layered annulus supports the fictional effect without glowing runes or sentient movement. Yellow threshold is also labeled in text. Leave early after sufficient measurements; do not reward staying for loot.

The finale occurs remotely from above. An inspection-camera description, sound, and gauge change can communicate fracture. Do not stage a character inside the chamber to watch the glass break. Afterward the lower path remains closed for assessment.

## Item bible

### i_inventory — Estate inventory

Ada's core tool and emotional form. Provisional entries, dates, condition, provenance, ownership, and unknowns. Begins as a finite professional assignment; ends as an honest account capable of correction. It never becomes a mystical diary. UI can show known entries without requiring the player to manually type catalogue prose. Safe handling: observe in place, then record any move.

### i_ivo_letter — Ivo's letter

Cream envelope, plain Ada label, small green cloth square. Text grants permission to leave and directs Ada toward Tomas and Nessa. It neither predicts her every choice nor grants power to erase public evidence. A later instruction saying Ada may decide is rejected as an inappropriate attempt to privatize a public record. Preserve ambiguity of affection, not ambiguity of the fixed solution.

### i_first_strip — Cup trace

Narrow cream paper, pressure-scored dark writing with Ada's distinctive compressed forms. Explicit tomorrow date and16:12 claim. Produced at Day1 19:58; sent at Day2 19:15. Main visual clue is handwriting, but narration describes it for all players. First treatment: photograph in place with ruler and clock, identify source and limits.

### i_bell_strip — Bell trace

Same physical paper path. Exact sentence: WHEN THE BELL FALLS, NO ONE WILL DROWN. Produced Day1 20:03; sent Day2 19:20. Its apparent reassurance becomes dangerous only if treated as authority; the story repeatedly grounds the safe operation in independent checks. Do not embellish it with an unnamed victim or an ellipsis implying a sequel.

### i_channel_log — Operational channel log

Contemporary columns, dated entries, legend A measurements/B instructions, trial-routing stamp. At21:04, WEST LEVEL HOLDING is not a vessel hold. This is the central ordinary clue and must be readable without listening ability or knowledge of maritime radio. Matching color may aid the interface but labels carry the meaning.

### i_chart — Corrected working chart

Fold-worn chart showing sheltered west approach absent from the issued public exhibit. Ivo's draft says omit it and use the issued version. The exhibit omission is deliberate; the chart alone does not prove every person's motive or a guaranteed rescue outcome. In route activity, move abstract blocks only through supported positions, never animate a sensational fatal sinking.

### i_blue_handle — Blue cup handle

Old adhesive seam, ceramic crescent, mundane fragility. Break caused by ordinary vibration at the predicted minute, no injury. Where it falls points attention under the table. Later fragments remain a conservation question, not a sacred relic. A plain steel mug replaces it. No requirement to hear the crack.

### i_caliper — Survey caliper

Sera's ordinary measuring tool. Records reference-bolt distance and establishes that the team checks the actual installation against drawings. It symbolizes neither genius nor magic. Gesture: square jaws carefully, read twice, another person repeats the value.

### i_tape — Original magnetic reel

Source-dated1998 audio and complete inquiry deposition. Storage box label is misleading; physical provenance and source identification are assessed separately. Ordinary magnetic audio never crosses the time mechanism. Preserve unedited copy, transfer history, uncertain words, and channel separation. During playback, the archival speaker's ID is explicit.

### i_test_strip — Calibration record

Three strokes and flattened circle. Video at Day1 18:35; matching input Day2 17:52. Demonstrates a self-consistent mechanical/time relation under observed conditions, not arbitrary date choice. The scientific limitation is part of the clue, not optional footnote material.

### i_evidence_packet — Verified deposit

Readable transcripts, scans, original audio copies, provenance, checksums, version history. Verified at two independent off-island repositories before release. Original custody separately recorded at the hall. Do not represent a spinning upload icon as evidence of successful preservation. This object changes the climax's choices: preserving truth no longer requires retaining a hazardous machine.

### i_blank_strip — Unmarked paper

No trace. The initial fearful interpretation is corrected explicitly. It belongs in the final catalogue as unmarked receiving paper, retained. Camera lets it be quiet without giving it a secret final mark. The player should leave with room for uncertainty, not a new cliffhanger.

## Supporting objects

Postcard with wrong construction date; bird book with Ada's childhood joke; household loan ledger and green tray; old and replacement remotes; delayed family letter; song timestamp card; repairs list; six authorized ordinary-life folders; check-in list; radio out-of-service box. These prevent every prop becoming a plot token. Some are private, some public, some simply useful. Do not auto-add every sentimental object to a collectible checklist.

## Performance and animation grammar

Three readable pose families per living character are sufficient for an initial production: listening, explaining/working, and stressed/resting. Use hand placement and torso orientation rather than exaggerated facial loops. Characters face the relevant speaker or object, not always the camera. Idle breathing must not make grief scenes look mechanically cheerful.

Ada aligns paper when uneasy; Nessa checks physical space then deliberately leaves room; Tomas listens before touching controls; Sera draws or points to a bounded section of a diagram. Under pressure, everyone reduces movement. Nobody pantomimes entering a restricted zone. Ivo and archival speakers have a recorded-speaker treatment instead of staged bodies.

Transitions are readable fades or cuts. Retain object state across transitions. No time pressure in menus, conversations, transcript reading, or evidence activities. A player can stop during a narrated timed operation without causing failure.

## Sound language

Original score direction: sparse felt piano, bowed metal resonance, low reed, restrained string harmonics, and an unadorned human melody. Avoid continuous melancholy. Leave practical scenes with room tone and small tool sounds. Music should not declare which person is guilty before the evidence does.

Harbor: ropes, mast taps, distant work, modern radio. House: kettle, refrigerator, wind at a sash, paper. Archive: motor, tape hiss, lamp hum, carefully distinguished live and recorded space. Tower: pressure knocks, metal resonance, wind behind glass. Cave: controlled water resonance, never a monster breath.

The bell's descent transforms a thin pressure tone into broad water rush, then quiet. No piercing peak required. Offer separate dialogue/music/effects controls and optional dynamic-range reduction. Caption every clue-bearing sound and every speaker. The song "A Little Room in the Weather" is fictional and original; the short lyric in the manuscript is original. The current text does not imply a recorded song asset exists.

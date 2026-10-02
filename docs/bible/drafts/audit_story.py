"""Check content references and temporal model-access gates; generate spoiler bible."""
import json,re
from pathlib import Path
root=Path(__file__).resolve().parents[3]
s=json.loads((root/'Content/story.json').read_text())
facts={f['id']:f['text'] for f in s['facts']}; chars={c['id']:c for c in s['characters']}; items={i['id']:i for i in s['items']}
seen=set(); known=set(); found=set(); out=['# Scene-by-scene clue and model knowledge gates','','Full spoilers. This file is generated from the actual runtime content, not a separate promised outline. An entry fact can be discussed only where the current conversation also explicitly allows it. A scene without a conversation does not permit arbitrary model interjections.','','## Global rules','','The plot is deterministic. All essential discoveries occur in required beats. Allowed facts are intersected with save-unlocked facts and the selected character’s knowledge. The forbidden list below is conservative: the model must not confirm those later facts even if the player guesses them. Ordinary speculation can be acknowledged as speculation without confirmation.','','## Fact register','']
for k,v in facts.items():out += [f'- **{k}**: {v}']
for c in s['chapters']:
 out += ['',f"## {c['title']} ({c['id']})",'']
 for scene in c['scenes']:
  assert scene['id']not in seen;seen.add(scene['id']);entry=set(known);reveals=[];convs=[];acts=[];cues=[]
  assert scene['location']in ['harbor','keeper_house','archive','lantern_room','tide_cave']
  for cid in scene['characterIds']:assert cid in chars,cid
  for b in scene['beats']:
   assert b['id']not in seen;seen.add(b['id']);assert b['speaker']=='narrator'or b['speaker']in chars,(b['id'],b['speaker'])
   for f in b.get('unlockFacts',[]):assert f in facts;known.add(f);reveals.append((b['id'],f))
   for i in b.get('unlockItems',[]):assert i in items;found.add(i)
   if 'conversation'in b:
    q=b['conversation'];assert q['characterId']in chars
    assert set(q['allowedFacts'])<=known,(b['id'],'premature',set(q['allowedFacts'])-known)
    assert set(q['allowedFacts'])<=set(chars[q['characterId']]['knowledge'])
    assert len(q['suggestions'])==3 and all(q['suggestions']) and q['fallback']
    convs.append((b['id'],q))
   if 'activity'in b:
    a=b['activity'];assert 0<=a['correctIndex']<len(a['options']);assert a['explanation'];acts.append((b['id'],a))
   if 'stageCue'in b:cues.append((b['id'],b['stageCue']))
  out += [f"### {scene['id']} — {scene['title']}",f"Location: `{scene['location']}`; time: {scene['timeOfDay']}; living stage participants: {', '.join(chars[x]['name'] for x in scene['characterIds'])}.",'',f"Entry-known facts: {', '.join(sorted(entry)) or 'none'}.",'',f"Required new facts: {', '.join(f'{f} at {bid}' for bid,f in reveals) or 'none; this scene deepens existing context without a new secret'}.",f"Forbidden future confirmations after this scene: {', '.join(sorted(set(facts)-known)) or 'none; no unsupported sequel events may be invented'}.",'']
  for bid,q in convs:
   out += [f"- Conversation `{bid}` with {chars[q['characterId']]['name']}: {q['prompt']}",f"  - Exact allowed facts: {', '.join(q['allowedFacts'])}.",f"  - Authored offline fallback: {q['fallback']}"]
  for bid,a in acts:out += [f"- Required evidence activity `{bid}`: {a['prompt']}",f"  - Supported answer: {a['options'][a['correctIndex']]}",f"  - Explanation: {a['explanation']}"]
  for bid,cue in cues:out += [f"- Persisted stage cue `{cue}` at `{bid}`."]
  if not convs:out += ['No optional model conversation is enabled in this scene.']
assert known==set(facts);assert found==set(items),(set(items)-found)
assert sum(b.get('stageCue')=='bell_lowered'for c in s['chapters']for sc in c['scenes']for b in sc['beats'])==1
(root/'docs/bible/02_SCENE_KNOWLEDGE_GATES.md').write_text('\n'.join(out)+'\n')
print(f"PASS: {len(s['chapters'])} chapters; {len(seen)} unique scene/beat IDs; all {len(facts)} facts and {len(items)} items unlocked; conversations contain no premature fact IDs; exactly one safe bell cue.")

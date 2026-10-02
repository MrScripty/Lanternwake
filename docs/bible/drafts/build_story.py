"""Deterministically compile the human-readable authored manuscript into runtime JSON."""
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
DRAFTS=Path(__file__).parent
facts=[
('f_inventory','Ada Vale is an adult archivist cataloguing the estate of her uncle Ivo Vale. He died six weeks ago from a documented cardiac arrhythmia. The cause of death is not a mystery.'),
('f_future_note','The chronograph receiver bears a note dated tomorrow: "At 16:12 the blue cup will break. Leave the handle where it falls." Its writing resembles Ada\'s. Origin unestablished.'),
('f_bell_warning','Ada and Tomas witnessed a second trace form: "When the bell falls, no one will drown." They documented it. It is not a verified safety instruction.'),
('f_tomas_loss','Tomas\'s father Ewan Rook was one of six adult West Quay crew members who died during the 1998 flood. Tomas seeks the original records.'),
('f_wrong_channel','The operational log distinguishes Channel A measurements from Channel B movement orders. "West level holding" at 21:04 describes water, not a vessel hold. The public summary conflates them.'),
('f_west_omission','The corrected chart includes a sheltered west approach absent from the public exhibit. Ivo\'s draft explicitly directs use of the older issued chart. This is documented omission, not a copying accident.'),
('f_cup_prediction','At16:12:08 the old repaired cup broke after pressure-line vibration, with everyone clear. Its fallen handle pointed toward a calibration plate under the table. The advance wording matches a witnessed event.'),
('f_pressure_engine','The tower apparatus couples a manual input stylus and receiving stylus through a pressure loop around a natural resonant inclusion. It carries traces, not bodies, thoughts, memories, or voices. A counterweight descent opens a pressure bypass.'),
('f_ordinary_tape','The 1998 archive recording is an ordinary physical magnetic tape dated by independent evidence. It is not a voice sent across time.'),
('f_ivo_signature','Ivo signed supervised trial acceptance despite documented unresolved alarm-channel cross-talk. This does not alone establish every consequence or legal liability.'),
('f_interval','The configured trace interval is exactly 23 hours 17 minutes backward. A harmless matched pattern, documented clocks, and pressure response support a self-consistent trace. No arbitrary destination date or branch has been observed.'),
('f_script_match','Ada\'s handwriting on the input platen has the same distinctive movements and mechanical terminal hooks as the mysterious traces. Authorship is strongly suggested, not yet explicitly resolved.'),
('f_full_testimony','Ivo\'s full deposition says: 21:06 coordinator Pell ordered the obsolete east route; 21:08 Ivo sent west correction on A; 21:09 he knew there was no repeat-back, yet kept repairing routing rather than using the direct line. He later suppressed the corrected chart and false hold claim to protect his position. Wider institutional failures also matter.'),
('f_evidence_preserved','Source recordings, scans, provenance, transcripts, and checksums are verified at two off-island archives. Originals are safeguarded at the high hall. The truth no longer depends on keeping the pressure machine active.'),
('f_authorship','Ada recognizes she is the author of the two notes. Recorded output times yesterday 19:58 and 20:03 correspond to today 19:15 and 19:20 inputs. The first points to already-observed cup evidence; the second describes a checked safe plan. Blank paper is absence of a signal, not a death prediction.'),
('f_loop_completed','Ada wrote both exact notes at the documented matching input times. The received records did not change. One self-consistent loop is completed without altered history or transported people.'),
('f_release_safe','After verified evacuation, clear exclusion zones, tested brake/cradle, and pressure checks, the bell counterweight lowered safely into its empty cradle and opened the bypass. Everyone is accounted for. Pressure equalized, resonant inclusion fractured, receiving ceased; no one drowned.')]
items=[
('i_inventory','Estate inventory','Ada\'s working catalogue. Records provenance, condition, authority, and the limits of interpretation.'),
('i_ivo_letter','Ivo\'s letter','A letter granting Ada permission to leave, directing her to tapes and a map, and warning that no person has the whole story.'),
('i_first_strip','Cup trace','The documented receiving trace naming tomorrow 16:12 and the blue cup handle.'),
('i_bell_strip','Bell trace','The witnessed line: When the bell falls, no one will drown. A claim to investigate, never a substitute for safety checks.'),
('i_channel_log','Channel log','The contemporary log distinguishes measurements on A from harbor instructions on B.'),
('i_chart','Corrected working chart','Shows the sheltered west approach omitted from the public exhibit, with Ivo\'s direction to use the superseded version.'),
('i_blue_handle','Blue cup handle','The repaired cup handle broke at the predicted minute and marked a useful viewing angle beneath the table.'),
('i_caliper','Survey caliper','Sera\'s tool for comparing actual reference-bolt spacing to the maintenance record.'),
('i_tape','Original magnetic reel','Physical 1998 recording with source identification, original channel separation, and complete deposition.'),
('i_test_strip','Calibration record','A harmless three-stroke-and-circle trace documented across the fixed interval. Its experimental limitations remain explicit.'),
('i_evidence_packet','Verified evidence packet','Source copies, provenance, transcripts, and checksums acknowledged by two independent off-island repositories.'),
('i_blank_strip','Unmarked receiving paper','No recorded signal. Kept as unmarked paper; it predicts neither death nor any other event.')]
ids=[x[0] for x in facts]
characters=[
{'id':'ada','name':'Ada Vale','role':'Adult estate archivist and player viewpoint character.','voice':'Precise, observant, dryly funny. Names uncertainty. Uses object details to approach feelings but can speak directly. Never decides what the player feels.','color':'#A88CC7','knowledge':ids},
{'id':'nessa','name':'Nessa Ward','role':'Adult Saltmere harbor master.','voice':'Spare practical sentences, kindness through concrete tasks, gentle dry humor. Requires named responsibility and repeat-back. Never treats a claim as a safety procedure.','color':'#F2A65A','knowledge':ids},
{'id':'tomas','name':'Tomas Rook','role':'Adult audio conservator working in the island archive.','voice':'Warm, exact about sources, listens before answering, occasional modest joke. Distinguishes evidence from interpretation. Does not use grief as authority.','color':'#63B8B0','knowledge':ids},
{'id':'sera','name':'Dr Sera Wynn','role':'Adult coastal-systems hydrologist.','voice':'Careful bounded claims, corrects overstatement, direct and short under pressure. Professional curiosity balanced by concrete safety. Wry warmth, no pseudo-mystical certainty.','color':'#DFBF5A','knowledge':ids},
{'id':'ivo','name':'Ivo Vale (recording)','role':'The late adult lighthouse keeper, heard in authored archival recordings only.','voice':'Exact vocabulary, indirect domestic humor, pauses before difficult admissions. Recorded dialogue only; never simulate new messages from him.','color':'#B68B76','knowledge':[]},
{'id':'operator','name':'Station operator (recording)','role':'Adult station operator heard only in historical audio.','voice':'Brief technical repeat-back; authored recording only.','color':'#B8C2D1','knowledge':[]},
{'id':'clerk','name':'Inquiry clerk (recording)','role':'Adult clerk heard only in the historical inquiry recording.','voice':'Neutral, factual questions. Authored archival recording only.','color':'#B8C2D1','knowledge':[]}]
chapters=[]
for path in sorted(DRAFTS.glob('chapter[1-5].txt')):
 c=None;s=None; pending={}
 for raw in path.read_text().splitlines():
  if not raw.strip():continue
  if raw.startswith('# '):
   cid,title=raw[2:].split(' | '); c={'id':cid,'title':title,'scenes':[]};chapters.append(c)
  elif raw.startswith('## '):
   sid,title,loc,time,chars=raw[3:].split(' | '); s={'id':sid,'title':title,'location':loc,'timeOfDay':time,'characterIds':chars.split(','),'beats':[]};c['scenes'].append(s)
  elif raw.startswith('!'):
   fs,it=raw[1:].split('|');pending.update({'unlockFacts':fs.split(',') if fs else [],'unlockItems':it.split(',') if it else []})
  elif raw.startswith('^'):pending['stageCue']=raw[1:]
  elif raw.startswith('?'):
   who,prompt,choices,fallback,allowed=raw[1:].split('|');s['beats'].append({'id':f"{s['id']}_b{len(s['beats'])+1:03}",'speaker':'narrator','text':prompt,'conversation':{'characterId':who,'prompt':prompt,'suggestions':choices.split('~'),'fallback':fallback,'allowedFacts':allowed.split(',')}})
  else:
   speaker,txt=raw.split(': ',1);b={'id':f"{s['id']}_b{len(s['beats'])+1:03}",'speaker':speaker,'text':txt};b.update(pending);pending={};s['beats'].append(b)
# Expansion files contain scene headers and use the same simple grammar, with all fact unlocks in base manuscript.
for path in sorted(DRAFTS.glob('expand_*.txt')):
 c=None;s=None
 for raw in path.read_text().splitlines():
  if not raw.strip():continue
  if raw.startswith('# '):c=next(x for x in chapters if x['id']==raw[2:].split(' | ')[0])
  elif raw.startswith('## '):
   sid,title,loc,time,chars=raw[3:].split(' | ');s={'id':sid,'title':title,'location':loc,'timeOfDay':time,'characterIds':chars.split(','),'beats':[]};c['scenes'].append(s)
  elif raw.startswith('?'):
   who,prompt,choices,fallback,allowed=raw[1:].split('|');s['beats'].append({'id':f"{s['id']}_b{len(s['beats'])+1:03}",'speaker':'narrator','text':prompt,'conversation':{'characterId':who,'prompt':prompt,'suggestions':choices.split('~'),'fallback':fallback,'allowedFacts':allowed.split(',')}})
  else:
   speaker,txt=raw.split(': ',1);s['beats'].append({'id':f"{s['id']}_b{len(s['beats'])+1:03}",'speaker':speaker,'text':txt})
for c in chapters:c['scenes'].sort(key=lambda s:(int(re.search(r'_s(\d+)',s['id']).group(1)),re.search(r'_s\d+(.*)',s['id']).group(1)))
activities={
'ch1_s2':('Which inventory statement is supported without interpretation?',['The cup proves Ivo expected Ada.','The blue cup has a repaired handle.','Every object was deliberately left as a clue.'],1,'The repair is directly observable. Intent requires further evidence.'),
'ch1_s3':('What is the strongest claim the first documentation supports?',['The machine guarantees everyone is safe.','Ivo sent a message from the afterlife.','Two people documented a dated trace whose origin is unknown.'],2,'Witnesses and photographs establish the observation, not its mechanism or authority.'),
'ch2_s1':('In the operational legend, what does Channel A carry?',['Water-level measurements.','Vessel movement orders.','The published inquiry summary.'],0,'Channel A records hydrographic measurements; Channel B carries harbor instructions.'),
'ch2_s3':('What makes the working chart relevant to the 1998 rescue route?',['It has a prettier coastline.','It shows a sheltered west approach absent from the issued map.','It predicts tomorrow\'s wind.'],1,'The correction changes route information available to operators. It is not a forecast.'),
'ch2_s5':('What did the cup event establish?',['A documented advance description matched a witnessed break.','Every future message must be obeyed.','Nobody can ever move a cup.'],0,'The match is evidence to investigate. It creates no rule against intervention and no moral command.'),
'ch3_s1':('Which documented mechanism opens the bypass?',['The receiving pen writes a special password.','Someone enters the loaded shaft.','The counterweight descends into its clear cradle and lifts the gate.'],2,'The gravity linkage is physical. It must be inspected and operated with the shaft clear.'),
'ch3_s2':('Which source contains ordinary historical recorded voices?',['The blank chronograph paper.','The physically dated magnetic reel.','The broken cup handle.'],1,'The historical audio is a magnetic recording, not a message from a different year through the apparatus.'),
'ch3_s4':('What interval is supported for this installation?',['Any year the operator chooses.','Exactly twenty-three hours and seventeen minutes backward.','One hour forward.'],1,'The dial, plans, clock comparison, and matched trace support one fixed interval only.'),
'ch4_s1':('What does Ivo\'s deposition explicitly admit?',['He used the direct line immediately.','The crew disobeyed a valid vessel hold.','He knew there was no repeat-back and kept repairing routing instead of calling directly.'],2,'His own account establishes that specific failure, alongside the coordinator\'s obsolete route and later concealment.'),
'ch4_s2':('Why can the team prioritize the pressure shutdown without losing the evidence?',['Verified copies exist at independent off-island archives.','The machine will remember everything forever.','The originals are no longer important.'],0,'Copies with provenance and checksums are acknowledged elsewhere, while originals remain safeguarded.'),
'ch4_s4':('What does the unmarked receiving strip tell us?',['Ada must die before writing again.','No signal has been recorded there.','The storm cannot cause any damage.'],1,'Absence of a trace is not a prediction of death, safety, or any other outcome.'),
'ch5_s2':('What authorizes proceeding with the release?',['The reassuring line alone.','Ivo\'s reputation.','Verified evacuation, clear zones, tested mechanism, and current safe pressure conditions.'],2,'The team acts on a checked procedure and stops if conditions change. The note overrides nothing.'),
'ch5_s3':('What should happen after the resonant inclusion fractures?',['Restart the damaged pressure system to satisfy curiosity.','Keep it isolated, preserve records, and verify shutdown safely.','Destroy the surviving archive.'],1,'Safe isolation and independent verification protect people and evidence.'),
'ch5_s5':('Which catalogue description is appropriately bounded?',['Unmarked receiving paper; retained.','Paper proving nothing bad can ever happen.','Paper containing an invisible final instruction.'],0,'The catalogue distinguishes observation from speculation, even at the ending.')}
activities.update({
'ch1_s1a':('Which route is the agreed bad-weather route to the keeper house?',['The locked cliff maintenance steps.','The inland road above the storm surge.','Whichever path looks shortest.'],1,'Nessa identifies the inland road; conditions still require current checks.'),
'ch1_s2a':('What should Ada record about the unopened replacement remote?',['Its presence and condition, with the reason for keeping it unknown.','Proof that Ivo planned every object as a clue.','A definite story about why he disliked it.'],0,'Observation can be catalogued while motive remains unknown.'),
'ch1_s3a':('What is the best immediate response to the bell trace?',['Operate the unknown controls to test its promise.','Ask a qualified person to inspect and keep the shaft clear.','Assume a hidden author has guaranteed safety.'],1,'The witnessed wording justifies investigation; it does not replace an inspection.'),
'ch2_s2a':('Why preserve the source audio alongside a transcript?',['Transcripts never contain useful information.','The original automatically proves every interpretation.','Transcription can hide overlap, uncertain words, and editorial choices.'],2,'Source links and explicit conventions let another reader check those choices.'),
'ch2_s3a':('What does the route reconstruction support without inventing an alternate ending?',['Everyone certainly survives in an unobserved alternative history.','The obsolete route lost time and omitted a documented opportunity.','The visitor map predicts the tide.'],1,'It documents a lost opportunity without claiming certainty about an outcome that was never observed.'),
'ch3_s2a':('Which shutdown objective is appropriate?',['Preserve evidence and make the installation safe.','Keep it active until every skeptic agrees.','Restart damaged equipment in secret.'],0,'Records can be preserved independently; scientific curiosity does not authorize additional unsafe operation.'),
'ch4_s2a':('Who may Tomas speak for in the statement?',['Every bereaved family automatically.','Only the instrument.','His own work and experience, with other families represented only by their consent.'],2,'Relevant expertise or personal loss does not confer permission to represent every other person.'),
'ch4_s3a':('Two similar names appear on different household codes. What should the team do?',['Delete one to simplify the total.','Treat them as distinct until identities and locations are checked.','Assume both names refer to the same person.'],1,'The second identifier prevents a false duplicate and an inaccurate headcount.'),
'ch5_s4a':('How should the corrected archive handle the misleading old summary?',['Silently replace it and erase the publication history.','Leave it unqualified forever.','Retain it as a historical document with prominent correction and source links.'],2,'The correction must preserve what was published while making the evidence against it clear.')
})
for c in chapters:
 for s in c['scenes']:
  if s['id'] in activities:
   p,o,i,e=activities[s['id']];s['beats'].append({'id':s['id']+'_evidence','speaker':'narrator','text':'Review the evidence before continuing.','activity':{'prompt':p,'options':o,'correctIndex':i,'explanation':e}})
story={'schemaVersion':1,'title':'Lanternwake','characters':characters,'facts':[{'id':i,'text':t} for i,t in facts],'chapters':chapters,'items':[{'id':i,'name':n,'description':d} for i,n,d in items]}
out=ROOT/'Content'/'story.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(story,ensure_ascii=False,indent=2)+'\n')
words=sum(len(re.findall(r"\b[\w’'-]+\b",b['text'])) for c in chapters for s in c['scenes'] for b in s['beats'])
report={'authoredMainPathWords':words,'chapters':len(chapters),'scenes':sum(len(c['scenes']) for c in chapters),'beats':sum(len(s['beats']) for c in chapters for s in c['scenes']),'activities':len(activities),'conversations':sum('conversation'in b for c in chapters for s in c['scenes'] for b in s['beats']),'readingMinutesAt150Wpm':round(words/150,1),'readingMinutesAt180Wpm':round(words/180,1),'readingMinutesAt220Wpm':round(words/220,1),'playtested':False}
(ROOT/'docs'/'bible'/'content_metrics.json').write_text(json.dumps(report,indent=2)+'\n')
print(out);print(json.dumps(report,indent=2))

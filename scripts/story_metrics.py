"""Refresh derived statistics from the authoritative story; never writes story content."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
story = json.loads((root / "Content/story.json").read_text())
chapters = story["chapters"]
scenes = [scene for chapter in chapters for scene in chapter["scenes"]]
beats = [beat for scene in scenes for beat in scene["beats"]]
words = sum(len(re.findall(r"\b[\w’'-]+\b", beat["text"])) for beat in beats)
report = {
    "authoredMainPathWords": words, "chapters": len(chapters), "scenes": len(scenes),
    "beats": len(beats), "activities": sum("activity" in b for b in beats),
    "conversations": sum("conversation" in b for b in beats),
    "readingMinutesAt150Wpm": round(words / 150, 1),
    "readingMinutesAt180Wpm": round(words / 180, 1),
    "readingMinutesAt220Wpm": round(words / 220, 1), "playtested": False,
}
(root / "docs/bible/content_metrics.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))

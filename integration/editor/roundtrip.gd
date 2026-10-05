@tool
extends Node

# The normal --editor lifecycle is essential: runtime instantiation cannot expose placeholder/default loss.
var failures: Array[String] = []

func _ready() -> void:
	call_deferred("_run")

func snapshot(node: Node, origin: Node, result: Dictionary) -> void:
	var values: Dictionary = {}
	if node is Node3D:
		values["transform"] = node.transform
		values["visible"] = node.visible
	if node is AudioStreamPlayer:
		values["bus"] = node.bus
		values["playing"] = node.playing
		values["stream_path"] = node.stream.resource_path if node.stream != null else ""
		if node.playing:
			failures.append("Audio started in the Editor: " + str(origin.get_path_to(node)))
	var script: Script = node.get_script()
	if script != null:
		var properties: Array[String] = []
		match script.resource_path.get_file():
			"StageScene.cs":
				properties = ["BellLoweredOffset", "DawnKeyLightColor", "DuskKeyLightColor", "DawnKeyLightEnergyMultiplier", "DuskKeyLightEnergyMultiplier", "PairSpacing", "TrioSpacing", "EnsembleSpacing", "FacingDegrees", "FacingStepDegrees"]
				for binding in ["CupIntact", "CupFragments", "CupFloorHandle", "CupBoxed", "SteelMug"]:
					var target: Node = node.get(binding)
					values[binding] = str(node.get_path_to(target)) if target != null else ""
					if origin.name == "KeeperHouse" and target == null:
						failures.append("Unassigned keeper-house cup state: " + binding)
			"StageMotion.cs":
				properties = ["Kind", "Enabled", "Phase", "Amount", "Speed", "RainFirstFallDistance", "RainHeight", "RainSlant"]
			"StageDirector.cs":
				properties = ["MotionEnabled"]
			"AudioDirector.cs":
				properties = ["FadeSeconds"]
			"GameView.cs":
				properties = ["StoryPath", "CharactersPerSecond", "StartWithInstantText"]
		for property in properties:
			values[property] = node.get(property)
			if values[property] == null:
				failures.append("Null exported value: " + str(origin.get_path_to(node)) + "." + property)
	result[str(origin.get_path_to(node))] = values
	for child in node.get_children():
		snapshot(child, origin, result)

func _run() -> void:
	if not Engine.is_editor_hint():
		push_error("This regression requires --editor.")
		get_tree().quit(1)
		return
	var paths: Array[String] = ["res://Scenes/Main.tscn", "res://Scenes/Stages/StageDirector.tscn", "res://Scenes/Audio/AudioDirector.tscn", "res://Scenes/UI/AudioSettingsControls.tscn"]
	for name in ["Harbor", "KeeperHouse", "Archive", "LanternRoom", "TideCave"]:
		paths.append("res://Scenes/Stages/" + name + ".tscn")
	for name in ["Ada", "Nessa", "Tomas", "Sera"]:
		paths.append("res://Scenes/Characters/" + name + ".tscn")
	for path in paths:
		var scene: PackedScene = load(path)
		var node: Node = scene.instantiate(PackedScene.GEN_EDIT_STATE_MAIN)
		get_tree().root.add_child(node)
		var before: Dictionary = {}
		snapshot(node, node, before)
		await get_tree().process_frame
		await get_tree().process_frame
		var after_frames: Dictionary = {}
		snapshot(node, node, after_frames)
		if before != after_frames:
			failures.append("Opening a scene changed authored state: " + path)
		if path.ends_with("Main.tscn") and node.get_node("StageDirector").get_child_count() != 0:
			failures.append("Main started gameplay in the Editor")
		var saved := PackedScene.new()
		var temporary := "user://editor-roundtrip-" + str(Time.get_ticks_usec()) + ".tscn"
		if saved.pack(node) != OK or ResourceSaver.save(saved, temporary) != OK:
			failures.append("Could not save " + path)
		else:
			var reloaded: PackedScene = ResourceLoader.load(temporary, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE)
			var reopened: Node = reloaded.instantiate(PackedScene.GEN_EDIT_STATE_MAIN)
			get_tree().root.add_child(reopened)
			var observed: Dictionary = {}
			snapshot(reopened, reopened, observed)
			if before != observed:
				failures.append("Editor save/reopen changed defaults or transforms: " + path)
			reopened.free()
			DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		node.free()
	for failure in failures:
		push_error(failure)
	if failures.is_empty():
		print("LANTERNWAKE_EDITOR_ROUNDTRIP_OK scenes=13 defaults preserved; editor gameplay/motion/audio inactive")
	get_tree().quit(0 if failures.is_empty() else 1)

extends SceneTree

var game: Node
var output: String
var fixture: String

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, claim: String) -> bool:
	if not value:
		push_error("Graphical probe: " + claim)
		quit(1)
	return value

func settled() -> void:
	for i in range(4):
		await process_frame
	await RenderingServer.frame_post_draw

func screenshot(window: Window, name: String) -> void:
	await settled()
	var image := window.get_texture().get_image()
	if check(not image.is_empty(), "renderer supplies actual pixels"):
		check(image.save_png(output.path_join(name + ".png")) == OK, "save screenshot")

func click(window: Window, control: Control) -> void:
	var point := control.get_global_transform_with_canvas() * (control.size / 2.0)
	var containing := window
	while containing.is_embedded():
		point += Vector2(containing.position)
		containing = containing.get_parent().get_window()
	point = Vector2(containing.position) + containing.get_final_transform() * point
	var helper := ProjectSettings.globalize_path("res://integration/qa/x11_input.py")
	check(OS.execute("python3", [helper, "click", str(point.x), str(point.y)]) == 0, "owned X11 mouse dispatch")
	await settled()

func modal() -> Window:
	for child in game.get_children():
		if child is Window and child.visible and not child.is_queued_for_deletion():
			return child
	return null

func key(name: String) -> void:
	var helper := ProjectSettings.globalize_path("res://integration/qa/x11_input.py")
	check(OS.execute("python3", [helper, "key", name]) == 0, "owned X11 keyboard dispatch")
	await settled()

func action_button(text: String) -> Button:
	for action in modal().get_node("%ModalActions").get_children():
		if action is Button and action.text == text:
			return action
	return null

func focus_control(control: Control) -> bool:
	for i in range(24):
		if control.has_focus():
			return true
		await key("Tab")
	return check(false, "native Tab reaches " + str(control.name))

func activate(text: String) -> void:
	var target := action_button(text)
	if check(target != null, "existing action " + text) and await focus_control(target):
		await key("Return")

func run() -> void:
	fixture = OS.get_environment("LANTERNWAKE_GRAPHICAL_FIXTURE")
	output = OS.get_environment("LANTERNWAKE_GRAPHICAL_OUTPUT")
	if not check(fixture.is_absolute_path() and FileAccess.file_exists(fixture.path_join("owned-fixture")) and output.is_absolute_path() and DirAccess.dir_exists_absolute(output), "owned fixture/output required"):
		return
	if not check(ProjectSettings.globalize_path("user://").begins_with(fixture + "/"), "isolated userdata required"):
		return
	game = load("res://Scenes/Main.tscn").instantiate()
	root.add_child(game)
	var args := OS.get_cmdline_user_args()
	if args.has("--author-preview-beat"):
		var beat := args[args.find("--author-preview-beat") + 1]
		await settled()
		var passage: RichTextLabel = game.get("InterfaceRoot").get_node("%DialogueText")
		var prose := passage.text
		if passage.visible_characters >= 0 and passage.visible_characters < passage.get_total_character_count():
			await key("space")
		if not check(passage.text == prose and (passage.visible_characters == -1 or passage.visible_characters >= passage.get_total_character_count()), "native Space reveals selected authored passage without advancing"):
			return
		if beat == "ch1_s2_b008":
			var stage: Node3D = game.get("Stage").get_node("KeeperHouse")
			var camera: Camera3D = root.get_camera_3d()
			if not check(camera == stage.get_node("CupInventoryCamera"), "first inventory selects the static table insert"):
				return
			var panel: Control = game.get("InterfaceRoot").get_node("DialoguePanel")
			var measurements: Array = []
			for mesh: MeshInstance3D in stage.get_node("Scenery/CupIntact").get_children():
				var point := camera.unproject_position(mesh.global_position)
				measurements.append({"node": str(mesh.name), "x": point.x, "y": point.y})
				var bounds := mesh.get_aabb()
				for corner in range(8):
					var local := bounds.position + bounds.size * Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1)
					var world := mesh.global_transform * local
					var screen := camera.unproject_position(world)
					if not check(not camera.is_position_behind(world) and screen.x > 0 and screen.x < root.get_visible_rect().size.x and screen.y > 80 and screen.y < panel.global_position.y, "intact mesh bounds project above the reading panel: " + str(mesh.name)):
						return
			print("LANTERNWAKE_CUP_INVENTORY_PROJECTION " + JSON.stringify({"meshCentres": measurements, "panelTop": panel.global_position.y, "viewportSize": str(root.get_visible_rect().size), "windowSize": str(root.size)}))
		if beat == "ch2_s5_b012":
			var stage: Node3D = game.get("Stage").get_node("KeeperHouse")
			var camera: Camera3D = root.get_camera_3d()
			if not check(camera == stage.get_node("CupFloorCamera"), "authored break selects the object insert"):
				return
			var panel: Control = game.get("InterfaceRoot").get_node("DialoguePanel")
			var measurements: Array = []
			var meshes := stage.get_node("Scenery/CupFragments").get_children()
			meshes.append_array(stage.get_node("Scenery/CupFloorHandle").get_children())
			for mesh: MeshInstance3D in meshes:
				var point := camera.unproject_position(mesh.global_position)
				measurements.append({"node": str(mesh.name), "x": point.x, "y": point.y})
				if not check(not camera.is_position_behind(mesh.global_position) and point.x > 0 and point.x < root.get_visible_rect().size.x and point.y > 100 and point.y < panel.global_position.y, "fallen mesh projects above reading panel: " + str(mesh.name)):
					return
			print("LANTERNWAKE_CUP_INSERT_PROJECTION " + JSON.stringify({"meshCentres": measurements, "panelTop": panel.global_position.y, "viewportSize": str(root.get_visible_rect().size), "windowSize": str(root.size)}))
		await screenshot(root, beat)
		var authored: Dictionary = {}
		var story: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Content/story.json"))
		for chapter in story.chapters:
			for scene in chapter.scenes:
				for candidate in scene.beats:
					if candidate.id == beat:
						authored = candidate
		if authored.has("conversation"):
			var interface: Node = game.get("InterfaceRoot")
			await click(root, interface.get_node("%TalkButton"))
			# The enabled voice action explains the unavailable producer contract.
			# Do not invoke it or assume disabled presentation is the capability gate.
			if not check(AudioServer.get_bus_index("LanternwakeCapture") == -1, "no microphone capture bus created"):
				return
			var suggestion: Button = interface.get_node("%Suggestions").get_child(0)
			await click(root, suggestion)
			check(interface.get_node("%PlayerEntry").text == authored.conversation.suggestions[0], "native suggestion populates editable input")
			await screenshot(root, "editable-conversation")
			await click(root, interface.get_node("%SendButton"))
			check(passage.text == authored.conversation.fallback, "native submission uses exact authored fallback with empty model")
			await screenshot(root, "authored-fallback")
			await click(root, interface.get_node("%ReturnButton"))
			check(not interface.get_node("%ChatPanel").visible and passage.text == prose, "native Return restores exact authored passage")
			await screenshot(root, "returned-passage")
		if beat == "ch5_s5_evidence":
			await click(root, game.get("InterfaceRoot").get_node("%AdvanceButton"))
			check(modal() != null and modal().title == "Compare the evidence", "final evidence remains an authored gate")
			await activate(authored.activity.options[authored.activity.correctIndex])
			check(modal() == null, "canonical final answer closes question")
			await click(root, game.get("InterfaceRoot").get_node("%AdvanceButton"))
			check(modal() != null and modal().title == "The light remains", "actual Finish opens completion")
			await screenshot(modal(), "completion")
			await quit_from_settings()
			return
		await click(root, game.get("InterfaceRoot").get_node("%SettingsButton"))
		await quit_from_settings()
		return
	await screenshot(root, "title")
	var interface: Node = game.get("InterfaceRoot")
	await click(root, interface.get_node("%ContentNoteButton"))
	if not check(modal() != null and modal().title == "Content note", "mouse input opens note"):
		return
	await screenshot(modal(), "title-content-note")
	await click(modal(), modal().get_node("%ModalCloseButton"))
	if not check(modal() == null, "mouse input closes note"):
		return
	await click(root, interface.get_node("%SettingsButton"))
	if not check(modal() != null and modal().title == "Reading settings", "mouse input opens Settings"):
		return
	await screenshot(modal(), "reading-settings")
	await activate("Sound settings")
	check(modal() != null and modal().title == "Sound settings", "native control opens Sound settings")
	var controls := modal().get_node("%ModalActions").get_node("AudioSettingsControls")
	await click(modal(), controls.get_node("Mute"))
	check(AudioServer.is_bus_mute(AudioServer.get_bus_index("Master")), "visible mute changes native Master bus")
	var music: HSlider = controls.get_node("Music")
	await focus_control(music)
	var volume := music.value
	await key("Right")
	check(music.value > volume and abs(AudioServer.get_bus_volume_db(AudioServer.get_bus_index("Music")) - linear_to_db(music.value / 100.0)) < 0.01, "visible keyboard slider changes native music level")
	await screenshot(modal(), "sound-settings")
	await click(modal(), modal().get_node("%ModalCloseButton"))
	await click(root, interface.get_node("%SettingsButton"))
	await activate("Larger reading text")
	await activate("Larger reading text")
	await activate("Content note")
	modal().size = Vector2i(480, 320)
	await settled()
	var bar: VScrollBar = modal().get_node("%ModalText").get_v_scroll_bar()
	await focus_control(bar)
	var scroll := bar.value
	await key("Down")
	check(bar.has_focus() and bar.value > scroll, "real X11 Down scrolls 150% note without leaving focus")
	await screenshot(modal(), "content-note-150-small")
	await key("Tab")
	check(modal().get_node("%ModalCloseButton").has_focus(), "real Tab reaches close after note scrolling")
	await key("Return")
	check(modal() != null and modal().title == "Reading settings", "native note return preserves Settings destination")
	await quit_from_settings()

func quit_from_settings() -> void:
	for action in modal().get_node("%ModalActions").get_children():
		if action is Button and action.text == "Quit game":
			for i in range(12):
				if action.has_focus():
					break
				await key("Tab")
			if not check(action.has_focus(), "keyboard reaches Quit in scrolled Settings"):
				return
			print("LANTERNWAKE_GRAPHICAL_PROBE_OK title/note/Settings mouse clicks and keyboard Quit")
			# Quit may end the tree before another render frame. Finish this coroutine
			# before the queued native key is processed, so it owns no pending await.
			var helper := ProjectSettings.globalize_path("res://integration/qa/x11_input.py")
			check(OS.execute("python3", [helper, "key", "Return"]) == 0, "native Quit key dispatch")
			return
	check(false, "production Quit action present")

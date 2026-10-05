extends "res://integration/qa/graphical_probe.gd"

func run() -> void:
	fixture = OS.get_environment("LANTERNWAKE_GRAPHICAL_FIXTURE")
	output = OS.get_environment("LANTERNWAKE_GRAPHICAL_OUTPUT")
	if not check(fixture.is_absolute_path() and FileAccess.file_exists(fixture.path_join("owned-fixture")) and output.is_absolute_path() and DirAccess.dir_exists_absolute(output), "owned header fixture/output required"):
		return
	if not check(ProjectSettings.globalize_path("user://").begins_with(fixture + "/"), "isolated header userdata required"):
		return
	game = load("res://Scenes/Main.tscn").instantiate()
	root.add_child(game)
	await settled()
	var interface: Control = game.get("InterfaceRoot")
	var passage: RichTextLabel = interface.get_node("%DialogueText")
	var chapter: Label = interface.get_node("%ChapterLabel")
	var place: Label = interface.get_node("%PlaceLabel")
	var original := [passage.text, chapter.text, place.text]
	var camera: Camera3D = root.get_camera_3d()
	var base_size := passage.get_theme_font_size("normal_font_size")
	if passage.visible_characters >= 0:
		await key("space")
	var records: Array = []
	for percent in [100, 125, 150]:
		if percent > 100:
			await click(root, interface.get_node("%SettingsButton"))
			await activate("Larger reading text")
			await click(modal(), modal().get_node("%ModalCloseButton"))
		await settled()
		if not check([passage.text, chapter.text, place.text] == original and root.get_camera_3d() == camera, "reading size preserves exact authored header/passage and camera"):
			return
		if not check(passage.get_theme_font_size("normal_font_size") == roundi(base_size * percent / 100.0), "native reading size applied"):
			return
		var title_box: Control = interface.get_node("TopBar/TopRow/Titles")
		var evidence: Button = interface.get_node("%EvidenceButton")
		if not check(title_box.get_global_rect().end.x <= evidence.get_global_rect().position.x and title_box.get_global_rect().encloses(chapter.get_global_rect()) and title_box.get_global_rect().encloses(place.get_global_rect()), "header labels fit and do not overlap controls"):
			return
		var record := {"percent": percent, "bodyFont": passage.get_theme_font_size("normal_font_size"), "chapterFont": chapter.get_theme_font_size("font_size"), "placeFont": place.get_theme_font_size("font_size"), "chapterText": chapter.text, "placeText": place.text, "camera": str(camera.name)}
		if OS.get_environment("LANTERNWAKE_HEADER_PHASE") == "after":
			if not check(title_box is PanelContainer, "header has native panel backing"):
				return
			var pixels := root.get_texture().get_image()
			var point := root.get_final_transform() * Vector2(title_box.get_global_rect().end.x - 8, chapter.get_global_rect().get_center().y)
			var background := pixels.get_pixel(roundi(point.x), roundi(point.y))
			record["backgroundPixel"] = [background.r, background.g, background.b]
			if not check(maxf(background.r, maxf(background.g, background.b)) < 0.15, "actual header interior stays dark over the stage"):
				return
		await screenshot(root, str(percent))
		records.append(record)
	print("LANTERNWAKE_HEADER_CONTRAST_OK " + JSON.stringify(records))
	await click(root, interface.get_node("%SettingsButton"))
	await quit_from_settings()
